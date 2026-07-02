namespace WebDts.Blazor.Models;

public class WebDtsSettings
{
    public string PluginsDirectory { get; set; } = "Plugins";
    public string UploadDirectory { get; set; } = "Uploads";
    public long MaxFileSize { get; set; } = 104857600; // 100MB
    public List<string> AllowedFileTypes { get; set; } = new() { ".csv", ".xlsx", ".xls", ".json", ".xml" };
}
