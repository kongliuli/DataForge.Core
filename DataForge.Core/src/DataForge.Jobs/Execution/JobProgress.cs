namespace DataForge.Jobs.Execution;

public sealed class JobProgress
{
    public string Phase { get; init; } = "";

    public string? JobName { get; init; }

    public string? Message { get; init; }

    public long? RecordsProcessed { get; init; }

    public int? JobIndex { get; init; }

    public int? JobCount { get; init; }

    public double? Percent { get; init; }

    public static JobProgress Started(string? jobName, string message = "started") => new()
    {
        Phase = "started",
        JobName = jobName,
        Message = message
    };

    public static JobProgress Reading(string? jobName, long records) => new()
    {
        Phase = "reading",
        JobName = jobName,
        RecordsProcessed = records,
        Message = $"read {records} row(s)"
    };

    public static JobProgress Writing(string? jobName, long records) => new()
    {
        Phase = "writing",
        JobName = jobName,
        RecordsProcessed = records,
        Message = $"wrote {records} row(s)"
    };

    public static JobProgress Completed(string? jobName, long? records = null) => new()
    {
        Phase = "completed",
        JobName = jobName,
        RecordsProcessed = records,
        Percent = 100,
        Message = "completed"
    };

    public static JobProgress Failed(string? jobName, string error) => new()
    {
        Phase = "failed",
        JobName = jobName,
        Message = error
    };

    public static JobProgress Batch(string? jobName, int index, int count, string phase) => new()
    {
        Phase = phase,
        JobName = jobName,
        JobIndex = index,
        JobCount = count,
        Percent = count <= 0 ? null : Math.Round(100.0 * index / count, 1),
        Message = $"job {index}/{count}"
    };
}
