# 场景手册（0.1.0）

只收 **4 个**能对着当前 API 写的例子。更多签名 → [api-implemented.md](./api-implemented.md)。

---

## 1. CSV → 过滤 → JSON

```csharp
using DataForge.Core;

var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 0)
    .Select(o => new { o.Id, o.Amount })
    .ToJsonAsync("positive.json");

Console.WriteLine($"{result.RecordsWritten} rows → {result.OutputPath}");
```

---

## 2. 内存数据分组汇总

`GroupBy` 得到 `IGroupedDataPipeline`；用 `Select` 投影 `IGrouping`。

```csharp
using DataForge.Core;

var rows = await DataForgePipeline
    .FromMemory(orders)
    .Where(o => o.Status == "Completed")
    .GroupBy(o => o.Region)
    .Select(g => new
    {
        Region = g.Key,
        Count = g.Count(),
        Total = g.Sum(x => x.Amount)
    })
    .OrderByDescending(x => x.Total)
    .ToListAsync();
```

导出同理：把最后的 `ToListAsync` 换成 `ToCsvAsync("report.csv")`。

---

## 3. 验证后导出（跳过非法行）

```csharp
using DataForge.Core;
using DataForge.Core.Core.Validation;

public class OrderValidator : DataValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Amount).GreaterThan(0);
        RuleFor(o => o.CustomerId).NotEmpty();
    }
}

var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .ToCsvAsync("valid-only.csv");

if (result.HasErrors)
    foreach (var e in result.Errors)
        Console.WriteLine(e);
```

FluentValidation：

```csharp
using DataForge.Core.FluentValidation;

var adapter = new FluentValidationAdapter<Order>(new MyFluentOrderValidator());
await pipeline.ValidateWith(adapter).FailOnValidationError().ToListAsync();
```

---

## 4. SQL Server 表 → CSV

入口在扩展类上，**不是** `DataForgePipeline.FromSqlServer`。

```csharp
using DataForge.Core;
using DataForge.Core.SqlServer;

var result = await SqlServerPipelineExtensions
    .FromSqlServer<SalesOrder>(conn, "SalesOrders")
    .Where(o => o.OrderDate.Year == year)
    .ToCsvAsync($"sales-{year}.csv");

Console.WriteLine($"{result.RecordsWritten} / {result.Duration}");
```

写入：`await pipeline.ToSqlServer(conn, "SalesOrders");`

---

## 不做的场景（0.1.0）

| 想法 | 现状 |
|---|---|
| 实时流 / 变更追踪 | 未提供 |
| `CollectValidationResults` 管道内汇总 | 未实现；管道外自行 `Validate` |
| `DataForgePipeline.FromSqlServer` | 用 `*PipelineExtensions.From*` |
| 核心真 `.xlsx` | 装 `DataForge.Core.Excel`，`ToExcel` |
