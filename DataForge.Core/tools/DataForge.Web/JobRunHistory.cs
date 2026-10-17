namespace DataForge.Web;

public sealed class JobRunHistory
{
    private readonly object _gate = new();
    private readonly List<JobHistoryEntry> _entries = [];
    private long _nextId;

    public JobHistoryEntry Add(string yaml, IReadOnlyList<object> results, bool success)
    {
        lock (_gate)
        {
            var entry = new JobHistoryEntry(++_nextId, DateTimeOffset.UtcNow, yaml, success, results);
            _entries.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<JobHistoryEntry> List()
    {
        lock (_gate)
            return _entries.AsEnumerable().Reverse().ToArray();
    }
}

public sealed record JobHistoryEntry(
    long Id,
    DateTimeOffset At,
    string Yaml,
    bool Success,
    IReadOnlyList<object> Results);
