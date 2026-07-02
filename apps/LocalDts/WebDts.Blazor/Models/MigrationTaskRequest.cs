namespace WebDts.Blazor.Models
{
    public class MigrationTaskRequest
    {
        public string Name { get; set; } = string.Empty;
        public string DataSourceId { get; set; } = string.Empty;
        public string DataTargetId { get; set; } = string.Empty;
        public List<string> TransformerIds { get; set; } = new();
    }
}