using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;

namespace DataForge.Core.Json;

public class JsonSource<T> : IFileDataSource<T>
{
    private readonly JsonSourceOptions _options;

    public string FilePath { get; }
    public string Name => $"JSON: {FilePath}";
    public DataSourceType SourceType => DataSourceType.Json;

    public JsonSource(string filePath, JsonSourceOptions? options = null)
    {
        FilePath = filePath;
        _options = options ?? new JsonSourceOptions();
    }

    public async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read);

        if (_options.UseStreaming)
        {
            await foreach (var item in DeserializeStreamAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        else
        {
            var items = await System.Text.Json.JsonSerializer.DeserializeAsync<List<T>>(stream, _options.SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new List<T>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }
    }

    private async IAsyncEnumerable<T> DeserializeStreamAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = _options.SerializerOptions ?? new JsonSerializerOptions();
        using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = _options.AllowTrailingCommas,
            CommentHandling = _options.ReadCommentHandling ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow
        }, cancellationToken).ConfigureAwait(false);

        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return element.Deserialize<T>(options)!;
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root.Deserialize<T>(options)!;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(FilePath);
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "JSON",
            Location = FilePath,
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(FilePath));
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

public class JsonSourceOptions
{
    public JsonSerializerOptions? SerializerOptions { get; set; }
    public bool AllowTrailingCommas { get; set; } = true;
    public bool ReadCommentHandling { get; set; } = true;
    public bool UseStreaming { get; set; } = true;
}
