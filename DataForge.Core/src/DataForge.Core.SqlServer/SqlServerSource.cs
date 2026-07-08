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

    public string Name => $"SQL Server: {_tableName}";
    public DataSourceType SourceType => DataSourceType.SqlServer;

    public SqlServerSource(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = SqlIdentifier.ValidateTableName(tableName);
    }

    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in QueryAsync($"SELECT * FROM {_tableName}", null, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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
                    // ponytail: skip unmapped columns
                }
            }

            yield return item;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "SqlServer",
            Location = _tableName,
            AdditionalInfo = { ["ConnectionString"] = _connectionString }
        });
    }

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in ReadAsync(cancellationToken))
        {
            results.Add(item);
        }
        return results;
    }
}
