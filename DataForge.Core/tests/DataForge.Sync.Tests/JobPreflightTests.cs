using DataForge.Jobs.Models;
using DataForge.Jobs.Preflight;
using DataForge.Jobs.Parsing;
using Xunit;

namespace DataForge.Sync.Tests;

/// <summary>
/// Public job preflight (<see cref="JobPreflight"/>): structured error/warning report over configuration and
/// registered adapter capabilities, without ever executing a job, migration, DDL or DML.
/// </summary>
public class JobPreflightTests
{
    [Fact]
    public async Task ValidFileJob_HasNoErrorsOrWarnings_AndReportsLimitations()
    {
        var directory = TempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "input.csv"), "Id\n1\n");
            await File.WriteAllTextAsync(
                Path.Combine(directory, "job.yaml"),
                """
                source: { type: csv, path: input.csv }
                sink: { type: json, path: output.json }
                """);

            var report = await JobPreflight.CheckAsync(Path.Combine(directory, "job.yaml"));

            Assert.True(report.Success, report.ToString());
            Assert.Empty(report.Errors);
            Assert.Empty(report.Warnings);
            // Every report must state what preflight does not cover.
            Assert.Contains(report.Issues, i => i.Code == PreflightCodes.LimitationTypeMappingNotPerformed);
            Assert.Contains(report.Issues, i => i.Code == PreflightCodes.LimitationPermissionsNotVerified);
            Assert.Contains(report.Issues, i => i.Code == PreflightCodes.LimitationFileContentNotRead);
            var schemaNote = report.Issues.Single(i => i.Code == PreflightCodes.SchemaNotChecked);
            Assert.Equal(PreflightSeverity.Info, schemaNote.Severity);
            Assert.Contains("CheckSchema", schemaNote.Message);
            // Non-destructive: nothing was written and nothing was executed.
            Assert.False(File.Exists(Path.Combine(directory, "output.json")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task MissingRequiredSinkPath_ReportsErrorAtItsPath()
    {
        var report = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            sink: { type: csv }
            """);

        Assert.False(report.Success);
        Assert.Contains(report.Errors, e => e.Code == PreflightCodes.SinkPathRequired && e.Path == "job.sink.path");
    }

    [Fact]
    public async Task UnregisteredSinkType_IsReportedAsError()
    {
        // The test assembly registers csv/json plus sqlite/postgresql/sqlserver/parquet/duckdb — not mysql.
        var report = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            sink: { type: mysql, connection: "Server=localhost;Database=x;", table: t }
            """);

        Assert.Contains(report.Errors, e => e.Code == PreflightCodes.SinkTypeUnregistered);
    }

    [Fact]
    public async Task UpsertWithoutKeys_ReportsError_UpsertOnCsvSink_ReportsWarnings()
    {
        var upsert = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            sink: { type: sqlite, connection: "Data Source=data.db", table: t, mode: upsert }
            """);
        Assert.Contains(upsert.Errors, e => e.Code == PreflightCodes.SinkKeysRequired);

        // csv sinks only support insert; upsert silently degrades to insert at run time.
        var csvSink = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            sink: { type: csv, path: output.csv, mode: upsert, keys: [Id] }
            """);
        Assert.Empty(csvSink.Errors);
        Assert.Contains(csvSink.Warnings, w => w.Code == PreflightCodes.SinkModeUnsupported);
    }

    [Fact]
    public async Task InvalidWhereExpressionAndUndefinedVariable_AreErrors()
    {
        var report = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            transforms:
              - where: "Age between 1 and 10"
            sink: { type: json, path: output.json }
            """);

        Assert.Contains(report.Errors, e => e.Code == PreflightCodes.TransformWhereInvalid);

        var undefinedVariable = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            transforms:
              - where: "Age > @MIN"
            sink: { type: json, path: output.json }
            """);
        Assert.Contains(undefinedVariable.Errors, e => e.Code == PreflightCodes.TransformWhereUndefinedVariable);
    }

    [Fact]
    public async Task InvalidSchedule_IsAnError()
    {
        var report = await JobPreflight.CheckYamlAsync(
            """
            schedule: "not-a-cron"
            source: { type: csv, path: input.csv }
            sink: { type: json, path: output.json }
            """);

        Assert.Contains(report.Errors, e => e.Code == PreflightCodes.ScheduleInvalid);
    }

    [Fact]
    public async Task MissingFileSourceAndJobFile_AreReported()
    {
        var report = await JobPreflight.CheckYamlAsync(
            """
            source: { type: csv, path: input.csv }
            sink: { type: json, path: output.json }
            """, Path.Combine(TempDirectory(), "job.yaml"));

        // A missing csv source is a warning: preflight never opens the file, only reports existence.
        Assert.Contains(report.Warnings, w => w.Code == PreflightCodes.SourcePathMissing);
        Assert.Empty(report.Errors);

        var missingJobFile = await JobPreflight.CheckAsync(Path.Combine(TempDirectory(), "job.yaml"));
        Assert.Contains(missingJobFile.Errors, e => e.Code == PreflightCodes.FileNotFound);
    }

    [Fact]
    public async Task BatchFile_Jobs_ReportsEachJobUnderItsLabel()
    {
        var directory = TempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "job.yaml"),
                """
                jobs:
                  - source: { type: csv, path: input.csv }
                    sink: { type: json, path: output1.json }
                  - source: { type: csv, path: input.csv }
                    sink: { type: csv }
                """);

            var report = await JobPreflight.CheckAsync(Path.Combine(directory, "job.yaml"));

            Assert.False(report.Success);
            Assert.Contains(report.Errors,
                e => e.Code == PreflightCodes.SinkPathRequired && e.Path == "jobs[1].sink.path");
            // Primary-source issues are attributed per job label, not shared across the batch.
            Assert.Contains(report.Warnings,
                w => w.Code == PreflightCodes.SourcePathMissing && w.Path == "jobs[0].source.path");
            Assert.Contains(report.Warnings,
                w => w.Code == PreflightCodes.SourcePathMissing && w.Path == "jobs[1].source.path");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CheckAsync_DoesNotMutateTheCallerJobDefinition()
    {
        var directory = TempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "input.csv"), "Id\n1\n");
            var job = JobYamlParser.Parse(
                """
                source: { type: csv, path: '${INPUT_DIR}/input.csv' }
                sink: { type: json, path: output.json }
                """);

            var report = await JobPreflight.CheckAsync(
                job,
                Path.Combine(directory, "job.yaml"),
                new JobPreflightOptions
                {
                    Variables = new Dictionary<string, string> { ["INPUT_DIR"] = "." }
                });

            Assert.True(report.Success, report.ToString());
            Assert.Equal("${INPUT_DIR}/input.csv", job.Source.Path);
            Assert.Equal("output.json", job.Sink.Path);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void SyncCheck_AndUndefinedVariable_AreReported()
    {
        var report = JobPreflight.Check(
            new JobDefinition
            {
                Source = new SourceDefinition { Type = "csv", Path = "${INPUT_DIR}/input.csv" },
                Sink = new SinkDefinition { Type = "json", Path = "output.json" }
            });

        Assert.Empty(report.Errors);
        Assert.Contains(report.Warnings, w => w.Code == PreflightCodes.SourceVariableUnresolved);
        Assert.Contains(report.Issues, i => i.Code == PreflightCodes.SchemaNotChecked);
    }

    [Fact]
    public async Task CheckSchema_ProbesRelationalSource_AndReportsMissingTable()
    {
        // sqlite catalog provider is registered in this assembly and opening a connection string creates the
        // file, so connectivity succeeds and the first real finding is the missing source table. The probe is
        // catalog-only (read-only metadata): preflight must not create tables or rows.
        var report = await JobPreflight.CheckYamlAsync(
            """
            source: { type: sqlite, connection: "Data Source=missing-preflight-db.sqlite;Mode=ReadOnly", table: t }
            sink: { type: json, path: output.json }
            """,
            options: new JobPreflightOptions { CheckSchema = true, CheckTimeout = TimeSpan.FromSeconds(3) });

        Assert.False(report.Success);
        Assert.Contains(report.Errors, e => e.Code == PreflightCodes.SourceTableMissing);
        // The footer always re-states the schema-scope caveat; with CheckSchema on it no longer claims
        // that nothing was checked.
        var schemaNote = report.Issues.Single(i => i.Code == PreflightCodes.SchemaNotChecked);
        Assert.DoesNotContain("未检查", schemaNote.Message);
    }

    private static string TempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dataforge-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
