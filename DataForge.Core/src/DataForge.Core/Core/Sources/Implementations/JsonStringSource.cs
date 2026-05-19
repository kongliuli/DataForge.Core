using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Sources.Implementations;

internal class JsonStringSource<T> : IDataSource<T>
{
    private readonly string _jsonContent;

    public string Name => "JSON String";
    public DataSourceType SourceType => DataSourceType.Json;

    public JsonStringSource(string jsonContent)
    {
        _jsonContent = jsonContent;
    }

    public async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(_jsonContent));
        var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var items = JsonSerializer.Deserialize<List<T>>(_jsonContent) ?? [];
        return Task.FromResult<IReadOnlyList<T>>(items);
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "JSON String",
            Location = "In-Memory String",
            Size = _jsonContent.Length
        });
    }
}
