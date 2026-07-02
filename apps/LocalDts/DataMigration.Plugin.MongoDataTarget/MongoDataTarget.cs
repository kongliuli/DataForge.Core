using DataMigration.Contracts;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.MongoDataTarget;

public class MongoDataTarget : IDataTarget
{
    private IMongoDatabase _database;
    private IMongoCollection<BsonDocument> _collection;

    public string Id => "DataMigration.Plugin.MongoDataTarget";
    public string Name => "MongoDB 目标源";
    public string Description => "支持将数据写入 MongoDB 数据库";
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
        // 关闭逻辑，这里可以为空，因为 MongoDB 客户端会在使用后自动释放
    }

    public async Task LoadAsync(IAsyncEnumerable<DataRecord> data, TargetConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var connectionString = config["ConnectionString"];
        var databaseName = config["DatabaseName"];
        var collectionName = config["CollectionName"];
        var batchSize = config.TryGetValue("BatchSize", out var batchSizeValue) && int.TryParse(batchSizeValue, out var batch) ? batch : 100;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(connectionString))
            throw new ConfigurationException("ConnectionString is required");
        if (string.IsNullOrEmpty(databaseName))
            throw new ConfigurationException("DatabaseName is required");
        if (string.IsNullOrEmpty(collectionName))
            throw new ConfigurationException("CollectionName is required");

        // 建立 MongoDB 连接
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
        _collection = _database.GetCollection<BsonDocument>(collectionName);

        // 批量写入数据
        var batchDocuments = new List<BsonDocument>();
        await foreach (var record in data)
        {
            // 将 DataRecord 转换为 BsonDocument
            var document = DataRecordToBsonDocument(record);
            batchDocuments.Add(document);

            // 当批次大小达到阈值时，执行批量写入
            if (batchDocuments.Count >= batchSize)
            {
                await _collection.InsertManyAsync(batchDocuments, cancellationToken: ct);
                batchDocuments.Clear();
            }
        }

        // 写入剩余的文档
        if (batchDocuments.Count > 0)
        {
            await _collection.InsertManyAsync(batchDocuments, cancellationToken: ct);
        }
    }

    private BsonDocument DataRecordToBsonDocument(DataRecord record)
    {
        var document = new BsonDocument();

        foreach (var kvp in record)
        {
            document[kvp.Key] = BsonValue.Create(kvp.Value);
        }

        return document;
    }
}
