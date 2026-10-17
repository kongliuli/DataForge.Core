using DuckDB.NET.Data;
using System.Runtime.CompilerServices;

namespace DataForge.Sync.Sources;

internal sealed class JobRowDuckDbSource
{
    private readonly string _databasePath;
    private readonly string _sql;

    public JobRowDuckDbSource(string databasePath, string sql)
    {
        _databasePath = databasePath;
        _sql = sql;
    }

    public async IAsyncEnumerable<JobRow> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connectionString = DuckDbConnectionHelper.BuildConnectionString(_databasePath);
        await using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = _sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new JobRow();
            for (var i = 0; i < reader.FieldCount; i++)
                row.Values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
            yield return row;
        }
    }
}

internal static class DuckDbConnectionHelper
{
    public static string BuildConnectionString(string databasePath) =>
        string.IsNullOrWhiteSpace(databasePath) || databasePath == ":memory:"
            ? "Data Source=:memory:"
            : $"Data Source={databasePath}";
}
