using DataForge.Jobs.Catalog;
using DuckDB.NET.Data;

namespace DataForge.Jobs.DuckDB;

internal sealed class DuckDbCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["duckdb"];

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = Open(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
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
        await using var connection = Open(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main' ORDER BY table_name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            tables.Add(reader.GetString(0));
        return tables;
    }

    public async Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        var safe = tableName.Replace("'", "''", StringComparison.Ordinal);
        var columns = new List<TableColumnInfo>();
        await using var connection = Open(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'main' AND table_name = '{safe}'
            ORDER BY ordinal_position
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new TableColumnInfo
            {
                Name = reader.GetString(0),
                Type = reader.IsDBNull(1) ? "VARCHAR" : reader.GetString(1),
                IsNullable = !reader.IsDBNull(2) && reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase)
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
        var quoted = Quote(tableName);
        var take = Math.Clamp(limit, 1, 500);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        await using var connection = Open(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {quoted} LIMIT {take}";
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
        var tables = await ListTablesAsync(connectionString, cancellationToken).ConfigureAwait(false);
        return tables.Any(name => name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
    }

    private static DuckDBConnection Open(string connectionString)
    {
        var cs = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? connectionString
            : string.IsNullOrWhiteSpace(connectionString) || connectionString == ":memory:"
                ? "Data Source=:memory:"
                : $"Data Source={connectionString}";
        return new DuckDBConnection(cs);
    }

    private static string Quote(string tableName)
    {
        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(part => part.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_')))
            throw new InvalidOperationException($"Invalid DuckDB table name '{tableName}'.");
        return string.Join('.', parts.Select(part => $"\"{part}\""));
    }
}
