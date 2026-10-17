using DataForge.Jobs;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.PostgreSql;

public static class JobPostgreSqlRegistration
{
    public static void AddDataForgeJobsPostgreSql()
    {
        var provider = new PostgreSqlJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
        JobCatalog.Register(new PostgreSqlCatalogProvider());
    }
}

internal sealed class PostgreSqlJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["postgresql", "postgres"];

    public IReadOnlyList<string> SinkTypes { get; } = ["postgresql", "postgres"];

    public IDataPipeline<JobRow> Create(JobDefinition job) => Create(job, null);

    public IDataPipeline<JobRow> Create(JobDefinition job, string? watermark)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" => CreatePg(job, watermark),
            _ => throw new NotSupportedException(job.Source.Type)
        };
    }

    private static IDataPipeline<JobRow> CreatePg(JobDefinition job, string? watermark)
    {
        var connection = job.Source.Connection ?? throw new InvalidOperationException("PostgreSQL source requires connection.");
        var table = job.Source.Table ?? throw new InvalidOperationException("PostgreSQL source requires table.");
        var source = new JobRowPostgreSqlSource(connection, table);
        return new DataPipeline<JobRow>(source.ReadAsync(job.Watermark?.Column, watermark));
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowPostgreSqlSink.WriteAsync(rows, sink, cancellationToken);
}
