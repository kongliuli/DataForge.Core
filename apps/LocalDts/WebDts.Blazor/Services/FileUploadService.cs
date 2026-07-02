using Microsoft.Extensions.Options;
using WebDts.Blazor.Models;

namespace WebDts.Blazor.Services;

public class FileUploadService : IFileUploadService
{
    private readonly WebDtsSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IOptions<WebDtsSettings> settings,
        IWebHostEnvironment environment,
        ILogger<FileUploadService> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(IFormFile file)
    {
        if (!ValidateFile(file, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var uploadPath = Path.Combine(_environment.ContentRootPath, _settings.UploadDirectory);
        
        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
        }

        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadPath, fileName);

        using (var stream = File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        _logger.LogInformation("File uploaded successfully: {FileName}", fileName);
        return fileName;
    }

    public bool ValidateFile(IFormFile file, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (file == null || file.Length == 0)
        {
            errorMessage = "文件不能为空";
            return false;
        }

        if (file.Length > _settings.MaxFileSize)
        {
            errorMessage = $"文件大小超过限制 ({_settings.MaxFileSize / 1024 / 1024}MB)";
            return false;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_settings.AllowedFileTypes.Contains(extension))
        {
            errorMessage = $"不支持的文件类型。允许的类型: {string.Join(", ", _settings.AllowedFileTypes)}";
            return false;
        }

        return true;
    }

    public string GetFilePath(string fileName)
    {
        return Path.Combine(_environment.ContentRootPath, _settings.UploadDirectory, fileName);
    }

    public void DeleteFile(string fileName)
    {
        var filePath = GetFilePath(fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted: {FileName}", fileName);
        }
    }
}
