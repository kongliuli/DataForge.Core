using DataForge.Jobs;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.DuckDB;

public static class JobDuckDBRegistration
{
    public static void AddDataForgeJobsDuckDB()
    {
        var provider = new DuckDBJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
        JobCatalog.Register(new DuckDbCatalogProvider());
    }
}

internal sealed class DuckDBJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["duckdb"];

    public IReadOnlyList<string> SinkTypes { get; } = ["duckdb"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {            "duckdb" => new DataPipeline<JobRow>(new JobRowDuckDbSource(job.Source.Path, job.Source.Query!).ReadAsync()),
            _ => throw new NotSupportedException(job.Source.Type)
        };
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowDuckDbSink.WriteAsync(rows, sink, cancellationToken);
}
