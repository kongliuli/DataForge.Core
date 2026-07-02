namespace DataMigration.Contracts;

public class MigrationTask
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString();
    public SourceConfig Source { get; set; } = new();
    public List<TransformConfig> Transforms { get; set; } = new();
    public TargetConfig Target { get; set; } = new();
    public ExecutionOptions Options { get; set; } = new();
}

public class ExecutionOptions
{
    public int BatchSize { get; set; } = 1000;
    public bool EnableCheckpoint { get; set; } = false;
    public int MaxDegreeOfParallelism { get; set; } = 1;
}
