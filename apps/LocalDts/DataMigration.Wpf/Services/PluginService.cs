using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DataMigration.Contracts;

namespace DataMigration.Wpf.Services;

/// <summary>
/// 插件服务接口，用于管理插件的加载、卸载和获取
/// </summary>
public interface IPluginService
{
    /// <summary>
    /// 同步加载指定目录中的插件
    /// </summary>
    /// <param name="directoryPath">插件目录路径</param>
    /// <returns>加载的插件列表和错误信息列表</returns>
    (IEnumerable<IPlugin> plugins, List<string> errors) LoadPlugins(string directoryPath);
    
    /// <summary>
    /// 异步加载指定目录中的插件
    /// </summary>
    /// <param name="directoryPath">插件目录路径</param>
    /// <returns>加载的插件列表和错误信息列表</returns>
    Task<(IEnumerable<IPlugin> plugins, List<string> errors)> LoadPluginsAsync(string directoryPath);
    
    /// <summary>
    /// 卸载所有已加载的插件
    /// </summary>
    void UnloadPlugins();
    
    /// <summary>
    /// 根据插件ID获取插件实例
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件实例，如果未找到则返回null</returns>
    IPlugin? GetPluginById(string pluginId);
    
    /// <summary>
    /// 获取所有已加载的插件
    /// </summary>
    /// <returns>已加载的插件列表</returns>
    IEnumerable<IPlugin> GetAllPlugins();
    
    /// <summary>
    /// 获取已加载的插件数量
    /// </summary>
    int PluginCount { get; }
}

/// <summary>
/// 插件服务实现，用于管理插件的加载、卸载和获取
/// </summary>
public class PluginService : IPluginService
{
    /// <summary>
    /// 已加载的插件列表
    /// </summary>
    private readonly List<IPlugin> _loadedPlugins = new();

    /// <summary>
    /// 获取已加载的插件数量
    /// </summary>
    public int PluginCount => _loadedPlugins.Count;

    /// <summary>
    /// 同步加载指定目录中的插件
    /// </summary>
    /// <param name="directoryPath">插件目录路径</param>
    /// <returns>加载的插件列表和错误信息列表</returns>
    public (IEnumerable<IPlugin> plugins, List<string> errors) LoadPlugins(string directoryPath)
    {
        return LoadPluginsAsync(directoryPath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步加载指定目录中的插件
    /// </summary>
    /// <param name="directoryPath">插件目录路径</param>
    /// <returns>加载的插件列表和错误信息列表</returns>
    public async Task<(IEnumerable<IPlugin> plugins, List<string> errors)> LoadPluginsAsync(string directoryPath)
    {
        _loadedPlugins.Clear();
        var errors = new List<string>();

        if (!Directory.Exists(directoryPath))
        {
            errors.Add($"插件目录不存在: {directoryPath}");
            return (_loadedPlugins, errors);
        }

        var pluginFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

        if (!pluginFiles.Any())
        {
            errors.Add("插件目录中没有 DLL 文件");
            return (_loadedPlugins, errors);
        }

        await Task.Run(() =>
        {
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(pluginFile);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

                    if (!pluginTypes.Any())
                    {
                        errors.Add($"插件文件中没有找到插件类型: {pluginFile}");
                        continue;
                    }

                    foreach (var pluginType in pluginTypes)
                    {
                        try
                        {
                            var plugin = (IPlugin?)Activator.CreateInstance(pluginType);
                            if (plugin != null)
                            {
                                _loadedPlugins.Add(plugin);
                            }
                            else
                            {
                                errors.Add($"无法创建插件实例: {pluginType.FullName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"创建插件实例时出错 {pluginType.FullName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"加载插件文件时出错 {pluginFile}: {ex.Message}");
                }
            }
        });

        return (_loadedPlugins, errors);
    }

    /// <summary>
    /// 卸载所有已加载的插件
    /// </summary>
    public void UnloadPlugins()
    {
        _loadedPlugins.Clear();
    }

    /// <summary>
    /// 根据插件ID获取插件实例
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>插件实例，如果未找到则返回null</returns>
    public IPlugin? GetPluginById(string pluginId)
    {
        return _loadedPlugins.FirstOrDefault(p => p.Id == pluginId);
    }

    /// <summary>
    /// 获取所有已加载的插件
    /// </summary>
    /// <returns>已加载的插件列表</returns>
    public IEnumerable<IPlugin> GetAllPlugins()
    {
        return _loadedPlugins;
    }
}