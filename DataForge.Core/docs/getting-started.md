# 快速上手（0.1.0）

5 分钟跑通：装包 → 读 CSV → 过滤 → 写 JSON。签名以源码与 [api-implemented.md](./api-implemented.md) 为准。

## 环境与安装

- **.NET 8**（`dotnet --version` ≥ 8）

```bash
dotnet add package DataForge.Core
```

```csharp
using DataForge.Core;

var n = await DataForgePipeline.FromMemory(new[] { 1, 2, 3 }).CountAsync();
Console.WriteLine(n); // 3
```

包版本看 NuGet / `PackageVersion`。没有 `DataForgeVersion` 类型。

## Hello World

`customers.csv`：

```csv
Id,Name,Email,City
1,张三,zhangsan@example.com,北京
2,李四,lisi@example.com,上海
```

```csharp
using DataForge.Core;

var result = await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .Where(c => c.City == "北京")
    .Select(c => new { c.Name, c.Email })
    .ToJsonAsync("beijing-customers.json");

Console.WriteLine($"写入 {result.RecordsWritten} 条 → {result.OutputPath}");
```

## 再进一步：清洗 + 验证

```csharp
using DataForge.Core;
using DataForge.Core.Core.Validation;

public class OrderValidator : DataValidator<SalesOrder>
{
    public OrderValidator()
    {
        RuleFor(o => o.OrderId).NotEmpty().WithMessage("订单号不能为空");
        RuleFor(o => o.Amount).GreaterThan(0).WithMessage("金额须 > 0");
    }
}

var result = await DataForgePipeline
    .FromCsv<SalesOrderRow>("dirty-orders.csv")
    .Select(row => new SalesOrder
    {
        OrderId = row.OrderID?.Trim() ?? "",
        Amount = decimal.TryParse(row.Amount, out var a) ? a : 0
    })
    .Where(o => o.OrderId.Length > 0)
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .ToCsvAsync("cleaned-orders.csv", new CsvExportOptions { IncludeHeader = true });

Console.WriteLine($"写入 {result.RecordsWritten}；错误条数 {result.Errors.Count}");
```

## 别踩的坑（极简）

| 话题 | 0.1.0 做法 |
|---|---|
| 命名空间 | `using DataForge.Core;` |
| Excel | 核心 `FromExcel`/`ToExcelAsync` 是**占位**；真 `.xlsx` → `DataForge.Core.Excel` 的 `ToExcel` |
| SQL | `SqlServerPipelineExtensions.FromSqlServer(conn, table)`，不是 `DataForgePipeline.FromSqlServer` |
| 导出结果 | `ExportResults`：`RecordsWritten` / `OutputPath` / `Duration` / `Errors`（没有 `FilePath`） |
| 选项字段 | CSV 用 `Delimiter` + `HasHeaderRow`；详见 [data-sources.md](./data-sources.md) / [export.md](./export.md) |

流程一句话：`Source → IDataPipeline<T> → Target`。细节别堆在本页。

## 下一步

| 想… | 看 |
|---|---|
| 管道 / GroupBy / 错误策略 | [pipeline-guide.md](./pipeline-guide.md) |
| 数据源与扩展包入口 | [data-sources.md](./data-sources.md) |
| 已实现 API 清单 | [api-implemented.md](./api-implemented.md) |
| 规划 / 旧名对照 | [api-roadmap.md](./api-roadmap.md) |
| 场景小品 | [scenarios.md](./scenarios.md) |
| FAQ | [faq.md](./faq.md) |
