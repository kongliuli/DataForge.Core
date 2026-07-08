using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Pipeline;

public static class PerformanceExtensions
{
    public static IDataPipeline<T> WithProgress<T>(
        this IDataPipeline<T> pipeline,
        Action<ProgressReport<T>> progressHandler,
        int reportInterval = 1000)
    {
        if (pipeline is not DataPipeline<T> dp)
        {
            throw new NotSupportedException("WithProgress requires a DataForge pipeline instance.");
        }

        return dp.WithPipelineInterceptor((source, ct) => ProgressInterceptor(source, progressHandler, reportInterval, ct));
    }

    public static IDataPipeline<T> WithCounter<T>(
        this IDataPipeline<T> pipeline,
        PerformanceCounter counter)
    {
        if (pipeline is not DataPipeline<T> dp)
        {
            throw new NotSupportedException("WithCounter requires a DataForge pipeline instance.");
        }

        return dp.WithPipelineInterceptor((source, ct) => CounterInterceptor(source, counter, ct));
    }

    [Obsolete("Use SelectParallelAsync on a future release. WithParallelization did not parallelize user transforms.")]
    public static IDataPipeline<T> WithParallelization<T>(
        this IDataPipeline<T> pipeline,
        int maxDegreeOfParallelism = 4)
        => pipeline;

    [Obsolete("Use SelectParallelAsync on a future release.")]
    public static IDataPipeline<T> WithParallelizationUnordered<T>(
        this IDataPipeline<T> pipeline,
        int maxDegreeOfParallelism = 4)
        => pipeline;

    private static async IAsyncEnumerable<T> CounterInterceptor<T>(
        IAsyncEnumerable<T> source,
        PerformanceCounter counter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        counter.Start();
        try
        {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                counter.IncrementProcessed();
                yield return item;
            }
        }
        finally
        {
            counter.Stop();
        }
    }

    private static async IAsyncEnumerable<T> ProgressInterceptor<T>(
        IAsyncEnumerable<T> source,
        Action<ProgressReport<T>> progressHandler,
        int reportInterval,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long count = 0;
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
            if (count % reportInterval == 0)
            {
                progressHandler(new ProgressReport<T>
                {
                    ProcessedCount = count,
                    CurrentItem = item
                });
            }

            yield return item;
        }
    }
}
