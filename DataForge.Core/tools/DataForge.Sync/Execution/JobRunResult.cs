using DataForge.Core.Core.Models;

namespace DataForge.Sync.Execution;

public sealed class JobRunResult
{
    public required bool Success { get; init; }

    public string? JobName { get; init; }

    public string? JobFilePath { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    public TimeSpan Duration => FinishedAt - StartedAt;

    public string? ErrorMessage { get; init; }

    public ExportResults? ExportResults { get; init; }
}
