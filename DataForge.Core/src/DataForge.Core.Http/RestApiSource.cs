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

public class RestApiSource<T> : IDataSource<T>, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly RestApiSourceOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public string Name => $"REST API: {_options.BaseUrl}{_options.Endpoint}";
    public DataSourceType SourceType => DataSourceType.RestApi;

    public RestApiSource(HttpClient? httpClient, RestApiSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _options = options;

        if (!string.IsNullOrWhiteSpace(options.BaseUrl) && _ownsHttpClient)
        {
            _httpClient.BaseAddress = new Uri(options.BaseUrl);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var request = CreateRequest(BuildUrl());
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

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
        ObjectDisposedException.ThrowIf(_disposed, this);

        var page = _options.StartPage;
        var hasMore = true;

        while (hasMore && (_options.MaxPages == 0 || page <= _options.MaxPages))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = CreateRequest(BuildUrl(page));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
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
        // Expects a JSON array payload. For a single JSON object, set UseStreaming = false.
        var enumerable = JsonSerializer.DeserializeAsyncEnumerable<T>(stream, _jsonOptions, cancellationToken);
        await foreach (var item in enumerable.ConfigureAwait(false))
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    private HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ResolveUri(url));
        foreach (var header in _options.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }

    private Uri ResolveUri(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return new Uri(new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/"), url.TrimStart('/'));
        }

        return new Uri(url, UriKind.RelativeOrAbsolute);
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
                ["PageSize"] = _options.PageSize.ToString(),
                ["OwnsHttpClient"] = _ownsHttpClient.ToString()
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
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

    /// <summary>
    /// When true, deserialize a JSON array with <see cref="JsonSerializer.DeserializeAsyncEnumerable{T}"/>.
    /// When false, buffer into <see cref="List{T}"/> first.
    /// </summary>
    public bool UseStreaming { get; set; } = true;

    public Dictionary<string, string> Headers { get; set; } = new();
}
