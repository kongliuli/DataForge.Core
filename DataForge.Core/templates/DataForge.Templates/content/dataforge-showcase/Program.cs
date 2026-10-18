using DataForge.Jobs.Execution;
using DataForge.Jobs.PostgreSql;
using DataForge.Jobs.Sqlite;

JobSqliteRegistration.AddDataForgeJobsSqlite();
JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();

if (args.Length != 2 || args[0] is not "run")
{
    Console.WriteLine("""
        DataForge showcase
          dotnet run -- run jobs/00-file-batch.yaml
          dotnet run -- run jobs/01-homo.yaml
          dotnet run -- run jobs/02-split-headers.yaml
          dotnet run -- run jobs/02-split-lines.yaml
          dotnet run -- run jobs/03-merge.yaml
        See README.md. Init SQL lives in sql/.
        """);
    return args.Length == 1 && args[0] is "--help" or "-h" ? 0 : 1;
}

using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};
Console.CancelKeyPress += onCancel;

try
{
    var results = await JobBatch.RunFileAsync(args[1], cancellationToken: cancellation.Token);
    foreach (var result in results)
    {
        if (result.Success)
            Console.WriteLine($"{result.JobName}: wrote {result.ExportResults?.RecordsWritten ?? 0} row(s).");
        else
            Console.Error.WriteLine($"{result.JobName}: {result.ErrorMessage}");
    }
    return results.All(result => result.Success) ? 0 : 1;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Job cancelled.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
finally
{
    Console.CancelKeyPress -= onCancel;
}
