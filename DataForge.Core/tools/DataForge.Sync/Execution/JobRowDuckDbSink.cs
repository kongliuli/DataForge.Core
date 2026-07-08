using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Sync.Models;
using DataForge.Sync.Sources;
using DuckDB.NET.Data;
using System.Diagnostics;

namespace DataForge.Sync.Execution;

internal static class JobRowDuckDbSink
{
    public static async Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        var databasePath = sink.Path;
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new InvalidOperationException("DuckDB sink requires path (database file).");

        var tableName = SqlIdentifier.ValidateTableName(
            sink.Table ?? throw new InvalidOperationException("DuckDB sink requires table."));

        var batchSize = sink.Options?.BatchSize ?? 1000;
        var sw = Stopwatch.StartNew();
        var count = 0;
        var batch = new List<JobRow>();
        var tableCreated = false;

        await using var connection = new DuckDBConnection(DuckDbConnectionHelper.BuildConnectionString(databasePath));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!tableCreated && row.Values.Count > 0)
            {
                await EnsureTableAsync(connection, tableName, row, cancellationToken).ConfigureAwait(false);
                tableCreated = true;
            }

            batch.Add(row);
            if (batch.Count >= batchSize)
            {
                count += await WriteBatchAsync(connection, tableName, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            if (!tableCreated)
                await EnsureTableAsync(connection, tableName, batch[0], cancellationToken).ConfigureAwait(false);
            count += await WriteBatchAsync(connection, tableName, batch, cancellationToken).ConfigureAwait(false);
        }

        sw.Stop();
        return new ExportResults
        {
            RecordsWritten = count,
            Duration = sw.Elapsed,
            OutputPath = databasePath
        };
    }

    private static async Task EnsureTableAsync(
        DuckDBConnection connection,
        string tableName,
        JobRow sample,
        CancellationToken ct)
    {
        var columns = string.Join(", ", sample.Values.Keys.Select(k => $"{SqlIdentifier.ValidateTableName(k)} VARCHAR"));
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} ({columns})";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<int> WriteBatchAsync(
        DuckDBConnection connection,
        string tableName,
        List<JobRow> batch,
        CancellationToken ct)
    {
        if (batch.Count == 0)
            return 0;

        var headers = batch[0].Values.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        var columnNames = headers.Select(SqlIdentifier.ValidateTableName).ToArray();

        foreach (var row in batch)
        {
            await using var command = connection.CreateCommand();
            var paramNames = headers.Select((_, i) => $"${i + 1}").ToArray();
            command.CommandText =
                $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";

            foreach (var header in headers)
                command.Parameters.Add(new DuckDBParameter(row.Get(header) ?? (object)DBNull.Value));

            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return batch.Count;
    }
}
