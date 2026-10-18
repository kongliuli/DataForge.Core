using DataForge.Core.Core.Models;

namespace DataForge.Jobs.Execution;

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

    /// <summary>
    /// 行数对账快照。成功的运行必然填充；失败的结果（<see cref="Success"/> 为 <c>false</c>）为 <c>null</c>。
    /// </summary>
    public JobRunReconciliation? Reconciliation { get; init; }
}
