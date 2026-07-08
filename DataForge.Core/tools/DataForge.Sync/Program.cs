using DataForge.Sync.Execution;

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
        Console.Error.WriteLine("Missing job file. Usage: dataforge run <job.yaml> [--var key=value ...]");
        return 1;
    }

    var jobPath = args[1];
    var variables = ParseVariables(args.AsSpan(2));
    var result = await new JobRunner().RunAsync(jobPath, variables).ConfigureAwait(false);

    if (!result.Success)
    {
        Console.Error.WriteLine($"Job failed: {result.ErrorMessage}");
        return 1;
    }

    var export = result.ExportResults!;
    Console.WriteLine($"Job completed: {export.RecordsWritten} record(s) written to {export.OutputPath}");
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
    var variables = ParseVariables(args.AsSpan(2));
    await JobScheduler.WatchAsync(new JobRunner(), jobPath, variables, cts.Token).ConfigureAwait(false);
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

static Dictionary<string, string> ParseVariables(ReadOnlySpan<string> args)
{
    var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            AddVariable(variables, arg["--var=".Length..]);
    }

    return variables;
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
          dataforge run <job.yaml> [--var key=value ...]
          dataforge watch <job.yaml> [--var key=value ...]
          dataforge help

        Supported:
          source: csv | json | sqlserver | parquet | duckdb
          transforms: where, select
          validate: rules (required/min/max/pattern), onError, badRowOutput
          sink: csv | json | sqlserver | parquet | duckdb
          schedule: cron (5-field, used by watch)

        Variables:
          --var lastSync=2026-07-01
          ${VAR} in paths/connections — CLI vars then environment
          @lastSync in where expressions — CLI vars only

        Docs: docs/roadmap-and-iteration.md §8.5
        """);
}
