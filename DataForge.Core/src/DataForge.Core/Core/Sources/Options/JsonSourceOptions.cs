using System.Text.Json;

namespace DataForge.Core.Core.Sources.Options;

public class JsonSourceOptions
{
    public JsonSerializerOptions? SerializerOptions { get; set; }
    
    public bool AllowTrailingCommas { get; set; } = true;
    
    public bool ReadCommentHandling { get; set; } = true;
    
    public bool UseStreaming { get; set; } = true;
}