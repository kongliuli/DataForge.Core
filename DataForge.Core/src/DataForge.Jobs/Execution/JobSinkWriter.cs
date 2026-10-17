using DataForge.Core.Core.Models;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.Execution;

internal static class JobSinkWriter
{
    public static Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        if (!JobProviderRegistry.TryGetSink(sink.Type, out var provider))
            throw new NotSupportedException(
                $"Sink type '{sink.Type}' is not registered. Reference the matching DataForge.Jobs.* adapter and call its registration method.");

        return provider.WriteAsync(rows, sink, cancellationToken);
    }
}
