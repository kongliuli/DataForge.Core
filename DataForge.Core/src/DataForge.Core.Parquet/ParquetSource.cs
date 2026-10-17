using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using Parquet.Serialization;

namespace DataForge.Core.Parquet;

public class ParquetSource<T> : IFileDataSource<T> where T : class, new()
{
    private readonly ParquetSourceOptions _options;

    public string FilePath { get; }

    public ParquetSource(string filePath, ParquetSourceOptions? options = null)
    {
        FilePath = filePath;
        _options = options ?? new ParquetSourceOptions();
    }

    public string Name => $"Parquet: {FilePath}";

    public DataSourceType SourceType => DataSourceType.Custom;

    public async IAsyncEnumerable<T> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ponytail: full-file deserialize; upgrade path = row-group streaming via ParquetReader
        var result = await ParquetSerializer
            .DeserializeAsync<T>(FilePath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var count = 0;
        foreach (var item in result.Data)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.MaxRows.HasValue && count >= _options.MaxRows.Value)
                yield break;

            yield return item;
            count++;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(FilePath);
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "Parquet",
            Location = FilePath,
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(FilePath));

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<T>();
        await foreach (var row in ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }
}
