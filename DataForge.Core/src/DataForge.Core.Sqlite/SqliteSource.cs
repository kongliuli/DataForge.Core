using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Sqlite;

public class SqliteSource<T> : IRelationalDataSource<T> where T : new()
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public SqliteSource(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = tableName;
    }

    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_tableName}";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new T();
            foreach (var prop in properties)
            {
                var ordinal = reader.GetOrdinal(prop.Name);
                if (!reader.IsDBNull(ordinal))
                {
                    prop.SetValue(item, reader.GetValue(ordinal));
                }
            }
            yield return item;
        }
    }

    public async IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
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
                        prop.SetValue(item, reader.GetValue(ordinal));
                    }
                }
                catch { }
            }
            yield return item;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "Sqlite",
            Location = _tableName,
            AdditionalInfo = { ["ConnectionString"] = _connectionString }
        });
    }
}
