using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.SqlServer;

public class SqlServerTarget<T> : IDataTarget<T>
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;
    private readonly SqlServerExportOptions _options;

    public string Name => $"SQL Server Target: {_tableName}";
    public DataTargetType TargetType => DataTargetType.SqlServer;

    public SqlServerTarget(string connectionString, string tableName, SqlServerExportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _tableName = SqlIdentifiers.Validate(tableName, nameof(tableName));
        _quotedTableName = SqlIdentifiers.QuoteSqlServer(_tableName);
        _options = options ?? new SqlServerExportOptions();

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
        _ = destination; // destination kept for IDataTarget; connection/table come from ctor
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
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        foreach (var prop in properties)
        {
            SqlIdentifiers.Validate(prop.Name, nameof(T));
        }

        var quotedColumns = properties.Select(p => SqlIdentifiers.QuoteSqlServer(p.Name)).ToArray();
        var columns = string.Join(", ", quotedColumns);
        var valuePlaceholders = string.Join(", ", properties.Select((_, i) => $"@p{i}"));

        SqlTransaction? transaction = null;
        if (_options.UseTransaction)
        {
            transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
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
                    {
                        var idx = Array.IndexOf(properties, p);
                        return $"{SqlIdentifiers.QuoteSqlServer(p.Name)} = @p{idx}";
                    }));
                    var where = string.Join(" AND ", keyCols.Select(k =>
                    {
                        var idx = Array.IndexOf(properties, properties.First(p => p.Name == k));
                        return $"{SqlIdentifiers.QuoteSqlServer(k)} = @p{idx}";
                    }));

                    command.CommandText = $@"
IF EXISTS (SELECT 1 FROM {_quotedTableName} WHERE {where})
    UPDATE {_quotedTableName} SET {updateSet} WHERE {where}
ELSE
    INSERT INTO {_quotedTableName} ({columns}) VALUES ({valuePlaceholders})";
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
