using DataForge.Core.Core.Pipeline;
using System.Collections.Generic;
using System.Threading;

namespace DataForge.Core;

public static class PipelineCollectionExtensions
{
    public static IDataPipeline<T> ToDataForge<T>(this IEnumerable<T> source)
        => DataForgePipeline.FromMemory(source);

    public static IDataPipeline<T> ToDataForge<T>(this IAsyncEnumerable<T> source)
        => new DataPipeline<T>(source);
}
