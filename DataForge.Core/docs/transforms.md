# 数据转换（0.1.0）

入口：`using DataForge.Core;`。管道用 `TransformWith`；内置类型在 `DataForge.Core.Core.Transforms`（`CommonTransforms.cs`）。索引：[api-implemented.md](./api-implemented.md)；规划：[api-roadmap.md](./api-roadmap.md)。

日常投影优先 `Select` / `SelectAsync`；需要可复用组件时再用转换器。

---

## TransformWith

```csharp
IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform);
```

```csharp
using DataForge.Core;
using DataForge.Core.Core.Transforms;

await DataForgePipeline.FromMemory(names)
    .TransformWith(new TrimTransform())
    .TransformWith(new ToCaseTransform(toUpper: false))
    .ToListAsync();
```

```csharp
public interface IDataTransform<in TSource, out TResult>
{
    TResult Transform(TSource input);
}

public interface IAsyncDataTransform<in TSource, TResult>
{
    Task<TResult> TransformAsync(TSource input);
}
```

---

## CommonTransforms（源码实有）

| 类型 | 作用 |
|------|------|
| `MapTransform<TIn, TOut>` | `Func` 映射 |
| `AsyncMapTransform<TIn, TOut>` | 异步映射 |
| `CastTransform<TIn, TOut>` | 转型（`TOut : TIn`，失败抛） |
| `DefaultIfEmptyTransform<T>` | `null` → 默认值 |
| `SkipNullsTransform<T>` | 遇 `null` 抛（`T : class`） |
| `DateFormatTransform` | `DateTime` → 字符串（默认 `yyyy-MM-dd`） |
| `TrimTransform` | Trim |
| `ToCaseTransform` | 大小写（`toUpper`） |
| `NumberFormatTransform` | `decimal` → 格式（默认 `N2`） |
| `BoolFormatTransform` | `bool` → 文案（默认「是/否」） |
| `ComputeTransform<TIn, TOut>` | 计算投影 |
| `ConditionalTransform<TIn, TOut>` | 条件映射 |
| `LookupTransform<TKey, TValue>` | 字典查找 |
| `ComposeTransform<TIn, TMid, TOut>` | 串联两个转换 |

```csharp
.TransformWith(new MapTransform<Order, OrderDto>(o => new OrderDto { Id = o.Id, Total = o.Amount * o.Qty }))
.TransformWith(new DateFormatTransform("yyyy-MM-dd HH:mm"))
.TransformWith(new NumberFormatTransform("C2"))
.TransformWith(new BoolFormatTransform("Y", "N"))
.TransformWith(new ConditionalTransform<Order, string>(
    o => o.Amount >= 1000, o => "VIP", o => "STD"))
.TransformWith(new LookupTransform<string, string>(regionNames))
.TransformWith(new ComposeTransform<string, string, string>(
    new TrimTransform(), new ToCaseTransform(toUpper: false)))
```

过滤整行用 `Where`，不要指望 `SkipNullsTransform` 充当管道过滤器。

---

## 自定义

```csharp
public sealed class CentsToYuan : IDataTransform<long, decimal>
{
    public decimal Transform(long input) => input / 100m;
}

pipeline.TransformWith(new CentsToYuan());
```

异步 IO：实现 `IAsyncDataTransform`，或直接 `SelectAsync`。

---

## 分工

| 需求 | 优先 |
|------|------|
| 过滤 / 排序 / 去重 / 分页 | `Where` / `OrderBy*` / `Distinct*` / `Skip`/`Take` |
| 简单投影 | `Select` / `SelectAsync` |
| 可复用格式化 / 查找 / 组合 | `TransformWith` + 上表 |
| 校验 | `ValidateWith` |

不要假设存在未出现在 `CommonTransforms.cs` 的内置转换器名。
