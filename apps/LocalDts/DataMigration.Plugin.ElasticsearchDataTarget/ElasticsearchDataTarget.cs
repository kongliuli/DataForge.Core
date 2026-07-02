using DataMigration.Contracts;
using Elasticsearch.Net;
using Nest;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.ElasticsearchDataTarget;

public class ElasticsearchDataTarget : IDataTarget
{
    private ElasticClient _client;

    public string Id => "DataMigration.Plugin.ElasticsearchDataTarget";
    public string Name => "Elasticsearch 目标源";
    public string Description => "支持将数据写入 Elasticsearch 搜索引擎";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataTarget 中不需要实现，因为数据写入是通过 LoadAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空，因为客户端是在 LoadAsync 中创建的
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭逻辑，这里可以为空，因为 ElasticClient 不需要显式关闭
    }

    public async Task LoadAsync(IAsyncEnumerable<DataRecord> data, TargetConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var nodes = config["Nodes"];
        var index = config["Index"];
        var username = config.TryGetValue("Username", out var usernameValue) ? usernameValue : null;
        var password = config.TryGetValue("Password", out var passwordValue) ? passwordValue : null;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(nodes))
            throw new ConfigurationException("Nodes is required");
        if (string.IsNullOrEmpty(index))
            throw new ConfigurationException("Index is required");

        // 配置 Elasticsearch 客户端
        var nodeList = nodes.Split(',').Select(node => new Uri(node.Trim())).ToList();
        var connectionSettings = new ConnectionSettings(new Uri(nodes));

        // 如果提供了用户名和密码，添加基本认证
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            connectionSettings = connectionSettings.BasicAuthentication(username, password);
        }

        // 创建 Elasticsearch 客户端
        _client = new ElasticClient(connectionSettings);

        // 批量写入数据
        var batchSize = 100;
        var batch = new List<object>();

        await foreach (var record in data)
        {
            // 将 DataRecord 转换为匿名对象
            var document = ConvertDataRecordToObject(record);
            batch.Add(document);

            // 当批次大小达到阈值时，执行批量写入
            if (batch.Count >= batchSize)
            {
                await IndexBatchAsync(index, batch, ct);
                batch.Clear();
            }
        }

        // 写入剩余的文档
        if (batch.Count > 0)
        {
            await IndexBatchAsync(index, batch, ct);
        }
    }

    private object ConvertDataRecordToObject(DataRecord record)
    {
        // 将 DataRecord 转换为匿名对象
        var document = new Dictionary<string, object>();
        foreach (var kvp in record)
        {
            document[kvp.Key] = kvp.Value;
        }
        return document;
    }

    private async Task IndexBatchAsync(string index, List<object> documents, CancellationToken ct)
    {
        // 创建批量请求
        var bulkRequest = new BulkRequest(index)
        {
            Operations = new List<IBulkOperation>()
        };

        // 添加索引操作
        foreach (var document in documents)
        {
            bulkRequest.Operations.Add(new BulkIndexOperation<object>(document));
        }

        // 执行批量请求
        var response = await _client.BulkAsync(bulkRequest, ct);

        // 检查响应
        if (response.Errors)
        {
            // 处理错误
            foreach (var item in response.ItemsWithErrors)
            {
                // 可以在这里添加错误处理逻辑
            }
        }
    }
}
