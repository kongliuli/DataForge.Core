using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Sync.Models;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace DataForge.Sync.Execution;

internal static class JobRowSqlServerSink
{
    public static async Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        var connectionString = sink.Connection
            ?? throw new InvalidOperationException("SQL Server sink requires connection.");
        var tableName = SqlIdentifier.ValidateTableName(sink.Table
            ?? throw new InvalidOperationException("SQL Server sink requires table."));

        var batchSize = sink.Options?.BatchSize ?? 1000;
        var upsert = sink.Mode.Equals("upsert", StringComparison.OrdinalIgnoreCase);
        var keyColumns = sink.Keys.Select(SqlIdentifier.ValidateTableName).ToArray();

        if (upsert && keyColumns.Length == 0)
            throw new InvalidOperationException("Upsert mode requires sink.keys.");

        var sw = Stopwatch.StartNew();
        var count = 0;
        var batch = new List<JobRow>();

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(row);
            if (batch.Count >= batchSize)
            {
                count += await WriteBatchAsync(batch, connectionString, tableName, upsert, keyColumns, cancellationToken)
                    .ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            count += await WriteBatchAsync(batch, connectionString, tableName, upsert, keyColumns, cancellationToken)
                .ConfigureAwait(false);
        }

        sw.Stop();
        return new ExportResults
        {
            RecordsWritten = count,
            Duration = sw.Elapsed,
            OutputPath = $"{tableName}@{connectionString.GetHashCode():X}"
        };
    }

    private static async Task<int> WriteBatchAsync(
        List<JobRow> batch,
        string connectionString,
        string tableName,
        bool upsert,
        string[] keyColumns,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            foreach (var row in batch)
            {
                var columns = row.Values.Keys.Select(SqlIdentifier.ValidateTableName).ToArray();
                if (columns.Length == 0)
                    continue;

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var paramNames = columns.Select((_, i) => $"@p{i}").ToArray();
                for (var i = 0; i < columns.Length; i++)
                    command.Parameters.AddWithValue(paramNames[i], (object?)row.Get(columns[i]) ?? DBNull.Value);

                if (upsert)
                {
                    var nonKeyColumns = columns.Where(c => !keyColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray();
                    var keyWhere = string.Join(" AND ", keyColumns.Select(k =>
                    {
                        var idx = Array.FindIndex(columns, c => c.Equals(k, StringComparison.OrdinalIgnoreCase));
                        return $"{k} = @p{idx}";
                    }));

                    if (nonKeyColumns.Length == 0)
                    {
                        command.CommandText = $@"
IF NOT EXISTS (SELECT 1 FROM {tableName} WHERE {keyWhere})
    INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";
                    }
                    else
                    {
                        var updateSet = string.Join(", ", nonKeyColumns.Select(c =>
                        {
                            var idx = Array.FindIndex(columns, x => x.Equals(c, StringComparison.OrdinalIgnoreCase));
                            return $"{c} = @p{idx}";
                        }));

                        command.CommandText = $@"
IF EXISTS (SELECT 1 FROM {tableName} WHERE {keyWhere})
    UPDATE {tableName} SET {updateSet} WHERE {keyWhere}
ELSE
    INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";
                    }
                }
                else
                {
                    command.CommandText =
                        $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";
                }

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return batch.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
