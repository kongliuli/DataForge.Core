using DataMigration.Contracts;
using System.Data.SQLite;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DataMigration.Plugin.SqliteTarget;

public class SqliteTarget : IDataTarget
{
    public string Id => "DataMigration.Plugin.SqliteTarget";
    public string Name => "SQLite 目标源";
    public Version Version => new Version(1, 0, 0);

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

    public async Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        CancellationToken ct
    )
    {
        // 从配置中获取连接字符串和表名
        if (!config.TryGetValue("ConnectionString", out var connectionString))
        {
            throw new InvalidOperationException("缺少连接字符串配置");
        }

        if (!config.TryGetValue("TableName", out var tableName))
        {
            throw new InvalidOperationException("缺少表名配置");
        }

        // 先将所有记录转换为列表
        var records = await input.ToListAsync(ct);
        if (records.Count == 0)
        {
            return; // 没有数据要写入
        }

        // 建立数据库连接
        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync(ct);

        // 获取第一个记录以构建 SQL 语句
        var firstRecord = records[0];

        // 构建插入语句
        var columnNames = string.Join(", ", firstRecord.Keys);
        var parameterNames = string.Join(", ", firstRecord.Keys.Select(key => $"@{key}"));
        var insertCommandText = $"INSERT INTO {tableName} ({columnNames}) VALUES ({parameterNames})";

        // 执行插入
        using var command = new SQLiteCommand(insertCommandText, connection);
        foreach (var record in records)
        {
            // 清除之前的参数
            command.Parameters.Clear();

            // 添加参数
            foreach (var (key, value) in record)
            {
                command.Parameters.AddWithValue($"@{key}", value ?? DBNull.Value);
            }

            // 执行插入
            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
