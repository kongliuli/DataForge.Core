using DataForge.Core.Core.Infrastructure;
using DataForge.Jobs.Catalog;
using Npgsql;

namespace DataForge.Jobs.PostgreSql;

internal sealed class PostgreSqlCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["postgresql", "postgres"];

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            ORDER BY table_name
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.column_name, c.data_type, c.is_nullable,
                   CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN true ELSE false END AS is_pk
            FROM information_schema.columns c
            LEFT JOIN information_schema.key_column_usage kcu
              ON c.table_schema = kcu.table_schema AND c.table_name = kcu.table_name AND c.column_name = kcu.column_name
            LEFT JOIN information_schema.table_constraints tc
              ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
             AND tc.constraint_type = 'PRIMARY KEY'
            WHERE c.table_schema = 'public' AND c.table_name = @table
            ORDER BY c.ordinal_position
            """;
        command.Parameters.AddWithValue("table", safe);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new TableColumnInfo
            {
                Name = reader.GetString(0),
                Type = reader.GetString(1),
                IsNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
                IsPrimaryKey = !reader.IsDBNull(3) && reader.GetBoolean(3)
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {SqlIdentifiers.QuotePostgreSql(safe)} LIMIT {take}";
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @table
            LIMIT 1
            """;
        command.Parameters.AddWithValue("table", safe);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null && result is not DBNull;
    }
}
