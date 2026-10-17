using DataForge.Sync.Execution;
using Microsoft.Data.Sqlite;
using Npgsql;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobRunnerSqlitePostgreSqlTests
{
    private const string PgConnection =
        "Host=127.0.0.1;Port=5433;Username=dataforge;Password=dataforge;Database=dataforge";

    [Fact]
    public async Task RunAsync_SqliteToSqlite_Homogeneous()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-sync-sqlite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var srcDb = Path.Combine(tempDir, "src.db");
            var dstDb = Path.Combine(tempDir, "dst.db");
            var jobPath = Path.Combine(tempDir, "job.yaml");
            var srcConn = SqliteConn(srcDb);
            var dstConn = SqliteConn(dstDb);

            await using (var conn = new SqliteConnection(srcConn))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE orders (OrderId INTEGER PRIMARY KEY, Amount REAL);
                    INSERT INTO orders VALUES (1, 10.5), (2, -1), (3, 7);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var conn = new SqliteConnection(dstConn))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE orders (OrderId INTEGER PRIMARY KEY, Amount REAL);";
                await cmd.ExecuteNonQueryAsync();
            }

            await File.WriteAllTextAsync(jobPath, $$"""
                name: sqlite-homo
                source:
                  type: sqlite
                  connection: {{srcConn}}
                  table: orders
                transforms:
                  - where: "Amount > 0"
                sink:
                  type: sqlite
                  connection: {{dstConn}}
                  table: orders
                  mode: insert
                """);

            var result = await new JobRunner().RunAsync(jobPath);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(2, result.ExportResults!.RecordsWritten);

            await using (var verify = new SqliteConnection(dstConn))
            {
                await verify.OpenAsync();
                await using var countCmd = verify.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM orders";
                Assert.Equal(2L, (long)(await countCmd.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteDirectory(tempDir);
        }
    }

    [Fact]
    public async Task RunAsync_SqliteToPostgreSql_ThenBack()
    {
        await EnsurePostgresAvailableAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-sync-pg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var pgTable = $"sync_orders_{suffix}";
        var summaryTable = $"sync_summary_{suffix}";

        try
        {
            var srcDb = Path.Combine(tempDir, "src.db");
            var outDb = Path.Combine(tempDir, "out.db");
            var toPgJob = Path.Combine(tempDir, "to-pg.yaml");
            var toSqliteJob = Path.Combine(tempDir, "to-sqlite.yaml");
            var srcConn = SqliteConn(srcDb);
            var outConnStr = SqliteConn(outDb);

            await using (var conn = new SqliteConnection(srcConn))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE orders (OrderId INTEGER PRIMARY KEY, Customer TEXT, Amount REAL);
                    INSERT INTO orders VALUES (1, 'Alice', 10), (2, 'Bob', 20);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var pg = new NpgsqlConnection(PgConnection))
            {
                await pg.OpenAsync();
                await using var cmd = pg.CreateCommand();
                cmd.CommandText = $"""
                    DROP TABLE IF EXISTS "{pgTable}";
                    CREATE TABLE "{pgTable}" ("OrderId" INT PRIMARY KEY, "Customer" TEXT, "Amount" DOUBLE PRECISION);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            await File.WriteAllTextAsync(toPgJob, $$"""
                name: sqlite-to-pg
                source:
                  type: sqlite
                  connection: {{srcConn}}
                  table: orders
                sink:
                  type: postgresql
                  connection: {{PgConnection}}
                  table: {{pgTable}}
                  mode: insert
                """);

            var toPg = await new JobRunner().RunAsync(toPgJob);
            Assert.True(toPg.Success, toPg.ErrorMessage);
            Assert.Equal(2, toPg.ExportResults!.RecordsWritten);

            await using (var outConn = new SqliteConnection(outConnStr))
            {
                await outConn.OpenAsync();
                await using var cmd = outConn.CreateCommand();
                cmd.CommandText = $"CREATE TABLE {summaryTable} (OrderId INTEGER PRIMARY KEY, Buyer TEXT, OrderTotal REAL);";
                await cmd.ExecuteNonQueryAsync();
            }

            await File.WriteAllTextAsync(toSqliteJob, $$"""
                name: pg-to-sqlite-hetero
                source:
                  type: postgresql
                  connection: {{PgConnection}}
                  table: {{pgTable}}
                transforms:
                  - select:
                      OrderId: OrderId
                      Buyer: Customer
                      OrderTotal: Amount
                sink:
                  type: sqlite
                  connection: {{outConnStr}}
                  table: {{summaryTable}}
                  mode: upsert
                  keys: [OrderId]
                """);

            var back = await new JobRunner().RunAsync(toSqliteJob);
            Assert.True(back.Success, back.ErrorMessage);
            Assert.Equal(2, back.ExportResults!.RecordsWritten);

            await using (var verify = new SqliteConnection(outConnStr))
            {
                await verify.OpenAsync();
                await using var q = verify.CreateCommand();
                q.CommandText = $"SELECT Buyer, OrderTotal FROM {summaryTable} WHERE OrderId = 2";
                await using var reader = await q.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Bob", reader.GetString(0));
                Assert.Equal(20d, reader.GetDouble(1));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                await using var pg = new NpgsqlConnection(PgConnection);
                await pg.OpenAsync();
                await using var cmd = pg.CreateCommand();
                cmd.CommandText = $"DROP TABLE IF EXISTS \"{pgTable}\";";
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // best-effort cleanup
            }

            TryDeleteDirectory(tempDir);
        }
    }

    private static string SqliteConn(string path) =>
        $"Data Source={path};Pooling=False;Cache=Shared";

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Windows may still hold the file briefly; leave temp for OS cleanup.
        }
    }

    private static async Task EnsurePostgresAvailableAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(PgConnection);
            await conn.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "PostgreSQL is required for this test. Start: docker start dataforge-pg (port 5433). " + ex.Message,
                ex);
        }
    }
}
