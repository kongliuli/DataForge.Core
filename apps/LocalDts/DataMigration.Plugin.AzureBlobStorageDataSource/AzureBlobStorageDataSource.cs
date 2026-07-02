using DataMigration.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.AzureBlobStorageDataSource;

public class AzureBlobStorageDataSource : IDataSource
{
    private BlobServiceClient _blobServiceClient;
    private BlobContainerClient _containerClient;

    public string Id => "DataMigration.Plugin.AzureBlobStorageDataSource";
    public string Name => "Azure Blob Storage 数据源";
    public string Description => "从 Azure Blob Storage 读取数据";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataSource 中不需要实现，因为数据提取是通过 ExtractAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化逻辑，这里可以为空，因为客户端是在 ExtractAsync 中创建的
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 关闭逻辑，这里可以为空，因为客户端会在使用后自动释放
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var connectionString = config["ConnectionString"];
        var containerName = config["ContainerName"];
        var blobPrefix = config.TryGetValue("BlobPrefix", out var prefixValue) ? prefixValue : "";
        var filePattern = config.TryGetValue("FilePattern", out var patternValue) ? patternValue : "*";

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(connectionString))
            throw new ConfigurationException("ConnectionString is required");
        if (string.IsNullOrEmpty(containerName))
            throw new ConfigurationException("ContainerName is required");

        // 创建 Blob 服务客户端
        _blobServiceClient = new BlobServiceClient(connectionString);

        // 获取容器客户端
        _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // 确保容器存在
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        // 列出 Blob
        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: blobPrefix, cancellationToken: ct))
        {
            // 检查是否匹配文件模式
            if (System.IO.Path.GetFileName(blobItem.Name).Like(filePattern))
            {
                // 获取 Blob 客户端
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);

                // 下载 Blob 内容
                var downloadResult = await blobClient.DownloadAsync(cancellationToken: ct);

                // 读取内容
                using (var reader = new StreamReader(downloadResult.Value.Content))
                {
                    var content = await reader.ReadToEndAsync();

                    // 解析内容（假设是 JSON 格式）
                    var dataRecord = await ParseBlobContentAsync(blobItem, content, ct);
                    yield return dataRecord;
                }
            }
        }
    }

    private async Task<DataRecord> ParseBlobContentAsync(BlobItem blobItem, string content, CancellationToken ct)
    {
        var dataRecord = new DataRecord
        {
            ["BlobName"] = blobItem.Name,
            ["ContentType"] = blobItem.Properties.ContentType,
            ["ContentLength"] = blobItem.Properties.ContentLength,
            ["LastModified"] = blobItem.Properties.LastModified?.DateTime,
            ["ETag"] = blobItem.Properties.ETag.ToString()
        };

        // 尝试解析 JSON 内容
        try
        {
            var jsonDocument = JsonDocument.Parse(content);
            var rootElement = jsonDocument.RootElement;

            if (rootElement.ValueKind == JsonValueKind.Object)
            {
                // 如果是对象，将所有属性添加到数据记录中
                foreach (var property in rootElement.EnumerateObject())
                {
                    dataRecord[property.Name] = JsonElementToObject(property.Value);
                }
            }
            else if (rootElement.ValueKind == JsonValueKind.Array)
            {
                // 如果是数组，将其作为 "Items" 属性添加
                dataRecord["Items"] = JsonElementToObject(rootElement);
            }
            else
            {
                // 其他类型，将其作为 "Value" 属性添加
                dataRecord["Value"] = content;
            }
        }
        catch (JsonException)
        {
            // 如果不是 JSON，将内容作为 "Value" 属性添加
            dataRecord["Value"] = content;
        }

        return dataRecord;
    }

    private object JsonElementToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.ToString();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Array:
                var array = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(JsonElementToObject(item));
                }
                return array;
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    obj[property.Name] = JsonElementToObject(property.Value);
                }
                return obj;
            default:
                return element.ToString();
        }
    }
}

// 扩展方法，用于文件模式匹配
public static class StringExtensions
{
    public static bool Like(this string str, string pattern)
    {
        // 简单的通配符匹配实现
        pattern = pattern.Replace("*", ".*");
        pattern = pattern.Replace("?", ".");
        return System.Text.RegularExpressions.Regex.IsMatch(str, pattern);
    }
}
