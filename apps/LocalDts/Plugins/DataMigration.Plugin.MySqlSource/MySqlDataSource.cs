using DataMigration.Contracts;
using MySqlConnector;
using System.Data;

namespace DataMigration.Plugin.MySqlSource;

public class MySqlDataSource : IDataSource
{
    public string Id => "DataMigration.Plugin.MySqlSource";
    public string Name => "MySQL Data Source";
    public Version Version => new(1, 0, 0);

    private MySqlConnection? _connection;

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        // 初始化逻辑
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        // 执行逻辑（可选）
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
            _connection = null;
        }
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // 从配置中获取连接字符串和查询语句
        var connectionString = config.TryGetValue("ConnectionString", out var cs) ? cs : throw new ArgumentException("ConnectionString is required");
        var query = config.TryGetValue("Query", out var q) ? q : throw new ArgumentException("Query is required");

        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandType = CommandType.Text;

        using var reader = await command.ExecuteReaderAsync(ct);
        var fieldCount = reader.FieldCount;
        var fieldNames = new string[fieldCount];

        for (int i = 0; i < fieldCount; i++)
        {
            fieldNames[i] = reader.GetName(i);
        }

        while (await reader.ReadAsync(ct))
        {
            var record = new DataRecord();
            for (int i = 0; i < fieldCount; i++)
            {
                var value = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                record.SetValue(fieldNames[i], value);
            }
            yield return record;
        }
    }
}
