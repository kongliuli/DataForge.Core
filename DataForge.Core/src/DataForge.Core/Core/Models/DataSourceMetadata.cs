namespace DataForge.Core.Core.Models;

public class DataSourceMetadata
{
    public string SourceType { get; set; } = string.Empty;
    
    public string Location { get; set; } = string.Empty;
    
    public long Size { get; set; }
    
    public DateTime? LastModified { get; set; }
    
    public int? RecordCount { get; set; }
    
    public Dictionary<string, string> AdditionalInfo { get; set; } = [];
}

public class ExportResults
{
    public int RecordsWritten { get; set; }
    
    public string OutputPath { get; set; } = string.Empty;
    
    public long OutputSize { get; set; }
    
    public TimeSpan Duration { get; set; }
    
    public List<string> Errors { get; } = [];
    
    public bool HasErrors => Errors.Count > 0;
}