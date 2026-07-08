# 快速上手

本指南将帮助你在 5 分钟内快速上手 DataForge.Core，完成第一个数据处理任务。

## 环境要求

- **.NET 8.0** 或更高版本
- **Visual Studio 2022 17.8+** / **VS Code** / **JetBrains Rider**
- 推荐 **.NET SDK 8.0**

### 验证 .NET 版本

```bash
dotnet --version
# 输出应为 8.0.100 或更高
```

## 安装

### 通过 NuGet 包管理器

```bash
dotnet add package DataForge.Core
```

### 通过 .NET CLI

```bash
dotnet add package DataForge.Core --version 0.1.0
```

### 验证安装

```csharp
using DataForge.Core;

var pipeline = DataForgePipeline.FromMemory(new[] { 1, 2, 3 });
Console.WriteLine("DataForge.Core ready");
```

## 5 分钟 Hello World

### 场景：读取 CSV 并导出为 JSON

假设有一个 `customers.csv` 文件：

```csv
Id,Name,Email,City
1,张三,zhangsan@example.com,北京
2,李四,lisi@example.com,上海
3,王五,wangwu@example.com,深圳
```

创建控制台项目并编写代码：

```csharp
using DataForge.Core;

var result = await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .Where(c => c.City == "北京")
    .Select(c => new { c.Name, c.Email })
    .ToJsonAsync("beijing-customers.json");

Console.WriteLine($"处理完成：{result.RecordsWritten} 条记录");
```

运行后生成 `beijing-customers.json`：

```json
[
  { "name": "张三", "email": "zhangsan@example.com" }
]
```

## 核心概念速览

DataForge.Core 的核心概念非常简洁：

```
┌─────────────┐    ┌─────────────────┐    ┌─────────────┐
│  DataSource │───▶│    Pipeline     │───▶│ DataTarget  │
│   数据源    │    │      管道       │    │    数据目标 │
└─────────────┘    └─────────────────┘    └─────────────┘
                           │
                           ▼
                   ┌─────────────────┐
                   │   Transform     │
                   │     转换器      │
                   └─────────────────┘
                           │
                           ▼
                   ┌─────────────────┐
                   │    Validator    │
                   │     验证器      │
                   └─────────────────┘
```

### 1. 数据源 (DataSource)

数据源是数据的入口，支持多种格式：

```csharp
// 文件
.FromCsv<T>("file.csv")
.FromJson<T>("file.json")
.FromJsonString<T>(jsonContent)
.FromMemory(collection)

// Excel（需 DataForge.Core.Excel 扩展包）
// using DataForge.Core.Excel;
// ExcelPipelineExtensions.FromExcel<T>("file.xlsx")
```

### 2. 管道 (Pipeline)

管道是数据处理的核心，提供链式操作：

```csharp
DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Status == "Completed")           // 过滤
    .Select(o => new { o.OrderId, o.Amount })      // 映射
    .OrderBy(o => o.OrderDate)                     // 排序
    .Distinct()                                     // 去重
    .Take(100)                                      // 限制
```

### 3. 目标 (Target)

目标是数据的出口：

```csharp
.ToCsv("output.csv")           // CSV 文件
.ToExcel("output.xlsx")        // Excel 文件
.ToJson("output.json")         // JSON 文件
.ToSqlServer(conn, table)      // SQL Server
.ToMySql(conn, table)          // MySQL
.ToConsole()                   // 控制台输出
```

### 4. 转换器 (Transform)

转换器在管道中修改或聚合数据：

```csharp
.GroupBy(o => o.Category)                                    // 分组
.Aggregate(g => new { Category = g.Key, Total = g.Sum(x => x.Amount) })  // 聚合
.CustomTransform(row => ProcessRow(row))                       // 自定义
```

### 5. 验证器 (Validator)

验证器确保数据质量：

```csharp
.ValidateWith(new OrderValidator())                    // 单条验证
.ValidateAllWith(validator).ContinueOnError()          // 验证全部，收集错误
```

## 完整示例：订单数据清洗流程

```csharp
using DataForge;

// 定义验证器
public class OrderValidator : DataValidator<SalesOrder>
{
    public OrderValidator()
    {
        RuleFor(o => o.OrderId).NotEmpty().WithMessage("订单号不能为空");
        RuleFor(o => o.Amount).GreaterThan(0).WithMessage("订单金额必须大于0");
        RuleFor(o => o.CustomerId).NotEmpty().WithMessage("客户ID不能为空");
    }
}

// 执行数据清洗
var result = await DataForgePipeline
    .FromExcel<SalesOrderRow>("dirty-orders.xlsx")
    .Select(row => new SalesOrder
    {
        OrderId = row.OrderID?.Trim(),
        CustomerId = row.CustomerID,
        Amount = decimal.TryParse(row.Amount, out var a) ? a : 0,
        OrderDate = DateTime.TryParse(row.Date, out var d) ? d : DateTime.MinValue
    })
    .Where(o => o.OrderId != null)
    .ValidateWith(new OrderValidator())
    .ToCsv("cleaned-orders.csv", includeHeader: true);

Console.WriteLine($"清洗完成: {result.SuccessCount} 成功, {result.FailedCount} 失败");
```

## 常见配置

### CSV 选项

```csharp
.FromCsv<Order>("orders.csv", options: new CsvSourceOptions
{
    Encoding = Encoding.UTF8,
    Separator = ',',
    HasHeader = true,
    SkipLines = 0,
    TrimFields = true
})
```

### Excel 选项

```csharp
.FromExcel<Order>("orders.xlsx", options: new ExcelSourceOptions
{
    SheetName = "订单数据",
    // 或使用 SheetIndex
    // SheetIndex = 0,
    HeaderRow = 1,
    SkipEmptyRows = true
})
```

### SQL 连接池

```csharp
.FromSqlServer(connectionString, options: new SqlSourceOptions
{
    CommandTimeout = 300,
    EnableRetry = true,
    MaxRetryCount = 3,
    Pooling = true,
    MinPoolSize = 5,
    MaxPoolSize = 100
})
```

## 下一步阅读建议

| 如果你想... | 推荐阅读 |
|------------|---------|
| 理解核心设计 | [架构设计文档](./architecture.md) |
| 掌握高级管道用法 | [管道编程指南](./pipeline-guide.md) |
| 接入新的数据源 | [数据源接入指南](./data-sources.md) |
| 学习数据转换 | [数据转换指南](./transforms.md) |
| 处理真实业务场景 | [场景实战手册](./scenarios.md) |

## 获取帮助

- 📖 查看 [常见问题](./faq.md)
- 🐛 发现 Bug？[提交 Issue](https://github.com/dataforge-team/dataforge-core/issues)
- 💬 需要帮助？[发起 Discussion](https://github.com/dataforge-team/dataforge-core/discussions)
