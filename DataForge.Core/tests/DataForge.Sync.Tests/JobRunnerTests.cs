using DataForge.Jobs;
using DataForge.Jobs.DuckDB;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using DataForge.Jobs.Parquet;
using DataForge.Jobs.Parsing;
using DataForge.Jobs.PostgreSql;
using DataForge.Jobs.Scheduling;
using DataForge.Jobs.Sqlite;
using DataForge.Jobs.SqlServer;
using DataForge.Jobs.Validation;
using Xunit;

namespace DataForge.Sync.Tests;

public class SyncProviderRegistration
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register()
    {
        JobSqliteRegistration.AddDataForgeJobsSqlite();
        JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();
        JobSqlServerRegistration.AddDataForgeJobsSqlServer();
        JobParquetRegistration.AddDataForgeJobsParquet();
        JobDuckDBRegistration.AddDataForgeJobsDuckDB();
    }
}

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
            new YamlValidationRule { Field = "OrderId", Required = true }
        ]);

        var result = validator.Validate(new JobRow { Values = { ["OrderId"] = "" } });
        Assert.False(result.IsValid);
        Assert.Equal("OrderId", result.Errors[0].PropertyName);
    }

    [Fact]
    public void Validate_EnforcesMinValue()
    {
        var validator = new JobRowValidator([
            new YamlValidationRule { Field = "Amount", Min = 0 }
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

    [Fact]
    public void GetNextOccurrence_HonoursTimeZone()
    {
        var utc = new DateTimeOffset(2026, 3, 22, 0, 30, 0, TimeSpan.Zero);
        var shanghai = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var next = JobScheduler.GetNextOccurrence("0 9 * * *", utc, shanghai);

        Assert.NotNull(next);
        Assert.Equal(new DateTimeOffset(2026, 3, 22, 9, 0, 0, TimeSpan.FromHours(8)), next!.Value);
    }

    [Fact]
    public void ParseSchedule_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => JobScheduler.ParseSchedule("  "));
    }

    [Fact]
    public async Task WatchAsync_ThrowsWhenJobHasNoSchedule()
    {
        var scheduler = new JobScheduler();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scheduler.WatchAsync(new JobDefinition { Name = "no-schedule" }, "no-schedule.yaml"));
    }

    [Fact]
    public async Task WatchAsync_RunsUntilMaxRunsAndReportsTicks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-sched-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, "in.csv");
        var json = Path.Combine(dir, "out.json");
        await File.WriteAllTextAsync(csv, "Id,Name\n1,sched\n");

        var job = JobYamlParser.Parse($"""
            name: sched-job
            schedule: "* * * * *"
            source:
              type: csv
              path: {csv.Replace('\\', '/')}
            sink:
              type: json
              path: {json.Replace('\\', '/')}
            """);

        var baseTime = new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero);
        var ticks = new List<JobScheduleTick>();
        var scheduler = new JobScheduler
        {
            MaxRuns = 2,
            OnTick = ticks.Add,
            Clock = () => baseTime,
            DelayAsync = (_, _) => Task.CompletedTask
        };
        scheduler.Clock = () => baseTime = baseTime.AddMinutes(1);

        await scheduler.WatchAsync(job, Path.Combine(dir, "job.yaml"));

        Assert.Equal(2, ticks.Count);
        Assert.All(ticks, tick => Assert.True(tick.Success, tick.Error));
        Assert.NotNull(ticks[0].Result!.ExportResults);
        Assert.NotNull(ticks[0].NextDueAt);
        Assert.True(ticks[0].NextDueAt > ticks[0].StartedAt);
    }

    [Fact]
    public async Task GetNextRunAsync_ReturnsNullWithoutSchedule()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-sched-null-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "job.yaml");
        await File.WriteAllTextAsync(path, """
            name: no-sched
            source:
              type: csv
              path: ./in.csv
            sink:
              type: json
              path: ./out.json
            """);

        var scheduler = new JobScheduler();
        Assert.Null(await scheduler.GetNextRunAsync(path));
    }

    [Fact]
    public async Task GetNextRunAsync_ReturnsScheduledInstant()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-sched-next-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "job.yaml");
        await File.WriteAllTextAsync(path, """
            name: sched
            schedule: "0 2 * * *"
            source:
              type: csv
              path: ./in.csv
            sink:
              type: json
              path: ./out.json
            """);

        var scheduler = new JobScheduler
        {
            Clock = () => new DateTimeOffset(2026, 3, 22, 0, 0, 0, TimeSpan.Zero)
        };
        var next = await scheduler.GetNextRunAsync(path);

        Assert.NotNull(next);
        Assert.True(next > scheduler.Clock());
    }
}

public class JobWatermarkTests
{
    [Fact]
    public async Task Run_WritesOnlyRowsAfterStoredWatermark()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-wm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "in.csv");
        var outPath = Path.Combine(tempDir, "out.json");
        var statePath = Path.Combine(tempDir, "wm.txt");
        var jobPath = Path.Combine(tempDir, "job.yaml");

        try
        {
            await File.WriteAllTextAsync(csvPath, "Id,Name\n1,a\n2,b\n3,c\n");
            await File.WriteAllTextAsync(statePath, "1");
            await File.WriteAllTextAsync(jobPath, $"""
                name: incremental
                watermark:
                  column: Id
                  statePath: {statePath.Replace('\\', '/')}
                source:
                  type: csv
                  path: {csvPath.Replace('\\', '/')}
                sink:
                  type: json
                  path: {outPath.Replace('\\', '/')}
                """);

            var result = await new JobRunner().RunAsync(jobPath);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(2, result.ExportResults!.RecordsWritten);
            Assert.Equal("3", (await File.ReadAllTextAsync(statePath)).Trim());
            var json = await File.ReadAllTextAsync(outPath);
            Assert.DoesNotContain("\"Id\": \"1\"", json.Replace("\"Id\":\"1\"", "\"Id\": \"1\""));
            Assert.Contains("b", json);
            Assert.Contains("c", json);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Sqlite_PushesWatermarkIntoSql()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-wm-sql-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var db = Path.Combine(tempDir, "src.db");
        var json = Path.Combine(tempDir, "out.json");
        var state = Path.Combine(tempDir, "wm.txt");
        var job = Path.Combine(tempDir, "job.yaml");
        var cs = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = db, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate }.ToString();

        try
        {
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(cs))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Orders (Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO Orders VALUES (1,'a'),(2,'b'),(5,'e');";
                await command.ExecuteNonQueryAsync();
            }

            await File.WriteAllTextAsync(state, "2");
            var yaml = string.Join('\n',
            [
                "name: sqlite-wm",
                "watermark:",
                "  column: Id",
                $"  statePath: {state.Replace('\\', '/')}",
                "source:",
                "  type: sqlite",
                "  connection: " + cs.Replace("\\", "\\\\"),
                "  table: Orders",
                "sink:",
                "  type: json",
                $"  path: {json.Replace('\\', '/')}"
            ]);
            await File.WriteAllTextAsync(job, yaml);

            var result = await new JobRunner().RunAsync(job);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, result.ExportResults!.RecordsWritten);
            Assert.Equal("5", (await File.ReadAllTextAsync(state)).Trim());
            Assert.Contains("e", await File.ReadAllTextAsync(json));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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
