using DataMigration.Contracts;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.AzureBlobStorageDataTarget;

public class AzureBlobStorageDataTarget : IDataTarget
{
    private BlobServiceClient _blobServiceClient;
    private BlobContainerClient _containerClient;

    public string Id => "DataMigration.Plugin.AzureBlobStorageDataTarget";
    public string Name => "Azure Blob Storage 目标源";
    public string Description => "支持将数据写入 Azure Blob Storage";
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
        // 关闭逻辑，这里可以为空，因为客户端会在使用后自动释放
    }

    public async Task LoadAsync(IAsyncEnumerable<DataRecord> data, TargetConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var connectionString = config["ConnectionString"];
        var containerName = config["ContainerName"];
        var blobPrefix = config.TryGetValue("BlobPrefix", out var prefixValue) ? prefixValue : "";
        var fileExtension = config.TryGetValue("FileExtension", out var extensionValue) ? extensionValue : ".json";

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

        // 写入数据
        await foreach (var record in data)
        {
            // 生成 Blob 名称
            var blobName = GenerateBlobName(record, blobPrefix, fileExtension);

            // 创建 Blob 客户端
            var blobClient = _containerClient.GetBlobClient(blobName);

            // 将 DataRecord 转换为 JSON 字符串
            var json = JsonSerializer.Serialize(record);

            // 上传 Blob
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                await blobClient.UploadAsync(stream, cancellationToken: ct);
            }
        }
    }

    private string GenerateBlobName(DataRecord record, string blobPrefix, string fileExtension)
    {
        // 生成 Blob 名称
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var random = new Random().Next(1000, 9999);
        var blobName = $"{blobPrefix}{timestamp}_{random}{fileExtension}";

        return blobName;
    }
}
