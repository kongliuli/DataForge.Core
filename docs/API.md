# DataForge.Core API 契约参考

> 版本：0.1.0 | 目标框架：net8.0 | 最后更新：2026-06-11

本文档描述 DataForge.Core 全部**公开 API 契约**。每个方法签名均为稳定接口，集成项目依赖此文档即可安全编码。

---

## 目录

1. [核心入口：DataForgePipeline](#1-核心入口dataforgepipeline)
2. [核心管道接口：IDataPipeline\<T\>](#2-核心管道接口idatapipelinet)
3. [分组管道：IGroupedDataPipeline\<TKey, T\>](#3-分组管道igroupeddatapipelinetkey-t)
4. [数据源接口体系](#4-数据源接口体系)
5. [数据目标接口与模型](#5-数据目标接口与模型)
6. [验证框架](#6-验证框架)
7. [转换器体系](#7-转换器体系)
8. [管道扩展方法](#8-管道扩展方法)
9. [性能工具](#9-性能工具)
10. [扩展包 API](#10-扩展包-api)
11. [关键枚举与错误处理](#11-关键枚举与错误处理)
12. [完整使用示例](#12-完整使用示例)

---

## 1. 核心入口：DataForgePipeline

**命名空间**: `DataForge.Core` | **程序集**: `DataForge.Core.dll`

| 方法 | 返回 | 说明 |
|------|------|------|
| `FromCsv<T>(string filePath, CsvSourceOptions? options = null)` | `IDataPipeline<T>` | 从 CSV 文件创建管道 |
| `FromJson<T>(string filePath, JsonSourceOptions? options = null)` | `IDataPipeline<T>` | 从 JSON 文件创建管道 |
| `FromExcel<T>(string filePath, string sheetName = "Sheet1")` | `IDataPipeline<T>` | 从 Excel 文件创建管道（sheetName 指定工作表） |
| `FromMemory<T>(IEnumerable<T> data)` | `IDataPipeline<T>` | 从内存集合创建管道 |
| `FromJsonString<T>(string jsonContent)` | `IDataPipeline<T>` | 从 JSON 字符串创建管道 |
| `FromJsonArray<T>(string filePath)` | `IDataPipeline<T>` | 等价于 `FromJson<T>(filePath)` |
| `Merge<T>(params IDataSource<T>[] sources)` | `IDataPipeline<T>` | 合并多个数据源顺序输出 |

> **设计说明**: `DataForgePipeline` 是非静态类，支持扩展方法。扩展包通过 `this DataForgePipeline` 注入 `FromXxx` 方法。

---

## 2. 核心管道接口：IDataPipeline\<T\>

**命名空间**: `DataForge.Core.Core.Pipeline` | **程序集**: `DataForge.Core.dll`

### 2.1 链式变换操作（返回 IDataPipeline，惰性）

| 方法签名 | 说明 |
|---------|------|
| `Select<TResult>(Func<T, TResult> selector)` | 映射每个元素到新类型 |
| `SelectAsync<TResult>(Func<T, Task<TResult>> selector)` | 异步映射 |
| `SelectMany<TResult>(Func<T, IEnumerable<TResult>> selector)` | 一对多展平映射 |
| `Where(Func<T, bool> predicate)` | 过滤元素 |
| `WhereAsync(Func<T, Task<bool>> predicate)` | 异步过滤 |
| `OrderBy<TKey>(Func<T, TKey> keySelector)` | 升序排序 |
| `OrderByDescending<TKey>(Func<T, TKey> keySelector)` | 降序排序 |
| `ThenBy<TKey>(Func<T, TKey> keySelector)` | 次级升序排序 |
| `ThenByDescending<TKey>(Func<T, TKey> keySelector)` | 次级降序排序 |
| `Distinct()` | 去重（基于默认相等比较器） |
| `DistinctBy<TKey>(Func<T, TKey> keySelector)` | 按指定键去重 |
| `Skip(int count)` | 跳过前 N 个元素 |
| `Take(int count)` | 取前 N 个元素 |
| `Batch(int batchSize)` | 批量分组为 `List<T>` 块 |
| `GroupBy<TKey>(Func<T, TKey> keySelector)` | 返回 `IGroupedDataPipeline<TKey, T>`（见第 3 节） |
| `Zip<TSecond>(IDataPipeline<TSecond> second)` | 将两个管道逐对合并为元组 |
| `TransformWith<TResult>(IDataTransform<T, TResult> transform)` | 使用自定义转换器 |
| `ValidateWith(IValidator<T> validator)` | 注册验证器（默认 FailOnValidationError） |
| `ContinueOnValidationError()` | 验证失败时过滤无效项继续 |
| `FailOnValidationError()` | 验证失败时抛 `ValidationException` |
| `OnErrorContinue()` | 异常时跳过继续 |
| `OnErrorStop()` | 异常时停止 |
| `OnErrorSkip()` | 异常时跳过 |
| `OnError(Func<Exception, T, ErrorAction> handler)` | 自定义错误处理 |

### 2.2 终端方法（触发执行，返回 Task）

| 方法签名 | 返回类型 | 说明 |
|---------|---------|------|
| `ToListAsync(CancellationToken ct)` | `Task<List<T>>` | 收集为列表 |
| `ToArrayAsync(CancellationToken ct)` | `Task<T[]>` | 收集为数组 |
| `FirstOrDefaultAsync(CancellationToken ct)` | `Task<T?>` | 获取第一个元素 |
| `CountAsync(CancellationToken ct)` | `Task<int>` | 计数 |
| `AnyAsync(CancellationToken ct)` | `Task<bool>` | 是否有任何元素 |
| `AggregateAsync<TResult>(Func<TResult,T,TResult> agg, TResult seed, CancellationToken ct)` | `Task<TResult>` | 聚合操作 |
| `AsAsyncEnumerable(CancellationToken ct)` | `IAsyncEnumerable<T>` | 暴露异步流 |

### 2.3 导出终端方法

| 方法签名 | 返回 | 说明 |
|---------|------|------|
| `ToCsvAsync(string filePath, CsvExportOptions? options, CancellationToken ct)` | `Task<ExportResults>` | 导出为 CSV |
| `ToJsonAsync(string filePath, JsonExportOptions? options, CancellationToken ct)` | `Task<ExportResults>` | 导出为 JSON |
| `ToExcelAsync(string filePath, ExcelExportOptions? options, CancellationToken ct)` | `Task<ExportResults>` | 导出为 Excel |
| `ToConsoleAsync(Func<T,string>? formatter, CancellationToken ct)` | `Task<ExportResults>` | 输出到控制台 |
| `ToStreamAsync(Stream stream, ExportFormat format, CancellationToken ct)` | `Task<ExportResults>` | 导出到任意流 |

---

## 3. 分组管道：IGroupedDataPipeline\<TKey, T\>

**命名空间**: `DataForge.Core.Core.Pipeline` | 约束: `TKey : notnull`

| 方法签名 | 返回类型 | 说明 |
|---------|---------|------|
| `Select<TResult>(Func<IGrouping<TKey,T>, TResult> selector)` | `IDataPipeline<TResult>` | 映射每个分组 |
| `OrderBy<TKey2>(Func<IGrouping<TKey,T>, TKey2> keySelector)` | `IDataPipeline<IGrouping<TKey,T>>` | 排序分组 |
| `OrderByDescending<TKey2>(...)` | `IDataPipeline<IGrouping<TKey,T>>` | 降序排序分组 |
| `ToListAsync(CancellationToken ct)` | `Task<List<IGrouping<TKey,T>>>` | 收集所有分组 |
| `ToDictionaryAsync(CancellationToken ct)` | `Task<Dictionary<TKey, List<T>>>` | 转为字典 |

---

## 4. 数据源接口体系

**命名空间**: `DataForge.Core.Core.Sources`

### 4.1 IDataSource\<T\>

```csharp
public interface IDataSource<T>
{
    string Name { get; }
    DataSourceType SourceType { get; }
    IAsyncEnumerable<T> ReadAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);
}
```

### 4.2 IFileDataSource\<T\>

```csharp
public interface IFileDataSource<T> : IDataSource<T>
{
    string FilePath { get; }
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
```

### 4.3 IRelationalDataSource\<T\>

```csharp
public interface IRelationalDataSource<T> : IDataSource<T>
{
    IAsyncEnumerable<T> QueryAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);
}
```

### 4.4 CsvSourceOptions

**命名空间**: `DataForge.Core.Core.Sources.Options`

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Delimiter` | `char` | `','` | 字段分隔符 |
| `QuoteChar` | `char` | `'"'` | 引用字符 |
| `Encoding` | `Encoding` | `UTF8` | 文件编码 |
| `HasHeaderRow` | `bool` | `true` | 首行是否为表头 |
| `SkipRows` | `int` | `0` | 跳过行数 |
| `MaxRows` | `int?` | `null` | 最大读取行数 |
| `CommentPrefix` | `string?` | `null` | 注释行前缀（如 `#`） |

### 4.5 DataSourceMetadata

**命名空间**: `DataForge.Core.Core.Models`

| 属性 | 类型 | 说明 |
|------|------|------|
| `SourceType` | `string` | 数据源类型名称 |
| `Location` | `string` | 数据源位置 |
| `Size` | `long` | 数据源大小 |
| `LastModified` | `DateTime?` | 最后修改时间 |
| `RecordCount` | `int?` | 记录数 |
| `AdditionalInfo` | `Dictionary<string,string>` | 额外元数据 |

---

## 5. 数据目标接口与模型

**命名空间**: `DataForge.Core.Core.Targets`

### 5.1 IDataTarget\<in T\>

```csharp
public interface IDataTarget<in T>
{
    string Name { get; }
    DataTargetType TargetType { get; }
    Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken ct = default);
    Task WriteAsync(T item, CancellationToken ct = default);
    Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken ct = default);
    Task CompleteAsync(CancellationToken ct = default);
}
```

### 5.2 ExportResults

| 属性 | 类型 | 说明 |
|------|------|------|
| `RecordsWritten` | `int` | 写入记录数 |
| `OutputPath` | `string` | 输出路径 |
| `OutputSize` | `long` | 输出文件大小 |
| `Duration` | `TimeSpan` | 耗时 |
| `Errors` | `List<string>` | 错误信息 |
| `HasErrors` | `bool` | 是否有错误 |

### 5.3 WriteResult / WriteError

| WriteResult 属性 | 类型 | 说明 |
|-----------------|------|------|
| `SuccessCount` | `int` | 成功条数 |
| `FailedCount` | `int` | 失败条数 |
| `Errors` | `List<WriteError>` | 错误列表 |

| WriteError 属性 | 类型 | 说明 |
|----------------|------|------|
| `Item` | `object?` | 出错的条目 |
| `Error` | `string` | 错误信息 |

### 5.4 导出选项

**CsvExportOptions**（`DataForge.Core.Core.Targets`）

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Delimiter` | `char` | `','` | 字段分隔符 |
| `QuoteChar` | `char` | `'"'` | 引用字符 |
| `Encoding` | `Encoding` | `UTF8` | 输出编码 |
| `IncludeHeader` | `bool` | `true` | 包含表头 |
| `BatchSize` | `int?` | `null` | 批写入大小 |

**JsonExportOptions**

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Indented` | `bool` | `true` | 缩进格式 |
| `RootPropertyName` | `string?` | `null` | 根节点属性名 |

**ExcelExportOptions**

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `SheetName` | `string` | `"Sheet1"` | 工作表名 |
| `FreezeFirstRow` | `bool` | `true` | 冻结首行 |
| `AutoSizeColumns` | `bool` | `true` | 自适应列宽 |
| `IncludeHeader` | `bool` | `true` | 包含表头 |

### 5.5 ExportFormat 枚举

```csharp
public enum ExportFormat { Csv, Json, Excel }
```

---

## 6. 验证框架

**命名空间**: `DataForge.Core.Core.Validation`

### 6.1 IValidator\<in T\>

```csharp
public interface IValidator<in T>
{
    ValidationResult Validate(T item);
    Task<ValidationResult> ValidateAsync(T item, CancellationToken ct = default);
}
```

### 6.2 ValidationResult

| 成员 | 类型 | 说明 |
|------|------|------|
| `IsValid` | `bool` | 验证是否通过（`Errors.Count == 0`） |
| `Errors` | `List<ValidationError>` | 错误列表 |
| `Success()` | `static ValidationResult` | 创建成功结果 |
| `Failure(IEnumerable<ValidationError>)` | `static ValidationResult` | 创建失败结果 |

### 6.3 ValidationError

| 属性 | 类型 | 说明 |
|------|------|------|
| `PropertyName` | `string` | 属性名 |
| `Message` | `string` | 错误消息 |
| `ErrorCode` | `ValidationErrorCode` | 错误码枚举 |
| `Severity` | `ValidationSeverity` | 严重程度（默认 Error） |

| 静态工厂方法 | 说明 |
|-------------|------|
| `Required(string propertyName)` | 必填错误 |
| `InvalidFormat(string propertyName)` | 格式错误 |
| `Custom(string propertyName, string message)` | 自定义错误 |

### 6.4 ValidationErrorCode 枚举

```csharp
public enum ValidationErrorCode { Required, InvalidFormat, Range, Length, Custom }
```

### 6.5 ValidationSeverity 枚举

```csharp
public enum ValidationSeverity { Warning, Error, Critical }
```

### 6.6 ValidationException

```csharp
public class ValidationException : Exception
{
    public List<ValidationError> Errors { get; }
    public ValidationException(List<ValidationError> errors)
}
```

### 6.7 DataValidator\<T\> 抽象基类

```csharp
public abstract class DataValidator<T> : IValidator<T>
{
    protected ValidationRuleBuilder<T, TProperty> RuleFor<TProperty>(
        Expression<Func<T, TProperty>> propertyExpression);
    protected void AddRule(IValidationRule<T> rule);
    public ValidationResult Validate(T instance);
    public Task<ValidationResult> ValidateAsync(T instance, CancellationToken ct);
}
```

### 6.8 ValidationRuleBuilder\<T, TProperty\>

链式规则构建器，常用方法：

| 方法 | 说明 |
|------|------|
| `Required()` | 非空且（如为字符串）非空白 |
| `NotEmpty()` | 非 null 且非空集合/空字符串 |
| `Length(int min, int max)` | 字符串长度范围检查 |
| `MinLength(int minLength)` | 最小长度 |
| `InRange(TProperty min, TProperty max)` | 数值范围检查 |
| `GreaterThan(TProperty threshold)` | 大于阈值 |
| `GreaterThanOrEqualTo(TProperty threshold)` | 大于等于 |
| `LessThan(TProperty threshold)` | 小于阈值 |
| `Must(Func<TProperty, bool> predicate)` | 自定义条件 |
| `When(Func<T, bool> condition)` | 条件性验证（仅当条件为 true） |
| `WithSeverity(ValidationSeverity severity)` | 设置严重级别 |
| `WithMessage(string message)` | 覆盖错误消息 |

---

## 7. 转换器体系

**命名空间**: `DataForge.Core.Core.Transforms`

### 7.1 核心接口

```csharp
public interface IDataTransform<in TSource, out TResult>
{
    TResult Transform(TSource source);
}

public interface IAsyncDataTransform<in TSource, TResult>
{
    Task<TResult> TransformAsync(TSource source);
}
```

### 7.2 预置转换器速查表

| 类名 | 签名 | 说明 |
|------|------|------|
| `MapTransform<TIn,TOut>` | `MapTransform(Func<TIn,TOut>)` | 通用映射 |
| `AsyncMapTransform<TIn,TOut>` | `AsyncMapTransform(Func<TIn,Task<TOut>>)` | 异步映射 |
| `CastTransform<TIn,TOut>` | — | 类型转换（`TOut : TIn`） |
| `DefaultIfEmptyTransform<T>` | `DefaultIfEmptyTransform(T defaultValue)` | null 替换为默认值 |
| `SkipNullsTransform<T>` | — | 跳过 null（class 约束） |
| `DateFormatTransform` | `DateFormatTransform(string format = "yyyy-MM-dd")` | `DateTime → string` |
| `NumberFormatTransform` | `NumberFormatTransform(string format = "N2")` | `decimal → string` |
| `BoolFormatTransform` | `BoolFormatTransform(string true = "是", string false = "否")` | `bool → string` |
| `TrimTransform` | `TrimTransform(bool removeEmpty = false)` | 字符串修剪 |
| `ToCaseTransform` | `ToCaseTransform(bool toUpper = true)` | 大小写转换 |
| `ComputeTransform<TIn,TOut>` | `ComputeTransform(Func<TIn,TOut>)` | 计算字段 |
| `ConditionalTransform<TIn,TOut>` | `ConditionalTransform(predicate, trueMapper, falseMapper)` | 条件转换 |
| `LookupTransform<TKey,TValue>` | `LookupTransform(Dictionary<TKey,TValue>)` | 字典查找 |
| `ComposeTransform<TIn,TMid,TOut>` | `ComposeTransform(first, second)` | 组合两个转换器 |

---

## 8. 管道扩展方法

**命名空间**: `DataForge.Core.Core.Pipeline` | 来源：`PerformanceExtensions`

### 8.1 进度与性能

| 扩展方法 | 说明 |
|---------|------|
| `WithProgress<T>(Action<ProgressReport<T>> handler, int interval = 1000)` | 每 N 条回调进度报告 |
| `WithCounter<T>(PerformanceCounter counter)` | 附加性能计数器（开始/结束/计数） |
| `WithParallelization<T>(int maxDegree = 4)` | 并行处理，保留顺序 |
| `WithParallelizationUnordered<T>(int maxDegree = 4)` | 并行处理，不保留顺序 |

### 8.2 ProgressReport\<T\>

| 属性 | 类型 | 说明 |
|------|------|------|
| `CurrentItem` | `T?` | 当前处理的元素 |
| `ProcessedCount` | `long` | 已处理数量 |
| `ItemsPerSecond` | `double` | 处理速度 |
| `ProgressPercentage` | `double` | 进度百分比 |

---

## 9. 性能工具

**命名空间**: `DataForge.Core.Core.Infrastructure`

### 9.1 PerformanceCounter

| 成员 | 类型 | 说明 |
|------|------|------|
| `TotalItems` | `long` | 总条目数 |
| `ProcessedItems` | `long` | 已处理数 |
| `StartTime` | `DateTime` | 开始时间 |
| `EndTime` | `DateTime?` | 结束时间 |
| `Elapsed` | `TimeSpan` | 已耗时 |
| `ItemsPerSecond` | `double` | 处理速度 |
| `ProgressPercentage` | `double` | 进度百分比 |
| `Start()` | `void` | 启动计数 |
| `Stop()` | `void` | 停止计数 |
| `Reset()` | `void` | 重置 |
| `IncrementProcessed(long count = 1)` | `void` | 递增处理数（线程安全） |
| `SetTotalItems(long total)` | `void` | 设置总数（线程安全） |
| `RecordSnapshot()` | `void` | 记录快照 |
| `GetSnapshots()` | `IReadOnlyList<PerformanceSnapshot>` | 获取所有快照 |
| `GetLatestSnapshot()` | `PerformanceSnapshot` | 获取最新快照 |

### 9.2 其他工具

| 类 | 说明 |
|----|------|
| `BatchOptimizer<T>` | 批量缓冲写入器 |
| `ParallelProcessingOptions` | 并行处理配置 |
| `ProgressReporter<T>` | 进度报告器 |
| `AsyncBuffer<T>` | 异步队列缓冲区 |
| `MemoryOptimizer` | 内存优化工具（GC 管理、内存压力检测） |

---

## 10. 扩展包 API

### 10.1 DataForge.Core.Excel

| 扩展方法 | 说明 |
|---------|------|
| `FromExcel<T>(this DataForgePipeline, string filePath, ExcelSourceOptions?)` | 从 Excel 文件创建管道 |
| `ToExcel<T>(this IDataPipeline<T>, string filePath, ExcelExportOptions?)` | 导出到 Excel |

**ExcelSourceOptions** (`DataForge.Core.Excel`): 包含 `SheetName`、`HeaderRow`、`StartRow` 等配置。

### 10.2 DataForge.Core.Json

| 扩展方法 | 说明 |
|---------|------|
| `FromJson<T>(this DataForgePipeline, string filePath, JsonSourceOptions?)` | 从 JSON 文件创建管道 |
| `ToJson<T>(this IDataPipeline<T>, string filePath, JsonExportOptions?)` | 导出到 JSON |

**JsonSourceOptions** (`DataForge.Core.Json`): 包含 `RootElementName` 等 JSON 读取配置。

### 10.3 DataForge.Core.Http

| 扩展方法 | 说明 |
|---------|------|
| `FromRestApi<T>(this DataForgePipeline, string baseUrl, string endpoint, RestApiSourceOptions?)` | 从 REST API 读取 |
| `FromRestApi<T>(this DataForgePipeline, HttpClient, string endpoint, RestApiSourceOptions?)` | 从 REST API 读取（自定义 HttpClient） |
| `FromRestApiWithPagination<T>(this DataForgePipeline, string baseUrl, string endpoint, RestApiSourceOptions?)` | 自动分页读取 |
| `ToRestApi<T>(this IDataPipeline<T>, string baseUrl, string endpoint, RestApiTargetOptions?)` | 写入 REST API |
| `ToRestApi<T>(this IDataPipeline<T>, HttpClient, string endpoint, RestApiTargetOptions?)` | 写入 REST API（自定义 HttpClient） |

**RestApiSourceOptions**: 包含 `BaseUrl`、`Endpoint`、`HttpMethod`、`Headers`、`PageSize` 等。

**RestApiTargetOptions**: 包含 `BaseUrl`、`Endpoint`、`HttpMethod`、`Headers`、`BatchSize` 等。

### 10.4 数据库扩展包

| 包 | 命名空间 | 典型用法 |
|----|---------|---------|
| `DataForge.Core.SqlServer` | `DataForge.Core.SqlServer` | `FromSqlServer<T>()` / `ToSqlServer<T>()` |
| `DataForge.Core.MySql` | `DataForge.Core.MySql` | `FromMySql<T>()` / `ToMySql<T>()` |
| `DataForge.Core.Sqlite` | `DataForge.Core.Sqlite` | `FromSqlite<T>()` / `ToSqlite<T>()` |
| `DataForge.Core.FluentValidation` | `DataForge.Core.FluentValidation` | FluentValidation 验证器集成 |

---

## 11. 关键枚举与错误处理

### 11.1 DataSourceType

```csharp
namespace DataForge.Core.Core.Infrastructure;
public enum DataSourceType { Csv, Json, Excel, SqlServer, MySql, Sqlite, Memory, RestApi, Custom }
```

### 11.2 DataTargetType

```csharp
public enum DataTargetType { Csv, Json, Excel, SqlServer, MySql, Sqlite, Console, Stream, RestApi, Custom }
```

### 11.3 ErrorAction / PipelineErrorContext

```csharp
public enum ErrorAction { Continue, Skip, Stop, Throw }

public class PipelineErrorContext
{
    public Exception Exception { get; init; }
    public object? Item { get; init; }
    public int ItemIndex { get; init; }
    public string PipelineStage { get; init; }
}
```

---

## 12. 完整使用示例

### 12.1 基本管道：CSV → 过滤 → JSON

```csharp
using DataForge.Core;

var results = await DataForgePipeline
    .FromCsv<Product>("input.csv")
    .Where(p => p.Price > 100)
    .OrderByDescending(p => p.Price)
    .ToJsonAsync("output.json");

Console.WriteLine($"导出了 {results.RecordsWritten} 条记录，耗时 {results.Duration}");
```

### 12.2 带验证的管道

```csharp
var validator = new ProductValidator(); // 继承 DataValidator<Product>

var results = await DataForgePipeline
    .FromCsv<Product>("input.csv", new CsvSourceOptions { HasHeaderRow = true })
    .ValidateWith(validator)
    .ContinueOnValidationError()  // 无效项过滤掉继续
    .ToExcelAsync("valid_products.xlsx", new ExcelExportOptions
    {
        SheetName = "Products",
        AutoSizeColumns = true
    });
```

### 12.3 性能监控管道

```csharp
var counter = new PerformanceCounter();

var data = await DataForgePipeline
    .FromCsv<SaleRecord>("sales.csv")
    .WithProgress(report =>
    {
        Console.WriteLine($"已处理 {report.ProcessedCount} 条，速度 {report.ItemsPerSecond:F1}/s");
    }, reportInterval: 5000)
    .WithCounter(counter)
    .Where(s => s.Amount > 0)
    .ToListAsync();

Console.WriteLine($"总计处理 {counter.ProcessedItems} 条，平均速度 {counter.ItemsPerSecond:F1}/s");
```

### 12.4 自定义验证器

```csharp
public class ProductValidator : DataValidator<Product>
{
    public ProductValidator()
    {
        RuleFor(p => p.Name)
            .Required()
            .Length(1, 200);

        RuleFor(p => p.Price)
            .NotEmpty()
            .GreaterThan(0)
            .WithMessage("价格必须大于 0");

        RuleFor(p => p.Email)
            .Must(email => email.Contains('@'))
            .When(p => !string.IsNullOrEmpty(p.Email))
            .WithSeverity(ValidationSeverity.Warning);
    }
}
```

### 12.5 分组与聚合

```csharp
var grouped = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .GroupBy(o => o.CustomerId)
    .Select(g => new CustomerSummary
    {
        CustomerId = g.Key,
        TotalOrders = g.Count(),
        TotalAmount = g.Sum(o => o.Amount)
    })
    .OrderByDescending(s => s.TotalAmount)
    .ToListAsync();
```

### 12.6 合并多数据源

```csharp
var source1 = new CsvSource<Product>("products_2024.csv");
var source2 = new CsvSource<Product>("products_2025.csv");

var allProducts = await DataForgePipeline
    .Merge(source1, source2)
    .DistinctBy(p => p.SKU)
    .ToListAsync();
```

### 12.7 REST API 数据源（需引入 DataForge.Core.Http）

```csharp
using DataForge.Core.Http;

var users = await new DataForgePipeline()
    .FromRestApi<User>(
        "https://api.example.com",
        "/v1/users",
        new RestApiSourceOptions { PageSize = 100 }
    )
    .Where(u => u.IsActive)
    .ToListAsync();
```

### 12.8 混合管道：Memory → 转换 → 多输出

```csharp
var source = new List<Employee>
{
    new() { Name = "张三", Salary = 15000, Department = "Engineering" },
    new() { Name = "李四", Salary = 12000, Department = "Marketing" },
};

var pipeline = DataForgePipeline
    .FromMemory(source)
    .Where(e => e.Salary > 10000)
    .Select(e => new { e.Name, e.Department, AnnualSalary = e.Salary * 12 });

await Task.WhenAll(
    pipeline.ToCsvAsync("employees.csv"),
    pipeline.ToJsonAsync("employees.json"),
    pipeline.ToConsoleAsync(e => $"{e.Name,-10} {e.Department,-15} {e.AnnualSalary:C}")
);
```
