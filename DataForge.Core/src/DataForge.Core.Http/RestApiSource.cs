using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;

namespace DataForge.Core.Http;

public class RestApiSource<T> : IDataSource<T>
{
    private readonly HttpClient _httpClient;
    private readonly RestApiSourceOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public string Name => $"REST API: {_options.BaseUrl}{_options.Endpoint}";
    public DataSourceType SourceType => DataSourceType.RestApi;

    public RestApiSource(HttpClient? httpClient, RestApiSourceOptions options)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(options.BaseUrl);
        }
        _options = options;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var url = BuildUrl();
        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (_options.UseStreaming)
        {
            await foreach (var item in DeserializeStreamAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        else
        {
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            if (items != null)
            {
                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }
    }

    public async IAsyncEnumerable<T> ReadWithPaginationAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = _options.StartPage;
        var hasMore = true;

        while (hasMore && (_options.MaxPages == 0 || page <= _options.MaxPages))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = BuildUrl(page);
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);

            if (items == null || items.Count == 0)
            {
                hasMore = false;
            }
            else
            {
                foreach (var item in items)
                {
                    yield return item;
                }

                if (items.Count < _options.PageSize)
                {
                    hasMore = false;
                }
                else
                {
                    page++;
                }
            }
        }
    }

    private async IAsyncEnumerable<T> DeserializeStreamAsync(System.IO.Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return element.Deserialize<T>(_jsonOptions)!;
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root.Deserialize<T>(_jsonOptions)!;
        }
    }

    private string BuildUrl(int? page = null)
    {
        var url = _options.Endpoint;

        if (!string.IsNullOrWhiteSpace(_options.QueryParams))
        {
            url += $"?{_options.QueryParams}";
        }

        if (page.HasValue && _options.PageParam != null)
        {
            var separator = string.IsNullOrWhiteSpace(_options.QueryParams) ? "?" : "&";
            url += $"{separator}{_options.PageParam}={page.Value}";
        }

        return url;
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "RestApi",
            Location = $"{_options.BaseUrl}{_options.Endpoint}",
            AdditionalInfo = new Dictionary<string, string>
            {
                ["HttpMethod"] = "GET",
                ["PageSize"] = _options.PageSize.ToString()
            }
        });
    }

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in ReadAsync(cancellationToken))
        {
            results.Add(item);
        }
        return results;
    }
}

public class RestApiSourceOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string QueryParams { get; set; } = string.Empty;
    public int PageSize { get; set; } = 100;
    public string? PageParam { get; set; }
    public int StartPage { get; set; } = 1;
    public int MaxPages { get; set; } = 0;
    public bool UseStreaming { get; set; } = true;
}
