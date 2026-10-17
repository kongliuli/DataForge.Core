namespace DataForge.Core.Core.Pipeline;

internal delegate IAsyncEnumerable<T> PipelineInterceptor<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken);
