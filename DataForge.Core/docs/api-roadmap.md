# API 路线图（规划中 · 非 0.1.0 保证）

下列内容来自历史 `api-reference.md` / `architecture.md` 中的目标设计，**当前源码未实现或以不同形状实现**。请勿当作可编译公共 API。已实现面见 [api-implemented.md](./api-implemented.md)；索引见 [api-reference.md](./api-reference.md)。

---

## 1. 命名 / 形状差异（文档曾写错）

| 规划 / 旧文档写法 | 0.1.0 实际 |
|---|---|
| `IDataPipeline<TIn, TOut>`，成员谓词/选择器用 `TOut` | `IDataPipeline<T>` |
| `IGroupedPipeline<TKey, TOut>` + `IGroupResult` | `IGroupedDataPipeline<TKey, TElement>` + `IGrouping` |
| `ContinueOnError` / `StopOnError` / `SkipOnError` | `OnErrorContinue` / `OnErrorStop` / `OnErrorSkip` |
| `ValidateWith\<TValidator\>(...) where TValidator : IValidator\<TOut\>` | `ValidateWith(IValidator\<T\>)` |
| FluentValidation 自动实现 `IValidator\<T\>` | 需显式 `FluentValidationAdapter\<T\>` |
| `ToDataForge(this IEnumerable\<T\>)` / `ToDataForge(this IAsyncEnumerable\<T\>)` | **未实现** |
| `DataForgeVersion.Current` | **未实现** |
| `Merge` → `MergedPipeline\<T\>` 或 `params IDataPipeline` 混搭 | `Merge(params IDataSource\<T\>[])` |
| `SkipWhile` / `TakeWhile` / 带 index 的 `Where`/`Select` | **未实现** |
| `ValidateAllWith` / `CollectErrors` 等链式辅助 | **未实现** |
| `using DataForge;` / `FromCollection` / `FromEnumerable` | `using DataForge.Core;` / `FromMemory` |
| 核心/扩展导出返回 `DbExportResult` | 返回 `ExportResults` |

---

## 2. 曾规划的管道签名（节选）

以下为历史文档中的**目标**形态，**不是**当前可编译 API。

```csharp
// 规划：双类型管道（未采用）
public interface IDataPipeline<TIn, TOut> : IAsyncEnumerable<TOut>
{
    string Name { get; }
    IExecutionPlan ExecutionPlan { get; }

    IDataPipeline<TIn, TOut> Where(Func<TOut, bool> predicate);
    IDataPipeline<TIn, TResult> Select<TResult>(Func<TOut, TResult> selector);
    IDataPipeline<TIn, TOut> SkipWhile(Func<TOut, bool> predicate);
    IDataPipeline<TIn, TOut> TakeWhile(Func<TOut, bool> predicate);

    IDataPipeline<TIn, TOut> ValidateWith<TValidator>()
        where TValidator : IValidator<TOut>, new();

    IDataPipeline<TIn, TOut> ContinueOnError();
    IDataPipeline<TIn, TOut> StopOnError();

    Task<IReadOnlyList<TOut>> ToListAsync(CancellationToken cancellationToken = default);
    Task<TOut?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
}

// 规划：错误命名的分组接口（未采用）
public interface IGroupedPipeline<TKey, TOut>
{
    IDataPipeline<object, IGroupResult<TKey, TOut>> Select(...);
    // Count / Sum / Average / Min / Max 作为接口一等成员
}

// 规划：IEnumerable 入口（未实现）
public static class EnumerableExtensions
{
    public static IDataPipeline<T> ToDataForge<T>(this IEnumerable<T> source);
    public static IDataPipeline<T> ToDataForge<T>(this IAsyncEnumerable<T> source);
}

// 规划：更丰富的 Merge（未实现）
public static MergedPipeline<T> Merge<T>(params IDataPipeline<T>[] pipelines);
public static IDataPipeline<T> Merge<T>(this IDataPipeline<T> first, IDataPipeline<T> second, MergeOptions? options = null);
```

### 物化 / 查询曾规划但未挂在接口上

- `AnyAsync(Func<T, bool>)` / `AllAsync` / `FirstAsync`（无 OrDefault）
- `IDataPipeline<T> : IAsyncEnumerable<T>`（当前通过 `AsAsyncEnumerable` 暴露）
- 管道级 `Name` / `ExecutionPlan` 只读诊断属性

---

## 3. 源 / 目标工厂目标形状

### SQL（架构稿曾写的完整工厂）

```csharp
// 规划示例 — 与现状不符
IDataPipeline<T> FromSqlServer<T>(string connectionString); // 仅连接串
IDataPipeline<T> FromSqlServer<T>(string connectionString, string sql, object? parameters = null);
IDataPipeline<T> FromSqlServer<T>(DbConnection connection, string tableOrSql, SqlSourceOptions? options = null);

Task<DbExportResult> ToSqlServer<T>(..., SqlBulkOptions bulkOptions);
```

**现状**：`FromSqlServer(connectionString, tableName)`（及 MySql/Sqlite 同形）为**静态**方法；导出为 `ToSqlServer` → `ExportResults` + `SqlServerExportOptions`（BatchSize / InsertMode / UpsertKeyColumns / UseTransaction）。查询文本、参数对象、BulkCopy 专用选项等仍属规划。

### RestApi

```csharp
// 规划：双类型包装
IDataPipeline<RestApiSource<T>, T> FromRestApi<T>(...);
```

**现状**：`FromRestApi` / `FromRestApiWithPagination` / `ToRestApi` 均工作在 `IDataPipeline<T>` 上（`this DataForgePipeline` 实例扩展）。

### Merge / 版本

- 花哨的 `MergedPipeline` 合并策略、跨管道 Zip 以外的 join 语义 → 规划中
- `DataForgeVersion` 公共版本门面 → 规划中

---

## 4. 扩展与平台目标（架构稿）

- **更完整的 SQL 工厂**：任意 SQL、参数、事务边界、SqlBulkCopy / 对应 MySQL LOAD 选项等 —— 现状多为 `(connectionString, tableName)` + Options
- **DI 一等集成**：`services.AddDataForge(...)` 与选项绑定 —— 现状示例多为手写注册
- **分组聚合一等成员**：`Count` / `Sum` / `Average` / `Min` / `Max` 直接挂在 grouped 接口 —— 现状 `Select` + LINQ on `IGrouping`
- **验证批处理链**：`ValidateAllWith`、错误收集后再决定 Fail/Continue —— 现状为逐条 `ValidateWith` + Continue/FailOnValidationError
- **核心 Excel**：`DataForgePipeline.FromExcel` / `ToExcelAsync` 仍为占位；生产路径用 `DataForge.Core.Excel` 扩展

---

## 5. 文档维护约定

1. 新 API 落地后：先改源码与测试，再挪入 [api-implemented.md](./api-implemented.md)，并从本文删除或降级为「已对照实现」。
2. 示例教程（getting-started / scenarios）应只演示已实现 API；规划能力请链到本文，勿混进可运行示例。
3. `architecture.md` 中的目标接口图应放在「规划中」小节或链到本文，避免与 0.1.0 读者混淆。
