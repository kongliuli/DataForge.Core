# 架构（0.1.0）

只描述当前能跑通的结构。接口清单 → [api-implemented.md](./api-implemented.md)；未落地 → [api-roadmap.md](./api-roadmap.md)。

## 分层

```
DataForgePipeline / *PipelineExtensions   入口
        ↓
IDataSource<T>                            源
        ↓
IDataPipeline<T>                          查询 / 验证 / 错误策略
        ↓
IDataTarget<T> → ExportResults            导出
```

扩展包挂在入口与 Target 两侧，不改核心管道契约。

## 核心契约

| 类型 | 作用 |
|---|---|
| `DataForgePipeline` | 静态工厂：`FromCsv` / `FromJson` / `FromMemory` / 核心 `FromExcel`（占位） / `Merge` |
| `IDataPipeline<T>` | 单类型管道；`Select`/`Where`/`GroupBy`/`ValidateWith`/`OnError*`/`To*Async` |
| `IGroupedDataPipeline<TKey,TElement>` | `GroupBy` 之后；`Select(IGrouping→TResult)` |
| `IDataSource<T>` / `IDataTarget<T>` | 读 / 写 |
| `IValidator<T>` | 验证；FluentValidation 用 `FluentValidationAdapter<T>` |
| `ExportResults` | `RecordsWritten` / `OutputPath` / `OutputSize` / `Duration` / `Errors` |

## 包边界

| 包 | 职责 |
|---|---|
| `DataForge.Core` | 零第三方依赖；CSV/JSON/Memory；Excel **文本占位** |
| `DataForge.Core.Excel` | ClosedXML；`FromExcel` / `ToExcel`（无 Async） |
| `DataForge.Core.Json` | JSON 扩展；`ToJson`（无 Async） |
| `DataForge.Core.Http` | `FromRestApi` / `ToRestApi` |
| `DataForge.Core.FluentValidation` | `FluentValidationAdapter<T>` |
| `*.SqlServer` / `*.MySql` / `*.Sqlite` | 静态 `From*(conn, table)` + `To*` |

## 执行模型

- 基于 `IAsyncEnumerable<T>`；终端操作才拉数。
- 验证：`ContinueOnValidationError` / `FailOnValidationError`。
- 错误：`OnErrorContinue` / `OnErrorStop` / `OnErrorSkip` / `OnError(...)`。

## 0.1.0 不做

双泛型管道、DI、完整 SQL 构建器、自动挂 `ValidationResult`、以及 `CollectValidationResults` / `ContinueOnError` / `ToDataForge` 等历史名。详见 [api-roadmap.md](./api-roadmap.md)。
