using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Catalog;
using DataForge.Jobs.Models;
using DataForge.Jobs.Providers;

namespace DataForge.Jobs.MySql;

public static class JobMySqlRegistration
{
    public static void AddDataForgeJobsMySql()
    {
        var provider = new MySqlJobProvider();
        JobProviderRegistry.Register((IJobSourceProvider)provider);
        JobProviderRegistry.Register((IJobSinkProvider)provider);
        JobCatalog.Register(new MySqlCatalogProvider());
    }
}

internal sealed class MySqlJobProvider : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["mysql"];
    public IReadOnlyList<string> SinkTypes { get; } = ["mysql"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        var connection = job.Source.Connection ?? throw new InvalidOperationException("MySQL source requires connection.");
        var table = job.Source.Table ?? throw new InvalidOperationException("MySQL source requires table.");
        return new DataPipeline<JobRow>(new JobRowMySqlSource(connection, table).ReadAsync());
    }

    public Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken cancellationToken = default) =>
        JobRowMySqlSink.WriteAsync(rows, sink, cancellationToken);
}
