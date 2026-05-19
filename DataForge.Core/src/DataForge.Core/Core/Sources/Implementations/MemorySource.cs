using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Sources.Implementations;

internal class MemorySource<T> : IDataSource<T>
{
    private readonly IEnumerable<T> _data;

    public MemorySource(IEnumerable<T> data)
    {
        _data = data;
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
            Size = _data.Count() * 1024L
        });
    }
}