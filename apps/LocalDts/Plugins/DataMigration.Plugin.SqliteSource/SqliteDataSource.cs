namespace DataMigration.Plugin.SqliteSource;

using DataMigration.Contracts;
using System.Data.SQLite;

public class SqliteDataSource : IDataSource
{
    public string Id => "DataMigration.Plugin.SqliteSource";
    public string Name => "SQLite 数据源";
    public Version Version => new(1, 0, 0);

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(
        SourceConfig config, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct
    )
    {
        // 从配置中获取连接字符串和查询语句
        string connectionString = config.TryGetValue("ConnectionString", out var cs) ? cs : throw new ArgumentException("ConnectionString is required");
        string query = config.TryGetValue("Query", out var q) ? q : throw new ArgumentException("Query is required");

        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync(ct);

        using var command = new SQLiteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var record = new DataRecord();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = reader.GetName(i);
                object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                record.SetValue(columnName, value);
            }
            yield return record;
        }
    }
}
