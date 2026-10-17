# 管道指南（0.1.0）

入口：`using DataForge.Core;`。签名以源码与 [api-implemented.md](./api-implemented.md) 为准；规划项见 [api-roadmap.md](./api-roadmap.md)。

管道是惰性的：中间操作只描述流程，终端操作才会真正拉取数据。

```csharp
using DataForge.Core;

var result = await DataForgePipeline.FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 0)
    .OrderBy(o => o.CreatedAt)
    .ToCsvAsync("out.csv");

Console.WriteLine($"{result.RecordsWritten} → {result.OutputPath} ({result.Duration})");
```

---

## 创建

```csharp
DataForgePipeline.FromCsv<T>(path, options?);
DataForgePipeline.FromJson<T>(path, options?);
DataForgePipeline.FromMemory<T>(items);
DataForgePipeline.FromExcel<T>(path, sheetName = "Sheet1"); // 核心占位，见 data-sources.md
DataForgePipeline.FromJsonString<T>(json);
DataForgePipeline.Merge<T>(params IDataSource<T>[] sources);
```

SQL / 真实 Excel / Http 等见扩展包入口（`FromSqlServer` / `FromExcel` 扩展等）。

---

## `IDataPipeline<T>` 中间操作

当前接口**没有** `SkipWhile` / `TakeWhile` / `ContinueOnError` / `ToDataForge`。

| 类别 | 方法 |
|------|------|
| 投影 | `Select` / `SelectAsync` / `SelectMany` |
| 过滤 | `Where` / `WhereAsync` |
| 排序 | `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` |
| 去重 | `Distinct` / `DistinctBy` |
| 切片 | `Skip` / `Take` / `Batch` |
| 组合 | `GroupBy` → `IGroupedDataPipeline`；`Zip` |
| 转换 | `TransformWith(IDataTransform<T, TResult>)` |
| 验证 | `ValidateWith` / `ContinueOnValidationError` / `FailOnValidationError` |
| 错误 | `OnErrorContinue` / `OnErrorStop` / `OnErrorSkip` / `OnError` |

```csharp
await DataForgePipeline.FromMemory(orders)
    .Where(o => o.Status == "Open")
    .Select(o => new { o.Id, o.Amount })
    .DistinctBy(x => x.Id)
    .Skip(10)
    .Take(50)
    .ToListAsync();
```

### GroupBy

```csharp
IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull;
```

`IGroupedDataPipeline<TKey, TElement>` 仅提供：

- `Select(Func<IGrouping<TKey, TElement>, TResult>)`
- `OrderBy` / `OrderByDescending`（按分组键选择器）
- `ToListAsync()` → `List<IGrouping<...>>`
- `ToDictionaryAsync()` → `Dictionary<TKey, List<TElement>>`

```csharp
var byCustomer = await DataForgePipeline.FromCsv<Order>("orders.csv")
    .GroupBy(o => o.CustomerId)
    .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => x.Amount) })
    .ToListAsync();
```

### ValidateWith

```csharp
pipeline
    .ValidateWith(validator)          // IValidator<T>
    .FailOnValidationError()          // 或 ContinueOnValidationError()
```

FluentValidation 适配见 FAQ / `DataForge.Core.FluentValidation`。

### OnError*

```csharp
pipeline
    .OnErrorContinue()   // 记录并继续
    .OnErrorStop()       // 遇错停止
    .OnErrorSkip()       // 跳过当前项
    .OnError((ex, item) => ErrorAction.Skip);
```

自定义处理器返回 `ErrorAction`（定义于基础设施命名空间）。与验证策略是两套 API：没有名为 `ContinueOnError` 的方法。

---

## 终端操作

| 方法 | 返回 |
|------|------|
| `ToListAsync` / `ToArrayAsync` | 集合 |
| `FirstOrDefaultAsync` / `CountAsync` / `AnyAsync` | 标量 |
| `AggregateAsync` | 聚合结果 |
| `AsAsyncEnumerable` | 流式枚举 |
| `ToCsvAsync` / `ToJsonAsync` / `ToExcelAsync` | `ExportResults` |
| `ToConsoleAsync` / `ToStreamAsync` | `ExportResults` |

`ExportResults` 字段：`RecordsWritten`、`OutputPath`、`Duration`、`Errors`（另有 `OutputSize`、`HasErrors`）。**没有** `FilePath`。

```csharp
var export = await pipeline.ToJsonAsync("out.json");
if (export.HasErrors)
    foreach (var e in export.Errors) Console.WriteLine(e);
```

核心接口上的导出方法均带 `Async` 后缀。扩展包可能另有 `ToExcel` / `ToJson` 等别名扩展，语义仍是异步 `Task<ExportResults>`。

---

## 性能扩展（可选）

`PerformanceExtensions`（`DataForge.Core.Core.Pipeline`）在管道上提供：

```csharp
pipeline
    .WithProgress(report => Console.WriteLine(report.ProcessedCount), reportInterval: 1000)
    .WithCounter(counter)
    .WithParallelization(maxDegreeOfParallelism: 4)           // 保序
    .WithParallelizationUnordered(maxDegreeOfParallelism: 4); // 不保序
```

进度报告类型为 `ProgressReport<T>`（含 `ProcessedCount`、`CurrentItem`）。大文件优先流式终端（`AsAsyncEnumerable` / 导出），避免无必要的 `ToListAsync`。

---

## 最小完整示例

```csharp
using DataForge.Core;

var results = await DataForgePipeline.FromCsv<Order>("orders.csv")
    .Where(o => o.Amount >= 100)
    .OrderByDescending(o => o.Amount)
    .Take(100)
    .OnErrorSkip()
    .ToCsvAsync("top.csv");

Console.WriteLine($"Wrote {results.RecordsWritten} to {results.OutputPath}");
```

更多源/转换细节见 [data-sources.md](./data-sources.md)、[transforms.md](./transforms.md)。
