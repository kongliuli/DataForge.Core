namespace DataForge.Core.Core.Pipeline;

public sealed class ExternalSortOptions
{
    public int? MaxInMemoryRows { get; set; } = 100_000;

    public string? TempDirectory { get; set; }

    public int RunBufferRows { get; set; } = 10_000;
}
