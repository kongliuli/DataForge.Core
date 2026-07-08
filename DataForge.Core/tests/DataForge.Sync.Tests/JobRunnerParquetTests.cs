using DataForge.Sync.Execution;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobRunnerParquetTests
{
    [Fact]
    public async Task RunAsync_CsvToParquet_WritesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "input.csv");
            var parquetPath = Path.Combine(tempDir, "output.parquet");
            var jobPath = Path.Combine(tempDir, "job.yaml");

            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount
                1,10
                2,20
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                transforms:
                  - where: "Amount >= 10"
                sink:
                  type: parquet
                  path: {{parquetPath}}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(File.Exists(parquetPath));
            Assert.Equal(2, result.ExportResults!.RecordsWritten);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
