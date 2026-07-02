using System;
using System.IO;
using System.Text.Json;
using DataMigration.Contracts;

namespace DataMigration.Wpf.Services;

/// <summary>
/// 配置服务接口，用于管理配置的保存、加载、删除和查询
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 保存配置到文件
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configuration">配置对象</param>
    /// <param name="configName">配置名称</param>
    void SaveConfiguration<T>(T configuration, string configName);
    
    /// <summary>
    /// 从文件加载配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configName">配置名称</param>
    /// <returns>配置对象，如果文件不存在或加载失败则返回默认值</returns>
    T? LoadConfiguration<T>(string configName);
    
    /// <summary>
    /// 检查配置是否存在
    /// </summary>
    /// <param name="configName">配置名称</param>
    /// <returns>如果配置存在则返回true，否则返回false</returns>
    bool ConfigurationExists(string configName);
    
    /// <summary>
    /// 删除配置文件
    /// </summary>
    /// <param name="configName">配置名称</param>
    void DeleteConfiguration(string configName);
    
    /// <summary>
    /// 获取配置目录路径
    /// </summary>
    /// <returns>配置目录路径</returns>
    string GetConfigDirectory();
}

/// <summary>
/// 配置服务实现，用于管理配置的保存、加载、删除和查询
/// </summary>
public class ConfigurationService : IConfigurationService
{
    /// <summary>
    /// 配置目录路径
    /// </summary>
    private readonly string _configDirectory;

    /// <summary>
    /// 构造函数，初始化配置目录
    /// </summary>
    public ConfigurationService()
    {
        try
        {
            _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DataMigrationTool");

            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Configuration directory initialization error: {ex.Message}");
            throw new InvalidOperationException($"Failed to initialize configuration directory: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configuration">配置对象</param>
    /// <param name="configName">配置名称</param>
    public void SaveConfiguration<T>(T configuration, string configName)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
            {
                throw new ArgumentException("Config name cannot be null or empty", nameof(configName));
            }

            var configPath = Path.Combine(_configDirectory, $"{configName}.json");
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Save configuration error: {ex.Message}");
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从文件加载配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configName">配置名称</param>
    /// <returns>配置对象，如果文件不存在或加载失败则返回默认值</returns>
    public T? LoadConfiguration<T>(string configName)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
            {
                throw new ArgumentException("Config name cannot be null or empty", nameof(configName));
            }

            var configPath = Path.Combine(_configDirectory, $"{configName}.json");

            if (!File.Exists(configPath))
            {
                return default;
            }

            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            // 记录错误日志
            Console.WriteLine($"Load configuration error (JSON): {ex.Message}");
            return default;
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Load configuration error: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// 检查配置是否存在
    /// </summary>
    /// <param name="configName">配置名称</param>
    /// <returns>如果配置存在则返回true，否则返回false</returns>
    public bool ConfigurationExists(string configName)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
            {
                throw new ArgumentException("Config name cannot be null or empty", nameof(configName));
            }

            var configPath = Path.Combine(_configDirectory, $"{configName}.json");
            return File.Exists(configPath);
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Configuration exists check error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 删除配置文件
    /// </summary>
    /// <param name="configName">配置名称</param>
    public void DeleteConfiguration(string configName)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
            {
                throw new ArgumentException("Config name cannot be null or empty", nameof(configName));
            }

            var configPath = Path.Combine(_configDirectory, $"{configName}.json");

            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Delete configuration error: {ex.Message}");
            throw new InvalidOperationException($"Failed to delete configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 获取配置目录路径
    /// </summary>
    /// <returns>配置目录路径</returns>
    public string GetConfigDirectory()
    {
        return _configDirectory;
    }
}