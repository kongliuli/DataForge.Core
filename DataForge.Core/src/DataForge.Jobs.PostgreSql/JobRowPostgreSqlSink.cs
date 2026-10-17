using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Jobs.Models;
using Npgsql;
using System.Diagnostics;
using System.Globalization;

namespace DataForge.Jobs.PostgreSql;

public static class JobRowPostgreSqlSink
{
    public static async Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        var connectionString = sink.Connection
            ?? throw new InvalidOperationException("PostgreSQL sink requires connection.");
        var tableName = SqlIdentifier.ValidateTableName(sink.Table
            ?? throw new InvalidOperationException("PostgreSQL sink requires table."));
        var quotedTable = SqlIdentifiers.QuotePostgreSql(tableName);

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
                count += await WriteBatchAsync(batch, connectionString, quotedTable, upsert, keyColumns, sink.CreateTable, cancellationToken)
                    .ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            count += await WriteBatchAsync(batch, connectionString, quotedTable, upsert, keyColumns, sink.CreateTable, cancellationToken)
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
        string quotedTable,
        bool upsert,
        string[] keyColumns,
        bool createTable,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (createTable)
            await EnsureTableAsync(connection, quotedTable, batch[0], keyColumns, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var row in batch)
            {
                var columns = row.Values.Keys.Select(SqlIdentifier.ValidateTableName).ToArray();
                if (columns.Length == 0)
                    continue;

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                var quotedColumns = columns.Select(SqlIdentifiers.QuotePostgreSql).ToArray();
                var paramNames = columns.Select((_, i) => $"@p{i}").ToArray();
                for (var i = 0; i < columns.Length; i++)
                    command.Parameters.AddWithValue(paramNames[i], Coerce(row.Get(columns[i])));

                if (upsert)
                {
                    var conflict = string.Join(", ", keyColumns.Select(SqlIdentifiers.QuotePostgreSql));
                    var nonKeyColumns = columns.Where(c => !keyColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray();
                    if (nonKeyColumns.Length == 0)
                    {
                        command.CommandText =
                            $"INSERT INTO {quotedTable} ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", paramNames)}) " +
                            $"ON CONFLICT ({conflict}) DO NOTHING";
                    }
                    else
                    {
                        var updateSet = string.Join(", ", nonKeyColumns.Select(c =>
                        {
                            var idx = Array.FindIndex(columns, x => x.Equals(c, StringComparison.OrdinalIgnoreCase));
                            return $"{SqlIdentifiers.QuotePostgreSql(c)} = {paramNames[idx]}";
                        }));
                        command.CommandText =
                            $"INSERT INTO {quotedTable} ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", paramNames)}) " +
                            $"ON CONFLICT ({conflict}) DO UPDATE SET {updateSet}";
                    }
                }
                else
                {
                    command.CommandText =
                        $"INSERT INTO {quotedTable} ({string.Join(", ", quotedColumns)}) VALUES ({string.Join(", ", paramNames)})";
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

    /// <summary>
    /// JobRow stores string bags; PostgreSQL rejects untyped text for int/float columns.
    /// </summary>
    private static object Coerce(string? value)
    {
        if (value is null)
            return DBNull.Value;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return l;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var m))
            return m;

        if (bool.TryParse(value, out var b))
            return b;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt;

        return value;
    }

    private static async Task EnsureTableAsync(
        NpgsqlConnection connection,
        string quotedTable,
        JobRow sample,
        string[] keyColumns,
        CancellationToken cancellationToken)
    {
        var columns = sample.Values.Keys.Select(SqlIdentifier.ValidateTableName).ToArray();
        var defs = columns.Select(column =>
        {
            var quoted = SqlIdentifiers.QuotePostgreSql(column);
            var pk = keyColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? " PRIMARY KEY" : "";
            return $"{quoted} text{pk}";
        });
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE IF NOT EXISTS {quotedTable} ({string.Join(", ", defs)})";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
