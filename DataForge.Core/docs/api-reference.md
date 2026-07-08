# API 完整参考

> **注意（2026-07-08）**：下文部分章节仍描述早期双泛型 `IDataPipeline<TIn, TOut>` 与已移除的扩展入口。  
> **以代码为准**：`DataForge.Core` v0.2+ 使用单泛型 `IDataPipeline<T>`，终端方法为 `ToJsonAsync` / `ToCsvAsync` 等。  
> 快速上手见 [getting-started.md](./getting-started.md)；Sync CLI 见 [tools/DataForge.Sync/README.md](../tools/DataForge.Sync/README.md)。

## 当前公共 API 摘要（v0.2.1）

### 入口 — `DataForgePipeline`

| 方法 | 说明 |
|------|------|
| `FromCsv<T>(path, options?)` | CSV → `IDataPipeline<T>` |
| `FromJson<T>(path, options?)` | JSON → 管道 |
| `FromMemory<T>(data)` | 内存集合 |
| `FromExcel<T>(...)` | 抛出 `EXCEL_EXTENSION_REQUIRED`；请用 `DataForge.Core.Excel` |
| `Merge<T>(sources...)` | 合并多个数据源 |

扩展包：`ExcelPipelineExtensions.FromExcel`、`HttpPipelineExtensions.FromRestApi` 等。

### 管道 — `IDataPipeline<T>`

链式：`Select` / `Where` / `OrderBy`+`ThenBy`+`WithExternalSort` / `ValidateWith` / `WithBadRowOutput`。  
终端：`ToJsonAsync` / `ToCsvAsync` / `ToListAsync` / `AsAsyncEnumerable`。

### 其它

- `RowError` + `ExportResults.RowErrors`；`.WithBadRowOutput(path)` 导出 NDJSON  
- `DataForge.Core.DependencyInjection`：`AddDataForge()`

---

## 历史文档（待逐步对齐）

以下章节保留早期设计参考，使用前请对照 `src/DataForge.Core` 源码。

## 目录

- [DataForge.Entry](#dataforgeentry)
- [DataForge.Sources](#dataforgesources)
- [DataForge.Pipeline](#dataforgepipeline)
- [DataForge.Transforms](#dataorgetransforms)
- [DataForge.Validation](#dataforgevalidation)
- [DataForge.Export](#dataforgeexport)

---

## DataForge.Entry

入口命名空间，提供静态入口类 `DataForgePipeline`。

### DataForgePipeline — 静态入口类

静态入口类，提供创建数据管道的工厂方法。

```csharp
public static class DataForgePipeline
{
    // ============ SQL 数据源 ============
    
    /// <summary>
    /// 从 SQL Server 创建数据管道
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>数据管道</returns>
    public static SqlServerSourcePipeline<T> FromSqlServer<T>(string connectionString);
    
    /// <summary>
    /// 从 SQL Server 创建数据管道（使用 DbConnection）
    /// </summary>
    /// <param name="connection">数据库连接</param>
    public static SqlServerSourcePipeline<T> FromSqlServer<T>(DbConnection connection);
    
    /// <summary>
    /// 从 SQL Server 创建数据管道（指定表名）
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    public static SqlServerSourcePipeline<T> FromSqlServer<T>(string connectionString, string tableName);
    
    /// <summary>
    /// 从 SQL Server 创建数据管道（带选项）
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="options">数据源选项</param>
    public static SqlServerSourcePipeline<T> FromSqlServer<T>(string connectionString, SqlSourceOptions options);
    
    // ============ MySQL 数据源 ============
    
    /// <summary>
    /// 从 MySQL 创建数据管道
    /// </summary>
    public static MySqlSourcePipeline<T> FromMySql<T>(string connectionString);
    
    /// <summary>
    /// 从 MySQL 创建数据管道（使用连接）
    /// </summary>
    public static MySqlSourcePipeline<T> FromMySql<T>(DbConnection connection);
    
    /// <summary>
    /// 从 MySQL 创建数据管道（带选项）
    /// </summary>
    public static MySqlSourcePipeline<T> FromMySql<T>(string connectionString, MySqlSourceOptions options);
    
    // ============ SQLite 数据源 ============
    
    /// <summary>
    /// 从 SQLite 创建数据管道
    /// </summary>
    public static SqliteSourcePipeline<T> FromSqlite<T>(string databasePath);
    
    /// <summary>
    /// 从 SQLite 创建数据管道（使用连接）
    /// </summary>
    public static SqliteSourcePipeline<T> FromSqlite<T>(DbConnection connection);
    
    /// <summary>
    /// 从 SQLite 创建数据管道（带选项）
    /// </summary>
    public static SqliteSourcePipeline<T> FromSqlite<T>(string connectionString, SqliteSourceOptions options);
    
    // ============ CSV 数据源 ============
    
    /// <summary>
    /// 从 CSV 文件创建数据管道
    /// </summary>
    /// <param name="filePath">CSV 文件路径</param>
    public static CsvSourcePipeline<T> FromCsv<T>(string filePath);
    
    /// <summary>
    /// 从 CSV 文件创建数据管道（带选项）
    /// </summary>
    public static CsvSourcePipeline<T> FromCsv<T>(string filePath, CsvSourceOptions options);
    
    /// <summary>
    /// 从 CSV 流创建数据管道
    /// </summary>
    public static CsvSourcePipeline<T> FromCsv<T>(Stream stream, CsvSourceOptions? options = null);
    
    // ============ Excel 数据源 ============
    
    /// <summary>
    /// 从 Excel 文件创建数据管道
    /// </summary>
    /// <param name="filePath">Excel 文件路径</param>
    public static ExcelSourcePipeline<T> FromExcel<T>(string filePath);
    
    /// <summary>
    /// 从 Excel 文件创建数据管道（指定 Sheet）
    /// </summary>
    /// <param name="sheetName">工作表名称</param>
    public static ExcelSourcePipeline<T> FromExcel<T>(string filePath, string sheetName);
    
    /// <summary>
    /// 从 Excel 文件创建数据管道（带选项）
    /// </summary>
    public static ExcelSourcePipeline<T> FromExcel<T>(string filePath, ExcelSourceOptions options);
    
    /// <summary>
    /// 从 Excel 流创建数据管道
    /// </summary>
    public static ExcelSourcePipeline<T> FromExcel<T>(Stream stream, ExcelSourceOptions? options = null);
    
    // ============ JSON 数据源 ============
    
    /// <summary>
    /// 从 JSON 文件创建数据管道（单个对象）
    /// </summary>
    public static JsonSourcePipeline<T> FromJson<T>(string filePath);
    
    /// <summary>
    /// 从 JSON 文件创建数据管道（数组）
    /// </summary>
    public static JsonSourcePipeline<T> FromJsonArray<T>(string filePath);
    
    /// <summary>
    /// 从 JSON 流创建数据管道
    /// </summary>
    public static JsonSourcePipeline<T> FromJson<T>(Stream stream);
    
    /// <summary>
    /// 从 JSON 字符串创建数据管道
    /// </summary>
    public static JsonSourcePipeline<T> FromJsonString<T>(string json);
    
    /// <summary>
    /// 从 JSON 创建数据管道（带选项）
    /// </summary>
    public static JsonSourcePipeline<T> FromJson<T>(string filePath, JsonSourceOptions options);
    
    // ============ 内存数据源 ============
    
    /// <summary>
    /// 从集合创建数据管道
    /// </summary>
    public static MemoryPipeline<T> FromCollection<T>(IEnumerable<T> collection);
    
    /// <summary>
    /// 从可枚举创建数据管道
    /// </summary>
    public static MemoryPipeline<T> FromEnumerable<T>(IEnumerable<T> enumerable);
    
    /// <summary>
    /// 从数组创建数据管道
    /// </summary>
    public static MemoryPipeline<T> FromArray<T>(params T[] items);
    
    // ============ 多数据源合并 ============
    
    /// <summary>
    /// 合并多个数据源
    /// </summary>
    public static MergedPipeline<T> Merge<T>(params IDataSource<T>[] sources);
    
    /// <summary>
    /// 合并多个数据管道
    /// </summary>
    public static MergedPipeline<T> Merge<T>(params IDataPipeline source, params IDataPipeline<T>[] pipelines);
}
```

---

## DataForge.Sources

数据源相关类型。

### IDataSource<T>

```csharp
/// <summary>
/// 数据源接口
/// </summary>
public interface IDataSource<T>
{
    /// <summary>
    /// 数据源名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 数据源类型
    /// </summary>
    DataSourceType SourceType { get; }
    
    /// <summary>
    /// 异步读取数据流
    /// </summary>
    IAsyncEnumerable<T> ReadAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 读取所有数据到内存
    /// </summary>
    Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取元数据
    /// </summary>
    DataSourceMetadata GetMetadata();
}
```

### 数据源选项类

#### CsvSourceOptions

```csharp
public class CsvSourceOptions
{
    /// <summary>
    /// 文件编码，默认 UTF8
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    
    /// <summary>
    /// 分隔符，默认逗号
    /// </summary>
    public char Separator { get; set; } = ',';
    
    /// <summary>
    /// 是否有表头行，默认 true
    /// </summary>
    public bool HasHeader { get; set; } = true;
    
    /// <summary>
    /// 跳过的行数（表头后）
    /// </summary>
    public int SkipLines { get; set; } = 0;
    
    /// <summary>
    /// 是否修剪字段值
    /// </summary>
    public bool TrimFields { get; set; } = true;
    
    /// <summary>
    /// 空值表示形式
    /// </summary>
    public string NullValue { get; set; } = "";
    
    /// <summary>
    /// 引号字符
    /// </summary>
    public char QuoteChar { get; set; } = '"';
    
    /// <summary>
    /// 转义字符
    /// </summary>
    public char EscapeChar { get; set; } = '"';
    
    /// <summary>
    /// 是否忽略引号
    /// </summary>
    public bool IgnoreQuotes { get; set; } = false;
    
    /// <summary>
    /// 列映射（字段名 -> 类型）
    /// </summary>
    public Dictionary<string, Type>? ColumnMapping { get; set; }
}
```

#### ExcelSourceOptions

```csharp
public class ExcelSourceOptions
{
    /// <summary>
    /// 工作表名称（与 SheetIndex 二选一）
    /// </summary>
    public string? SheetName { get; set; }
    
    /// <summary>
    /// 工作表索引（从 0 开始）
    /// </summary>
    public int SheetIndex { get; set; } = 0;
    
    /// <summary>
    /// 表头所在行（从 1 开始），默认 1
    /// </summary>
    public int HeaderRow { get; set; } = 1;
    
    /// <summary>
    /// 数据起始行（从 1 开始），默认表头行+1
    /// </summary>
    public int? DataStartRow { get; set; }
    
    /// <summary>
    /// 数据结束行，null 表示自动检测
    /// </summary>
    public int? DataEndRow { get; set; }
    
    /// <summary>
    /// 是否跳过空行
    /// </summary>
    public bool SkipEmptyRows { get; set; } = true;
    
    /// <summary>
    /// 列映射（列名 -> 属性名）
    /// </summary>
    public Dictionary<string, string>? ColumnMapping { get; set; }
    
    /// <summary>
    /// 是否使用强类型映射
    /// </summary>
    public bool UseStrongTyping { get; set; } = true;
}
```

#### SqlSourceOptions

```csharp
public class SqlSourceOptions
{
    /// <summary>
    /// 命令超时时间（秒）
    /// </summary>
    public int CommandTimeout { get; set; } = 300;
    
    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool EnableRetry { get; set; } = false;
    
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;
    
    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// 是否启用连接池
    /// </summary>
    public bool Pooling { get; set; } = true;
    
    /// <summary>
    /// 最小连接池大小
    /// </summary>
    public int MinPoolSize { get; set; } = 5;
    
    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;
    
    /// <summary>
    /// 是否将空数值作为 DBNull
    /// </summary>
    public bool ConvertEmptyValuesToNull { get; set; } = true;
}
```

### 数据源元数据

```csharp
public class DataSourceMetadata
{
    /// <summary>
    /// 数据源名称
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// 数据源类型
    /// </summary>
    public DataSourceType Type { get; }
    
    /// <summary>
    /// 预估记录数（可能为 null）
    /// </summary>
    public long? RecordCount { get; }
    
    /// <summary>
    /// 字段元数据列表
    /// </summary>
    public IReadOnlyList<FieldMetadata>? Fields { get; }
    
    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, object?> CustomProperties { get; }
}

public class FieldMetadata
{
    /// <summary>
    /// 字段名称
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// 字段类型
    /// </summary>
    public Type Type { get; }
    
    /// <summary>
    /// 是否可为空
    /// </summary>
    public bool IsNullable { get; }
    
    /// <summary>
    /// 最大长度（适用于字符串）
    /// </summary>
    public int? MaxLength { get; }
}

public enum DataSourceType
{
    SqlServer,
    MySql,
    Sqlite,
    Csv,
    Excel,
    Json,
    RestApi,
    Memory
}
```

---

## DataForge.Pipeline

管道相关类型。

### IDataPipeline<TIn, TOut>

```csharp
public interface IDataPipeline<TIn, TOut> : IAsyncEnumerable<TOut>
{
    // ============ 属性 ============
    
    /// <summary>
    /// 管道名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 执行计划描述
    /// </summary>
    string ExecutionPlan { get; }
    
    // ============ 转换操作 ============
    
    /// <summary>
    /// 过滤数据
    /// </summary>
    IDataPipeline<TIn, TOut> Where(Func<TOut, bool> predicate);
    
    /// <summary>
    /// 异步过滤
    /// </summary>
    IDataPipeline<TIn, TOut> WhereAsync(Func<TOut, Task<bool>> predicate);
    
    /// <summary>
    /// 索引过滤
    /// </summary>
    IDataPipeline<TIn, TOut> Where(Func<TOut, int, bool> predicate);
    
    /// <summary>
    /// 投影/选择
    /// </summary>
    IDataPipeline<TIn, TResult> Select<TResult>(Func<TOut, TResult> selector);
    
    /// <summary>
    /// 异步投影
    /// </summary>
    IDataPipeline<TIn, TResult> SelectAsync<TResult>(Func<TOut, Task<TResult>> selector);
    
    /// <summary>
    /// 索引投影
    /// </summary>
    IDataPipeline<TIn, TResult> Select<TResult>(Func<TOut, int, TResult> selector);
    
    /// <summary>
    /// 去重（全部）
    /// </summary>
    IDataPipeline<TIn, TOut> Distinct();
    
    /// <summary>
    /// 按键去重
    /// </summary>
    IDataPipeline<TIn, TOut> DistinctBy<TKey>(Func<TOut, TKey> keySelector);
    
    /// <summary>
    /// 排序（升序）
    /// </summary>
    IDataPipeline<TIn, TOut> OrderBy<TKey>(Func<TOut, TKey> keySelector);
    
    /// <summary>
    /// 排序（降序）
    /// </summary>
    IDataPipeline<TIn, TOut> OrderByDescending<TKey>(Func<TOut, TKey> keySelector);
    
    /// <summary>
    /// 多级排序 - 升序
    /// </summary>
    IDataPipeline<TIn, TOut> ThenBy<TKey>(Func<TOut, TKey> keySelector);
    
    /// <summary>
    /// 多级排序 - 降序
    /// </summary>
    IDataPipeline<TIn, TOut> ThenByDescending<TKey>(Func<TOut, TKey> keySelector);
    
    /// <summary>
    /// 跳过记录
    /// </summary>
    IDataPipeline<TIn, TOut> Skip(int count);
    
    /// <summary>
    /// 跳过满足条件的记录
    /// </summary>
    IDataPipeline<TIn, TOut> SkipWhile(Func<TOut, bool> predicate);
    
    /// <summary>
    /// 取记录
    /// </summary>
    IDataPipeline<TIn, TOut> Take(int count);
    
    /// <summary>
    /// 取满足条件的记录
    /// </summary>
    IDataPipeline<TIn, TOut> TakeWhile(Func<TOut, bool> predicate);
    
    // ============ 分组操作 ============
    
    /// <summary>
    /// 分组
    /// </summary>
    IGroupedPipeline<TIn, TOut, TKey> GroupBy<TKey>(Func<TOut, TKey> keySelector);
    
    /// <summary>
    /// 分组并展平
    /// </summary>
    IDataPipeline<TIn, TElement> SelectMany<TElement>(
        Func<TOut, IEnumerable<TElement>> selector);
    
    // ============ 验证操作 ============
    
    /// <summary>
    /// 添加验证器
    /// </summary>
    IDataPipeline<TIn, TOut> ValidateWith<TValidator>(TValidator validator)
        where TValidator : IValidator<TOut>;
    
    /// <summary>
    /// 继续执行即使验证失败
    /// </summary>
    IDataPipeline<TIn, TOut> ContinueOnValidationError();
    
    /// <summary>
    /// 验证失败时抛出异常
    /// </summary>
    IDataPipeline<TIn, TOut> FailOnValidationError();
    
    // ============ 自定义转换 ============
    
    /// <summary>
    /// 添加自定义转换器
    /// </summary>
    IDataPipeline<TIn, TOut> TransformWith(IDataTransform<TOut, TOut> transform);
    
    /// <summary>
    /// 添加自定义转换函数
    /// </summary>
    IDataPipeline<TIn, TOut> TransformWith(Func<TOut, TOut> transform);
    
    /// <summary>
    /// 添加异步自定义转换函数
    /// </summary>
    IDataPipeline<TIn, TOut> TransformWithAsync(Func<TOut, Task<TOut>> transform);
    
    // ============ 执行操作 ============
    
    /// <summary>
    /// 执行并收集到列表
    /// </summary>
    Task<IReadOnlyList<TOut>> ToListAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行并返回第一个或默认
    /// </summary>
    Task<TOut?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行并返回第一个
    /// </summary>
    Task<TOut> FirstAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行并计数
    /// </summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 判断是否存在满足条件的元素
    /// </summary>
    Task<bool> AnyAsync(Func<TOut, bool>? predicate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 判断是否所有元素都满足条件
    /// </summary>
    Task<bool> AllAsync(Func<TOut, bool> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 聚合操作
    /// </summary>
    Task<TResult> AggregateAsync<TResult>(
        Func<TResult, TOut, TResult> aggregator, 
        TResult seed,
        CancellationToken cancellationToken = default);
    
    // ============ 导出操作 ============
    
    /// <summary>
    /// 导出到 CSV
    /// </summary>
    Task<CsvExportResult> ToCsv(string filePath, CsvExportOptions? options = null);
    
    /// <summary>
    /// 导出到 Excel
    /// </summary>
    Task<ExcelExportResult> ToExcel(string filePath, ExcelExportOptions? options = null);
    
    /// <summary>
    /// 导出到 JSON
    /// </summary>
    Task<JsonExportResult> ToJson(string filePath, JsonExportOptions? options = null);
    
    /// <summary>
    /// 导出到 SQL Server
    /// </summary>
    Task<DbExportResult> ToSqlServer(string connectionString, string tableName,
        SqlServerExportOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 导出到 MySQL
    /// </summary>
    Task<DbExportResult> ToMySql(string connectionString, string tableName,
        MySqlExportOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 导出到 SQLite
    /// </summary>
    Task<DbExportResult> ToSqlite(string connectionString, string tableName,
        SqliteExportOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 输出到控制台
    /// </summary>
    Task<ConsoleExportResult> ToConsole(Func<TOut, string>? formatter = null);
    
    /// <summary>
    /// 输出到流
    /// </summary>
    Task<StreamExportResult> ToStream(Stream outputStream, ExportFormat format);
}
```

### IGroupedPipeline

```csharp
public interface IGroupedPipeline<TIn, TOut, TKey> : IDataPipeline<TIn, IGroupResult<TKey, TOut>>
{
    /// <summary>
    /// 对每个分组执行投影
    /// </summary>
    IDataPipeline<TIn, TResult> Select<TResult>(Func<IGroupResult<TKey, TOut>, TResult> selector);
    
    /// <summary>
    /// 对每个分组计数
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, int>> Count();
    
    /// <summary>
    /// 对每个分组求和
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, decimal>> Sum<TValue>(Func<TOut, TValue> valueSelector)
        where TValue : struct;
    
    /// <summary>
    /// 对每个分组求平均值
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, double>> Average<TValue>(Func<TOut, TValue> valueSelector)
        where TValue : struct;
    
    /// <summary>
    /// 对每个分组求最大值
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, TOut>> Max<TValue>(Func<TOut, TValue> valueSelector)
        where TValue : struct;
    
    /// <summary>
    /// 对每个分组求最小值
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, TOut>> Min<TValue>(Func<TOut, TValue> valueSelector)
        where TValue : struct;
}
```

---

## DataForge.Transforms

内置转换器相关类型。

### 内置转换器

```csharp
// SelectTransform<TIn, TOut> - 投影转换
public class SelectTransform<TIn, TOut> : IDataTransform<TIn, TOut>
{
    public SelectTransform(Func<TIn, TOut> selector);
    public SelectTransform(Func<TIn, int, TOut> selector);
}

// WhereTransform<T> - 过滤转换
public class WhereTransform<T> : IDataTransform<T, T>
{
    public WhereTransform(Func<T, bool> predicate);
    public WhereTransform(Func<T, int, bool> predicate);
}

// OrderByTransform<T, TKey> - 排序转换
public class OrderByTransform<T, TKey> : IDataTransform<T, T>
{
    public OrderByTransform(Func<T, TKey> keySelector, bool descending = false);
}

// DistinctTransform<T, TKey> - 去重转换
public class DistinctTransform<T, TKey> : IDataTransform<T, T>
{
    public DistinctTransform();
    public DistinctTransform(Func<T, TKey> keySelector);
}

// TakeTransform<T> - 取前N条
public class TakeTransform<T> : IDataTransform<T, T>
{
    public TakeTransform(int count);
}

// SkipTransform<T> - 跳过N条
public class SkipTransform<T> : IDataTransform<T, T>
{
    public SkipTransform(int count);
}
```

### 分组聚合器

```csharp
// GroupByAggregator<T, TKey> - 分组聚合
public class GroupByAggregator<T, TKey> : IDataTransform<T, IGroupResult<TKey, T>>
{
    public GroupByAggregator(Func<T, TKey> keySelector);
}

// AggregateFunctions - 聚合函数
public static class AggregateFunctions
{
    public static int Count<T>(IEnumerable<T> source);
    public static int? Count<T>(IEnumerable<T?> source);
    public static decimal Sum<T>(IEnumerable<T> source, Func<T, decimal> selector);
    public static double Average<T>(IEnumerable<T> source, Func<T, int> selector);
    public static T Max<T>(IEnumerable<T> source) where T : IComparable<T>;
    public static T Min<T>(IEnumerable<T> source) where T : IComparable<T>;
}
```

---

## DataForge.Validation

验证相关类型。

### IValidator<T>

```csharp
public interface IValidator<T>
{
    string Name { get; }
    IReadOnlyList<IValidationRule<T>> Rules { get; }
    ValidationResult Validate(T instance);
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ValidationResult> ValidateBatchAsync(
        IEnumerable<T> instances, 
        CancellationToken cancellationToken = default);
}
```

### DataValidator<T>

```csharp
public abstract class DataValidator<T> : IValidator<T>
{
    protected DataValidator();
    
    /// <summary>
    /// 添加属性验证规则
    /// </summary>
    protected void RuleFor<TProperty>(Expression<Func<T, TProperty>> property);
    
    /// <summary>
    /// 添加自定义验证规则
    /// </summary>
    protected void AddRule(IValidationRule<T> rule);
    
    /// <summary>
    /// 添加条件规则
    /// </summary>
    protected void AddCondition(Func<T, bool> condition, Action addRules);
}
```

### 内置验证规则

```csharp
// RequiredRule<T, TProperty> - 必填规则
public class RequiredRule<T, TProperty> : IValidationRule<T>
{
    public RequiredRule(Expression<Func<T, TProperty>> property);
}

// NotEmptyRule<T, TProperty> - 非空规则
public class NotEmptyRule<T, TProperty> : IValidationRule<T> where TProperty : struct;

// NotNullRule<T, TProperty> - 非空引用规则
public class NotNullRule<T, TProperty> : IValidationRule<T>;

// GreaterThanRule<T, TProperty> - 大于规则
public class GreaterThanRule<T, TProperty> : IValidationRule<T> 
    where TProperty : IComparable<TProperty>
{
    public GreaterThanRule(Expression<Func<T, TProperty>> property, TProperty value);
}

// LessThanRule<T, TProperty> - 小于规则
public class LessThanRule<T, TProperty> : IValidationRule<T>
    where TProperty : IComparable<TProperty>;

// RangeRule<T, TProperty> - 范围规则
public class RangeRule<T, TProperty> : IValidationRule<T>
    where TProperty : IComparable<TProperty>
{
    public RangeRule(Expression<Func<T, TProperty>> property, TProperty min, TProperty max);
}

// LengthRule<T> - 字符串长度规则
public class LengthRule<T> : IValidationRule<T>
{
    public LengthRule(Expression<Func<T, string?>> property, int? minLength, int? maxLength);
}

// RegexRule<T> - 正则表达式规则
public class RegexRule<T> : IValidationRule<T>
{
    public RegexRule(Expression<Func<T, string?>> property, string pattern);
}

// EmailRule<T> - 邮箱格式规则
public class EmailRule<T> : IValidationRule<T>;

// CustomRule<T> - 自定义规则
public class CustomRule<T> : IValidationRule<T>
{
    public CustomRule(Expression<Func<T, object?>> property, Func<T, bool> validator, string errorMessage);
}
```

### FluentValidation 适配器

```csharp
// FluentValidationAdapter<T> - FluentValidation 适配器
public class FluentValidationAdapter<T> : IValidator<T>
{
    public FluentValidationAdapter(IValidator<T> validator);
}
```

---

## DataForge.Export

导出相关类型。

### CsvExportOptions

```csharp
public class CsvExportOptions
{
    /// <summary>
    /// 文件编码，默认 UTF8
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    
    /// <summary>
    /// 分隔符，默认逗号
    /// </summary>
    public char Separator { get; set; } = ',';
    
    /// <summary>
    /// 是否包含表头
    /// </summary>
    public bool IncludeHeader { get; set; } = true;
    
    /// <summary>
    /// 是否追加到文件
    /// </summary>
    public bool Append { get; set; } = false;
    
    /// <summary>
    /// 引号字符
    /// </summary>
    public char QuoteChar { get; set; } = '"';
    
    /// <summary>
    /// 何时使用引号：Always / AsNeeded / Never
    /// </summary>
    public QuoteMode QuoteMode { get; set; } = QuoteMode.AsNeeded;
    
    /// <summary>
    /// 空值输出为什么
    /// </summary>
    public string NullValue { get; set; } = "";
    
    /// <summary>
    /// 是否写入 BOM
    /// </summary>
    public bool WriteBom { get; set; } = true;
    
    /// <summary>
    /// 每批次写入的行数
    /// </summary>
    public int BatchSize { get; set; } = 10000;
}

public enum QuoteMode
{
    Always,     // 始终加引号
    AsNeeded,   // 按需加引号
    Never       // 从不加引号
}
```

### ExcelExportOptions

```csharp
public class ExcelExportOptions
{
    /// <summary>
    /// 工作表名称
    /// </summary>
    public string SheetName { get; set; } = "Sheet1";
    
    /// <summary>
    /// 是否包含表头
    /// </summary>
    public bool IncludeHeader { get; set; } = true;
    
    /// <summary>
    /// 起始行（从 1 开始）
    /// </summary>
    public int StartRow { get; set; } = 1;
    
    /// <summary>
    /// 是否自动调整列宽
    /// </summary>
    public bool AutoFitColumns { get; set; } = true;
    
    /// <summary>
    /// 批量写入的行数
    /// </summary>
    public int BatchSize { get; set; } = 1000;
    
    /// <summary>
    /// 表头样式
    /// </summary>
    public ExcelStyle? HeaderStyle { get; set; }
    
    /// <summary>
    /// 是否冻结首行
    /// </summary>
    public bool FreezeHeader { get; set; } = false;
}

public class ExcelStyle
{
    public string FontName { get; set; } = "微软雅黑";
    public int FontSize { get; set; } = 11;
    public bool Bold { get; set; } = false;
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string FontColor { get; set; } = "#000000";
    public string Alignment { get; set; } = "left"; // left, center, right
}
```

### JsonExportOptions

```csharp
public class JsonExportOptions
{
    /// <summary>
    /// JSON 格式化
    /// </summary>
    public bool Indented { get; set; } = true;
    
    /// <summary>
    /// 是否输出数组（false 则输出对象）
    /// </summary>
    public bool OutputArray { get; set; } = true;
    
    /// <summary>
    /// 根对象属性名
    /// </summary>
    public string? RootPropertyName { get; set; }
    
    /// <summary>
    /// 日期格式
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ss";
    
    /// <summary>
    /// 空值处理
    /// </summary>
    public NullValueHandling NullValueHandling { get; set; } = NullValueHandling.Ignore;
    
    /// <summary>
    /// 属性命名策略
    /// </summary>
    public NamingStrategy NamingStrategy { get; set; } = NamingStrategy.CamelCase;
}

public enum NullValueHandling
{
    Include,    // 包含空值
    Ignore      // 忽略空值
}

public enum NamingStrategy
{
    None,           // 保持原样
    CamelCase,      // 驼峰命名
    PascalCase,     // 帕斯卡命名
    SnakeCase,      // 蛇形命名
    KebabCase       // 短横线命名
}
```

### 导出结果类型

```csharp
// CsvExportResult
public class CsvExportResult
{
    public string FilePath { get; }
    public long RecordsWritten { get; }
    public TimeSpan Duration { get; }
    public long BytesWritten { get; }
}

// ExcelExportResult
public class ExcelExportResult
{
    public string FilePath { get; }
    public long RecordsWritten { get; }
    public TimeSpan Duration { get; }
    public int SheetsCreated { get; }
}

// JsonExportResult
public class JsonExportResult
{
    public string FilePath { get; }
    public long RecordsWritten { get; }
    public TimeSpan Duration { get; }
    public long BytesWritten { get; }
}

// DbExportResult
public class DbExportResult
{
    public string TableName { get; }
    public long SuccessCount { get; }
    public long FailedCount { get; }
    public long DuplicateCount { get; }
    public IReadOnlyList<WriteError> Errors { get; }
    public TimeSpan Duration { get; }
}

public class WriteError
{
    public object? Item { get; }
    public string ErrorMessage { get; }
    public Exception? Exception { get; }
}

// ConsoleExportResult
public class ConsoleExportResult
{
    public long RecordsWritten { get; }
    public TimeSpan Duration { get; }
}

// StreamExportResult
public class StreamExportResult
{
    public ExportFormat Format { get; }
    public long RecordsWritten { get; }
    public long BytesWritten { get; }
}

public enum ExportFormat
{
    Csv,
    Json,
    Excel
}
```

### 数据库导出选项

```csharp
public class SqlServerExportOptions
{
    /// <summary>
    /// 批量插入大小
    /// </summary>
    public int BatchSize { get; set; } = 1000;
    
    /// <summary>
    /// 是否自动创建表
    /// </summary>
    public bool AutoCreateTable { get; set; } = false;
    
    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int CommandTimeout { get; set; } = 300;
    
    /// <summary>
    /// 插入模式：Insert / Upsert
    /// </summary>
    public InsertMode InsertMode { get; set; } = InsertMode.Insert;
    
    /// <summary>
    /// Upsert 的键列
    /// </summary>
    public string[]? UpsertKeyColumns { get; set; }
    
    /// <summary>
    /// 是否启用事务
    /// </summary>
    public bool UseTransaction { get; set; } = true;
}

public enum InsertMode
{
    Insert,
    Upsert
}
```

---

## 错误处理 API

```csharp
// PipelineErrorHandling - 管道错误处理
public interface IPipelineErrorHandling
{
    /// <summary>
    /// 遇到错误时继续处理
    /// </summary>
    IDataPipeline<TIn, TOut> ContinueOnError();
    
    /// <summary>
    /// 遇到错误时停止处理
    /// </summary>
    IDataPipeline<TIn, TOut> StopOnError();
    
    /// <summary>
    /// 遇到错误时跳过并记录
    /// </summary>
    IDataPipeline<TIn, TOut> SkipOnError();
    
    /// <summary>
    /// 自定义错误处理
    /// </summary>
    IDataPipeline<TIn, TOut> OnError(Func<Exception, object?, ErrorAction> handler);
}

// ErrorAction - 错误处理动作
public enum ErrorAction
{
    Continue,   // 继续处理
    Skip,       // 跳过当前项
    Stop,       // 停止处理
    Throw       // 抛出异常
}
```

---

## 扩展方法

### IEnumerable 扩展

```csharp
// ToDataForge - 将 IEnumerable 转为 DataForge 管道
public static IDataPipeline<T, T> ToDataForge<T>(this IEnumerable<T> source);

// ToCsvAsync - 将 IEnumerable 导出为 CSV
public static Task<CsvExportResult> ToCsvAsync<T>(
    this IEnumerable<T> source,
    string filePath,
    CsvExportOptions? options = null);
```

### IAsyncEnumerable 扩展

```csharp
// ToDataForge - 将 IAsyncEnumerable 转为 DataForge 管道
public static IDataPipeline<T, T> ToDataForge<T>(this IAsyncEnumerable<T> source);

// ToListAsync - 收集到列表
public static async Task<IReadOnlyList<T>> ToListAsync<T>(
    this IAsyncEnumerable<T> source,
    CancellationToken cancellationToken = default);
```
