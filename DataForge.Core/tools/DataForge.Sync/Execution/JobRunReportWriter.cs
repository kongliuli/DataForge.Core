using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataForge.Sync.Execution;

public static class JobRunReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task WriteAsync(JobRunResult result, string reportPath, CancellationToken cancellationToken = default)
    {
        var report = new
        {
            result.JobName,
            result.JobFilePath,
            result.Success,
            result.ErrorMessage,
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
            DurationMs = (int)result.Duration.TotalMilliseconds,
            Export = result.ExportResults == null ? null : new
            {
                result.ExportResults.RecordsWritten,
                result.ExportResults.OutputPath,
                result.ExportResults.OutputSize,
                ExportDurationMs = result.ExportResults.Duration.TotalMilliseconds,
                RowErrorCount = result.ExportResults.RowErrors.Count,
                BadRowPaths = result.ExportResults.RowErrors.Count > 0 ? result.ExportResults.OutputPath : null
            }
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken)
            .ConfigureAwait(false);
    }
}
