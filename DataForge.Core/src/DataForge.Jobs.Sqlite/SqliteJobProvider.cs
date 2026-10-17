using DataForge.Jobs;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.Sqlite;

public static class JobSqliteRegistration
{
    public static void AddDataForgeJobsSqlite()
    {
        var provider = new SqliteJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
        JobCatalog.Register(new SqliteCatalogProvider());
    }
}

internal sealed class SqliteJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["sqlite"];

    public IReadOnlyList<string> SinkTypes { get; } = ["sqlite"];

    public IDataPipeline<JobRow> Create(JobDefinition job) => Create(job, null);

    public IDataPipeline<JobRow> Create(JobDefinition job, string? watermark)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {
            "sqlite" => CreateSqlite(job, watermark),
            _ => throw new NotSupportedException(job.Source.Type)
        };
    }

    private static IDataPipeline<JobRow> CreateSqlite(JobDefinition job, string? watermark)
    {
        var connection = job.Source.Connection ?? throw new InvalidOperationException("SQLite source requires connection.");
        var table = job.Source.Table ?? throw new InvalidOperationException("SQLite source requires table.");
        var source = new JobRowSqliteSource(connection, table);
        return new DataPipeline<JobRow>(source.ReadAsync(job.Watermark?.Column, watermark));
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowSqliteSink.WriteAsync(rows, sink, cancellationToken);
}
