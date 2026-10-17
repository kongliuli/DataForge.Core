using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Jobs.Models;
using MySqlConnector;
using System.Diagnostics;

namespace DataForge.Jobs.MySql;

public static class JobRowMySqlSink
{
    public static async Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        var connectionString = sink.Connection ?? throw new InvalidOperationException("MySQL sink requires connection.");
        var table = SqlIdentifier.ValidateTableName(sink.Table ?? throw new InvalidOperationException("MySQL sink requires table."));
        var quoted = SqlIdentifiers.QuoteMySql(table);
        var sw = Stopwatch.StartNew();
        var count = 0;
        string[]? columns = null;

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            columns ??= row.Values.Keys.Select(SqlIdentifier.ValidateTableName).ToArray();
            if (columns.Length == 0)
                continue;

            var names = string.Join(", ", columns.Select(SqlIdentifiers.QuoteMySql));
            var parms = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
            await using var command = connection.CreateCommand();
            command.CommandText = $"INSERT INTO {quoted} ({names}) VALUES ({parms})";
            for (var i = 0; i < columns.Length; i++)
                command.Parameters.AddWithValue($"@p{i}", (object?)row.Get(columns[i]) ?? DBNull.Value);
            count += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        sw.Stop();
        return new ExportResults
        {
            RecordsWritten = count,
            Duration = sw.Elapsed,
            OutputPath = table
        };
    }
}
