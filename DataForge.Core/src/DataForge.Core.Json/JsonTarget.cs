using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;

namespace DataForge.Core.Json;

public class JsonTarget<T> : IDataTarget<T>
{
    private readonly JsonExportOptions _options;

    public string Name => "JSON Target";
    public DataTargetType TargetType => DataTargetType.Json;

    public JsonTarget(JsonExportOptions? options = null)
    {
        _options = options ?? new JsonExportOptions();
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = _options.Indented,
            PropertyNameCaseInsensitive = true
        };

        string json;
        if (!string.IsNullOrEmpty(_options.RootPropertyName))
        {
            var wrapper = new Dictionary<string, List<T>>
            {
                { _options.RootPropertyName, items }
            };
            json = JsonSerializer.Serialize(wrapper, options);
        }
        else
        {
            json = JsonSerializer.Serialize(items, options);
        }

        await File.WriteAllTextAsync(destination, json, cancellationToken).ConfigureAwait(false);

        return new ExportResults
        {
            RecordsWritten = items.Count,
            OutputPath = destination,
            OutputSize = new FileInfo(destination).Length
        };
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await ExportAsync(ToAsyncEnumerable(item), "", cancellationToken).ConfigureAwait(false);
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), "", cancellationToken).ConfigureAwait(false);
        return new WriteResult { SuccessCount = result.RecordsWritten };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(T item)
    {
        yield return item;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
}

public class JsonExportOptions
{
    public bool Indented { get; set; } = true;
    public string? RootPropertyName { get; set; }
}
