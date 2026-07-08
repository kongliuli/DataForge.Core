using DataForge.Sync.Execution;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return args.Length == 0 ? 1 : 0;
}

if (args[0] == "run")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Missing job file. Usage: dataforge run <job.yaml> [--var key=value ...]");
        return 1;
    }

    var jobPath = args[1];
    var variables = ParseVariables(args.AsSpan(2));
    var runner = new JobRunner();
    var result = await runner.RunAsync(jobPath, variables).ConfigureAwait(false);

    if (!result.Success)
    {
        Console.Error.WriteLine($"Job failed: {result.ErrorMessage}");
        return 1;
    }

    var export = result.ExportResults!;
    Console.WriteLine(
        $"Job completed: {export.RecordsWritten} record(s) written to {export.OutputPath}");
    return 0;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
PrintHelp();
return 1;

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
        {
            AddVariable(variables, arg["--var=".Length..]);
        }
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
          dataforge help

        Supported v0.3:
          source: csv | json
          transforms: where, select
          sink: csv | json

        Variables:
          --var lastSync=2026-07-01
          ${DB_CONN} in paths resolves CLI vars then environment variables
          @lastSync in where expressions resolves --var values

        Docs: docs/roadmap-and-iteration.md §8.5
        """);
}
