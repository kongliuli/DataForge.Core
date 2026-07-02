namespace DataMigration.Contracts;

public interface IDataTarget : IPlugin
{
    Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        CancellationToken ct
    );
}

public interface IBatchDataTarget : IDataTarget
{
    Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        int batchSize,
        CancellationToken ct
    );
}
