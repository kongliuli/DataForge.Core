using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using DuckDB.NET.Data;
using System.Diagnostics;
using System.Reflection;

namespace DataForge.Core.DuckDB;

public class DuckDbTarget<T> : IDataTarget<T>
{
    private readonly DuckDbExportOptions _options;

    public string Name => "DuckDB Target";
    public DataTargetType TargetType => DataTargetType.Custom;

    public DuckDbTarget(DuckDbExportOptions? options = null)
    {
        _options = options ?? new DuckDbExportOptions();
    }

    public async Task<ExportResults> ExportAsync(
        IAsyncEnumerable<T> data,
        string destination,
        CancellationToken cancellationToken = default)
    {
        var parts = destination.Split('|', 2);
        var connectionString = DuckDbConnectionHelper.BuildConnectionString(parts[0]);
        var tableName = parts.Length > 1 ? parts[1] : throw new ArgumentException("Destination must be 'databasePath|tableName'.");

        ValidateIdentifier(tableName);

        var sw = Stopwatch.StartNew();
        var count = 0;
        var batch = new List<T>(_options.BatchSize);

        await using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (_options.CreateTableIfNotExists)
            await EnsureTableAsync(connection, tableName, cancellationToken).ConfigureAwait(false);

        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= _options.BatchSize)
            {
                count += await WriteBatchAsync(connection, tableName, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            count += await WriteBatchAsync(connection, tableName, batch, cancellationToken).ConfigureAwait(false);

        sw.Stop();
        return new ExportResults { RecordsWritten = count, Duration = sw.Elapsed };
    }

    private async Task EnsureTableAsync(DuckDBConnection connection, string tableName, CancellationToken ct)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columns = string.Join(", ", properties.Select(p => $"{ValidateIdentifier(p.Name)} {MapColumnType(p.PropertyType)}"));
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} ({columns})";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<int> WriteBatchAsync(
        DuckDBConnection connection,
        string tableName,
        List<T> batch,
        CancellationToken ct)
    {
        if (batch.Count == 0)
            return 0;

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columnNames = properties.Select(p => ValidateIdentifier(p.Name)).ToArray();

        foreach (var item in batch)
        {
            await using var command = connection.CreateCommand();
            var paramNames = properties.Select((_, i) => $"${i + 1}").ToArray();

            if (_options.InsertMode == DuckDbInsertMode.Upsert && _options.UpsertKeyColumns is { Length: > 0 })
            {
                var keys = _options.UpsertKeyColumns.Select(ValidateIdentifier).ToArray();
                var nonKey = properties.Where(p => !keys.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
                var updateSet = string.Join(", ", nonKey.Select(p =>
                {
                    var idx = Array.FindIndex(properties, x => x.Name == p.Name);
                    return $"{ValidateIdentifier(p.Name)} = ${idx + 1}";
                }));

                command.CommandText = $@"
INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})
ON CONFLICT ({string.Join(", ", keys)}) DO UPDATE SET {updateSet}";
            }
            else
            {
                command.CommandText =
                    $"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
            }

            for (var i = 0; i < properties.Length; i++)
                command.Parameters.Add(new DuckDBParameter(properties[i].GetValue(item) ?? DBNull.Value));

            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return batch.Count;
    }

    private static string ValidateIdentifier(string name) => SqlIdentifier.ValidateTableName(name);

    private static string MapColumnType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string))
            return "VARCHAR";
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short))
            return "BIGINT";
        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
            return "DOUBLE";
        if (underlying == typeof(bool))
            return "BOOLEAN";
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
            return "TIMESTAMP";
        return "VARCHAR";
    }

    public Task WriteAsync(T item, CancellationToken cancellationToken = default) =>
        ExportAsync(ToAsyncEnumerable(item), "", cancellationToken);

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), "", cancellationToken).ConfigureAwait(false);
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
            yield return item;
        await Task.CompletedTask;
    }
}
