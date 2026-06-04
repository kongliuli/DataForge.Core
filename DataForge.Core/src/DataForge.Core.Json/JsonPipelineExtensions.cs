using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Json;

public static class JsonPipelineExtensions
{
    public static IDataPipeline<T> FromJson<T>(this DataForgePipeline builder, string filePath, JsonSourceOptions? options = null)
    {
        var source = new JsonSource<T>(filePath, options);
        return new DataPipeline<T>(source.ReadAsync());
    }

    public static Task<ExportResults> ToJson<T>(this IDataPipeline<T> pipeline, string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var target = new JsonTarget<T>(options);
        return target.ExportAsync(pipeline.AsAsyncEnumerable(cancellationToken), filePath, cancellationToken);
    }
}
