using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Sqlite;

public class SqliteTarget<T> : IDataTarget<T>
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;
    private readonly SqliteExportOptions _options;

    public string Name => $"SQLite Target: {_tableName}";
    public DataTargetType TargetType => DataTargetType.Sqlite;

    public SqliteTarget(string connectionString, string tableName, SqliteExportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _tableName = SqlIdentifiers.Validate(tableName, nameof(tableName));
        _quotedTableName = SqlIdentifiers.QuoteSqlite(_tableName);
        _options = options ?? new SqliteExportOptions();

        if (_options.UpsertKeyColumns is { Length: > 0 })
        {
            foreach (var key in _options.UpsertKeyColumns)
            {
                SqlIdentifiers.Validate(key, nameof(_options.UpsertKeyColumns));
            }
        }
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        _ = destination;
        var sw = Stopwatch.StartNew();
        var count = 0;
        var batch = new List<T>();

        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= _options.BatchSize)
            {
                await WriteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                count += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            count += batch.Count;
        }

        sw.Stop();
        return new ExportResults
        {
            RecordsWritten = count,
            Duration = sw.Elapsed,
            OutputPath = _tableName
        };
    }

    private async Task WriteBatchAsync(List<T> batch, CancellationToken ct)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        foreach (var prop in properties)
        {
            SqlIdentifiers.Validate(prop.Name, nameof(T));
        }

        var quotedColumns = properties.Select(p => SqlIdentifiers.QuoteSqlite(p.Name)).ToArray();
        var columns = string.Join(", ", quotedColumns);
        var valuePlaceholders = string.Join(", ", properties.Select((_, i) => $"@p{i}"));

        SqliteTransaction? transaction = null;
        if (_options.UseTransaction)
        {
            transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        }

        try
        {
            foreach (var item in batch)
            {
                using var command = connection.CreateCommand();
                if (transaction != null)
                {
                    command.Transaction = transaction;
                }

                if (_options.InsertMode == InsertMode.Upsert && _options.UpsertKeyColumns is { Length: > 0 })
                {
                    var keyCols = _options.UpsertKeyColumns;
                    var nonKeyProps = properties.Where(p => !keyCols.Contains(p.Name)).ToArray();
                    var updateSet = string.Join(", ", nonKeyProps.Select(p =>
                        $"{SqlIdentifiers.QuoteSqlite(p.Name)} = excluded.{SqlIdentifiers.QuoteSqlite(p.Name)}"));
                    var conflict = string.Join(", ", keyCols.Select(SqlIdentifiers.QuoteSqlite));
                    command.CommandText =
                        $"INSERT INTO {_quotedTableName} ({columns}) VALUES ({valuePlaceholders}) ON CONFLICT({conflict}) DO UPDATE SET {updateSet}";
                }
                else
                {
                    command.CommandText = $"INSERT INTO {_quotedTableName} ({columns}) VALUES ({valuePlaceholders})";
                }

                for (var i = 0; i < properties.Length; i++)
                {
                    command.Parameters.AddWithValue($"@p{i}", properties[i].GetValue(item) ?? DBNull.Value);
                }

                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
            }
            throw;
        }
    }

    public Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        return ExportAsync(ToAsyncEnumerable(item), _tableName, cancellationToken);
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), _tableName, cancellationToken).ConfigureAwait(false);
        return new WriteResult { SuccessCount = result.RecordsWritten };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(T item)
    {
        yield return item;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
