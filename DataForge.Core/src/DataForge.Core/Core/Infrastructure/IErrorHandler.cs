namespace DataForge.Core.Core.Infrastructure;

public enum ErrorAction
{
    Continue,
    Skip,
    Stop,
    Throw
}

public class PipelineErrorContext
{
    public Exception Exception { get; init; } = null!;
    public object? Item { get; init; }
    public int ItemIndex { get; init; }
    public string PipelineStage { get; init; } = "";
}