# API 参考索引

DataForge.Core **0.1.0** 的 API 文档已拆成两套，避免「已实现」与「规划」混读：

| 文档 | 用途 |
|---|---|
| [api-implemented.md](./api-implemented.md) | **已实现**公共 API（以 `src/` 为准） |
| [api-roadmap.md](./api-roadmap.md) | **规划中** / 历史稿中的目标签名 |

入门请先看 [getting-started.md](./getting-started.md)。架构概念见 [architecture.md](./architecture.md)（其中接口清单以已实现文档为准，目标架构见路线图）。

## 快速对照

```csharp
using DataForge.Core;
using DataForge.Core.Core.Validation;

var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 0)
    .ValidateWith(new OrderValidator()) // : DataValidator<Order> 或 IValidator<Order>
    .ToJsonAsync("orders.json");
```

扩展包注意：

- Excel / Json / Http：`this DataForgePipeline` 实例扩展；导出常为 `ToExcel` / `ToJson` / `ToRestApi`（无 `Async` 后缀）
- SqlServer / MySql / Sqlite：静态 `From*(connectionString, tableName)` + `To*`
- FluentValidation：必须 `new FluentValidationAdapter<T>(validator)`

> 旧版「按虚构命名空间罗列全部签名」的长文已退役；若外链仍指向本文件，请改链到上表两份文档。
