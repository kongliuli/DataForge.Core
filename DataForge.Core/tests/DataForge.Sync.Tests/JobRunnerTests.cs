using DataForge.Sync.Execution;
using DataForge.Sync.Parsing;
using Xunit;

namespace DataForge.Sync.Tests;

public class WhereExpressionTests
{
    [Fact]
    public void Compile_ComparesNumericValues()
    {
        var predicate = WhereExpression.Compile("Amount > 0", new Dictionary<string, string>());
        var row = new JobRow { Values = { ["Amount"] = "10" } };

        Assert.True(predicate(row));
    }

    [Fact]
    public void Compile_ResolvesVariableReference()
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lastSync"] = "2026-07-01"
        };
        var predicate = WhereExpression.Compile("OrderDate >= @lastSync", vars);
        var row = new JobRow { Values = { ["OrderDate"] = "2026-07-02" } };

        Assert.True(predicate(row));
    }
}

public class JobRunnerTests
{
    [Fact]
    public async Task RunAsync_FiltersCsvAndWritesJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "input.csv");
            var outPath = Path.Combine(tempDir, "output.json");
            var jobPath = Path.Combine(tempDir, "job.yaml");

            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount
                1,10
                2,-1
                3,5
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                transforms:
                  - where: "Amount > 0"
                sink:
                  type: json
                  path: {{outPath}}
                """);

            var runner = new JobRunner();
            var result = await runner.RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.ExportResults);
            Assert.Equal(2, result.ExportResults!.RecordsWritten);
            Assert.True(File.Exists(outPath));
            var json = await File.ReadAllTextAsync(outPath);
            Assert.Contains("\"Amount\": \"10\"", json);
            Assert.Contains("\"Amount\": \"5\"", json);
            Assert.DoesNotContain("\"Amount\": \"-1\"", json);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_AppliesSelectProjection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "input.csv");
            var outPath = Path.Combine(tempDir, "output.json");
            var jobPath = Path.Combine(tempDir, "job.yaml");

            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount,Note
                1,10,keep
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                transforms:
                  - select:
                      Id: OrderId
                      Total: Amount
                sink:
                  type: json
                  path: {{outPath}}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            var json = await File.ReadAllTextAsync(outPath);
            Assert.Contains("\"Id\": \"1\"", json);
            Assert.Contains("\"Total\": \"10\"", json);
            Assert.DoesNotContain("Note", json);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

public class JobYamlParserTests
{
    [Fact]
    public void Parse_ReadsBasicJob()
    {
        var job = JobYamlParser.Parse("""
            source:
              type: csv
              path: ./a.csv
            sink:
              type: json
              path: ./b.json
            """);

        Assert.Equal("csv", job.Source.Type);
        Assert.Equal("json", job.Sink.Type);
    }
}
