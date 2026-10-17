using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;

namespace DataForge.Jobs.Scheduling;

/// <summary>Snapshot of one resident schedule.</summary>
public sealed class ScheduledJobStatus
{
    public required string Id { get; init; }

    public string? Name { get; init; }

    public required string Cron { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public DateTimeOffset? NextRunAt { get; internal set; }

    public DateTimeOffset? LastRunAt { get; internal set; }

    public bool? LastSuccess { get; internal set; }

    public string? LastError { get; internal set; }

    public int RunCount { get; internal set; }

    public bool Executing { get; internal set; }

    public ScheduledJobStatus Clone() => new()
    {
        Id = Id,
        Name = Name,
        Cron = Cron,
        RegisteredAt = RegisteredAt,
        NextRunAt = NextRunAt,
        LastRunAt = LastRunAt,
        LastSuccess = LastSuccess,
        LastError = LastError,
        RunCount = RunCount,
        Executing = Executing
    };
}

/// <summary>
/// Resident (always-on) host for several cron-scheduled YAML jobs — the process-local
/// replacement for a LocalDts service. Owned by the package so CLI, Web and embedders
/// share one implementation.
/// </summary>
public sealed class JobScheduleService : IAsyncDisposable
{
    private sealed class Entry
    {
        public required string Id { get; init; }
        public required JobDefinition Job { get; init; }
        public required string Yaml { get; init; }
        public required string JobFilePath { get; init; }
        public IReadOnlyDictionary<string, string>? Variables { get; init; }
        public bool RunOnStart { get; init; }
        public ScheduledJobStatus Status { get; init; } = null!;
        public CancellationTokenSource? Cts { get; set; }
        public Task? Loop { get; set; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
    }

    private readonly JobRunner _runner;
    private readonly IProgress<JobProgress>? _progress;
    private readonly TimeZoneInfo _timeZone;
    private readonly object _sync = new();
    private readonly List<Entry> _entries = [];
    private CancellationTokenSource? _cts;

    public JobScheduleService(
        JobRunner? runner = null,
        IProgress<JobProgress>? progress = null,
        TimeZoneInfo? timeZone = null)
    {
        _runner = runner ?? new JobRunner();
        _progress = progress;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public TimeZoneInfo TimeZone => _timeZone;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _cts is { IsCancellationRequested: false };
            }
        }
    }

    /// <summary>Registers a YAML job by its <c>schedule</c> field. Throws when the job has no valid cron.</summary>
    public ScheduledJobStatus Add(
        string yaml,
        string? id = null,
        string? jobFilePath = null,
        IReadOnlyDictionary<string, string>? variables = null,
        bool runOnStart = false)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new ArgumentException("yaml is required.", nameof(yaml));

        var job = JobYamlParser.Parse(yaml);
        if (string.IsNullOrWhiteSpace(job.Schedule))
            throw new InvalidOperationException("Job has no schedule field; nothing to register.");

        // Validate the cron expression up front so registration fails fast.
        var cron = JobScheduler.ParseSchedule(job.Schedule).ToString();
        var entryId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..8] : id.Trim();

        var entry = new Entry
        {
            Id = entryId,
            Job = job,
            Yaml = yaml,
            JobFilePath = Path.GetFullPath(jobFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "scheduled-job.yaml")),
            Variables = variables is null ? null : new Dictionary<string, string>(variables),
            RunOnStart = runOnStart,
            Status = new ScheduledJobStatus
            {
                Id = entryId,
                Name = job.Name,
                Cron = job.Schedule,
                RegisteredAt = DateTimeOffset.UtcNow
            }
        };

        lock (_sync)
        {
            if (_entries.Any(item => item.Id == entryId))
                throw new InvalidOperationException($"Schedule '{entryId}' already exists.");

            _entries.Add(entry);
            if (_cts is { IsCancellationRequested: false })
                StartEntry(entry, _cts);
        }

        return entry.Status.Clone();
    }

    /// <summary>Returns detached registrations suitable for JSON persistence. Does not include run history.</summary>
    public IReadOnlyList<ScheduleRegistration> ExportRegistrations()
    {
        lock (_sync)
        {
            return _entries.Select(entry => new ScheduleRegistration(
                entry.Id, entry.Yaml, entry.JobFilePath,
                entry.Variables is null ? null : new Dictionary<string, string>(entry.Variables),
                entry.RunOnStart)).ToArray();
        }
    }

    /// <summary>Restores one saved registration. Duplicate identifiers are rejected by Add.</summary>
    public ScheduledJobStatus Restore(ScheduleRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return Add(registration.Yaml, registration.Id, registration.JobFilePath,
            registration.Variables, registration.RunOnStart);
    }

    public bool Remove(string id)
    {
        Entry? entry;
        lock (_sync)
        {
            entry = _entries.FirstOrDefault(item => item.Id == id);
            if (entry is null)
                return false;

            _entries.Remove(entry);
            entry.Cts?.Cancel();
        }

        return true;
    }

    public ScheduledJobStatus? Get(string id)
    {
        lock (_sync)
        {
            return _entries.FirstOrDefault(item => item.Id == id)?.Status.Clone();
        }
    }

    public IReadOnlyList<ScheduledJobStatus> List()
    {
        lock (_sync)
        {
            return _entries.Select(item => item.Status.Clone()).ToArray();
        }
    }

    /// <summary>Starts every registered schedule (idempotent).</summary>
    public void Start()
    {
        lock (_sync)
        {
            if (_cts is { IsCancellationRequested: false })
                return;

            _cts = new CancellationTokenSource();
            foreach (var entry in _entries)
                StartEntry(entry, _cts);
        }
    }

    /// <summary>Runs one registered job immediately, outside its cron schedule.</summary>
    public async Task<JobRunResult> RunNowAsync(string id, CancellationToken cancellationToken = default)
    {
        Entry? entry;
        lock (_sync)
        {
            entry = _entries.FirstOrDefault(item => item.Id == id);
        }

        if (entry is null)
            throw new KeyNotFoundException($"Schedule '{id}' not found.");

        return await ExecuteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        Entry[] entries;
        CancellationTokenSource? cts;
        lock (_sync)
        {
            cts = _cts;
            _cts = null;
            entries = _entries.ToArray();
            foreach (var entry in entries)
            {
                entry.Cts?.Cancel();
                entry.Cts = null;
            }
        }

        cts?.Cancel();

        var loops = entries.Select(item => item.Loop).Where(task => task is not null).ToArray();
        if (loops.Length > 0)
            await Task.WhenAll(loops!).ConfigureAwait(false);

        cts?.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private void StartEntry(Entry entry, CancellationTokenSource serviceCts)
    {
        // Caller holds _sync.
        if (entry.Loop is { IsCompleted: false })
            return;

        entry.Cts = CancellationTokenSource.CreateLinkedTokenSource(serviceCts.Token);
        var token = entry.Cts.Token;
        entry.Loop = Task.Run(() => RunLoopAsync(entry, token), CancellationToken.None);
    }

    private async Task RunLoopAsync(Entry entry, CancellationToken cancellationToken)
    {
        try
        {
            if (entry.RunOnStart)
                await ExecuteAsync(entry, cancellationToken).ConfigureAwait(false);

            var scheduler = new JobScheduler(_runner, _timeZone, _progress)
            {
                Executor = (_, _, _, token) => ExecuteAsync(entry, token),
                OnWaiting = due => { lock (_sync) entry.Status.NextRunAt = due; },
                OnTick = tick =>
                {
                    lock (_sync)
                    {
                        entry.Status.NextRunAt = tick.NextDueAt;
                    }
                }
            };

            await scheduler.WatchAsync(entry.Job, entry.JobFilePath, entry.Variables, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Resident loop stopped on request.
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                entry.Status.LastError = ex.Message;
                entry.Status.LastSuccess = false;
            }
        }
    }

    private async Task<JobRunResult> ExecuteAsync(Entry entry, CancellationToken cancellationToken)
    {
        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            entry.Status.Executing = true;
        }

        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            var result = await _runner
                .RunAsync(entry.Job, entry.JobFilePath, entry.Variables, cancellationToken, _progress)
                .ConfigureAwait(false);

            lock (_sync)
            {
                entry.Status.RunCount++;
                entry.Status.LastRunAt = startedAt;
                entry.Status.LastSuccess = result.Success;
                entry.Status.LastError = result.Success ? null : result.ErrorMessage;
            }

            return result;
        }
        finally
        {
            lock (_sync)
            {
                entry.Status.Executing = false;
            }
            entry.Gate.Release();
        }
    }
}
