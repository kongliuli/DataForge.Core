namespace DataMigration.Contracts;

public interface ITransformer : IPlugin
{
    IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        CancellationToken ct
    );
}

public interface IParallelTransformer : ITransformer
{
    IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        int maxDegreeOfParallelism,
        CancellationToken ct
    );
}
