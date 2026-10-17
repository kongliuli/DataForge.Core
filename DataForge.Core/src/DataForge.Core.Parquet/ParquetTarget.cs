using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using Parquet;
using Parquet.Serialization;

namespace DataForge.Core.Parquet;

public class ParquetTarget<T> : IDataTarget<T> where T : class
{
    private readonly ParquetExportOptions _options;

    public string Name => "Parquet Target";
    public DataTargetType TargetType => DataTargetType.Custom;

    public ParquetTarget(ParquetExportOptions? options = null)
    {
        _options = options ?? new ParquetExportOptions();
    }

    public async Task<ExportResults> ExportAsync(
        IAsyncEnumerable<T> data,
        string destination,
        CancellationToken cancellationToken = default)
    {
        var batch = new List<T>(_options.RowGroupSize);
        var count = 0;
        var append = _options.Append;

        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count >= _options.RowGroupSize)
            {
                await WriteBatchAsync(batch, destination, append, cancellationToken).ConfigureAwait(false);
                count += batch.Count;
                batch.Clear();
                append = true;
            }
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(batch, destination, append, cancellationToken).ConfigureAwait(false);
            count += batch.Count;
        }

        var fileInfo = new FileInfo(destination);
        return new ExportResults
        {
            RecordsWritten = count,
            OutputPath = destination,
            OutputSize = fileInfo.Exists ? fileInfo.Length : 0
        };
    }

    private static Task WriteBatchAsync(
        IReadOnlyList<T> batch,
        string destination,
        bool append,
        CancellationToken cancellationToken)
    {
        var parquetOptions = new ParquetOptions { Append = append };
        return ParquetSerializer.SerializeAsync(batch, destination, parquetOptions, cancellationToken: cancellationToken);
    }

    public Task WriteAsync(T item, CancellationToken cancellationToken = default) =>
        ExportAsync(ToAsyncEnumerable(item), "", cancellationToken);

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), "", cancellationToken).ConfigureAwait(false);
        return new WriteResult { SuccessCount = result.RecordsWritten };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(T item)
    {
        yield return item;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(IEnumerable<T> items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }
}
