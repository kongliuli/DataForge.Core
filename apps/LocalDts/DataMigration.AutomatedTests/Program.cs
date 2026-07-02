using DataMigration.Core;
using DataMigration.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text;

namespace DataMigration.AutomatedTests;

class Program
{
    static async Task Main(string[] args)
    {
        // 注册编码提供程序，以支持Excel文件读取
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        Console.WriteLine("Data Migration Automated Tests");
        Console.WriteLine("==============================");
        
        // 初始化插件管理器
        var pluginManager = new PluginManager();
        
        // 加载插件
        string pluginsDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Plugins"));
        Console.WriteLine($"Loading plugins from: {pluginsDirectory}");
        pluginManager.LoadPlugins(pluginsDirectory);
        
        // 列出所有加载的组件
        var components = pluginManager.ListAllComponents();
        Console.WriteLine($"\nLoaded {components.Count()} components:");
        foreach (var component in components)
        {
            Console.WriteLine($"  - {component.Type}: {component.Name} (v{component.Version}) - ID: {component.Id}");
        }
        
        // 读取测试配置
        string configPath = Path.Combine(Directory.GetCurrentDirectory(), "test_config.json");
        if (File.Exists(configPath))
        {
            Console.WriteLine($"\nReading test configuration from: {configPath}");
            string configContent = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var config = JsonSerializer.Deserialize<TestConfig>(configContent, options);
            
            // 执行测试场景
            if (config != null && config.TestScenarios != null)
            {
                foreach (var scenario in config.TestScenarios)
                {
                    Console.WriteLine($"\nExecuting test scenario: {scenario.Name}");
                    
                    try
                    {
                        // 获取数据源
                        var dataSource = pluginManager.GetDataSource(scenario.DataSource.Id);
                        Console.WriteLine($"  Using DataSource: {dataSource.Name}");
                        
                        // 获取目标源
                        var dataTarget = pluginManager.GetTarget(scenario.DataTarget.Id);
                        Console.WriteLine($"  Using DataTarget: {dataTarget.Name}");
                        
                        // 执行数据迁移
                        Console.WriteLine("  Executing data migration...");
                        
                        // 创建配置对象
                        var sourceConfig = new SourceConfig();
                        foreach (var kvp in scenario.DataSource.Config)
                        {
                            sourceConfig[kvp.Key] = kvp.Value.ToString();
                        }
                        
                        var targetConfig = new TargetConfig();
                        foreach (var kvp in scenario.DataTarget.Config)
                        {
                            targetConfig[kvp.Key] = kvp.Value.ToString();
                        }
                        
                        // 提取数据
                        var cancellationToken = CancellationToken.None;
                        var dataRecords = dataSource.ExtractAsync(sourceConfig, cancellationToken);
                        
                        // 加载数据到目标
                        await dataTarget.LoadAsync(dataRecords, targetConfig, cancellationToken);
                        
                        Console.WriteLine("  Migration completed successfully.");
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error executing test scenario: {ex.Message}");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"\nTest configuration file not found: {configPath}");
        }
        
        Console.WriteLine("\nPlugin loading completed.");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}

public class TestConfig
{
    [JsonPropertyName("testScenarios")]
    public List<TestScenario> TestScenarios { get; set; }
}

public class TestScenario
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("dataSource")]
    public DataSourceConfig DataSource { get; set; }
    
    [JsonPropertyName("dataTarget")]
    public DataTargetConfig DataTarget { get; set; }
}

public class DataSourceConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("config")]
    public Dictionary<string, object> Config { get; set; }
}

public class DataTargetConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("config")]
    public Dictionary<string, object> Config { get; set; }
}
