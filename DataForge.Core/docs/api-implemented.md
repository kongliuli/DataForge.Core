# API 参考（已实现 · 0.1.0）

本文**只**描述当前 `src/` 中存在的公共 API。规划中的签名见 [api-roadmap.md](./api-roadmap.md)；索引见 [api-reference.md](./api-reference.md)。

> 入口：`using DataForge.Core;`。管道：`DataForge.Core.Core.Pipeline`；验证：`DataForge.Core.Core.Validation`。

---

## 1. 工厂：`DataForgePipeline`

源文件：`src/DataForge.Core/DataForgePipeline.cs`

`DataForgePipeline` 是普通类；下列方法为 `public static`。

```csharp
public class DataForgePipeline
{
    public static IDataPipeline<T> FromCsv<T>(string filePath, CsvSourceOptions? options = null);
    public static IDataPipeline<T> FromJson<T>(string filePath, JsonSourceOptions? options = null);
    public static IDataPipeline<T> FromMemory<T>(IEnumerable<T> data);
    public static IDataPipeline<T> FromExcel<T>(string filePath, string sheetName = "Sheet1"); // 核心占位
    public static IDataPipeline<T> FromJsonString<T>(string jsonContent);
    public static IDataPipeline<T> FromJsonArray<T>(string filePath); // 当前等同 FromJson
    public static IDataPipeline<T> Merge<T>(params IDataSource<T>[] sources);
}
```

### 源选项（核心）

```csharp
// DataForge.Core.Core.Sources.Options
public class CsvSourceOptions
{
    public char Delimiter { get; set; } = ',';
    public char QuoteChar { get; set; } = '"';
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public bool HasHeaderRow { get; set; } = true;
    public int SkipRows { get; set; } = 0;
    public int? MaxRows { get; set; }
    public string? CommentPrefix { get; set; }
}

public class JsonSourceOptions
{
    public JsonSerializerOptions? SerializerOptions { get; set; }
    public bool AllowTrailingCommas { get; set; } = true;
    public bool ReadCommentHandling { get; set; } = true;
    public bool UseStreaming { get; set; } = true;
}
```

---

## 2. 管道：`IDataPipeline<T>`

源文件：`src/DataForge.Core/Core/Pipeline/IDataPipeline.cs`

单类型参数（**不是** `IDataPipeline<TIn, TOut>`）。委托参数使用 `T`。

```csharp
public interface IDataPipeline<T>
{
    IDataPipeline<TResult> Select<TResult>(Func<T, TResult> selector);
    IDataPipeline<TResult> SelectAsync<TResult>(Func<T, Task<TResult>> selector);
    IDataPipeline<TResult> SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector);
    IDataPipeline<T> Where(Func<T, bool> predicate);
    IDataPipeline<T> WhereAsync(Func<T, Task<bool>> predicate);
    IDataPipeline<T> OrderBy<TKey>(Func<T, TKey> keySelector);
    IDataPipeline<T> OrderByDescending<TKey>(Func<T, TKey> keySelector);
    IDataPipeline<T> ThenBy<TKey>(Func<T, TKey> keySelector);
    IDataPipeline<T> ThenByDescending<TKey>(Func<T, TKey> keySelector);
    IDataPipeline<T> Distinct();
    IDataPipeline<T> DistinctBy<TKey>(Func<T, TKey> keySelector);
    IDataPipeline<T> Skip(int count);
    IDataPipeline<T> Take(int count);
    IDataPipeline<List<T>> Batch(int batchSize);
    IGroupedDataPipeline<TKey, T> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull;
    IDataPipeline<(T First, TSecond Second)> Zip<TSecond>(IDataPipeline<TSecond> second);
    IDataPipeline<TResult> TransformWith<TResult>(IDataTransform<T, TResult> transform);

    IDataPipeline<T> ValidateWith(IValidator<T> validator);
    IDataPipeline<T> ContinueOnValidationError();
    IDataPipeline<T> FailOnValidationError();

    IDataPipeline<T> OnErrorContinue();
    IDataPipeline<T> OnErrorStop();
    IDataPipeline<T> OnErrorSkip();
    IDataPipeline<T> OnError(Func<Exception, T, ErrorAction> handler);

    Task<TResult> AggregateAsync<TResult>(Func<TResult, T, TResult> aggregator, TResult seed, CancellationToken cancellationToken = default);
    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> AsAsyncEnumerable(CancellationToken cancellationToken = default);

    Task<ExportResults> ToCsvAsync(string filePath, CsvExportOptions? options = null, CancellationToken cancellationToken = default);
    Task<ExportResults> ToJsonAsync(string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default);
    Task<ExportResults> ToExcelAsync(string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default);
    Task<ExportResults> ToConsoleAsync(Func<T, string>? formatter = null, CancellationToken cancellationToken = default);
    Task<ExportResults> ToStreamAsync(Stream stream, ExportFormat format, CancellationToken cancellationToken = default);
}
```

### 查询 / 投影（摘要）

- `Select` / `SelectAsync` / `SelectMany`
- `Where` / `WhereAsync`
- `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending`
- `Distinct` / `DistinctBy`
- `Skip` / `Take` / `Batch`
- `GroupBy` → `IGroupedDataPipeline<TKey, T>`（`TKey : notnull`）
- `Zip`

### 转换 / 验证 / 错误

- `TransformWith(IDataTransform<T, TResult>)`
- `ValidateWith(IValidator<T>)`（非泛型约束方法形态）
- `ContinueOnValidationError` / `FailOnValidationError`
- `OnErrorContinue` / `OnErrorStop` / `OnErrorSkip` / `OnError(...)`

### 物化与核心导出

- 物化：`AggregateAsync` / `ToListAsync` / `ToArrayAsync` / `FirstOrDefaultAsync` / `CountAsync` / `AnyAsync()` / `AsAsyncEnumerable`
- 核心导出（均带 `Async`，返回 `ExportResults`）：`ToCsvAsync` / `ToJsonAsync` / `ToExcelAsync`（占位） / `ToConsoleAsync` / `ToStreamAsync`

接口上**没有**：`SkipWhile` / `TakeWhile`、带谓词的 `AnyAsync`、`AllAsync`、`FirstAsync`、`Name` / `ExecutionPlan`、继承 `IAsyncEnumerable<T>`、核心上的 `ToSqlServer` / `ToMySql` / `ToSqlite`。

---

## 3. 分组：`IGroupedDataPipeline<TKey, TElement>`

同文件。正确类型名是 **`IGroupedDataPipeline`**（不是 `IGroupedPipeline`）。

```csharp
public interface IGroupedDataPipeline<TKey, TElement> where TKey : notnull
{
    IDataPipeline<TResult> Select<TResult>(Func<IGrouping<TKey, TElement>, TResult> selector);
    IDataPipeline<IGrouping<TKey, TElement>> OrderBy<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector);
    IDataPipeline<IGrouping<TKey, TElement>> OrderByDescending<TKey2>(Func<IGrouping<TKey, TElement>, TKey2> keySelector);
    Task<List<IGrouping<TKey, TElement>>> ToListAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<TKey, List<TElement>>> ToDictionaryAsync(CancellationToken cancellationToken = default);
}
```

分组上的 `Count` / `Sum` / `Average` / `Min` / `Max` 辅助方法未作为接口成员落地；可在 `Select` 内对 `IGrouping` 使用 LINQ。

---

## 4. 数据源 / 目标 / 转换

### `IDataSource<T>`

源文件：`Core/Sources/IDataSource.cs`

```csharp
public interface IDataSource<T>
{
    string Name { get; }
    DataSourceType SourceType { get; }
    IAsyncEnumerable<T> ReadAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);
}

public interface IRelationalDataSource<T> : IDataSource<T>
{
    IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);
}

public interface IFileDataSource<T> : IDataSource<T>
{
    string FilePath { get; }
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// DataForge.Core.Core.Models
public class DataSourceMetadata
{
    public string SourceType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
    public int? RecordCount { get; set; }
    public Dictionary<string, string> AdditionalInfo { get; set; } = [];
}

// DataForge.Core.Core.Infrastructure
public enum DataSourceType { Csv, Json, Excel, SqlServer, MySql, Sqlite, Memory, RestApi, Custom }
public enum DataTargetType { Csv, Json, Excel, SqlServer, MySql, Sqlite, Console, Stream, RestApi, Custom }
```

### `IDataTarget<in T>`

源文件：`Core/Targets/IDataTarget.cs`

```csharp
public interface IDataTarget<in T>
{
    string Name { get; }
    DataTargetType TargetType { get; }
    Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default);
    Task WriteAsync(T item, CancellationToken cancellationToken = default);
    Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);
    Task CompleteAsync(CancellationToken cancellationToken = default);
}

public class WriteResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<WriteError> Errors { get; set; } = [];
}

public class WriteError
{
    public object? Item { get; set; }
    public string Error { get; set; } = "";
}

public enum ExportFormat { Csv, Json, Excel }
```

### 转换与错误

```csharp
// Core/Transforms/IDataTransform.cs
public interface IDataTransform<in TSource, out TResult>
{
    TResult Transform(TSource source);
}

public interface IAsyncDataTransform<in TSource, TResult>
{
    Task<TResult> TransformAsync(TSource source);
}

// Core/Infrastructure
public enum ErrorAction { Continue, Skip, Stop, Throw }
public enum InsertMode { Insert, Upsert }
```

---

## 5. 验证

命名空间：`DataForge.Core.Core.Validation`

```csharp
public interface IValidator<in T>
{
    ValidationResult Validate(T item);
    Task<ValidationResult> ValidateAsync(T item, CancellationToken cancellationToken = default);
}

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; set; } = [];
    public static ValidationResult Success();
    public static ValidationResult Failure(IEnumerable<ValidationError> errors);
}

public class ValidationError
{
    public string PropertyName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ValidationErrorCode ErrorCode { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

public enum ValidationErrorCode { Required, InvalidFormat, Range, Length, Custom }
public enum ValidationSeverity { Warning, Error, Critical }

public interface IValidationRule<T>
{
    string RuleName { get; }
    ValidationError? Validate(T instance);
}

public abstract class DataValidator<T> : IValidator<T>
{
    public IReadOnlyList<IValidationRule<T>> Rules { get; }
    protected ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> propertyExpression);
    protected void AddRule(IValidationRule<T> rule);
}

// ValidationRuleBuilder 已实现链式：
// NotEmpty, Required, Length(min,max), InRange, GreaterThan,
// GreaterThanOrEqualTo, LessThan, MinLength, Must, When, WithSeverity, WithMessage
```

### FluentValidation 扩展包

命名空间：`DataForge.Core.FluentValidation`

```csharp
public class FluentValidationAdapter<T> : IValidator<T>
{
    public FluentValidationAdapter(global::FluentValidation.IValidator<T> innerValidator);
    public ValidationResult Validate(T instance);
    public Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);
}
```

用法：`ValidateWith(new FluentValidationAdapter<T>(new MyAbstractValidator()))`。**无自动隐式适配。**

---

## 6. 导出选项与结果

```csharp
public class ExportResults
{
    public int RecordsWritten { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public long OutputSize { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; } = [];
    public bool HasErrors => Errors.Count > 0;
}

public class CsvExportOptions
{
    public char Delimiter { get; set; } = ',';
    public char QuoteChar { get; set; } = '"';
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public bool IncludeHeader { get; set; } = true;
    public int? BatchSize { get; set; }
}

public class JsonExportOptions
{
    public bool Indented { get; set; } = true;
    public string? RootPropertyName { get; set; }
}

public class ExcelExportOptions
{
    public string SheetName { get; set; } = "Sheet1";
    public bool FreezeFirstRow { get; set; } = true;
    public bool AutoSizeColumns { get; set; } = true;
    public bool IncludeHeader { get; set; } = true;
}
```

---

## 7. 扩展包管道方法

| 包 | 入口 | 说明 |
|---|---|---|
| `DataForge.Core.Excel` | 静态 `FromExcel(...)` **与** `this DataForgePipeline.FromExcel`；`ToExcel` / `ToExcelAsync` | ClosedXML；核心占位 `FromExcel`/`ToExcelAsync` 已 Obsolete |
| `DataForge.Core.Json` | 静态 `FromJson(...)` **与** builder 扩展；`ToJson` / `ToJsonAsync` | 与核心 `FromJson`/`ToJsonAsync` 并存 |
| `DataForge.Core.Http` | 静态 + builder：`FromRestApi` / `FromRestApiWithPagination`；`ToRestApi` / `ToRestApiAsync` | 返回 `IDataPipeline<T>` |
| `DataForge.Core.SqlServer` | 静态 + builder：`FromSqlServer`；`ToSqlServer` / `ToSqlServerAsync` | `(connectionString, tableName)` |
| `DataForge.Core.MySql` | 同上形 `FromMySql` / `ToMySql` / `ToMySqlAsync` | |
| `DataForge.Core.Sqlite` | 同上形 `FromSqlite` / `ToSqlite` / `ToSqliteAsync` | |

> **双轨约定**：扩展包 `From*` 同时提供静态工厂与 `this DataForgePipeline` 扩展；`To*` 提供同步名与 `*Async` 别名。二者等价，任选其一。

### Excel

```csharp
public static IDataPipeline<T> FromExcel<T>(string filePath, ExcelSourceOptions? options = null) where T : new();
public static IDataPipeline<T> FromExcel<T>(this DataForgePipeline builder, string filePath, ExcelSourceOptions? options = null) where T : new();
public static Task<ExportResults> ToExcel<T>(this IDataPipeline<T> pipeline, string filePath, ExcelExportOptions? options = null, CancellationToken cancellationToken = default);
public static Task<ExportResults> ToExcelAsync<T>(...); // alias

public class ExcelSourceOptions
{
    public string SheetName { get; set; } = string.Empty;
    public bool HasHeaderRow { get; set; } = true;
    public int SkipRows { get; set; } = 0;
    public int? MaxRows { get; set; }
}
```

### Json

```csharp
public static IDataPipeline<T> FromJson<T>(string filePath, JsonSourceOptions? options = null);
public static IDataPipeline<T> FromJson<T>(this DataForgePipeline builder, string filePath, JsonSourceOptions? options = null);
public static Task<ExportResults> ToJson<T>(this IDataPipeline<T> pipeline, string filePath, JsonExportOptions? options = null, CancellationToken cancellationToken = default);
public static Task<ExportResults> ToJsonAsync<T>(...); // alias
```

### Http

```csharp
public static IDataPipeline<T> FromRestApi<T>(string baseUrl, string endpoint, RestApiSourceOptions? options = null);
public static IDataPipeline<T> FromRestApi<T>(this DataForgePipeline builder, string baseUrl, string endpoint, RestApiSourceOptions? options = null);
public static IDataPipeline<T> FromRestApi<T>(HttpClient httpClient, string endpoint, RestApiSourceOptions? options = null);
public static IDataPipeline<T> FromRestApi<T>(this DataForgePipeline builder, HttpClient httpClient, string endpoint, RestApiSourceOptions? options = null);
public static IDataPipeline<T> FromRestApiWithPagination<T>(string baseUrl, string endpoint, RestApiSourceOptions? options = null);
public static IDataPipeline<T> FromRestApiWithPagination<T>(this DataForgePipeline builder, string baseUrl, string endpoint, RestApiSourceOptions? options = null);
public static Task<ExportResults> ToRestApi<T>(this IDataPipeline<T> pipeline, string baseUrl, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default);
public static Task<ExportResults> ToRestApi<T>(this IDataPipeline<T> pipeline, HttpClient httpClient, string endpoint, RestApiTargetOptions? options = null, CancellationToken cancellationToken = default);
public static Task<ExportResults> ToRestApiAsync<T>(...); // aliases

public class RestApiSourceOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string QueryParams { get; set; } = string.Empty;
    public int PageSize { get; set; } = 100;
    public string? PageParam { get; set; }
    public int StartPage { get; set; } = 1;
    public int MaxPages { get; set; } = 0;
    /// <summary>true: DeserializeAsyncEnumerable for JSON arrays; false: buffer List&lt;T&gt;. Source/Target apply Headers.</summary>
    public bool UseStreaming { get; set; } = true;
    public Dictionary<string, string> Headers { get; set; } = new();
}

public class RestApiTargetOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public bool UseBulkEndpoint { get; set; } = false;
    public Dictionary<string, string> Headers { get; set; } = new();
}
// RestApiSource/Target: IDisposable; only dispose HttpClient when constructed without an external client.
```

### SQL（SqlServer / MySql / Sqlite）

```csharp
public static IDataPipeline<T> FromSqlServer<T>(string connectionString, string tableName) where T : new();
public static IDataPipeline<T> FromSqlServer<T>(this DataForgePipeline builder, string connectionString, string tableName) where T : new();
public static Task<ExportResults> ToSqlServer<T>(this IDataPipeline<T> pipeline, string connectionString, string tableName, SqlServerExportOptions? options = null, CancellationToken cancellationToken = default);
public static Task<ExportResults> ToSqlServerAsync<T>(...); // alias
// FromMySql / ToMySql(/Async)、FromSqlite / ToSqlite(/Async) 同形

public class SqlServerExportOptions // MySqlExportOptions / SqliteExportOptions 同形
{
    public int BatchSize { get; set; } = 1000;
    public InsertMode InsertMode { get; set; } = InsertMode.Insert;
    public string[]? UpsertKeyColumns { get; set; }
    public bool UseTransaction { get; set; } = true;
}
```

调用示例：

```csharp
using DataForge.Core;
using DataForge.Core.Excel;

await new DataForgePipeline()
    .FromExcel<Order>("orders.xlsx")
    .ToExcel("out.xlsx");

using DataForge.Core.SqlServer;
var pipe = SqlServerPipelineExtensions.FromSqlServer<Order>(conn, "Orders");
```

扩展导出统一返回 `ExportResults`（不是 `DbExportResult`）。

---

## 8. 明确不在 0.1.0 的常见误写

| 文档旧写法 | 现状 |
|---|---|
| `using DataForge;` | → `using DataForge.Core;` |
| `DataForgeVersion` | 不存在 |
| `FromCollection` / `FromEnumerable` | → `FromMemory` |
| `IDataPipeline<TIn,TOut>` / 成员里的 `TOut` | → `IDataPipeline<T>` |
| `IGroupedPipeline` | → `IGroupedDataPipeline` |
| `ContinueOnError` / `StopOnError` | → `OnErrorContinue` / `OnErrorStop` |
| `ValidateWith<TValidator>(...)` | → `ValidateWith(IValidator<T>)` |
| 直接 `ValidateWith(new AbstractValidator())` | 需 `FluentValidationAdapter` |
| `ToDataForge()` | 未实现（见 roadmap） |
| SQL `FromSqlServer(conn)` 单参数 | 需要 `(connectionString, tableName)` |
| `SkipWhile` / `TakeWhile` | 未实现 |
| RestApi 返回 `IDataPipeline<RestApiSource<T>, T>` | 返回 `IDataPipeline<T>` |
| 核心/扩展导出返回 `DbExportResult` | 返回 `ExportResults` |
