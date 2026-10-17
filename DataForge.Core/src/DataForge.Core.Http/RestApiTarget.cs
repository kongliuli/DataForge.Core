using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;

namespace DataForge.Core.Http;

public class RestApiTarget<T> : IDataTarget<T>, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly RestApiTargetOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<T> _buffer = new();
    private bool _disposed;

    public string Name => $"REST API Target: {_options.BaseUrl}{_options.Endpoint}";
    public DataTargetType TargetType => DataTargetType.RestApi;

    public RestApiTarget(HttpClient? httpClient, RestApiTargetOptions? options = null)
    {
        _options = options ?? new RestApiTargetOptions();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl) && _ownsHttpClient)
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var count = 0;

        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            _buffer.Add(item);
            count++;

            if (_buffer.Count >= _options.BatchSize)
            {
                await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (_buffer.Count > 0)
        {
            await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        sw.Stop();
        return new ExportResults
        {
            RecordsWritten = count,
            Duration = sw.Elapsed
        };
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var json = JsonSerializer.Serialize(item, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = CreateRequest(HttpMethod.Post, _options.Endpoint, content);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<string>();

        foreach (var item in items)
        {
            try
            {
                await WriteAsync(item, cancellationToken).ConfigureAwait(false);
                successCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add(ex.Message);
            }
        }

        return new WriteResult
        {
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors.Select(e => new WriteError { Error = e }).ToList()
        };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private async Task FlushBufferAsync(CancellationToken cancellationToken)
    {
        if (_buffer.Count == 0) return;

        if (_options.UseBulkEndpoint && _buffer.Count > 1)
        {
            await SendBulkRequestAsync(_buffer, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var item in _buffer)
            {
                await WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }

        _buffer.Clear();
    }

    private async Task SendBulkRequestAsync(List<T> items, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(items, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = CreateRequest(HttpMethod.Post, _options.Endpoint, content);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, ResolveUri(url))
        {
            Content = content
        };

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

public class RestApiTargetOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public bool UseBulkEndpoint { get; set; } = false;
    public Dictionary<string, string> Headers { get; set; } = new();
}
