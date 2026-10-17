using DataForge.Jobs.Catalog;
using DataForge.Jobs.DuckDB;
using DataForge.Jobs.Excel;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Http;
using DataForge.Jobs.MySql;
using DataForge.Jobs.Parquet;
using DataForge.Jobs.PostgreSql;
using DataForge.Jobs.Sqlite;
using DataForge.Jobs.SqlServer;
using DataForge.Sync.Execution;
using System.Text.Json;

JobSqliteRegistration.AddDataForgeJobsSqlite();
JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();
JobSqlServerRegistration.AddDataForgeJobsSqlServer();
JobParquetRegistration.AddDataForgeJobsParquet();
JobDuckDBRegistration.AddDataForgeJobsDuckDB();
JobMySqlRegistration.AddDataForgeJobsMySql();
JobExcelRegistration.AddDataForgeJobsExcel();
JobHttpRegistration.AddDataForgeJobsHttp();

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return args.Length == 0 ? 1 : 0;
}

try
{
    return args[0] switch
    {
        "run" => await RunOnceAsync(args),
        "watch" => await WatchAsync(args),
        "catalog" => await CatalogAsync(args),
        _ => UnknownCommand(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static async Task<int> RunOnceAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Missing job file. Usage: dataforge run <job.yaml> [--var key=value] [--report run.json]");
        return 1;
    }

    var jobPath = args[1];
    var (variables, reportPath) = ParseRunOptions(args.AsSpan(2));
    var results = await JobBatch.RunFileAsync(jobPath, variables).ConfigureAwait(false);

    foreach (var result in results)
    {
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var report = results.Count == 1
                ? reportPath
                : Path.Combine(
                    Path.GetDirectoryName(reportPath) ?? ".",
                    Path.GetFileNameWithoutExtension(reportPath) + "-" + (result.JobName ?? "job") + Path.GetExtension(reportPath));
            await JobRunReportWriter.WriteAsync(result, report).ConfigureAwait(false);
        }

        if (!result.Success)
        {
            Console.Error.WriteLine($"Job failed: {result.ErrorMessage}");
            return 1;
        }

        var export = result.ExportResults!;
        Console.WriteLine($"Job completed: {export.RecordsWritten} record(s) written to {export.OutputPath}");
    }
    if (!string.IsNullOrWhiteSpace(reportPath))
        Console.WriteLine($"Report written to {Path.GetFullPath(reportPath)}");
    return 0;
}

static async Task<int> CatalogAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dataforge catalog <test|tables|describe|preview|types> --type <db> --connection <cs> [--table name] [--limit n]");
        return 1;
    }

    var action = args[1].ToLowerInvariant();
    if (action == "types")
    {
        Console.WriteLine(string.Join(Environment.NewLine, JobCatalog.RegisteredTypes));
        return 0;
    }

    string? type = null;
    string? connection = null;
    string? table = null;
    var limit = 20;

    for (var i = 2; i < args.Length; i++)
    {
        var arg = args[i];
        string? Next() => i + 1 < args.Length ? args[++i] : throw new InvalidOperationException($"Missing value for {arg}");

        if (arg is "--type" or "-t") type = Next();
        else if (arg.StartsWith("--type=", StringComparison.Ordinal)) type = arg["--type=".Length..];
        else if (arg is "--connection" or "-c") connection = Next();
        else if (arg.StartsWith("--connection=", StringComparison.Ordinal)) connection = arg["--connection=".Length..];
        else if (arg is "--table") table = Next();
        else if (arg.StartsWith("--table=", StringComparison.Ordinal)) table = arg["--table=".Length..];
        else if (arg is "--limit") limit = int.Parse(Next()!);
        else if (arg.StartsWith("--limit=", StringComparison.Ordinal)) limit = int.Parse(arg["--limit=".Length..]);
    }

    if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(connection))
    {
        Console.Error.WriteLine("--type and --connection are required");
        return 1;
    }

    object payload = action switch
    {
        "test" => await JobCatalog.TestConnectionAsync(type, connection),
        "tables" => await JobCatalog.ListTablesAsync(type, connection),
        "describe" => string.IsNullOrWhiteSpace(table)
            ? throw new InvalidOperationException("--table is required for describe")
            : await JobCatalog.DescribeTableAsync(type, connection, table),
        "preview" => string.IsNullOrWhiteSpace(table)
            ? throw new InvalidOperationException("--table is required for preview")
            : await JobCatalog.PreviewAsync(type, connection, table, limit),
        "exists" => string.IsNullOrWhiteSpace(table)
            ? throw new InvalidOperationException("--table is required for exists")
            : new { exists = await JobCatalog.TableExistsAsync(type, connection, table) },
        _ => throw new InvalidOperationException($"Unknown catalog action '{action}'")
    };

    Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

static async Task<int> WatchAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Missing job file. Usage: dataforge watch <job.yaml> [--var key=value ...]");
        return 1;
    }

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var jobPath = args[1];
    var (variables, _) = ParseRunOptions(args.AsSpan(2));
    await JobScheduler.WatchAsync(new JobRunner(), jobPath, variables, cts.Token).ConfigureAwait(false);
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

static (Dictionary<string, string> Variables, string? ReportPath) ParseRunOptions(ReadOnlySpan<string> args)
{
    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    string? reportPath = null;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg is "--var" or "-v")
        {
            if (i + 1 >= args.Length)
                throw new InvalidOperationException("Missing value for --var.");
            AddVariable(variables, args[++i]);
            continue;
        }

        if (arg.StartsWith("--var=", StringComparison.Ordinal))
        {
            AddVariable(variables, arg["--var=".Length..]);
            continue;
        }

        if (arg is "--report" or "-r")
        {
            if (i + 1 >= args.Length)
                throw new InvalidOperationException("Missing path for --report.");
            reportPath = args[++i];
            continue;
        }

        if (arg.StartsWith("--report=", StringComparison.Ordinal))
            reportPath = arg["--report=".Length..];
    }

    return (variables, reportPath);
}

static void AddVariable(Dictionary<string, string> variables, string pair)
{
    var index = pair.IndexOf('=');
    if (index <= 0)
        throw new InvalidOperationException($"Invalid --var value '{pair}'. Expected key=value.");
    variables[pair[..index]] = pair[(index + 1)..];
}

static void PrintHelp()
{
    Console.WriteLine("""
        DataForge.Sync — YAML-driven ETL CLI (DEC-03)

        Usage:
          dataforge run <job.yaml> [--var key=value] [--report run.json]
          dataforge watch <job.yaml> [--var key=value ...]
          dataforge catalog <test|tables|describe|preview|exists|types> --type <db> --connection <cs> [--table name] [--limit n]
          dataforge help

        Supported:
          source/sink: csv | json | sqlite | postgresql | sqlserver | mysql | excel | http | parquet | duckdb
          transforms: where, select, join (sources[])
          validate: rules (required/min/max/pattern), onError, badRowOutput
          schedule: cron (5-field, used by watch)
          catalog types: sqlite | postgresql | sqlserver | mysql

        Variables:
          --var lastSync=2026-07-01
          ${VAR} in paths/connections — CLI vars then environment
          @lastSync in where expressions — CLI vars only

        Observability (S-04 minimal):
          --report run.json   JSON run summary (records, duration, row errors)

        Docs: docs/roadmap-and-iteration.md §8.5
        """);
}
