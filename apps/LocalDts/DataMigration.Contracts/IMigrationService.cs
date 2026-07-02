namespace DataMigration.Contracts;

public interface IMigrationService
{
    Task<MigrationResult> ExecuteMigrationAsync(
        IDataSource dataSource,
        IDataTarget dataTarget,
        IEnumerable<ITransformer> transformers,
        SourceConfig sourceConfig,
        TargetConfig targetConfig,
        IEnumerable<TransformConfig> transformerConfigs,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken);
}

public class MigrationResult
{
    public bool Success { get; set; }
    public int ProcessedRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}

public class MigrationProgress
{
    public int Percentage { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProcessedRecords { get; set; }
    public int TotalRecords { get; set; }
}
