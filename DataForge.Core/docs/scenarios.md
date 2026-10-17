# 场景速查（0.1.0）

短菜谱：只覆盖当前 `src/` 能跑的写法。入口 `using DataForge.Core;`。SQL 用扩展包静态方法，不是 `DataForgePipeline.FromSqlServer`。

| # | 场景 | 入口 | 要点 |
|---|------|------|------|
| 1 | CSV 月报 | `FromCsv` | `GroupBy` → `Select(IGrouping)` |
| 2 | 清洗 + 验证 | `FromCsv` | `DataValidator` / `FluentValidationAdapter` |
| 3 | 多文件合并去重 | `FromCsv` / `FromJson` / `FromMemory` | `DistinctBy` |
| 4 | SQL 增量同步 | `SqlServerPipelineExtensions` | `ToSqlServer` + Upsert |

导出结果看 `ExportResults`：`RecordsWritten`、`OutputPath`、`Duration`、`Errors`（没有 `FilePath` / `SuccessCount`）。

---

## 1. CSV 订单月报

按区域汇总已完成订单，写出 CSV。

```csharp
using DataForge.Core;
using DataForge.Core.Core.Targets;

public async Task GenerateMonthlyReport(string ordersCsv, int year, int month, string outputPath)
{
    var report = await DataForgePipeline
        .FromCsv<SalesOrder>(ordersCsv)
        .Where(o => o.OrderDate.Year == year && o.OrderDate.Month == month)
        .Where(o => o.Status == "Completed")
        .GroupBy(o => o.Region)
        .Select(g => new MonthlySalesReport
        {
            Year = year,
            Month = month,
            Region = g.Key,
            OrderCount = g.Count(),
            TotalSales = g.Sum(o => o.Amount),
            GrossProfit = g.Sum(o => o.Amount - o.Cost),
            AvgOrderValue = Math.Round(g.Average(o => o.Amount), 2)
        })
        .OrderByDescending(r => r.TotalSales)
        .ToCsvAsync(outputPath, new CsvExportOptions
        {
            IncludeHeader = true,
            Encoding = System.Text.Encoding.UTF8
        });

    Console.WriteLine($"写出 {report.RecordsWritten} 行 → {report.OutputPath} ({report.Duration.TotalSeconds:F2}s)");
    if (report.HasErrors)
        foreach (var e in report.Errors) Console.WriteLine(e);
}

public record SalesOrder
{
    public DateTime OrderDate { get; init; }
    public string Status { get; init; } = "";
    public string Region { get; init; } = "";
    public decimal Amount { get; init; }
    public decimal Cost { get; init; }
}

public record MonthlySalesReport
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Region { get; init; } = "";
    public int OrderCount { get; init; }
    public decimal TotalSales { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal AvgOrderValue { get; init; }
}
```

要点：`GroupBy` 返回 `IGroupedDataPipeline`；聚合并在随后的 `Select(Func<IGrouping<TKey,T>, TResult>)` 里用 LINQ（`Count` / `Sum` / `Average`）。

---

## 2. CSV 清洗 + 验证 → JSON

脏 CSV → 规范化 → 内置校验（或 FluentValidation 适配器）→ JSON。

```csharp
using DataForge.Core;
using DataForge.Core.Core.Validation;
using DataForge.Core.FluentValidation; // 仅 FluentValidation 路径需要

public async Task CleanOrders(string inputCsv, string outputJson)
{
    var result = await DataForgePipeline
        .FromCsv<DirtyOrderRow>(inputCsv)
        .Select(row => new CleanedOrder
        {
            OrderId = row.OrderID?.Trim() ?? "",
            CustomerName = row.Customer?.Trim() ?? "",
            Amount = ParseAmount(row.Amount),
            OrderDate = ParseDate(row.OrderDate)
        })
        .Where(o => o.OrderId.Length > 0 && o.Amount > 0)
        .ValidateWith(new CleanedOrderValidator())
        .ContinueOnValidationError()
        .ToJsonAsync(outputJson);

    Console.WriteLine($"{result.RecordsWritten} → {result.OutputPath}");
}

public class CleanedOrderValidator : DataValidator<CleanedOrder>
{
    public CleanedOrderValidator()
    {
        RuleFor(o => o.OrderId).Required().Length(8, 20);
        RuleFor(o => o.CustomerName).Required().MinLength(2);
        RuleFor(o => o.Amount).InRange(0m, 10_000_000m);
    }
}

// FluentValidation：必须包一层适配器
// var fv = new FluentValidationAdapter<CleanedOrder>(new MyFluentValidator());
// pipeline.ValidateWith(fv).FailOnValidationError();

static decimal ParseAmount(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return 0;
    var cleaned = new string(s.Where(c => char.IsDigit(c) || c is '.' or '-').ToArray());
    return decimal.TryParse(cleaned, out var v) ? v : 0;
}

static DateTime ParseDate(string? s) =>
    DateTime.TryParse(s, out var d) ? d : DateTime.MinValue;

public record DirtyOrderRow
{
    public string? OrderID { get; init; }
    public string? Customer { get; init; }
    public string? Amount { get; init; }
    public string? OrderDate { get; init; }
}

public record CleanedOrder
{
    public string OrderId { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime OrderDate { get; init; }
}
```

没有 `CollectValidationResults`。要明细时在管道外 `validator.Validate(item)`。

---

## 3. CSV + JSON 合并去重

两路读入后用 `FromMemory` 合并（`Merge` 要 `IDataSource<T>`，实现类多为 internal，菜谱里不走那条）。

```csharp
using DataForge.Core;

public async Task<List<MergedCustomer>> MergeCustomers(string crmCsv, string financeJson)
{
    var crm = await DataForgePipeline
        .FromCsv<CrmRow>(crmCsv)
        .Select(c => new CustomerRow
        {
            Source = "CRM",
            Name = c.Name?.Trim() ?? "",
            Email = c.Email?.Trim().ToLowerInvariant()
        })
        .ToListAsync();

    var finance = await DataForgePipeline
        .FromJson<FinRow>(financeJson)
        .Select(c => new CustomerRow
        {
            Source = "Finance",
            Name = c.AccountName?.Trim() ?? "",
            Email = c.ContactEmail?.Trim().ToLowerInvariant()
        })
        .ToListAsync();

    return await DataForgePipeline
        .FromMemory(crm.Concat(finance))
        .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.Contains('@'))
        .DistinctBy(c => c.Email!)
        .Select(c => new MergedCustomer { Name = c.Name, Email = c.Email!, Source = c.Source })
        .ToListAsync();
}

public record CrmRow { public string? Name { get; init; } public string? Email { get; init; } }
public record FinRow { public string? AccountName { get; init; } public string? ContactEmail { get; init; } }
public record CustomerRow { public string Source { get; init; } = ""; public string Name { get; init; } = ""; public string? Email { get; init; } }
public record MergedCustomer { public string Name { get; init; } = ""; public string Email { get; init; } = ""; public string Source { get; init; } = ""; }
```

内存小样：`DataForgePipeline.FromMemory(list)`；JSON 字符串：`FromJsonString<T>(json)`（反序列化为 `List<T>`）。

---

## 4. SQL Server 增量 Upsert

包：`DataForge.Core.SqlServer`。入口是扩展类静态方法。

```csharp
using DataForge.Core.SqlServer;
using DataForge.Core.Core.Infrastructure;

public async Task SyncOrders(string sourceConn, string targetConn, DateTime since)
{
    var result = await SqlServerPipelineExtensions
        .FromSqlServer<OrderRow>(sourceConn, "Orders")
        .Where(o => o.UpdatedAt > since)
        .Select(o => new OrderRow
        {
            OrderId = o.OrderId,
            CustomerName = o.CustomerName,
            Amount = o.Amount,
            UpdatedAt = o.UpdatedAt
        })
        .ToSqlServer(targetConn, "Fact_Orders", new SqlServerExportOptions
        {
            BatchSize = 2000,
            InsertMode = InsertMode.Upsert,
            UpsertKeyColumns = ["OrderId"],
            UseTransaction = true
        });

    Console.WriteLine($"写入 {result.RecordsWritten}，耗时 {result.Duration}");
    if (result.HasErrors)
        foreach (var e in result.Errors) Console.WriteLine(e);
}

public class OrderRow
{
    public string OrderId { get; set; } = "";
    public string? CustomerName { get; set; }
    public decimal Amount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

`FromSqlServer` / `ToSqlServer` 挂在 `SqlServerPipelineExtensions` 上；`T` 读侧需 `new()`。MySQL / SQLite 同形（各自扩展包）。

---

## 不做（0.1.0）

- `DataForgePipeline.FromSqlServer`、`ToDataForge`、`DataForgeVersion`、`CollectValidationResults`
- 把 `AbstractValidator<T>` 直接丢进 `ValidateWith`（必须用 `FluentValidationAdapter`）
- 实时流 / Window / Kafka；核心 `FromExcel` 仅为占位（真 xlsx 用 `DataForge.Core.Excel`）
- HTTP：`FromRestApi` 是 `this DataForgePipeline` 实例扩展，菜谱未收录

更多 API 见 [api-implemented.md](./api-implemented.md)。
