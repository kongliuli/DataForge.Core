using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using Npgsql;
using System.Runtime.CompilerServices;

namespace DataForge.Sync.Sources;

internal sealed class JobRowPostgreSqlSource : IRelationalDataSource<JobRow>
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;

    public JobRowPostgreSqlSource(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = SqlIdentifier.ValidateTableName(tableName);
        _quotedTableName = SqlIdentifiers.QuotePostgreSql(_tableName);
    }

    public string Name => $"PostgreSQL: {_tableName}";

    public DataSourceType SourceType => DataSourceType.PostgreSql;

    public async IAsyncEnumerable<JobRow> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_quotedTableName}";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MapRow(reader);
        }
    }

    public async IAsyncEnumerable<JobRow> QueryAsync(
        string sql,
        object? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                command.Parameters.AddWithValue(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MapRow(reader);
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DataSourceMetadata
        {
            SourceType = "PostgreSql",
            Location = _tableName
        });

    public async Task<IReadOnlyList<JobRow>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<JobRow>();
        await foreach (var row in ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }

    private static JobRow MapRow(NpgsqlDataReader reader)
    {
        var row = new JobRow();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            row.Values[name] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
        }

        return row;
    }
}
