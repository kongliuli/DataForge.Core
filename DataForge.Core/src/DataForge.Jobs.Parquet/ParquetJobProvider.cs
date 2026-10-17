using DataForge.Jobs;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.Parquet;

public static class JobParquetRegistration
{
    public static void AddDataForgeJobsParquet()
    {
        var provider = new ParquetJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
    }
}

internal sealed class ParquetJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["parquet"];

    public IReadOnlyList<string> SinkTypes { get; } = ["parquet"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {            "parquet" => new DataPipeline<JobRow>(new JobRowParquetSource(job.Source.Path).ReadAsync()),
            _ => throw new NotSupportedException(job.Source.Type)
        };
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowParquetSink.WriteAsync(rows, sink, cancellationToken);
}
