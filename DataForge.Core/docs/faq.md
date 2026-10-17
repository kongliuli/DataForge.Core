# FAQ（0.1.0）

只覆盖当前真实 API。细节见 [api-implemented.md](./api-implemented.md)；规划见 [api-roadmap.md](./api-roadmap.md)。

---

### 应该用哪个命名空间？

业务代码通常只需：

```csharp
using DataForge.Core;
```

管道接口在 `DataForge.Core.Core.Pipeline`；验证在 `DataForge.Core.Core.Validation`；转换器在 `DataForge.Core.Core.Transforms`。扩展包另有各自命名空间（`DataForge.Core.Excel`、`DataForge.Core.SqlServer` 等）。

---

### Excel 要用哪个包？

- **核心** `DataForgePipeline.FromExcel` / `ToExcelAsync`：占位实现，已 `[Obsolete]`，不适合当真 xlsx。
- **生产**：安装 **`DataForge.Core.Excel`**，使用扩展 `FromExcel(...)` 与 `ToExcel` / `ToExcelAsync`。

核心 `IDataPipeline<T>` 上也有 `ToExcelAsync`；扩展包方法名是 `ToExcel`，返回值仍是 `Task<ExportResults>`。

---

### `To*Async` 和扩展包的 `ToExcel` / `ToJson` 有何区别？

| 位置 | 名称 |
|------|------|
| 核心 `IDataPipeline<T>` | `ToCsvAsync` / `ToJsonAsync` / `ToExcelAsync` / `ToConsoleAsync` / `ToStreamAsync` |
| `DataForge.Core.Excel` | `ToExcel` |
| `DataForge.Core.Json` | `ToJson` |

都是异步导出，返回 `ExportResults`。优先用你已引用程序集里的那一套即可。

---

### SQL 入口怎么写？

没有 `DataForgePipeline.FromSqlServer(...)` 这种核心工厂方法。使用各包静态方法：

```csharp
using DataForge.Core.SqlServer;

var p = SqlServerPipelineExtensions.FromSqlServer<Order>(conn, "Orders");
await p.ToSqlServer(conn, "Orders_Out");
```

同类：`MySqlPipelineExtensions.FromMySql`、`SqlitePipelineExtensions.FromSqlite`（均要求 `T : new()`）。

---

### 如何接 FluentValidation？

包：`DataForge.Core.FluentValidation`。

```csharp
using DataForge.Core.FluentValidation;

var adapter = new FluentValidationAdapter<Order>(new OrderValidator());
await DataForgePipeline.FromCsv<Order>("orders.csv")
    .ValidateWith(adapter)
    .FailOnValidationError()   // 或 ContinueOnValidationError()
    .ToListAsync();
```

`FluentValidationAdapter<T>` 实现 `IValidator<T>`，包装 `FluentValidation.IValidator<T>`。

---

### `ExportResults` 有哪些字段？

```csharp
public class ExportResults
{
    public int RecordsWritten { get; set; }
    public string OutputPath { get; set; }
    public long OutputSize { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; }
    public bool HasErrors => Errors.Count > 0;
}
```

使用 `RecordsWritten`、`OutputPath`、`Duration`、`Errors`。**没有** `FilePath` 属性。

---

### 有没有 `SkipWhile` / `ToDataForge` / `ContinueOnError`？

0.1.0 的 `IDataPipeline<T>` **没有**这些成员。可用：`Skip`/`Take`、`FromMemory`、`OnErrorContinue`/`OnErrorSkip`/`OnErrorStop`/`OnError`。进度与并行见 `WithProgress` / `WithParallelization`（`PerformanceExtensions`）。

---

### 管道是惰性的吗？

是。`Where`/`Select`/… 只构建管道；`ToListAsync`、`ToCsvAsync` 等终端操作才执行。
