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

var result = await new JobRunner().RunAsync(args[1]);
if (!result.Success)
{
    Console.Error.WriteLine(result.ErrorMessage);
    return 1;
}

Console.WriteLine($"Wrote {result.ExportResults!.RecordsWritten} row(s).");
return 0;
