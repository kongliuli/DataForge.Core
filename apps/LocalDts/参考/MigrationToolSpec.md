.NET 组件化数据迁移工具开发规范
版本：1.0
目标：设计并实现一个类似 AWS DMS / 阿里云 DTS 的组件化数据迁移工具，支持数据源、清洗规则、目标源的动态插件化扩展。

1. 项目目标与核心原则
组件化：数据源（Source）、清洗规则（Transformer）、目标源（Target）均可独立开发、部署、热插拔。

可扩展：在不修改核心框架的前提下，通过实现标准接口添加新组件。

流式处理：数据以 IAsyncEnumerable<DataRecord> 形式在管道中流动，支持高吞吐、低内存占用。

配置驱动：所有组件的行为由 JSON 配置定义，支持运行时修改。

.NET 版本：.NET 8 或更高版本。

2. 总体架构
采用 三层模型 + 插件系统：

核心框架：任务调度、插件管理、数据流编排、监控日志。

插件层：实现 IDataSource、ITransformer、IDataTarget 接口的独立程序集。

配置管理层：组件注册、任务配置、模板管理。

text
┌─────────────────────────────────────────────────────┐
│                  配置管理模块                         │
│   (组件发现/注册/配置存储/任务模板)                   │
└─────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────┐
│              任务调度与执行引擎                       │
│   (状态机/并发控制/断点续传/监控)                     │
└─────────────────────────────────────────────────────┘
                         ↓
┌───────────┐   ┌───────────┐   ┌───────────┐
│ IDataSource│ → │ITransformer│ → │IDataTarget│
│   实现集    │   │   实现集    │   │   实现集    │
└───────────┘   └───────────┘   └───────────┘
3. 核心接口定义（插件契约层）
创建一个独立的 .dll 项目 DataMigration.Contracts，所有插件必须引用它。

3.1 基础插件接口
csharp
namespace DataMigration.Contracts;

public interface IPlugin
{
    string Id { get; }          // 唯一标识，如 "MyCompany.SqlServerSource"
    string Name { get; }        // 显示名称
    Version Version { get; }    // 语义化版本

    Task InitializeAsync(IServiceProvider services, CancellationToken ct);
    Task ExecuteAsync(CancellationToken ct);   // 可选，用于长生命周期的组件
    Task ShutdownAsync(CancellationToken ct);
}
3.2 数据源接口
csharp
public interface IDataSource : IPlugin
{
    IAsyncEnumerable<DataRecord> ExtractAsync(
        SourceConfig config, 
        CancellationToken ct
    );
}
3.3 清洗规则接口
csharp
public interface ITransformer : IPlugin
{
    IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        CancellationToken ct
    );
}
3.4 目标源接口
csharp
public interface IDataTarget : IPlugin
{
    Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        CancellationToken ct
    );
}
3.5 数据记录通用模型
csharp
public class DataRecord : Dictionary<string, object?>
{
    public T? GetValue<T>(string key) => this.TryGetValue(key, out var val) ? (T?)val : default;
    public void SetValue(string key, object? value) => this[key] = value;
}
3.6 配置基类
csharp
public abstract class ComponentConfig : Dictionary<string, string>
{
    public string ComponentId { get; set; } = "";
    public string? Description { get; set; }
}

public class SourceConfig : ComponentConfig { }
public class TransformConfig : ComponentConfig { }
public class TargetConfig : ComponentConfig { }
4. 插件开发规范
每个插件必须遵循以下约定：

4.1 项目结构
text
Plugins/
├── DataMigration.Plugin.SqlServerSource/
│   ├── SqlServerDataSource.cs
│   ├── SqlServerSourceConfig.cs (可选)
│   └── DataMigration.Plugin.SqlServerSource.csproj
├── DataMigration.Plugin.RulesEngineTransformer/
└── DataMigration.Plugin.CsvTarget/
4.2 实现示例（SQL Server 数据源）
csharp
using DataMigration.Contracts;

public class SqlServerDataSource : IDataSource
{
    public string Id => "Standard.SqlServerSource";
    public string Name => "SQL Server Database Source";
    public Version Version => new(1, 0, 0);

    private string _connectionString = "";
    private string _query = "";

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        // 可在此注入日志、配置中心等依赖
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(
        SourceConfig config, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        _connectionString = config["ConnectionString"];
        _query = config["Query"] ?? "SELECT * FROM [Table]";
        
        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(_query, conn);
        await conn.OpenAsync(ct);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var record = new DataRecord();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                record[reader.GetName(i)] = reader.GetValue(i);
            }
            yield return record;
        }
    }

    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
4.3 清洗规则实现（规则引擎示例）
推荐使用 Microsoft.RulesEngine 作为内部实现，但接口仍为 ITransformer。

csharp
public class RulesEngineTransformer : ITransformer
{
    public string Id => "Standard.RulesEngine";
    public string Name => "JSON Rules Engine";
    public Version Version => new(1, 0, 0);

    private RulesEngine.RulesEngine _engine = null!;

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        // 初始化规则引擎
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> TransformAsync(
        IAsyncEnumerable<DataRecord> input,
        TransformConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // 从 config 中读取规则 JSON
        var rulesJson = config["RulesJson"];
        _engine = new RulesEngine.RulesEngine(Newtonsoft.Json.JsonConvert.DeserializeObject<Workflow[]>(rulesJson));

        await foreach (var record in input.WithCancellation(ct))
        {
            var result = await _engine.ExecuteAllRulesAsync("workflow", record);
            // 根据规则结果修改 record 或跳过
            if (result.Any(r => r.IsSuccess == false && config["SkipOnFail"] == "true"))
                continue;
            
            yield return record;
        }
    }

    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
4.4 目标源实现（CSV 写入）
csharp
public class CsvTarget : IDataTarget
{
    public string Id => "Standard.CsvTarget";
    public string Name => "CSV File Target";
    public Version Version => new(1, 0, 0);

    private StreamWriter _writer = null!;
    private string _filePath = "";

    public async Task LoadAsync(
        IAsyncEnumerable<DataRecord> input,
        TargetConfig config,
        CancellationToken ct)
    {
        _filePath = config["FilePath"];
        _writer = new StreamWriter(_filePath);
        
        bool headerWritten = false;
        await foreach (var record in input.WithCancellation(ct))
        {
            if (!headerWritten)
            {
                var headers = record.Keys;
                await _writer.WriteLineAsync(string.Join(",", headers));
                headerWritten = true;
            }
            var line = string.Join(",", record.Values.Select(v => $"\"{v}\""));
            await _writer.WriteLineAsync(line);
        }
        await _writer.FlushAsync();
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct) => _writer?.DisposeAsync() ?? ValueTask.CompletedTask;
}
5. 插件加载与管理机制
5.1 插件发现
插件程序集放置在约定目录（例如 ./plugins）。

每个插件可包含多个组件（例如一个程序集同时提供 Source 和 Target）。

使用 AssemblyLoadContext 实现隔离和可卸载。

5.2 插件管理器接口
csharp
public interface IPluginManager
{
    void LoadPlugins(string directory);
    IDataSource GetDataSource(string id);
    ITransformer GetTransformer(string id);
    IDataTarget GetTarget(string id);
    IEnumerable<PluginInfo> ListAllComponents();
}
5.3 动态加载实现要点
使用 System.Runtime.Loader.AssemblyLoadContext 创建独立的上下文。

扫描所有 .dll，通过反射查找实现了 IDataSource、ITransformer、IDataTarget 的类型。

使用 Activator.CreateInstance 创建实例，并调用 InitializeAsync。

将实例注册到内部字典（ConcurrentDictionary<string, IPlugin>）。

5.4 组件生命周期
Initialize：在加载时调用一次，用于建立连接、读取元数据。

Execute：仅在需要执行长运行任务时使用（例如 Kafka 消费者），普通数据流组件无需实现。

Shutdown：任务结束时调用，释放资源。

6. 执行引擎与数据流规范
6.1 任务配置模型
csharp
public class MigrationTask
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString();
    public SourceConfig Source { get; set; } = new();
    public List<TransformConfig> Transforms { get; set; } = new();
    public TargetConfig Target { get; set; } = new();
    public ExecutionOptions Options { get; set; } = new();
}

public class ExecutionOptions
{
    public int BatchSize { get; set; } = 1000;
    public bool EnableCheckpoint { get; set; } = false;
    public int MaxDegreeOfParallelism { get; set; } = 1;
}
6.2 引擎核心伪代码
csharp
public class MigrationEngine
{
    private readonly IPluginManager _pluginManager;

    public async Task RunAsync(MigrationTask task, CancellationToken ct)
    {
        var source = _pluginManager.GetDataSource(task.Source.ComponentId);
        var transforms = task.Transforms.Select(t => _pluginManager.GetTransformer(t.ComponentId)).ToList();
        var target = _pluginManager.GetTarget(task.Target.ComponentId);

        var dataFlow = source.ExtractAsync(task.Source, ct);
        foreach (var transformer in transforms)
        {
            dataFlow = transformer.TransformAsync(dataFlow, task.Transforms[0], ct);
        }
        await target.LoadAsync(dataFlow, task.Target, ct);
    }
}
6.3 错误处理与重试
每个组件内部应实现重试逻辑（针对瞬时故障）。

引擎层应捕获组件异常，并根据 ExecutionOptions 决定中止或跳过。

支持断点续传：组件需实现 ICheckpoint 接口（可选）。

csharp
public interface ICheckpoint
{
    Task<object?> GetCheckpointAsync(string checkpointId);
    Task SaveCheckpointAsync(string checkpointId, object state);
}
7. 配置管理规范
7.1 全局配置文件（appsettings.json）
json
{
  "PluginsDirectory": "./plugins",
  "Logging": { ... },
  "DefaultExecutionOptions": {
    "BatchSize": 5000,
    "EnableCheckpoint": false
  }
}
7.2 任务定义文件示例（task.json）
json
{
  "TaskId": "migrate_orders",
  "Source": {
    "ComponentId": "Standard.SqlServerSource",
    "ConnectionString": "Server=...",
    "Query": "SELECT * FROM Orders WHERE OrderDate > '2023-01-01'"
  },
  "Transforms": [
    {
      "ComponentId": "Standard.RulesEngine",
      "RulesJson": "{ ... }"
    }
  ],
  "Target": {
    "ComponentId": "Standard.CsvTarget",
    "FilePath": "./output/orders.csv"
  },
  "Options": {
    "BatchSize": 2000
  }
}
8. 扩展与版本管理建议
语义版本控制：主版本变更可能破坏接口，次版本添加功能，补丁版本修复问题。

接口版本化：若未来需要修改接口，可定义 IDataSourceV2，引擎同时支持新旧版本。

组件依赖声明：插件可声明依赖的其他 NuGet 包，通过 .deps.json 解析。

热插拔支持：运行时监控插件目录变化，动态加载/卸载新版本（高级特性）。

9. 测试与质量要求
每个插件必须包含单元测试（xUnit），测试其 ExtractAsync / TransformAsync / LoadAsync。

提供模拟数据源和目标的集成测试，验证完整数据流。

性能测试：至少验证 100 万条记录的吞吐量（每秒处理记录数）。

10. 参考开源项目
ETLBox - 数据流管道设计

Transformalize - IReader/IWriter 模式

RulesEngine - 清洗规则引擎

Dotmim.Sync - 数据库同步 Provider 模式

11. 交付物清单（给 trae 的输入）
请 trae 按照以上规范生成以下代码：

DataMigration.Contracts 项目（包含所有接口和基类）。

插件管理器 PluginManager 实现（支持 AssemblyLoadContext）。

执行引擎 MigrationEngine 实现（支持管道编排）。

至少三个示例插件：SQL Server 数据源、RulesEngine 清洗规则、CSV 目标源。

控制台宿主程序，能够加载插件并执行一个任务配置文件。

使用说明：请将本规范保存为 MigrationToolSpec.md，然后提供给 trae，并告知：“请严格按照此规范实现 .NET 组件化数据迁移工具。”