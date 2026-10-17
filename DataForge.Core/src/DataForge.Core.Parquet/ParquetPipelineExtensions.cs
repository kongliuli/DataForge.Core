using DataForge.Core;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Parquet;

public static class ParquetPipelineExtensions
{
    public static IDataPipeline<T> FromParquet<T>(string filePath, ParquetSourceOptions? options = null) where T : class, new()
    {
        var source = new ParquetSource<T>(filePath, options);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static IDataPipeline<T> FromParquet<T>(this DataForgePipeline _, string filePath, ParquetSourceOptions? options = null)
        where T : class, new()
        => FromParquet<T>(filePath, options);

    public static Task<ExportResults> ToParquetAsync<T>(
        this IDataPipeline<T> pipeline,
        string filePath,
        ParquetExportOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var target = new ParquetTarget<T>(options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), filePath, cancellationToken);
    }
}
