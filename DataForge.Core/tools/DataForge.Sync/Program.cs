using DataForge.Core;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return args.Length == 0 ? 1 : 0;
}

if (args[0] == "run")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Missing job file. Usage: dataforge run <job.yaml>");
        return 1;
    }

    Console.WriteLine($"DataForge.Sync scaffold: job execution is not implemented yet.");
    Console.WriteLine($"Requested job: {args[1]}");
    Console.WriteLine("See docs/roadmap-and-iteration.md §8.5 for the planned YAML schema.");
    return 0;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
PrintHelp();
return 1;

static void PrintHelp()
{
    Console.WriteLine("""
        DataForge.Sync (scaffold)
        
        Usage:
          dataforge run <job.yaml>   Run a YAML sync job (planned)
          dataforge help             Show help
        
        Docs: docs/roadmap-and-iteration.md
        """);
}
