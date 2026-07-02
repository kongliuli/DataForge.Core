using DataMigration.Contracts;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.MongoDataSource;

public class MongoDataSource : IDataSource
{
    private IMongoClient _client;
    private IMongoDatabase _database;
    private IMongoCollection<BsonDocument> _collection;

    public string Id => "DataMigration.Plugin.MongoDataSource";
    public string Name => "MongoDB 数据源";
    public string Description => "从 MongoDB 数据库读取数据";
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
        // 关闭逻辑，这里可以为空，因为 MongoDB 客户端会自动管理连接
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var connectionString = config["ConnectionString"];
        var databaseName = config["DatabaseName"];
        var collectionName = config["CollectionName"];
        var queryJson = config.TryGetValue("Query", out var queryValue) ? queryValue : "{}";
        var projectionJson = config.TryGetValue("Projection", out var projectionValue) ? projectionValue : "{}";
        var batchSize = config.TryGetValue("BatchSize", out var batchSizeValue) && int.TryParse(batchSizeValue, out var size) ? size : 100;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(connectionString))
            throw new ConfigurationException("ConnectionString is required");
        if (string.IsNullOrEmpty(databaseName))
            throw new ConfigurationException("DatabaseName is required");
        if (string.IsNullOrEmpty(collectionName))
            throw new ConfigurationException("CollectionName is required");

        // 建立连接
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);
        _collection = _database.GetCollection<BsonDocument>(collectionName);

        // 解析查询和投影
        var filter = BsonDocument.Parse(queryJson);
        var projection = BsonDocument.Parse(projectionJson);

        // 构建查询
        var findOptions = new FindOptions<BsonDocument>
        {
            BatchSize = batchSize
        };

        // 执行查询
        using var cursor = await _collection.FindAsync(filter, findOptions, ct);

        // 遍历结果
        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var document in cursor.Current)
            {
                ct.ThrowIfCancellationRequested();
                
                // 将 BsonDocument 转换为 DataRecord
                var dataRecord = new DataRecord();
                foreach (var element in document)
                {
                    dataRecord[element.Name] = BsonValueToObject(element.Value);
                }
                
                yield return dataRecord;
            }
        }
    }

    private object BsonValueToObject(BsonValue value)
    {
        switch (value.BsonType)
        {
            case BsonType.Null:
                return null;
            case BsonType.Int32:
                return value.AsInt32;
            case BsonType.Int64:
                return value.AsInt64;
            case BsonType.Double:
                return value.AsDouble;
            case BsonType.Boolean:
                return value.AsBoolean;
            case BsonType.String:
                return value.AsString;
            case BsonType.DateTime:
                return value.ToUniversalTime();
            case BsonType.ObjectId:
                return value.AsObjectId.ToString();
            case BsonType.Array:
                return value.AsBsonArray.Select(BsonValueToObject).ToList();
            case BsonType.Document:
                var document = new Dictionary<string, object>();
                foreach (var element in value.AsBsonDocument)
                {
                    document[element.Name] = BsonValueToObject(element.Value);
                }
                return document;
            default:
                return value.ToString();
        }
    }
}
