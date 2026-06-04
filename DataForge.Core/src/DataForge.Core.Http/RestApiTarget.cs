using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;

namespace DataForge.Core.Http;

public class RestApiTarget<T> : IDataTarget<T>
{
    private readonly HttpClient _httpClient;
    private readonly RestApiTargetOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<T> _buffer = new();

    public string Name => $"REST API Target: {_options.BaseUrl}{_options.Endpoint}";
    public DataTargetType TargetType => DataTargetType.RestApi;

    public RestApiTarget(HttpClient? httpClient, RestApiTargetOptions? options = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _options = options ?? new RestApiTargetOptions();

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
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
        var url = $"{_options.Endpoint}";
        var json = JsonSerializer.Serialize(item, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
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
        var url = $"{_options.Endpoint}";
        var json = JsonSerializer.Serialize(items, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
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
