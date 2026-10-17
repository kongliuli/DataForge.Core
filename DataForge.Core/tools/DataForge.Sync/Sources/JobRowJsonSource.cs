using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using System.Text.Json;

namespace DataForge.Sync.Sources;

internal sealed class JobRowJsonSource : IFileDataSource<JobRow>
{
    public string FilePath { get; }

    public JobRowJsonSource(string filePath) => FilePath = filePath;

    public string Name => $"JSON: {FilePath}";

    public DataSourceType SourceType => DataSourceType.Json;

    public async IAsyncEnumerable<JobRow> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(FilePath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return MapObject(element);
            }

            yield break;
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            yield return MapObject(doc.RootElement);
            yield break;
        }

        throw new InvalidOperationException($"JSON source must be an object or array: {FilePath}");
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

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(FilePath));

    public async Task<IReadOnlyList<JobRow>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<JobRow>();
        await foreach (var row in ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }

    private static JobRow MapObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JSON row must be an object.");

        var row = new JobRow();
        foreach (var property in element.EnumerateObject())
            row.Values[property.Name] = ToScalarString(property.Value);

        return row;
    }

    private static string? ToScalarString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.GetRawText()
        };
}
