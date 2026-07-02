using DataMigration.Contracts;
using DataMigration.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDts.Blazor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PluginsController : ControllerBase
{
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<PluginsController> _logger;

    public PluginsController(IPluginManager pluginManager, ILogger<PluginsController> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetAllPlugins()
    {
        try
        {
            var components = _pluginManager.ListAllComponents();
            return Ok(components);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugins");
            return StatusCode(500, new { error = "获取插件列表失败", details = ex.Message });
        }
    }

    [HttpGet("datasources")]
    public IActionResult GetDataSources()
    {
        try
        {
            var dataSources = _pluginManager.GetAvailableDataSources();
            return Ok(dataSources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data sources");
            return StatusCode(500, new { error = "获取数据源列表失败", details = ex.Message });
        }
    }

    [HttpGet("targets")]
    public IActionResult GetTargets()
    {
        try
        {
            var targets = _pluginManager.GetAvailableTargets();
            return Ok(targets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting targets");
            return StatusCode(500, new { error = "获取目标源列表失败", details = ex.Message });
        }
    }

    [HttpGet("transformers")]
    public IActionResult GetTransformers()
    {
        try
        {
            var transformers = _pluginManager.GetAvailableTransformers();
            return Ok(transformers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transformers");
            return StatusCode(500, new { error = "获取转换器列表失败", details = ex.Message });
        }
    }

    [HttpGet("datasource/{id}/config")]
    public IActionResult GetDataSourceConfig(string id)
    {
        try
        {
            var dataSource = _pluginManager.GetDataSource(id);
            // Return basic plugin info as config fields are not available in the interface
            var configInfo = new
            {
                PluginId = dataSource.Id,
                PluginName = dataSource.Name,
                PluginVersion = dataSource.Version,
                Message = "配置字段需要通过插件特定接口获取"
            };
            return Ok(configInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data source config for {Id}", id);
            return StatusCode(500, new { error = "获取数据源配置失败", details = ex.Message });
        }
    }

    [HttpGet("target/{id}/config")]
    public IActionResult GetTargetConfig(string id)
    {
        try
        {
            var target = _pluginManager.GetTarget(id);
            // Return basic plugin info as config fields are not available in the interface
            var configInfo = new
            {
                PluginId = target.Id,
                PluginName = target.Name,
                PluginVersion = target.Version,
                Message = "配置字段需要通过插件特定接口获取"
            };
            return Ok(configInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting target config for {Id}", id);
            return StatusCode(500, new { error = "获取目标源配置失败", details = ex.Message });
        }
    }

    [HttpGet("transformer/{id}/config")]
    public IActionResult GetTransformerConfig(string id)
    {
        try
        {
            var transformer = _pluginManager.GetTransformer(id);
            // Return basic plugin info as config fields are not available in the interface
            var configInfo = new
            {
                PluginId = transformer.Id,
                PluginName = transformer.Name,
                PluginVersion = transformer.Version,
                Message = "配置字段需要通过插件特定接口获取"
            };
            return Ok(configInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transformer config for {Id}", id);
            return StatusCode(500, new { error = "获取转换器配置失败", details = ex.Message });
        }
    }
}
