using DataForge.Jobs.Execution;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobJoinBatchTests
{
    [Fact]
    public async Task Join_TwoSqliteSources_WritesMergedRows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-join-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "src.db");
        var dest = Path.Combine(dir, "out.db");
        var yamlPath = Path.Combine(dir, "join.yaml");

        await using (var connection = new SqliteConnection($"Data Source={db}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE customers (Id TEXT, Name TEXT);
                INSERT INTO customers VALUES ('1','Ada'),('2','Bob');
                CREATE TABLE orders (Id TEXT, CustomerId TEXT, Amount TEXT);
                INSERT INTO orders VALUES ('10','1','12'),('11','1','8'),('12','2','5');
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var yaml = $"""
            name: join-orders
            sources:
              - type: sqlite
                alias: c
                connection: Data Source={db.Replace('\\','/')}
                table: customers
              - type: sqlite
                alias: o
                connection: Data Source={db.Replace('\\','/')}
                table: orders
                on: c_Id = o_CustomerId
            transforms:
              - select:
                  Buyer: c_Name
                  OrderTotal: o_Amount
            sink:
              type: sqlite
              connection: Data Source={dest.Replace('\\','/')}
              table: summary
              createTable: true
              mode: insert
            """;
        await File.WriteAllTextAsync(yamlPath, yaml);

        var result = await new JobRunner().RunAsync(yamlPath);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, result.ExportResults!.RecordsWritten);

        await using var check = new SqliteConnection($"Data Source={dest}");
        await check.OpenAsync();
        await using var read = check.CreateCommand();
        read.CommandText = "SELECT Buyer, OrderTotal FROM summary ORDER BY Buyer, OrderTotal";
        await using var reader = await read.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0) + ":" + reader.GetString(1));

        Assert.Equal(["Ada:12", "Ada:8", "Bob:5"], rows);
    }

    [Fact]
    public async Task Batch_RunsJobsInOrder_AndStopsOnFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-batch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "b.db");
        var csv = Path.Combine(dir, "in.csv");
        await File.WriteAllTextAsync(csv, "Id,Name\n1,Ada\n");
        var yamlPath = Path.Combine(dir, "batch.yaml");
        var yaml = $"""
            name: batch
            jobs:
              - name: load
                source:
                  type: csv
                  path: {csv.Replace('\\','/')}
                sink:
                  type: sqlite
                  connection: Data Source={db.Replace('\\','/')}
                  table: people
                  createTable: true
                  mode: insert
              - name: missing
                source:
                  type: csv
                  path: {dir.Replace('\\','/')}/nope.csv
                sink:
                  type: json
                  path: {dir.Replace('\\','/')}/out.json
            """;
        await File.WriteAllTextAsync(yamlPath, yaml);

        var results = await JobBatch.RunFileAsync(yamlPath);
        Assert.Equal(2, results.Count);
        Assert.True(results[0].Success, results[0].ErrorMessage);
        Assert.Equal(1, results[0].ExportResults!.RecordsWritten);
        Assert.False(results[1].Success);
    }
}
