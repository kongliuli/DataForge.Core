using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebDts.Blazor.Services;

namespace WebDts.Blazor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileUploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(IFileUploadService fileUploadService, ILogger<FileUploadController> logger)
    {
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "请选择要上传的文件" });
            }

            if (!_fileUploadService.ValidateFile(file, out var errorMessage))
            {
                return BadRequest(new { error = errorMessage });
            }

            var fileName = await _fileUploadService.SaveFileAsync(file);
            var filePath = _fileUploadService.GetFilePath(fileName);

            _logger.LogInformation("File uploaded successfully: {FileName}", fileName);

            return Ok(new 
            { 
                fileName, 
                originalName = file.FileName,
                filePath,
                size = file.Length,
                message = "文件上传成功" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { error = "文件上传失败", details = ex.Message });
        }
    }

    [HttpDelete("{fileName}")]
    public IActionResult DeleteFile(string fileName)
    {
        try
        {
            _fileUploadService.DeleteFile(fileName);
            return Ok(new { message = "文件已删除" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileName}", fileName);
            return StatusCode(500, new { error = "文件删除失败", details = ex.Message });
        }
    }

    [HttpGet("{fileName}/info")]
    public IActionResult GetFileInfo(string fileName)
    {
        try
        {
            var filePath = _fileUploadService.GetFilePath(fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "文件不存在" });
            }

            var fileInfo = new System.IO.FileInfo(filePath);
            
            return Ok(new 
            { 
                fileName,
                filePath,
                size = fileInfo.Length,
                createdAt = fileInfo.CreationTime,
                lastModified = fileInfo.LastWriteTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info for {FileName}", fileName);
            return StatusCode(500, new { error = "获取文件信息失败", details = ex.Message });
        }
    }
}
