using DataMigration.Contracts;
using DataMigration.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebDts.Blazor.Hubs;
using WebDts.Blazor.Models;
using WebDts.Blazor.Services;

namespace WebDts.Blazor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MigrationTasksController : ControllerBase
{
    private readonly IMigrationService _migrationService;
    private readonly IPluginManager _pluginManager;
    private readonly IHubContext<MigrationHub> _hubContext;
    private readonly ILogger<MigrationTasksController> _logger;
    private readonly ITaskService _taskService;

    public MigrationTasksController(
        IMigrationService migrationService,
        IPluginManager pluginManager,
        IHubContext<MigrationHub> hubContext,
        ILogger<MigrationTasksController> logger,
        ITaskService taskService)
    {
        _migrationService = migrationService;
        _pluginManager = pluginManager;
        _hubContext = hubContext;
        _logger = logger;
        _taskService = taskService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] MigrationTaskRequest request)
    {
        try
        {
            var task = new WebMigrationTask
            {
                Name = request.Name,
                SourcePluginId = request.DataSourceId,
                TargetPluginId = request.DataTargetId,
                SourceConfig = request.DataSourceConfig ?? new Dictionary<string, string>(),
                TargetConfig = request.DataTargetConfig ?? new Dictionary<string, string>(),
                Transformations = request.TransformerIds?.Select((id, index) => new WebTransformation
                {
                    TransformerId = id,
                    Configuration = request.TransformerConfigs?.Count > index ? request.TransformerConfigs[index] : new Dictionary<string, string>()
                }).ToList() ?? new List<WebTransformation>()
            };

            var createdTask = await _taskService.CreateTaskAsync(task);

            _logger.LogInformation("Migration task created: {TaskId}", createdTask.Id);
            return Ok(new { taskId = createdTask.Id, message = "任务创建成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating migration task");
            return StatusCode(500, new { error = "创建任务失败", details = ex.Message });
        }
    }

    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetTask(Guid taskId)
    {
        try
        {
            var task = await _taskService.GetTaskAsync(taskId);
            return Ok(task);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "任务不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration task");
            return StatusCode(500, new { error = "获取任务失败", details = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTasks()
    {
        try
        {
            var tasks = await _taskService.GetTasksAsync();
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all migration tasks");
            return StatusCode(500, new { error = "获取任务列表失败", details = ex.Message });
        }
    }

    [HttpPost("{taskId}/execute")]
    public async Task<IActionResult> ExecuteTask(Guid taskId)
    {
        try
        {
            var task = await _taskService.GetTaskAsync(taskId);

            if (task.Status == MigrationTaskStatus.Running)
            {
                return BadRequest(new { error = "任务正在执行中" });
            }

            await _taskService.StartTaskAsync(taskId);

            // Notify clients that task has started
            await _hubContext.Clients.Group(taskId.ToString()).SendAsync("ReceiveStatus", taskId, "Running", "任务开始执行");

            // Get plugins
            var dataSource = _pluginManager.GetDataSource(task.SourcePluginId);
            var dataTarget = _pluginManager.GetTarget(task.TargetPluginId);
            var transformers = task.Transformations.Select(t => _pluginManager.GetTransformer(t.TransformerId)).ToList();

            // Create configurations
            var sourceConfig = new SourceConfig();
            foreach (var kvp in task.SourceConfig)
            {
                sourceConfig[kvp.Key] = kvp.Value;
            }

            var targetConfig = new TargetConfig();
            foreach (var kvp in task.TargetConfig)
            {
                targetConfig[kvp.Key] = kvp.Value;
            }

            var transformerConfigs = new List<TransformConfig>();
            foreach (var t in task.Transformations)
            {
                var tConfig = new TransformConfig();
                foreach (var kvp in t.Configuration)
                {
                    tConfig[kvp.Key] = kvp.Value;
                }
                transformerConfigs.Add(tConfig);
            }

            // Execute migration
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var progress = new Progress<MigrationProgress>(async p =>
            {
                await _hubContext.Clients.Group(taskId.ToString()).SendAsync("ReceiveProgress", taskId, p.Percentage, p.Message);
            });

            var result = await _migrationService.ExecuteMigrationAsync(
                dataSource,
                dataTarget,
                transformers,
                sourceConfig,
                targetConfig,
                transformerConfigs,
                progress,
                cancellationToken);

            if (result.Success)
            {
                // 任务完成
                await _taskService.StopTaskAsync(taskId);
                await _hubContext.Clients.Group(taskId.ToString()).SendAsync("ReceiveStatus", taskId, "Completed", "任务执行完成");
            }
            else
            {
                // 任务失败
                await _taskService.FailTaskAsync(taskId, result.ErrorMessage);
                await _taskService.StopTaskAsync(taskId);
                await _hubContext.Clients.Group(taskId.ToString()).SendAsync("ReceiveStatus", taskId, "Failed", $"任务执行失败: {result.ErrorMessage}");
            }

            _logger.LogInformation("Migration task {TaskId} completed with status {Status}", taskId, result.Success ? "Completed" : "Failed");

            return Ok(new { taskId, status = result.Success ? "Completed" : "Failed", message = result.Success ? "任务执行成功" : result.ErrorMessage });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "任务不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing migration task {TaskId}", taskId);
            return StatusCode(500, new { error = "执行任务失败", details = ex.Message });
        }
    }

    [HttpDelete("{taskId}")]
    public async Task<IActionResult> DeleteTask(Guid taskId)
    {
        try
        {
            await _taskService.DeleteTaskAsync(taskId);
            _logger.LogInformation("Migration task deleted: {TaskId}", taskId);
            return Ok(new { message = "任务已删除" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "任务不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting migration task {TaskId}", taskId);
            return StatusCode(500, new { error = "删除任务失败", details = ex.Message });
        }
    }

    [HttpPut("{taskId}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] MigrationTaskRequest request)
    {
        try
        {
            var existingTask = await _taskService.GetTaskAsync(taskId);
            var updatedTask = existingTask with
            {
                Name = request.Name,
                SourcePluginId = request.DataSourceId,
                TargetPluginId = request.DataTargetId,
                SourceConfig = request.DataSourceConfig ?? new Dictionary<string, string>(),
                TargetConfig = request.DataTargetConfig ?? new Dictionary<string, string>(),
                Transformations = request.TransformerIds?.Select((id, index) => new WebTransformation
                {
                    TransformerId = id,
                    Configuration = request.TransformerConfigs?.Count > index ? request.TransformerConfigs[index] : new Dictionary<string, string>()
                }).ToList() ?? new List<WebTransformation>()
            };

            await _taskService.UpdateTaskAsync(updatedTask);
            _logger.LogInformation("Migration task updated: {TaskId}", taskId);
            return Ok(new { message = "任务已更新" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "任务不存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating migration task {TaskId}", taskId);
            return StatusCode(500, new { error = "更新任务失败", details = ex.Message });
        }
    }
}

public class MigrationTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string DataTargetId { get; set; } = string.Empty;
    public List<string>? TransformerIds { get; set; }
    public Dictionary<string, string>? DataSourceConfig { get; set; }
    public Dictionary<string, string>? DataTargetConfig { get; set; }
    public List<Dictionary<string, string>>? TransformerConfigs { get; set; }
}
