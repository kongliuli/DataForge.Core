using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Sources;

public interface IDataSource<T>
{
    string Name { get; }
    DataSourceType SourceType { get; }
    IAsyncEnumerable<T> ReadAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);
}

public interface IRelationalDataSource<T> : IDataSource<T>
{
    IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);
}

public interface IFileDataSource<T> : IDataSource<T>
{
    string FilePath { get; }
    
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}