using DataMigration.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.RedisDataTarget;

public class RedisDataTarget : IDataTarget
{
    private ConnectionMultiplexer _redis;
    private IDatabase _db;

    public string Id => "DataMigration.Plugin.RedisDataTarget";
    public string Name => "Redis 目标源";
    public string Description => "支持将数据写入 Redis 缓存";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataTarget 中不需要实现，因为数据写入是通过 LoadAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空，因为连接是在 LoadAsync 中建立的
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭 Redis 连接
        if (_redis != null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }

    public async Task LoadAsync(IAsyncEnumerable<DataRecord> data, TargetConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var connectionString = config["ConnectionString"];
        var databaseId = config.TryGetValue("DatabaseId", out var dbValue) && int.TryParse(dbValue, out var db) ? db : 0;
        var keyPattern = config.TryGetValue("KeyPattern", out var keyPatternValue) ? keyPatternValue : "data:{Id}";
        var dataFormat = config.TryGetValue("DataFormat", out var formatValue) ? formatValue.ToLower() : "json";

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(connectionString))
            throw new ConfigurationException("ConnectionString is required");

        // 建立 Redis 连接
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        _db = _redis.GetDatabase(databaseId);

        // 写入数据
        await foreach (var record in data)
        {
            // 生成 Redis 键
            var key = GenerateKey(record, keyPattern);

            // 根据格式写入数据
            switch (dataFormat)
            {
                case "json":
                    await WriteAsJsonAsync(key, record, ct);
                    break;
                case "hash":
                    await WriteAsHashAsync(key, record, ct);
                    break;
                case "string":
                    await WriteAsStringAsync(key, record, ct);
                    break;
                default:
                    await WriteAsJsonAsync(key, record, ct);
                    break;
            }
        }
    }

    private string GenerateKey(DataRecord record, string keyPattern)
    {
        var key = keyPattern;

        // 替换键模式中的占位符
        foreach (var kvp in record)
        {
            key = key.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString() ?? "");
        }

        // 如果键模式中没有替换任何占位符，使用当前时间戳作为键
        if (key == keyPattern)
        {
            key = $"data:{Guid.NewGuid()}";
        }

        return key;
    }

    private async Task WriteAsJsonAsync(string key, DataRecord record, CancellationToken ct)
    {
        // 将 DataRecord 转换为 JSON 字符串
        var json = JsonSerializer.Serialize(record);
        await _db.StringSetAsync(key, json);
    }

    private async Task WriteAsHashAsync(string key, DataRecord record, CancellationToken ct)
    {
        // 将 DataRecord 转换为 Redis 哈希
        var hashEntries = new List<HashEntry>();
        foreach (var kvp in record)
        {
            hashEntries.Add(new HashEntry(kvp.Key, kvp.Value?.ToString() ?? ""));
        }
        await _db.HashSetAsync(key, hashEntries.ToArray());
    }

    private async Task WriteAsStringAsync(string key, DataRecord record, CancellationToken ct)
    {
        // 将 DataRecord 转换为字符串
        var value = string.Join(",", record.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        await _db.StringSetAsync(key, value);
    }
}
