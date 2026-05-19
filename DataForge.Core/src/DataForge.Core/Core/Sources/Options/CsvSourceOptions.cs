using System.Text;

namespace DataForge.Core.Core.Sources.Options;

public class CsvSourceOptions
{
    public char Delimiter { get; set; } = ',';
    
    public char QuoteChar { get; set; } = '"';
    
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    
    public bool HasHeaderRow { get; set; } = true;
    
    public int SkipRows { get; set; } = 0;
    
    public int? MaxRows { get; set; }
    
    public string? CommentPrefix { get; set; }
}