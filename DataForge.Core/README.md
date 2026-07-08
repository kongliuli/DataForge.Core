# DataForge.Core

[![Build Status](https://img.shields.io/github/actions/workflow/status/kongliuli/DataForge.Core/build.yml)](https://github.com/kongliuli/DataForge.Core/actions)
[![NuGet Version](https://img.shields.io/nuget/v/DataForge.Core)](https://www.nuget.org/packages/DataForge.Core)
[![License](https://img.shields.io/github/license/kongliuli/DataForge.Core)](LICENSE)

DataForge.Core 是一个面向 .NET 开发者的轻量级数据处理核心库，被称为「.NET 生态的数据处理瑞士军刀」。它提供了统一的数据源接入、数据转换、数据验证和数据导出能力，通过流畅的链式 API 和强类型设计，让复杂的数据处理逻辑变得清晰易懂。

## ✨ 核心特性

- **统一数据源抽象**: 支持 CSV、JSON、Excel、数据库、REST API 等多种数据源的统一接入
- **管道式处理**: 链式 API 设计，支持延迟执行和流式处理
- **内置数据验证**: 支持 FluentValidation 集成和自定义验证规则
- **高性能导出**: 批量写入、内存流处理、大文件分片导出
- **丰富的转换器**: 内置映射、过滤、聚合、分组、排序等常用转换
- **高度可扩展**: 通过接口实现自定义扩展
- **零外部依赖**: 核心库不依赖任何第三方库
- **性能优化**: 内置性能计数器、进度报告、并行处理支持

## 🚀 快速开始

### 安装

```bash
Install-Package DataForge.Core
```

或使用 .NET CLI：

```bash
dotnet add package DataForge.Core
```

### 基本用法

```csharp
using DataForge.Core;

// 从 CSV 读取数据，过滤、转换后导出为 JSON
var results = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.OrderDate >= DateTime.Today.AddMonths(-1))
    .Select(o => new { o.OrderId, o.CustomerName, o.Amount })
    .OrderByDescending(o => o.Amount)
    .Take(100)
    .ToJson("top-orders.json");

Console.WriteLine($"导出了 {results.RecordsWritten} 条记录");
```

### 数据验证

```csharp
public class OrderValidator : DataValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.OrderId).NotEmpty();
        RuleFor(o => o.Amount).GreaterThan(0);
        RuleFor(o => o.Email).EmailAddress();
    }
}

await DataForgePipeline
    .FromExcel<Order>("sales.xlsx")
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .ToCsv("validated-orders.csv");
```

### 性能监控

```csharp
var counter = new PerformanceCounter();

await DataForgePipeline
    .FromCsv<Order>("large-file.csv")
    .WithCounter(counter)
    .Where(o => o.Status == "Active")
    .ToJson("filtered-orders.json");

Console.WriteLine($"处理速度: {counter.ItemsPerSecond:F2} 条/秒");
```

## 📊 支持的数据源

| 数据源 | 包名 | 说明 |
|--------|------|------|
| CSV | DataForge.Core | 内置支持 |
| JSON | DataForge.Core | 内置支持 |
| Excel | DataForge.Core.Excel | 需安装扩展包 |
| SQL Server | DataForge.Core.SqlServer | 需安装扩展包 |
| MySQL | DataForge.Core.MySql | 需安装扩展包 |
| SQLite | DataForge.Core.Sqlite | 需安装扩展包 |
| REST API | DataForge.Core.Http | 需安装扩展包 |
| FluentValidation | DataForge.Core.FluentValidation | 需安装扩展包 |

## 📁 项目结构

```
DataForge.Core/
├── src/
│   ├── DataForge.Core/              # 核心库（零依赖）
│   │   ├── Core/
│   │   │   ├── Pipeline/            # 管道核心
│   │   │   ├── Sources/             # 数据源接口和实现
│   │   │   ├── Targets/             # 数据目标（导出）
│   │   │   ├── Transforms/          # 转换器
│   │   │   ├── Validation/          # 验证
│   │   │   ├── Infrastructure/      # 基础设施
│   │   │   └── Models/              # 公共模型
│   │   ├── DataForgePipeline.cs     # 静态入口类
│   │   └── DataForge.Core.csproj
│   ├── DataForge.Core.SqlServer/    # SQL Server 支持
│   ├── DataForge.Core.MySql/        # MySQL 支持
│   ├── DataForge.Core.Sqlite/       # SQLite 支持
│   ├── DataForge.Core.Excel/        # Excel 支持（使用 ClosedXML）
│   ├── DataForge.Core.Json/         # JSON 扩展包
│   ├── DataForge.Core.Http/         # REST API 支持
│   └── DataForge.Core.FluentValidation/  # FluentValidation 集成
├── tests/
│   ├── DataForge.Core.Tests/        # 单元测试
│   └── DataForge.Core.IntegrationTests/  # 集成测试
├── docs/                            # 文档
├── README.md
├── CHANGELOG.md
├── CONTRIBUTING.md
└── LICENSE
```

## 🔧 API 示例

### 多源数据合并

```csharp
var customers = await DataForgePipeline
    .Merge(
        DataForgePipeline.FromCsv<Customer>("customers.csv").Source,
        DataForgePipeline.FromJson<Customer>("new-customers.json").Source,
        DataForgePipeline.FromMemory(existingCustomers).Source
    )
    .Select(c => new CustomerDto
    {
        Id = c.Id ?? Guid.NewGuid().ToString(),
        Name = c.Name?.Trim() ?? "Unknown"
    })
    .DistinctBy(c => c.Email)
    .OrderBy(c => c.Name)
    .ToListAsync();
```

### 增量数据同步

```csharp
public async Task<ExportResults> SyncOrders(DateTime lastSyncTime)
{
    return await DataForgePipeline
        .FromSqlServer<Order>(connectionString)
        .Where(o => o.UpdatedAt > lastSyncTime)
        .ToSqlServer(targetConnection, "Fact_Orders", new SqlServerExportOptions
        {
            BatchSize = 2000,
            InsertMode = InsertMode.Upsert,
            UpsertKeyColumns = new[] { "OrderId" }
        });
}
```

### REST API 数据同步

```csharp
// 从 REST API 读取数据并处理
var results = await DataForgePipeline
    .FromRestApi<Product>("https://api.example.com", "/products",
        new RestApiSourceOptions { PageSize = 100, PageParam = "page" })
    .Where(p => p.IsActive)
    .Select(p => new { p.Id, p.Name, p.Price })
    .ToExcel("products.xlsx");

// 导出数据到 REST API
await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Status == "Pending")
    .ToRestApi("https://api.example.com", "/orders",
        new RestApiTargetOptions { BatchSize = 50 });
```

### 数据转换与聚合

```csharp
var summary = await DataForgePipeline
    .FromJson<SalesData>("sales.json")
    .Where(s => s.Date >= DateTime.Today.AddMonths(-1))
    .GroupBy(s => s.Category)
    .Select(g => new
    {
        Category = g.Key,
        TotalSales = g.Sum(s => s.Amount),
        AverageAmount = g.Average(s => s.Amount),
        Count = g.Count()
    })
    .OrderByDescending(x => x.TotalSales)
    .ToListAsync();
```

### 性能优化

```csharp
// 使用进度报告
await pipeline
    .WithProgress(report => Console.WriteLine($"进度: {report.ProgressPercentage:F1}%"))
    .ToCsv("output.csv");

// 使用并行处理
await pipeline
    .WithParallelization(maxDegreeOfParallelism: 4)
    .Select(item => ExpensiveOperation(item))
    .ToListAsync();
```

## 📖 文档

- [调研汇总与迭代设计（v0.2 规划）](docs/roadmap-and-iteration.md)
- [架构设计](docs/architecture.md)
- [快速上手](docs/getting-started.md)
- [管道编程指南](docs/pipeline-guide.md)
- [数据源配置](docs/data-sources.md)
- [转换器使用](docs/transforms.md)
- [数据验证](docs/validation.md)
- [导出指南](docs/export.md)
- [API 参考](docs/api-reference.md)
- [常见问题](docs/faq.md)

## 🤝 贡献

欢迎贡献代码！请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 了解如何参与。

## 📄 许可证

DataForge.Core 使用 [Apache License 2.0](LICENSE) 许可证。

## 📮 联系方式

如有问题或建议，欢迎提交 Issue 或 Pull Request。
