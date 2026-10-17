using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using MySqlConnector;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.MySql;

public class MySqlSource<T> : IRelationalDataSource<T> where T : new()
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;

    public string Name => $"MySQL: {_tableName}";
    public DataSourceType SourceType => DataSourceType.MySql;

    public MySqlSource(string connectionString, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _tableName = SqlIdentifiers.Validate(tableName, nameof(tableName));
        _quotedTableName = SqlIdentifiers.QuoteMySql(_tableName);
    }

    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_quotedTableName}";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var item in MapReaderAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(parameters) ?? DBNull.Value);
            }
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var item in MapReaderAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "MySql",
            Location = _tableName,
            AdditionalInfo =
            {
                ["Provider"] = "MySqlConnector",
                ["Table"] = _tableName
            }
        });
    }

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(item);
        }
        return results;
    }

    private static async IAsyncEnumerable<T> MapReaderAsync(
        MySqlDataReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columns = new HashSet<string>(
            Enumerable.Range(0, reader.FieldCount).Select(reader.GetName),
            StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new T();
            foreach (var prop in properties)
            {
                if (!columns.Contains(prop.Name) || !prop.CanWrite)
                {
                    continue;
                }

                var ordinal = reader.GetOrdinal(prop.Name);
                if (reader.IsDBNull(ordinal))
                {
                    continue;
                }

                try
                {
                    var raw = reader.GetValue(ordinal);
                    var converted = new DefaultTypeConverter().Convert(raw, prop.PropertyType);
                    prop.SetValue(item, converted);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to map MySQL column '{prop.Name}' onto {typeof(T).Name}.{prop.Name}.",
                        ex);
                }
            }

            yield return item;
        }
    }
}
