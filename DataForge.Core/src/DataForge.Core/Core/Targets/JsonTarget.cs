using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

internal class JsonTarget<T> : IDataTarget<T>
{
    private readonly JsonExportOptions _options;

    public JsonTarget(JsonExportOptions options)
    {
        _options = options;
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
}