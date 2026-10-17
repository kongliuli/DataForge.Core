using DataForge.Core.Core.Infrastructure;
using DataForge.Jobs.Catalog;
using Microsoft.Data.SqlClient;

namespace DataForge.Jobs.SqlServer;

internal sealed class SqlServerCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["sqlserver"];

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
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
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            tables.Add(reader.GetString(0));
        return tables;
    }

    public async Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        var safe = SqlIdentifier.ValidateTableName(tableName);
        var columns = new List<TableColumnInfo>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                   CASE WHEN kcu.COLUMN_NAME IS NULL THEN 0 ELSE 1 END AS is_pk
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON c.TABLE_NAME = kcu.TABLE_NAME AND c.COLUMN_NAME = kcu.COLUMN_NAME
             AND EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                WHERE tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY')
            WHERE c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION
            """;
        command.Parameters.AddWithValue("@table", safe);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new TableColumnInfo
            {
                Name = reader.GetString(0),
                Type = reader.GetString(1),
                IsNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
                IsPrimaryKey = Convert.ToInt32(reader.GetValue(3)) == 1
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
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT TOP ({take}) * FROM {SqlIdentifiers.QuoteSqlServer(safe)}";
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
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1 FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME = @table
            """;
        command.Parameters.AddWithValue("@table", safe);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result is not DBNull;
    }
}
