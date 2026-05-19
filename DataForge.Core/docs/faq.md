# 常见问题

本文档解答关于 DataForge.Core 的常见问题。

## 目录

1. [基础问题](#基础问题)
2. [性能问题](#性能问题)
3. [集成问题](#集成问题)
4. [扩展问题](#扩展问题)

---

## 基础问题

### DataForge.Core 是什么？

DataForge.Core 是一个面向 .NET 开发者的数据处理核心库，提供统一的数据源接入、数据转换、数据验证和数据导出能力。定位为「.NET 生态的数据处理瑞士军刀」。

### 它与 Entity Framework Core 有什么区别？

| 对比项 | DataForge.Core | Entity Framework Core |
|-------|---------------|---------------------|
| **定位** | 数据处理/ETL | ORM/持久化 |
| **主要用途** | 数据导入导出、转换、清洗 | CRUD 操作、领域建模 |
| **学习曲线** | ⭐⭐ 低 | ⭐⭐⭐⭐ 高 |
| **类型安全** | ✅ 完整 | ✅ 完整 |
| **数据验证** | ✅ 内置 | ⚠️ DataAnnotations |
| **Excel/CSV** | ✅ 原生支持 | ❌ 需要第三方 |
| **性能** | ⭐⭐⭐⭐ 高 | ⭐⭐⭐ 中 |
| **SQL 生成** | ❌ 直接 SQL | ✅ LINQ |

**结论**：EF Core 适合应用开发中的数据持久化，DataForge.Core 适合数据处理、ETL、报表生成等场景。两者可以配合使用。

### 它与 Dapper 有什么区别？

| 对比项 | DataForge.Core | Dapper |
|-------|---------------|--------|
| **定位** | 数据处理管道 | 高性能查询 |
| **主要用途** | ETL、转换、导出 | 快速查询 |
| **API 风格** | 链式管道 | SQL 字符串 |
| **类型安全** | ✅ 编译时检查 | ⚠️ 运行时检查 |
| **Excel/CSV** | ✅ 原生支持 | ❌ 需要手动实现 |
| **数据验证** | ✅ 内置 | ❌ |
| **批量操作** | ✅ 批量写入 | ⚠️ 需要手动批量 |

**结论**：Dapper 适合高性能数据查询，DataForge.Core 适合端到端的数据处理流程。可以结合使用：DataForge.Core 从数据库读取数据，Dapper 执行复杂查询。

### 它与 Python Pandas 相比怎么样？

| 对比项 | DataForge.Core | Pandas |
|-------|---------------|--------|
| **平台** | .NET | Python |
| **学习曲线** | ⭐⭐ 低 | ⭐⭐⭐⭐ 中高 |
| **类型安全** | ✅ 完整 | ❌ 动态类型 |
| **性能** | ⭐⭐⭐⭐ 高 | ⭐⭐⭐ 中 |
| **内存占用** | ⭐ 低 | ⭐⭐⭐ 高 |
| **生态丰富度** | ⭐⭐⭐ 中 | ⭐⭐⭐⭐⭐ 极丰富 |
| **IDE 支持** | ✅ 智能提示 | ⚠️ 有限 |

**结论**：Pandas 在数据分析领域有更丰富的生态，DataForge.Core 在 .NET 生态下提供更好的开发体验和类型安全。

### 支持哪些数据源？

**内置支持：**
- SQL Server
- MySQL
- SQLite
- CSV 文件
- Excel 文件（.xlsx, .xls）
- JSON 文件
- 内存集合（IEnumerable）

**扩展支持（通过自定义实现）：**
- PostgreSQL
- MongoDB
- Redis
- REST API
- gRPC
- Kafka（消息流）

### 支持哪些导出格式？

- CSV
- Excel（.xlsx）
- JSON
- SQL Server
- MySQL
- SQLite
- 控制台
- Stream（可扩展到任意目标）

---

## 性能问题

### 大数据量怎么处理？

DataForge.Core 采用**流式处理**设计，内存占用极低：

```csharp
// ✅ 推荐：流式处理，内存占用恒定
await DataForgePipeline
    .FromCsv<Order>("large-file.csv")  // 不加载全部到内存
    .Where(o => o.Amount > 1000)        // 边读边过滤
    .ToCsv("filtered.csv");              // 边读边写入

// ❌ 避免：一次性加载全部
var allOrders = await DataForgePipeline
    .FromCsv<Order>("large-file.csv")
    .ToListAsync();  // 全部加载到内存
```

**处理建议：**
1. 使用流式 API（`ToCsv`、`ToJson`、`ToExcel`）而非 `ToListAsync`
2. 尽早过滤（Where 在数据源层执行）
3. 选择需要的字段（Select 减少数据传输）
4. 批量写入（`BatchSize` 配置）

### 性能如何？

测试环境：Intel i7-12700K, 32GB RAM, SSD

| 操作 | DataForge.Core | 说明 |
|------|---------------|------|
| CSV → CSV (100万行) | ~1.5s | 包含类型映射 |
| SQL → CSV (100万行) | ~2.5s | 包含数据库查询 |
| Excel → DB (10万行) | ~8s | 批量写入优化 |
| JSON → 内存集合 | ~400ms | 流式解析 |

### 如何优化性能？

1. **尽早过滤**
   ```csharp
   // ✅ 高效
   .FromCsv<Order>("orders.csv")
   .Where(o => o.Amount > 1000)  // 在读取时过滤
   .ToCsv("output.csv");
   ```

2. **减少字段**
   ```csharp
   // ✅ 只选择需要的字段
   .Select(o => new { o.OrderId, o.Amount })
   ```

3. **批量配置**
   ```csharp
   .ToCsv("output.csv", new CsvExportOptions { BatchSize = 50000 });
   .ToSqlServer(conn, "Orders", new SqlServerExportOptions { BatchSize = 5000 });
   ```

4. **避免不必要的操作**
   ```csharp
   // ❌ 不需要排序时不要排序
   .OrderBy(o => o.OrderDate)
   .Take(100)
   
   // ✅ 直接取
   .Take(100)
   ```

---

## 集成问题

### 如何集成到现有项目？

```bash
# 1. 安装 NuGet 包
dotnet add package DataForge.Core

# 2. 使用
using DataForge;

var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)
    .ToJson("filtered.json");
```

### 如何与依赖注入集成？

```csharp
// 注册服务
services.AddSingleton<IDataProcessor, DataProcessor>();

// 使用服务
public class DataProcessor
{
    public async Task ProcessOrders(string path)
    {
        await DataForgePipeline
            .FromCsv<Order>(path)
            .ToDatabaseAsync(connectionString);
    }
}
```

### 如何在 ASP.NET Core 中使用？

```csharp
// Controller
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> ExportOrders([FromQuery] DateTime? fromDate)
    {
        var stream = new MemoryStream();
        
        await DataForgePipeline
            .FromSqlServer<Order>(connectionString, "Orders")
            .Where(o => !fromDate.HasValue || o.OrderDate >= fromDate)
            .ToStream(stream, ExportFormat.Csv);
        
        stream.Position = 0;
        return File(stream, "text/csv", "orders.csv");
    }
}
```

### 如何在 Worker Service 中使用？

```csharp
public class OrderSyncWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncOrdersAsync();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
    
    private async Task SyncOrdersAsync()
    {
        await DataForgePipeline
            .FromSqlServer<Order>(sourceConn, "Orders")
            .Where(o => o.UpdatedAt > _lastSyncTime)
            .ToSqlServer(targetConn, "Fact_Orders", new SqlServerExportOptions
            {
                InsertMode = InsertMode.Upsert,
                UpsertKeyColumns = new[] { "OrderId" }
            });
    }
}
```

---

## 扩展问题

### 如何添加自定义数据源？

```csharp
// 1. 实现 IDataSource<T> 接口
public class MongoDbSource<T> : IDataSource<T>
{
    public async IAsyncEnumerable<T> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // 实现读取逻辑
    }
}

// 2. 添加扩展方法
public static class MongoDbSourceExtensions
{
    public static MongoDbSource<T> FromMongoDb<T>(
        IMongoDatabase db, string collectionName)
    {
        return new MongoDbSource<T>(db, collectionName);
    }
}

// 3. 使用
await DataForgePipeline
    .FromMongoDb<Order>(database, "orders")
    .ToCsv("orders.csv");
```

### 如何添加自定义转换器？

```csharp
// 1. 实现 IDataTransform<TIn, TOut>
public class UpperCaseTransform<T> : IDataTransform<T, T>
{
    public T? Transform(T input)
    {
        // 实现转换逻辑
        return input;
    }
}

// 2. 在管道中使用
await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .TransformWith(new UpperCaseTransform<Customer>())
    .ToCsv("uppercased.csv");

// 或者使用 Select（推荐用于简单转换）
await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .Select(c => c with { Name = c.Name.ToUpper() })
    .ToCsv("uppercased.csv");
```

### 如何添加自定义导出目标？

```csharp
// 1. 实现 IDataTarget<T>
public class ParquetTarget<T> : IDataTarget<T>
{
    public async Task WriteAsync(T item, CancellationToken ct)
    {
        // 实现写入逻辑
    }
}

// 2. 添加扩展方法
public static class ParquetExportExtensions
{
    public static Task ToParquet<T>(
        this IDataPipeline<T, T> pipeline,
        string filePath)
    {
        var target = new ParquetTarget<T>(filePath);
        return pipeline.ToTarget(target);
    }
}

// 3. 使用
await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ToParquet("orders.parquet");
```

### 如何与 FluentValidation 集成？

```bash
dotnet add package DataForge.Core.FluentValidation
```

```csharp
// 定义 FluentValidation 验证器
public class OrderValidator : FluentValidation.AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.OrderId).NotEmpty();
        RuleFor(o => o.Amount).GreaterThan(0);
    }
}

// 直接在管道中使用
await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ValidateWith(new OrderValidator())  // 自动适配
    .ToCsv("validated.csv");
```

---

## 错误处理

### 如何捕获处理错误？

```csharp
// 收集所有错误
var result = await pipeline
    .ContinueOnError()
    .CollectErrors()
    .ToCsv("output.csv");

foreach (var error in result.Errors)
{
    Console.WriteLine($"处理失败: {error.Exception.Message}");
}

// 或者验证失败时停止
try
{
    await pipeline
        .FailOnValidationError()
        .ToCsv("output.csv");
}
catch (ValidationException ex)
{
    Console.WriteLine($"验证失败: {ex.Message}");
}
```

### 如何重试失败的操作？

```csharp
// 数据库操作自动重试
await DataForgePipeline
    .FromSqlServer<Order>(connString, new SqlSourceOptions
    {
        EnableRetry = true,
        MaxRetryCount = 3,
        RetryDelayMs = 1000
    })
    .ToCsv("output.csv");
```

---

## 更多问题

如果你的问题没有在这里找到答案：
- 📖 查看 [完整文档](./docs/)
- 🐛 提交 [Issue](https://github.com/dataforge-team/dataforge-core/issues)
- 💬 加入 [Discussion](https://github.com/dataforge-team/dataforge-core/discussions)
