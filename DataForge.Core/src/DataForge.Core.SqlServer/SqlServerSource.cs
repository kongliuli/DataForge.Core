using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.SqlServer;

public class SqlServerSource<T> : IRelationalDataSource<T> where T : new()
{
    private static readonly ITypeConverter TypeConverter = new DefaultTypeConverter();
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;

    public string Name => $"SQL Server: {_tableName}";
    public DataSourceType SourceType => DataSourceType.SqlServer;

    public SqlServerSource(string connectionString, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        _tableName = SqlIdentifiers.Validate(tableName, nameof(tableName));
        _quotedTableName = SqlIdentifiers.QuoteSqlServer(_tableName);
    }

    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in QueryAsync($"SELECT * FROM {_quotedTableName}", null, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        using var connection = new SqlConnection(_connectionString);
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
            SourceType = "SqlServer",
            Location = _tableName,
            AdditionalInfo =
            {
                ["Provider"] = "Microsoft.Data.SqlClient",
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
        SqlDataReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                catch
                {
                    // skip unmapped columns
                }
            }

            yield return item;
        }
    }
}
