namespace WebDts.Blazor.Services;

public interface IFileUploadService
{
    Task<string> SaveFileAsync(IFormFile file);
    bool ValidateFile(IFormFile file, out string errorMessage);
    string GetFilePath(string fileName);
    void DeleteFile(string fileName);
}
