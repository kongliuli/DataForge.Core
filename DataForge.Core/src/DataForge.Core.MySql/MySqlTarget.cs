using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using MySqlConnector;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.MySql;

public class MySqlTarget<T> : IDataTarget<T>
{
    private readonly MySqlExportOptions _options;

    public string Name => "MySQL Target";
    public DataTargetType TargetType => DataTargetType.MySql;

    public MySqlTarget(MySqlExportOptions? options = null)
    {
        _options = options ?? new MySqlExportOptions();
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var parts = destination.Split('|');
        var connectionString = parts[0];
        var tableName = parts.Length > 1 ? parts[1] : string.Empty;

        var sw = Stopwatch.StartNew();
        var count = 0;
        var batch = new List<T>();

        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= _options.BatchSize)
            {
                await WriteBatchAsync(batch, connectionString, tableName, cancellationToken).ConfigureAwait(false);
                count += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(batch, connectionString, tableName, cancellationToken).ConfigureAwait(false);
            count += batch.Count;
        }

        sw.Stop();
        return new ExportResults { RecordsWritten = count, Duration = sw.Elapsed };
    }

    private async Task WriteBatchAsync(List<T> batch, string connectionString, string tableName, CancellationToken ct)
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columns = string.Join(", ", properties.Select(p => p.Name));
        var valuePlaceholders = string.Join(", ", properties.Select((_, i) => $"@p{i}"));

        MySqlTransaction? transaction = null;
        if (_options.UseTransaction)
        {
            transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
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
                    var updateSet = string.Join(", ", nonKeyProps.Select(p => $"{p.Name} = VALUES({p.Name})"));
                    command.CommandText = $"INSERT INTO {tableName} ({columns}) VALUES ({valuePlaceholders}) ON DUPLICATE KEY UPDATE {updateSet}";
                }
                else
                {
                    command.CommandText = $"INSERT INTO {tableName} ({columns}) VALUES ({valuePlaceholders})";
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
        return ExportAsync(ToAsyncEnumerable(item), "", cancellationToken);
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), "", cancellationToken).ConfigureAwait(false);
        return new WriteResult { SuccessCount = result.RecordsWritten };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(T item)
    {
        yield return item;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
}
