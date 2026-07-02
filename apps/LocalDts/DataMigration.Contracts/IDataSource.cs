namespace DataMigration.Contracts;

public interface IDataSource : IPlugin
{
    IAsyncEnumerable<DataRecord> ExtractAsync(
        SourceConfig config,
        CancellationToken ct
    );
}
