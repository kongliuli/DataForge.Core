using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Plugin.RestApiDataSource;

public class RestApiDataSource : IDataSource
{
    private HttpClient _httpClient;

    public string Id => "DataMigration.Plugin.RestApiDataSource";
    public string Name => "REST API 数据源";
    public string Description => "从 REST API 接口读取数据";
    public Version Version => new Version(1, 0, 0);

    public Task ExecuteAsync(CancellationToken ct)
    {
        // 这个方法在 IDataSource 中不需要实现，因为数据提取是通过 ExtractAsync 方法完成的
        return Task.CompletedTask;
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        // 初始化 HTTP 客户端
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task ShutdownAsync(CancellationToken ct)
    {
        // 释放 HTTP 客户端
        if (_httpClient != null)
        {
            _httpClient.Dispose();
        }
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 获取配置参数
        var baseUrl = config["BaseUrl"];
        var endpoint = config["Endpoint"];
        var httpMethod = config.TryGetValue("Method", out var methodValue) ? methodValue.ToUpper() : "GET";
        var requestBody = config.TryGetValue("RequestBody", out var bodyValue) ? bodyValue : "";
        var contentType = config.TryGetValue("ContentType", out var contentTypeValue) ? contentTypeValue : "application/json";
        var authentication = config.TryGetValue("Authentication", out var authValue) ? authValue : "";
        var authToken = config.TryGetValue("AuthToken", out var tokenValue) ? tokenValue : "";
        var pageSize = config.TryGetValue("PageSize", out var pageSizeValue) && int.TryParse(pageSizeValue, out var size) ? size : 100;
        var maxPages = config.TryGetValue("MaxPages", out var maxPagesValue) && int.TryParse(maxPagesValue, out var max) ? max : 10;

        // 验证必需的配置参数
        if (string.IsNullOrEmpty(baseUrl))
            throw new ConfigurationException("BaseUrl is required");
        if (string.IsNullOrEmpty(endpoint))
            throw new ConfigurationException("Endpoint is required");

        // 构建完整的 URL
        var url = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        // 配置 HTTP 客户端
        if (!string.IsNullOrEmpty(authentication) && !string.IsNullOrEmpty(authToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authentication, authToken);
        }

        // 构建请求
        var request = new HttpRequestMessage(new HttpMethod(httpMethod), url);
        if (!string.IsNullOrEmpty(requestBody))
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, contentType);
        }

        // 发送请求并处理响应
        var currentPage = 1;
        while (!ct.IsCancellationRequested && currentPage <= maxPages)
        {
            // 发送请求
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            // 读取响应内容
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            // 解析 JSON 响应
            var jsonDocument = JsonDocument.Parse(responseContent);
            var rootElement = jsonDocument.RootElement;

            // 处理响应数据
            if (rootElement.ValueKind == JsonValueKind.Array)
            {
                // 直接处理数组
                foreach (var element in rootElement.EnumerateArray())
                {
                    var dataRecord = JsonElementToDataRecord(element);
                    yield return dataRecord;
                }
            }
            else if (rootElement.ValueKind == JsonValueKind.Object)
            {
                // 处理对象，查找数据数组
                if (rootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in dataElement.EnumerateArray())
                    {
                        var dataRecord = JsonElementToDataRecord(element);
                        yield return dataRecord;
                    }
                }
                else if (rootElement.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in itemsElement.EnumerateArray())
                    {
                        var dataRecord = JsonElementToDataRecord(element);
                        yield return dataRecord;
                    }
                }
                else
                {
                    // 处理单个对象
                    var dataRecord = JsonElementToDataRecord(rootElement);
                    yield return dataRecord;
                }
            }

            // 检查是否有下一页
            if (!CheckForNextPage(rootElement, ref currentPage, ref request))
            {
                break;
            }
        }
    }

    private DataRecord JsonElementToDataRecord(JsonElement element)
    {
        var dataRecord = new DataRecord();

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                dataRecord[property.Name] = JsonElementToObject(property.Value);
            }
        }
        else
        {
            dataRecord["Value"] = JsonElementToObject(element);
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

    private bool CheckForNextPage(JsonElement rootElement, ref int currentPage, ref HttpRequestMessage request)
    {
        // 检查是否有下一页的逻辑
        // 这里实现一个简单的分页逻辑，实际实现需要根据 API 的具体分页机制进行调整
        currentPage++;

        // 如果当前页码超过最大页码，返回 false
        if (currentPage > 10) // 这里使用硬编码的 10 作为示例
        {
            return false;
        }

        // 更新请求 URL，添加分页参数
        var originalUrl = request.RequestUri.ToString();
        var separator = originalUrl.Contains('?') ? '&' : '?';
        request.RequestUri = new Uri($"{originalUrl}{separator}page={currentPage}");

        return true;
    }
}
