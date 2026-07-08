using DataForge.Sync.Execution;
using System.Text.Json;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobRunReportTests
{
    [Fact]
    public async Task WriteAsync_WritesJsonSummary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var reportPath = Path.Combine(tempDir, "run.json");

        try
        {
            var result = new JobRunResult
            {
                Success = true,
                JobName = "test-job",
                JobFilePath = "/tmp/job.yaml",
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-2),
                FinishedAt = DateTimeOffset.UtcNow,
                ExportResults = new DataForge.Core.Core.Models.ExportResults
                {
                    RecordsWritten = 42,
                    OutputPath = "/tmp/out.json"
                }
            };

            await JobRunReportWriter.WriteAsync(result, reportPath);

            Assert.True(File.Exists(reportPath));
            var json = await File.ReadAllTextAsync(reportPath);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("test-job", doc.RootElement.GetProperty("JobName").GetString());
            Assert.Equal(42, doc.RootElement.GetProperty("Export").GetProperty("RecordsWritten").GetInt32());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
