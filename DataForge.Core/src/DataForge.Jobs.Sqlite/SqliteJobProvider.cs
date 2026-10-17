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

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {            "sqlite" => CreateSqlite(job),
            _ => throw new NotSupportedException(job.Source.Type)
        };
    }

    private static IDataPipeline<JobRow> CreateSqlite(JobDefinition job)
    {
        var connection = job.Source.Connection ?? throw new InvalidOperationException("SQLite source requires connection.");
        var table = job.Source.Table ?? throw new InvalidOperationException("SQLite source requires table.");
        return new DataPipeline<JobRow>(new JobRowSqliteSource(connection, table).ReadAsync());
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowSqliteSink.WriteAsync(rows, sink, cancellationToken);
}
