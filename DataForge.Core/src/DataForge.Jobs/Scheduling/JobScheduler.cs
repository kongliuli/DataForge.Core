using Cronos;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;

namespace DataForge.Jobs.Scheduling;

/// <summary>Observability payload for one scheduler tick (used by hosts such as the CLI and Web).</summary>
public sealed class JobScheduleTick
{
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset DueAt { get; init; }

    public DateTimeOffset? NextDueAt { get; init; }

    public JobRunResult? Result { get; init; }

    public string? Error { get; init; }

    public bool Success => Result?.Success == true;
}

/// <summary>
/// Cron scheduling for YAML jobs. Lives in DataForge.Jobs so every host (CLI, Web, embedders)
/// gets scheduling from the package rather than from a tool-local copy.
/// </summary>
public sealed class JobScheduler
{
    private readonly JobRunner _runner;
    private readonly IProgress<JobProgress>? _progress;

    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = Task.Delay;

    public JobScheduler(
        JobRunner? runner = null,
        TimeZoneInfo? timeZone = null,
        IProgress<JobProgress>? progress = null)
    {
        _runner = runner ?? new JobRunner();
        _progress = progress;
        TimeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public TimeZoneInfo TimeZone { get; }

    /// <summary>Maximum number of runs before the watch loop stops; null means run forever.</summary>
    public int? MaxRuns { get; set; }

    /// <summary>Raised after each scheduled run (including failures).</summary>
    public Action<JobScheduleTick>? OnTick { get; set; }

    /// <summary>Raised with the instant the loop starts waiting for (resident hosts show it as "next run").</summary>
    public Action<DateTimeOffset>? OnWaiting { get; set; }

    /// <summary>
    /// Optional replacement for the default runner invocation. Resident hosts use it to
    /// record run history (execution count, last result) around each scheduled run.
    /// </summary>
    public Func<JobDefinition, string, IReadOnlyDictionary<string, string>?, CancellationToken, Task<JobRunResult>>? Executor { get; set; }

    public static CronExpression ParseSchedule(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression is required.", nameof(cronExpression));

        return CronExpression.Parse(cronExpression.Trim(), CronFormat.Standard);
    }

    public static DateTimeOffset? GetNextOccurrence(
        string cronExpression,
        DateTimeOffset fromUtc,
        TimeZoneInfo? timeZone = null) =>
        ParseSchedule(cronExpression).GetNextOccurrence(fromUtc, timeZone ?? TimeZoneInfo.Local);

    public DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset fromUtc) =>
        GetNextOccurrence(cronExpression, fromUtc, TimeZone);

    /// <summary>Runs the job defined in <paramref name="job"/> on its cron schedule until cancelled.</summary>
    public async Task WatchAsync(
        JobDefinition job,
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.Schedule))
            throw new InvalidOperationException(
                "Job has no schedule field. Add schedule: \"0 2 * * *\" or use 'run'.");

        var cron = ParseSchedule(job.Schedule);
        var runs = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (MaxRuns is { } limit && runs >= limit)
                return;

            var now = Clock();
            var due = cron.GetNextOccurrence(now, TimeZone);
            if (due is null)
            {
                OnTick?.Invoke(new JobScheduleTick
                {
                    StartedAt = now,
                    DueAt = now,
                    Error = "No upcoming schedule occurrence."
                });
                return;
            }

            var delay = due.Value - now;
            if (delay > TimeSpan.Zero)
            {
                OnWaiting?.Invoke(due.Value);
                await DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }

            var startedAt = Clock();
            var result = Executor is null
                ? await _runner
                    .RunAsync(job, jobFilePath, variables, cancellationToken, _progress)
                    .ConfigureAwait(false)
                : await Executor(job, jobFilePath, variables, cancellationToken).ConfigureAwait(false);

            runs++;
            OnTick?.Invoke(new JobScheduleTick
            {
                StartedAt = startedAt,
                DueAt = due.Value,
                NextDueAt = cron.GetNextOccurrence(startedAt, TimeZone),
                Result = result,
                Error = result.Success ? null : result.ErrorMessage
            });
        }
    }

    /// <summary>Reads the job file, then schedules it.</summary>
    public async Task WatchFileAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
        var job = JobYamlParser.Parse(yaml);
        await WatchAsync(job, jobFilePath, variables, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the next due instant of a job file, or null when it has no schedule.</summary>
    public async Task<DateTimeOffset?> GetNextRunAsync(
        string jobFilePath,
        CancellationToken cancellationToken = default)
    {
        var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
        var job = JobYamlParser.Parse(yaml);
        if (string.IsNullOrWhiteSpace(job.Schedule))
            return null;

        return GetNextOccurrence(job.Schedule, Clock());
    }
}
