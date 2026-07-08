using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using DuckDB.NET.Data;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DataForge.Core.DuckDB;

public class DuckDbSource<T> : IRelationalDataSource<T> where T : new()
{
    private static readonly ITypeConverter TypeConverter = new DefaultTypeConverter();
    private readonly string _connectionString;
    private readonly string _sql;
    private readonly DuckDbSourceOptions _options;

    public DuckDbSource(string databasePath, string sql, DuckDbSourceOptions? options = null)
    {
        _connectionString = DuckDbConnectionHelper.BuildConnectionString(databasePath);
        _sql = sql;
        _options = options ?? new DuckDbSourceOptions();
    }

    public string Name => $"DuckDB: {_sql}";
    public DataSourceType SourceType => DataSourceType.Custom;

    public async IAsyncEnumerable<T> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in QueryAsync(_sql, null, cancellationToken).ConfigureAwait(false))
            yield return item;
    }

    public async IAsyncEnumerable<T> QueryAsync(
        string sql,
        object? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command, parameters);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var count = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.MaxRows.HasValue && count >= _options.MaxRows.Value)
                yield break;

            var item = new T();
            foreach (var prop in properties)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(prop.Name);
                    if (!reader.IsDBNull(ordinal))
                    {
                        var raw = reader.GetValue(ordinal);
                        prop.SetValue(item, TypeConverter.Convert(raw, prop.PropertyType));
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // ponytail: skip unmapped columns
                }
            }

            yield return item;
            count++;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DataSourceMetadata
        {
            SourceType = "DuckDB",
            Location = _connectionString
        });

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<T>();
        await foreach (var row in ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }

    internal static string BuildConnectionString(string databasePath) =>
        DuckDbConnectionHelper.BuildConnectionString(databasePath);

    private static void BindParameters(DuckDBCommand command, object? parameters)
    {
        if (parameters == null)
            return;

        foreach (var prop in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            command.Parameters.Add(new DuckDBParameter($"@{prop.Name}", prop.GetValue(parameters) ?? DBNull.Value));
    }
}
