using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using MySqlConnector;
using System.Runtime.CompilerServices;

namespace DataForge.Jobs.MySql;

public sealed class JobRowMySqlSource : IRelationalDataSource<JobRow>
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;

    public JobRowMySqlSource(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = SqlIdentifier.ValidateTableName(tableName);
        _quotedTableName = SqlIdentifiers.QuoteMySql(_tableName);
    }

    public string Name => $"MySQL: {_tableName}";

    public DataSourceType SourceType => DataSourceType.MySql;

    public async IAsyncEnumerable<JobRow> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_quotedTableName}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Map(reader);
        }
    }

    public async IAsyncEnumerable<JobRow> QueryAsync(
        string sql,
        object? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return Map(reader);
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DataSourceMetadata { SourceType = "MySql", Location = _tableName });

    public async Task<IReadOnlyList<JobRow>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<JobRow>();
        await foreach (var row in ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }

    private static JobRow Map(MySqlDataReader reader)
    {
        var row = new JobRow();
        for (var i = 0; i < reader.FieldCount; i++)
            row.Values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
        return row;
    }
}
