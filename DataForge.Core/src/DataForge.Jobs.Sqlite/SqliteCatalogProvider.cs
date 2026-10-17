using DataForge.Core.Core.Infrastructure;
using DataForge.Jobs.Catalog;
using Microsoft.Data.Sqlite;

namespace DataForge.Jobs.Sqlite;

internal sealed class SqliteCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["sqlite"];

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail(ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var tables = new List<string>();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            tables.Add(reader.GetString(0));
        return tables;
    }

    public async Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        var safe = SqlIdentifier.ValidateTableName(tableName);
        var columns = new List<TableColumnInfo>();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({SqlIdentifiers.QuoteSqlite(safe)});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new TableColumnInfo
            {
                Name = reader.GetString(1),
                Type = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2),
                IsNullable = reader.GetInt32(3) == 0,
                IsPrimaryKey = reader.GetInt32(5) > 0
            });
        }

        return columns;
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> PreviewAsync(
        string connectionString,
        string tableName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var safe = SqlIdentifier.ValidateTableName(tableName);
        var take = Math.Clamp(limit, 1, 500);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {SqlIdentifiers.QuoteSqlite(safe)} LIMIT {take}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i));
            rows.Add(row);
        }

        return rows;
    }

    public async Task<bool> TableExistsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        var safe = SqlIdentifier.ValidateTableName(tableName);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1";
        command.Parameters.AddWithValue("$name", safe);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result is not DBNull;
    }
}
