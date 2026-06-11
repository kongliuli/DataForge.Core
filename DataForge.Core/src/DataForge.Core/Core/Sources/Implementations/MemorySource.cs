using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Sources.Implementations;

internal class MemorySource<T> : IDataSource<T>
{
    private readonly IEnumerable<T> _data;
    private readonly int? _cachedCount;

    public string Name => "Memory";
    public DataSourceType SourceType => DataSourceType.Memory;

    public MemorySource(IEnumerable<T> data)
    {
        _data = data;
        if (data is ICollection<T> collection)
            _cachedCount = collection.Count;
        else if (data is IReadOnlyCollection<T> roCollection)
            _cachedCount = roCollection.Count;
        else if (data is T[] array)
            _cachedCount = array.Length;
        else
            _cachedCount = null;
    }

    public async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in _data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "Memory",
            Location = "In-Memory Collection",
            Size = _cachedCount.HasValue ? _cachedCount.Value * 1024L : -1L
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