using DataForge.Jobs.Catalog;
using DataForge.Jobs.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataForge.Sync.Tests;

public class JobCatalogTests
{
    [Fact]
    public async Task SqliteCatalog_TestListDescribePreview_Works()
    {
        JobSqliteRegistration.AddDataForgeJobsSqlite();

        var dbPath = Path.Combine(Path.GetTempPath(), "df-catalog-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = $"Data Source={dbPath};Pooling=False";

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE orders (
                        id INTEGER PRIMARY KEY,
                        customer TEXT NOT NULL,
                        amount REAL
                    );
                    INSERT INTO orders(id, customer, amount) VALUES (1, 'A', 10.5), (2, 'B', 20);
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var test = await JobCatalog.TestConnectionAsync("sqlite", connectionString);
            Assert.True(test.Success, test.Error);

            var tables = await JobCatalog.ListTablesAsync("sqlite", connectionString);
            Assert.Contains("orders", tables);

            Assert.True(await JobCatalog.TableExistsAsync("sqlite", connectionString, "orders"));
            Assert.False(await JobCatalog.TableExistsAsync("sqlite", connectionString, "missing_table"));

            var columns = await JobCatalog.DescribeTableAsync("sqlite", connectionString, "orders");
            Assert.Contains(columns, c => c.Name.Equals("id", StringComparison.OrdinalIgnoreCase) && c.IsPrimaryKey);
            Assert.Contains(columns, c => c.Name.Equals("customer", StringComparison.OrdinalIgnoreCase));

            var rows = await JobCatalog.PreviewAsync("sqlite", connectionString, "orders", limit: 10);
            Assert.Equal(2, rows.Count);
            Assert.Equal("A", rows[0]["customer"]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public void RegisteredTypes_IncludesSqlite()
    {
        JobSqliteRegistration.AddDataForgeJobsSqlite();
        Assert.Contains("sqlite", JobCatalog.RegisteredTypes, StringComparer.OrdinalIgnoreCase);
    }
}
