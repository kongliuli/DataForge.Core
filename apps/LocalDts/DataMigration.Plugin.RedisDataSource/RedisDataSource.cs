using DataMigration.Contracts;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.RedisDataSource;

public class RedisDataSource : IDataSource
{
    private ConnectionMultiplexer _redis;
    private IDatabase _database;

    public string Id => "DataMigration.Plugin.RedisDataSource";
    public string Name => "Redis 数据源";
    public string Description => "从 Redis 缓存读取数据";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataSource 中不需要实现，因为数据提取是通过 ExtractAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空，因为连接是在 ExtractAsync 中建立的
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭连接
        if (_redis != null)
        {
            await _redis.CloseAsync();
        }
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var connectionString = config["ConnectionString"];
        var databaseIndex = config.TryGetValue("DatabaseIndex", out var dbValue) && int.TryParse(dbValue, out var db) ? db : 0;
        var keyPattern = config.TryGetValue("KeyPattern", out var patternValue) ? patternValue : "*";
        var batchSize = config.TryGetValue("BatchSize", out var batchSizeValue) && int.TryParse(batchSizeValue, out var size) ? size : 100;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(connectionString))
            throw new ConfigurationException("ConnectionString is required");

        // 建立连接
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        _database = _redis.GetDatabase(databaseIndex);

        // 获取所有匹配的键
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(databaseIndex, keyPattern);

        // 遍历键并获取值
        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            // 获取键的类型
            var keyType = await _database.KeyTypeAsync(key);
            var dataRecord = new DataRecord
            {
                ["Key"] = key.ToString(),
                ["Type"] = keyType.ToString()
            };

            // 根据键的类型获取值
            switch (keyType)
            {
                case RedisType.String:
                    var stringValue = await _database.StringGetAsync(key);
                    dataRecord["Value"] = stringValue.HasValue ? stringValue.ToString() : null;
                    break;
                case RedisType.Hash:
                    var hashValues = await _database.HashGetAllAsync(key);
                    var hashDict = new Dictionary<string, string>();
                    foreach (var hashValue in hashValues)
                    {
                        hashDict[hashValue.Name.ToString()] = hashValue.Value.ToString();
                    }
                    dataRecord["Value"] = hashDict;
                    break;
                case RedisType.List:
                    var listValues = await _database.ListRangeAsync(key, 0, -1);
                    dataRecord["Value"] = listValues.Select(v => v.ToString()).ToList();
                    break;
                case RedisType.Set:
                    var setValues = await _database.SetMembersAsync(key);
                    dataRecord["Value"] = setValues.Select(v => v.ToString()).ToList();
                    break;
                case RedisType.SortedSet:
                    var sortedSetValues = await _database.SortedSetRangeByRankWithScoresAsync(key, 0, -1);
                    var sortedSetDict = new Dictionary<string, double>();
                    foreach (var sortedSetValue in sortedSetValues)
                    {
                        sortedSetDict[sortedSetValue.Element.ToString()] = sortedSetValue.Score;
                    }
                    dataRecord["Value"] = sortedSetDict;
                    break;
                default:
                    dataRecord["Value"] = "Unknown type";
                    break;
            }

            yield return dataRecord;
        }
    }
}
