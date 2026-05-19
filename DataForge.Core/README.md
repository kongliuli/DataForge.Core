# DataForge.Core

![Build Status](https://img.shields.io/github/actions/workflow/status/dataforge-team/dataforge-core/build.yml?branch=main)
![NuGet](https://img.shields.io/nuget/v/DataForge.Core)
![License](https://img.shields.io/github/license/dataforge-team/dataforge-core)
![Stars](https://img.shields.io/github/stars/dataforge-team/dataforge-core)
![Downloads](https://img.shields.io/nuget/dt/DataForge.Core)

> **.NET 生态的数据处理瑞士军刀**
> 
> 简单、强大、类型安全的数据处理管道，让数据 ETL 从繁琐的手工编码中解放出来。

DataForge.Core 是一个面向 .NET 开发者的轻量级数据处理核心库，提供统一的数据源接入、数据转换、数据验证和数据导出能力。通过流畅的链式 API 和强类型设计，让复杂的数据处理逻辑变得清晰易懂。

## ✨ 核心特性

- 🔌 **统一数据源抽象** - SQL Server、MySQL、SQLite、CSV、Excel、JSON、REST API 等多数据源一站式接入
- 🔗 **管道式处理** - 链式 API 设计，支持延迟执行，性能优异
- ⚡ **类型安全** - 完整的泛型支持和编译时类型检查
- ✅ **内置数据验证** - 支持 FluentValidation 集成和自定义验证规则
- 🚀 **高性能导出** - 批量写入、内存流处理、大文件分片导出
- 📊 **丰富转换器** - 内置映射、过滤、聚合、分组、排序等常用转换
- 🔧 **高度可扩展** - 自定义数据源、转换器、导出格式轻松实现
- 📦 **零外部依赖** - 核心库无任何第三方依赖，轻松集成

## 🚀 快速上手

### 安装

```bash
dotnet add package DataForge.Core
```

### 30 秒入门：SQL 数据导出为 CSV

```csharp
using DataForge;

await DataForgePipeline
    .FromSqlServer("connection-string")
    .Select<SalesOrder>(o => new 
    {
        o.OrderId,
        o.CustomerName,
        o.OrderDate,
        o.TotalAmount
    })
    .Where(o => o.OrderDate >= DateTime.Today.AddMonths(-1))
    .OrderBy(o => o.OrderDate)
    .ToCsv("monthly-sales.csv");
```

### 完整场景示例

#### 场景 1：订单数据月报生成（SQL → CSV）

```csharp
using DataForge;

// 从 SQL Server 读取订单数据，聚合后导出为 CSV
var report = await DataForgePipeline
    .FromSqlServer(connectionString)
    .Source<SalesOrder>()
    .Where(o => o.OrderDate.Year == 2024 && o.OrderDate.Month == 1)
    .GroupBy(o => o.Region)
    .Select(g => new 
    {
        Region = g.Key,
        OrderCount = g.Count(),
        TotalSales = g.Sum(o => o.TotalAmount),
        AvgOrderValue = g.Average(o => o.TotalAmount)
    })
    .OrderByDescending(r => r.TotalSales)
    .ToCsv("regional-sales-report.csv");

Console.WriteLine($"已生成报告：{report.RecordsWritten} 条记录");
```

#### 场景 2：Excel 数据清洗入库（Excel → DB）

```csharp
using DataForge;

var result = await DataForgePipeline
    .FromExcel("sales-data.xlsx", sheetName: "Orders")
    .Source<ExcelOrderRow>()                          // 原始 Excel 行
    .Select(row => new SalesOrder                     // 映射到实体
    {
        OrderId = row.OrderID?.Trim(),
        CustomerName = row.Customer?.Trim(),
        Amount = decimal.Parse(row.Amount ?? "0"),
        OrderDate = DateTime.Parse(row.Date!)
    })
    .Where(o => !string.IsNullOrEmpty(o.OrderId))     // 过滤脏数据
    .ValidateWith(new SalesOrderValidator())          // 业务验证
    .ToSqlServer(connectionString, "SalesOrders");    // 批量写入

Console.WriteLine($"导入成功：{result.SuccessCount} 条，失败：{result.FailedCount} 条");
```

#### 场景 3：多源客户数据合并（多源 JOIN → 去重 → JSON）

```csharp
using DataForge;

var customers = await DataForgePipeline
    .Merge(
        DataForgePipeline.FromSqlServer(conn).Source<Customer>("Customers"),
        DataForgePipeline.FromExcel("new-customers.xlsx").Source<CustomerRow>(),
        DataForgePipeline.FromJsonArray<Customer>("api-response.json")
    )
    .Select(c => new CustomerDto
    {
        Id = c.Id ?? Guid.NewGuid().ToString(),
        Name = c.Name?.Trim() ?? "Unknown",
        Email = c.Email?.ToLower(),
        Source = c.Source
    })
    .DistinctBy(c => c.Email)  // 按邮箱去重
    .OrderBy(c => c.Name)
    .ToJson("consolidated-customers.json");

Console.WriteLine($"合并完成：{customers.RecordsWritten} 位客户");
```

## 📊 性能基准

> 测试环境：Intel i7-12700K, 32GB RAM, SSD
> 
> 测试数据：100 万条包含 10 个字段的记录

| 操作 | DataForge.Core | Raw ADO.NET | 说明 |
|------|---------------|-------------|------|
| SQL → CSV (100万行) | ~2.5s | ~3.1s | 包含类型映射 |
| Excel → DB (10万行) | ~8s | ~15s | 批量写入优化 |
| JSON → 内存集合 | ~400ms | ~600ms | 流式解析 |
| 管道转换 (100万行) | ~180ms | N/A | 纯内存操作 |

> 实际性能取决于数据复杂度、硬件配置和网络环境。

## 🗺️ 路线图

``` roadmap
v0.1.0 ──────────────── v0.2.0 ──────────────── v0.3.0 ──────────────── v1.0.0
│
├─ 核心管道抽象
├─ SQL 数据源
├─ CSV/Excel/JSON
├─ 基础转换器
├─ 基础验证器
│
├─ 批量写入优化
├─ 异步流式处理
├─ 更多数据源
│                        ├─ REST API 数据源
│                        ├─ 管道缓存
│                        ├─ 并行执行
│
│                                          ├─ 多表 JOIN
│                                          ├─ 增量同步
│                                          ├─ 数据质量报告
│
│                                                             ├─ 可视化管道设计器
│                                                             ├─ 性能监控
│                                                             ├─ 生产就绪
```

## 📦 与竞品对比

| 特性 | DataForge.Core | Dapper | EF Core | Pandas.NET |
|------|---------------|--------|---------|------------|
| 学习曲线 | ⭐⭐ 低 | ⭐⭐⭐ 中 | ⭐⭐⭐⭐ 高 | ⭐⭐ 中 |
| 类型安全 | ✅ 完整 | ✅ 完整 | ✅ 完整 | ❌ 动态 |
| API 设计 | 管道链式 | SQL 字符串 | LINQ | DataFrame |
| 数据验证 | ✅ 内置 | ❌ | ⚠️ DataAnnotations | ❌ |
| Excel 支持 | ✅ 原生 | ❌ | ❌ | ✅ |
| 零依赖 | ✅ | ⚠️ (ADO.NET) | ❌ (EF) | ❌ |
| 内存占用 | ⭐ 低 | ⭐ 低 | ⭐⭐ 中 | ⭐⭐⭐ 高 |
| 适用场景 | ETL/数据处理 | 高性能查询 | CRUD/领域建模 | 数据分析 |

## 📖 文档

- [快速上手](./docs/getting-started.md) - 5 分钟入门
- [架构设计](./docs/architecture.md) - 核心抽象和接口定义
- [管道编程指南](./docs/pipeline-guide.md) - 链式 API 完整参考
- [数据源接入](./docs/data-sources.md) - 多数据源配置和使用
- [数据转换](./docs/transforms.md) - 内置和自定义转换器
- [数据验证](./docs/validation.md) - 验证规则和错误处理
- [数据导出](./docs/export.md) - 多格式导出指南
- [场景实战](./docs/scenarios.md) - 8 个真实业务场景
- [常见问题](./docs/faq.md) - FAQ

## 📄 许可证

本项目基于 [MIT License](./LICENSE) 开源。

---

**DataForge.Core** - 让 .NET 数据处理更简单
