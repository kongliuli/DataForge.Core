using DataForge.Jobs.Execution;
using DataForge.Jobs.PostgreSql;
using DataForge.Jobs.Sqlite;

JobSqliteRegistration.AddDataForgeJobsSqlite();
JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();

if (args.Length < 2 || args[0] is not "run")
{
    Console.WriteLine("""
        DataForge showcase
          dotnet run -- run jobs/01-homo.yaml
          dotnet run -- run jobs/02-split-headers.yaml
          dotnet run -- run jobs/02-split-lines.yaml
          dotnet run -- run jobs/03-merge.yaml
        See README.md. Init SQL lives in sql/.
        """);
    return args.Length == 0 ? 1 : 0;
}

try
{
    var results = await JobBatch.RunFileAsync(args[1]);
    foreach (var result in results)
    {
        if (result.Success)
            Console.WriteLine($"{result.JobName}: wrote {result.ExportResults?.RecordsWritten ?? 0} row(s).");
        else
            Console.Error.WriteLine($"{result.JobName}: {result.ErrorMessage}");
    }
    return results.All(result => result.Success) ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
