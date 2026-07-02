namespace WebDts.Blazor.Models;

public record WebMigrationTask
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourcePluginId { get; set; } = string.Empty;
    public string TargetPluginId { get; set; } = string.Empty;
    public Dictionary<string, string> SourceConfig { get; set; } = new();
    public Dictionary<string, string> TargetConfig { get; set; } = new();
    public List<WebTransformation> Transformations { get; set; } = new();
    public MigrationTaskStatus Status { get; set; } = MigrationTaskStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public record WebTransformation
{
    public string TransformerId { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = new();
}
