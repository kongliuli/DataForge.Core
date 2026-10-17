using DataForge.Jobs;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.SqlServer;

public static class JobSqlServerRegistration
{
    public static void AddDataForgeJobsSqlServer()
    {
        var provider = new SqlServerJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
        JobCatalog.Register(new SqlServerCatalogProvider());
    }
}

internal sealed class SqlServerJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["sqlserver"];

    public IReadOnlyList<string> SinkTypes { get; } = ["sqlserver"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {            "sqlserver" => CreateSql(job),
            _ => throw new NotSupportedException(job.Source.Type)
        };
    }

    private static IDataPipeline<JobRow> CreateSql(JobDefinition job)
    {
        var connection = job.Source.Connection ?? throw new InvalidOperationException("SQL Server source requires connection.");
        var table = job.Source.Table ?? throw new InvalidOperationException("SQL Server source requires table.");
        return new DataPipeline<JobRow>(new JobRowSqlServerSource(connection, table).ReadAsync());
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowSqlServerSink.WriteAsync(rows, sink, cancellationToken);
}
