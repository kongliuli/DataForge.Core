using DataForge.Sync.Execution;
using DataForge.Sync.Parsing;
using DataForge.Sync.Validation;
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

public class JobRowValidatorTests
{
    [Fact]
    public void Validate_FailsRequiredField()
    {
        var validator = new JobRowValidator([
            new Models.YamlValidationRule { Field = "OrderId", Required = true }
        ]);

        var result = validator.Validate(new JobRow { Values = { ["OrderId"] = "" } });
        Assert.False(result.IsValid);
        Assert.Equal("OrderId", result.Errors[0].PropertyName);
    }

    [Fact]
    public void Validate_EnforcesMinValue()
    {
        var validator = new JobRowValidator([
            new Models.YamlValidationRule { Field = "Amount", Min = 0 }
        ]);

        Assert.False(validator.Validate(new JobRow { Values = { ["Amount"] = "-1" } }).IsValid);
        Assert.True(validator.Validate(new JobRow { Values = { ["Amount"] = "0" } }).IsValid);
    }
}

public class JobRunnerValidationTests
{
    [Fact]
    public async Task RunAsync_SkipsInvalidRowsOnContinue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "input.csv");
            var outPath = Path.Combine(tempDir, "output.json");
            var errPath = Path.Combine(tempDir, "errors.ndjson");
            var jobPath = Path.Combine(tempDir, "job.yaml");

            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount
                1,10
                2,-5
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                validate:
                  onError: continue
                  badRowOutput: {{errPath}}
                  rules:
                    - field: Amount
                      min: 0
                sink:
                  type: json
                  path: {{outPath}}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, result.ExportResults!.RecordsWritten);
            Assert.True(File.Exists(errPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

public class JobSchedulerTests
{
    [Fact]
    public void GetNextOccurrence_ReturnsFutureTime()
    {
        var next = JobScheduler.GetNextOccurrence("* * * * *", DateTimeOffset.UtcNow);
        Assert.NotNull(next);
        Assert.True(next > DateTimeOffset.UtcNow.AddSeconds(-1));
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

    [Fact]
    public void Parse_ReadsSqlServerSink()
    {
        var job = JobYamlParser.Parse("""
            source:
              type: csv
              path: ./a.csv
            sink:
              type: sqlserver
              connection: Server=.;Database=Test
              table: Fact_Orders
              mode: upsert
              keys: [OrderId]
            """);

        Assert.Equal("sqlserver", job.Sink.Type);
        Assert.Equal("Fact_Orders", job.Sink.Table);
        Assert.Single(job.Sink.Keys);
    }

    [Fact]
    public void Parse_RequiresKeysForUpsert()
    {
        Assert.Throws<InvalidOperationException>(() => JobYamlParser.Parse("""
            source:
              type: csv
              path: ./a.csv
            sink:
              type: sqlserver
              connection: Server=.
              table: T
              mode: upsert
            """));
    }
}
