# 架构设计文档

本文档详细描述 DataForge.Core 的核心架构设计，包括核心抽象层、接口定义、扩展点设计和依赖关系。后续开发将严格遵循此文档执行。

> **迭代规划**：版本路线图、调研汇总、P0 问题清单见 [roadmap-and-iteration.md](./roadmap-and-iteration.md)。

## 目录

1. [整体架构](#整体架构)
2. [核心抽象层设计](#核心抽象层设计)
3. [核心接口定义](#核心接口定义)
4. [接口关系图](#接口关系图)
5. [扩展点设计](#扩展点设计)
6. [依赖关系](#依赖关系)
7. [错误处理策略](#错误处理策略)
8. [异步编程模型](#异步编程模型)

---

## 整体架构

### 架构分层图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            入口层 (Entry Layer)                         │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                    DataForgePipeline (静态入口类)                   │ │
│  │         FromCsv | FromExcel | FromJson | FromSqlServer | ...        │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          数据源层 (Source Layer)                         │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐  │
│  │   IDataSource<T>  │  │  IRelationalSource │  │  IFileSource<T>     │  │
│  │    (数据源接口)    │  │   (关系型数据源)   │  │    (文件数据源)     │  │
│  └──────────────────┘  └──────────────────┘  └──────────────────────┘  │
│                                                                          
│  实现：SqlServerSource | MySqlSource | SqliteSource | CsvSource |        │
│        ExcelSource | JsonSource | MemorySource | RestApiSource          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          管道层 (Pipeline Layer)                         │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                  IDataPipeline<TIn, TOut>                           │ │
│  │                                                                       │ │
│  │   数据处理管道的核心抽象，支持链式调用和延迟执行                        │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                                                          
│  管道操作：Where | Select | OrderBy | GroupBy | Distinct | Take | Skip  │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        转换层 (Transform Layer)                          │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐    │
│  │  ISingleTransform │  │ IGroupTransform  │  │ IAggregateTransform  │    │
│  │   (单条转换器)    │  │    (分组转换)    │  │     (聚合转换)       │    │
│  └──────────────────┘  └──────────────────┘  └──────────────────────┘    │
│                                                                          
│  实现：SelectTransform | WhereTransform | OrderByTransform |             │
│        GroupByTransform | AggregateTransform | DistinctTransform        │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        验证层 (Validation Layer)                         │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                       IValidator<T>                                  │ │
│  │                                                                       │ │
│  │   数据验证抽象，支持单条验证和批量验证                                 │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                                                          
│  实现：DataValidator<T> (基类) | FluentValidationAdapter                  │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          目标层 (Target Layer)                           │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐    │
│  │   IDataTarget<T>  │  │ IRelationalTarget │  │   IFileTarget<T>    │    │
│  │    (数据目标接口)  │  │    (关系型目标)   │  │     (文件目标)      │    │
│  └──────────────────┘  └──────────────────┘  └──────────────────────┘    │
│                                                                          
│  实现：SqlServerTarget | MySqlTarget | CsvTarget | ExcelTarget |         │
│        JsonTarget | ConsoleTarget | StreamTarget                        │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        公共设施层 (Infrastructure)                       │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐    │
│  │  ITypeConverter  │  │   ILogProvider   │  │    IErrorHandler    │    │
│  │   (类型转换器)    │  │    (日志提供器)   │  │    (错误处理器)     │    │
│  └──────────────────┘  └──────────────────┘  └──────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────┘
```

### ASCII 架构流程图

```
                    ┌──────────────┐
                    │     用户     │
                    └──────┬───────┘
                           │
                           ▼
               ┌────────────────────────┐
               │   DataForgePipeline   │
               │       (入口类)        │
               └───────────┬───────────┘
                           │
                           ▼
              ┌─────────────────────────┐
              │      IDataSource<T>     │
              │   数据源 (可组合多个)    │
              └───────────┬─────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │  IDataPipeline<TIn,TOut>│
              │                         │
              │  ┌───────────────────┐  │
              │  │  验证器 (可选)    │  │
              │  └───────────────────┘  │
              │  ┌───────────────────┐  │
              │  │   转换器 (多个)   │  │
              │  └───────────────────┘  │
              │                         │
              └───────────┬─────────────┘
                          │
                          ▼
              ┌─────────────────────────┐
              │     IDataTarget<T>       │
              │      数据目标            │
              └─────────────────────────┘
```

---

## 核心抽象层设计

### 1. IDataSource<T> — 数据源接口

数据源是数据的入口点，负责从各种来源读取数据。

```csharp
/// <summary>
/// 定义数据源的抽象接口
/// </summary>
/// <typeparam name="T">数据源输出的数据类型</typeparam>
public interface IDataSource<T>
{
    /// <summary>
    /// 数据源名称，用于日志和错误信息
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 源数据类型
    /// </summary>
    DataSourceType SourceType { get; }
    
    /// <summary>
    /// 异步枚举数据流
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据对象的异步流</returns>
    IAsyncEnumerable<T> ReadAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 读取所有数据到内存集合
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据集合</returns>
    Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取数据源的元数据信息
    /// </summary>
    DataSourceMetadata GetMetadata();
}
```

### 2. IDataTarget<T> — 数据目标接口

数据目标是数据的出口点，负责将数据写入各种目标。

```csharp
/// <summary>
/// 定义数据目标的抽象接口
/// </summary>
/// <typeparam name="T">写入数据的类型</typeparam>
public interface IDataTarget<T>
{
    /// <summary>
    /// 目标名称，用于日志和错误信息
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 目标数据类型
    /// </summary>
    DataTargetType TargetType { get; }
    
    /// <summary>
    /// 写入单条数据
    /// </summary>
    /// <param name="item">要写入的数据项</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(T item, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 批量写入数据
    /// </summary>
    /// <param name="items">要写入的数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入结果</returns>
    Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 完成写入操作，释放资源
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task CompleteAsync(CancellationToken cancellationToken = default);
}
```

### 3. IDataPipeline<TIn, TOut> — 处理管道接口

管道是数据处理的核心抽象，支持链式调用。

```csharp
/// <summary>
/// 数据处理管道的核心抽象接口
/// </summary>
/// <typeparam name="TIn">管道输入类型</typeparam>
/// <typeparam name="TOut">管道输出类型</typeparam>
public interface IDataPipeline<TIn, TOut> : IAsyncEnumerable<TOut>
{
    /// <summary>
    /// 管道名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 当前管道的执行计划描述
    /// </summary>
    string ExecutionPlan { get; }
    
    /// <summary>
    /// 获取异步枚举器
    /// </summary>
    new IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken cancellationToken = default);
    
    // ============ 转换操作 ============
    
    /// <summary>
    /// 过滤数据
    /// </summary>
    /// <param name="predicate">过滤条件</param>
    IDataPipeline<TIn, TOut> Where(Func<TOut, bool> predicate);
    
    /// <summary>
    /// 异步过滤数据
    /// </summary>
    /// <param name="predicate">过滤条件</param>
    IDataPipeline<TIn, TOut> WhereAsync(Func<TOut, Task<bool>> predicate);
    
    /// <summary>
    /// 选择/投影数据
    /// </summary>
    /// <param name="selector">选择器函数</param>
    IDataPipeline<TIn, TResult> Select<TResult>(Func<TOut, TResult> selector);
    
    /// <summary>
    /// 异步选择/投影数据
    /// </summary>
    /// <param name="selector">选择器函数</param>
    IDataPipeline<TIn, TResult> SelectAsync<TResult>(Func<TOut, Task<TResult>> selector);
    
    /// <summary>
    /// 按指定键去重
    /// </summary>
    /// <param name="keySelector">键选择器</param>
    IDataPipeline<TIn, TOut> DistinctBy<K>(Func<TOut, K> keySelector);
    
    /// <summary>
    /// 排序（升序）
    /// </summary>
    /// <param name="keySelector">排序键选择器</param>
    IDataPipeline<TIn, TOut> OrderBy<K>(Func<TOut, K> keySelector);
    
    /// <summary>
    /// 排序（降序）
    /// </summary>
    /// <param name="keySelector">排序键选择器</param>
    IDataPipeline<TIn, TOut> OrderByDescending<K>(Func<TOut, K> keySelector);
    
    /// <summary>
    /// 多级排序
    /// </summary>
    IDataPipeline<TIn, TOut> ThenBy<K>(Func<TOut, K> keySelector);
    
    /// <summary>
    /// 多级降序排序
    /// </summary>
    IDataPipeline<TIn, TOut> ThenByDescending<K>(Func<TOut, K> keySelector);
    
    /// <summary>
    /// 分页 - 跳过
    /// </summary>
    /// <param name="count">跳过的数量</param>
    IDataPipeline<TIn, TOut> Skip(int count);
    
    /// <summary>
    /// 分页 - 取
    /// </summary>
    /// <param name="count">取出的数量</param>
    IDataPipeline<TIn, TOut> Take(int count);
    
    // ============ 聚合操作 ============
    
    /// <summary>
    /// 分组
    /// </summary>
    /// <typeparam name="TKey">分组键类型</typeparam>
    /// <param name="keySelector">分组键选择器</param>
    IGroupedPipeline<TIn, TOut, TKey> GroupBy<TKey>(Func<TOut, TKey> keySelector);
    
    // ============ 验证操作 ============
    
    /// <summary>
    /// 添加验证器
    /// </summary>
    /// <param name="validator">验证器实例</param>
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
    
    // ============ 执行操作 ============
    
    /// <summary>
    /// 执行管道并收集所有结果到列表
    /// </summary>
    Task<IReadOnlyList<TOut>> ToListAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行管道并返回第一个元素
    /// </summary>
    Task<TOut?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行管道并返回元素数量
    /// </summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行管道并判断是否存在满足条件的元素
    /// </summary>
    Task<bool> AnyAsync(Func<TOut, bool> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行管道并返回聚合值
    /// </summary>
    Task<TResult> AggregateAsync<TResult>(Func<TResult, TOut, TResult> aggregator, TResult seed);
    
    // ============ 导出操作 ============
    
    /// <summary>
    /// 导出到 CSV 文件
    /// </summary>
    Task<CsvExportResult> ToCsv(string filePath, CsvExportOptions? options = null);
    
    /// <summary>
    /// 导出到 Excel 文件
    /// </summary>
    Task<ExcelExportResult> ToExcel(string filePath, ExcelExportOptions? options = null);
    
    /// <summary>
    /// 导出到 JSON 文件
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

### 4. IDataTransform<TIn, TOut> — 转换器接口

转换器是管道中数据处理的基本单元。

```csharp
/// <summary>
/// 数据转换器的抽象接口
/// </summary>
/// <typeparam name="TIn">输入类型</typeparam>
/// <typeparam name="TOut">输出类型</typeparam>
public interface IDataTransform<TIn, TOut>
{
    /// <summary>
    /// 转换器名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 优先级，数值越小越先执行
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// 转换单条数据
    /// </summary>
    /// <param name="input">输入数据</param>
    /// <returns>输出数据（可为 null 表示过滤）</returns>
    TOut? Transform(TIn input);
    
    /// <summary>
    /// 异步转换单条数据
    /// </summary>
    /// <param name="input">输入数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TOut?> TransformAsync(TIn input, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 批量转换
    /// </summary>
    /// <param name="inputs">输入数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<TOut> TransformBatchAsync(IEnumerable<TIn> inputs, CancellationToken cancellationToken = default);
}

/// <summary>
/// 分组转换器接口
/// </summary>
/// <typeparam name="TIn">输入类型</typeparam>
/// <typeparam name="TOut">输出类型</typeparam>
/// <typeparam name="TKey">分组键类型</typeparam>
public interface IGroupedTransform<TIn, TOut, TKey>
{
    /// <summary>
    /// 分组键选择器
    /// </summary>
    Func<TIn, TKey> KeySelector { get; }
    
    /// <summary>
    /// 对每个分组执行转换
    /// </summary>
    IAsyncEnumerable<IGroupResult<TKey, TOut>> TransformGroupsAsync(
        IAsyncEnumerable<TIn> input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 分组结果接口
/// </summary>
/// <typeparam name="TKey">分组键类型</typeparam>
/// <typeparam name="TValue">分组值类型</typeparam>
public interface IGroupResult<TKey, TValue>
{
    TKey Key { get; }
    IReadOnlyList<TValue> Items { get; }
}
```

### 5. IValidator<T> — 验证器接口

验证器确保数据质量。

```csharp
/// <summary>
/// 数据验证器的抽象接口
/// </summary>
/// <typeparam name="T">要验证的数据类型</typeparam>
public interface IValidator<T>
{
    /// <summary>
    /// 验证器名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 验证规则集合
    /// </summary>
    IReadOnlyList<IValidationRule<T>> Rules { get; }
    
    /// <summary>
    /// 验证单个实例
    /// </summary>
    /// <param name="instance">要验证的实例</param>
    /// <returns>验证结果</returns>
    ValidationResult Validate(T instance);
    
    /// <summary>
    /// 异步验证单个实例
    /// </summary>
    /// <param name="instance">要验证的实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 批量验证
    /// </summary>
    /// <param name="instances">要验证的实例集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<ValidationResult> ValidateBatchAsync(
        IEnumerable<T> instances, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 验证规则接口
/// </summary>
/// <typeparam name="T">要验证的类型</typeparam>
public interface IValidationRule<T>
{
    /// <summary>
    /// 规则名称
    /// </summary>
    string RuleName { get; }
    
    /// <summary>
    /// 属性选择器
    /// </summary>
    Expression<Func<T, object?>> PropertySelector { get; }
    
    /// <summary>
    /// 执行验证
    /// </summary>
    ValidationError? Validate(T instance);
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 是否验证通过
    /// </summary>
    public bool IsValid { get; }
    
    /// <summary>
    /// 验证错误列表
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }
    
    /// <summary>
    /// 关联的验证数据
    /// </summary>
    public object? Data { get; init; }
}

/// <summary>
/// 验证错误详情
/// </summary>
public class ValidationError
{
    /// <summary>
    /// 错误所属属性
    /// </summary>
    public string PropertyName { get; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; }
    
    /// <summary>
    /// 错误代码
    /// </summary>
    public string ErrorCode { get; }
    
    /// <summary>
    /// 严重级别
    /// </summary>
    public ValidationSeverity Severity { get; init; }
}

public enum ValidationSeverity
{
    Warning,
    Error,
    Critical
}
```

### 6. ITypeConverter — 类型转换器接口

类型转换器处理数据类型的自动转换。

```csharp
/// <summary>
/// 类型转换器接口
/// </summary>
public interface ITypeConverter
{
    /// <summary>
    /// 是否可以从源类型转换到目标类型
    /// </summary>
    bool CanConvert(Type sourceType, Type targetType);
    
    /// <summary>
    /// 转换值
    /// </summary>
    object? Convert(object? value, Type targetType);
    
    /// <summary>
    /// 转换值（泛型版本）
    /// </summary>
    TResult? Convert<TResult>(object? value);
}

/// <summary>
/// 默认类型转换器
/// </summary>
public class DefaultTypeConverter : ITypeConverter
{
    /// <inheritdoc/>
    public bool CanConvert(Type sourceType, Type targetType)
    {
        // 支持标准类型转换
        if (sourceType == targetType) return true;
        if (targetType.IsAssignableFrom(sourceType)) return true;
        
        // 数值类型
        if (IsNumericType(sourceType) && IsNumericType(targetType)) return true;
        
        // 字符串与其他类型
        if (sourceType == typeof(string)) return true;
        if (targetType == typeof(string)) return true;
        
        // 日期类型
        if (IsDateType(sourceType) && IsDateType(targetType)) return true;
        if (sourceType == typeof(string) && IsDateType(targetType)) return true;
        
        // 布尔类型
        if (sourceType == typeof(string) && targetType == typeof(bool)) return true;
        
        // 可空类型
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return CanConvert(sourceType, Nullable.GetUnderlyingType(targetType)!);
        }
        
        return false;
    }
    
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType)
    {
        if (value == null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            targetType = Nullable.GetUnderlyingType(targetType)!;
        }
        
        if (value.GetType() == targetType) return value;
        
        // 根据目标类型进行转换
        if (targetType == typeof(string)) return value.ToString();
        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(decimal)) return Convert.ToDecimal(value);
        if (targetType == typeof(DateTime)) return Convert.ToDateTime(value);
        if (targetType == typeof(bool)) return bool.Parse(value.ToString()!);
        
        return Convert.ChangeType(value, targetType);
    }
    
    /// <inheritdoc/>
    public TResult? Convert<TResult>(object? value)
    {
        return (TResult?)Convert(value, typeof(TResult));
    }
}
```

### 7. IGroupedPipeline — 分组管道接口

```csharp
/// <summary>
/// 分组管道接口，继承自 IDataPipeline
/// </summary>
public interface IGroupedPipeline<TIn, TElement, TKey> : IDataPipeline<TIn, IGroupResult<TKey, TElement>>
{
    /// <summary>
    /// 对每个分组执行聚合选择
    /// </summary>
    IDataPipeline<TIn, TResult> Select<TResult>(Func<IGroupResult<TKey, TElement>, TResult> selector);
    
    /// <summary>
    /// 对每个分组计数
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, int>> Count();
    
    /// <summary>
    /// 对每个分组求和
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, decimal>> Sum<TValue>(Func<TElement, TValue> valueSelector)
        where TValue : struct;
    
    /// <summary>
    /// 对每个分组计算平均值
    /// </summary>
    IDataPipeline<TIn, KeyValuePair<TKey, double>> Average<TValue>(Func<TElement, TValue> valueSelector)
        where TValue : struct;
}
```

---

## 接口关系图

```
                              ┌─────────────────────┐
                              │   IAsyncEnumerable  │
                              │      (系统接口)      │
                              └──────────┬──────────┘
                                         │ 实现
                                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        核心接口继承关系                                   │
└─────────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         IDataPipeline<TIn, TOut>                         │
│                                ▲                                        │
│         ┌──────────────────────┴──────────────────────┐                 │
│         │                      │                      │                  │
│         ▼                      ▼                      ▼                  │
│  ┌─────────────┐       ┌─────────────┐       ┌─────────────────┐        │
│  │ SelectTransform│     │ WhereTransform│     │OrderByTransform │        │
│  └─────────────┘       └─────────────┘       └─────────────────┘        │
│                                                                         │
│                          ▲                                              │
│                          │ 继承                                          │
│                          │                                              │
│         ┌────────────────┴────────────────┐                            │
│         ▼                                 ▼                            │
│  ┌─────────────────┐              ┌─────────────────┐                  │
│  │IGroupedPipeline │              │ ValidatingPipeline│                 │
│  └─────────────────┘              └─────────────────┘                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                           数据源/目标层级                                 │
└─────────────────────────────────────────────────────────────────────────┘

         ┌──────────────────┐
         │  IDataSource<T>  │
         └────────┬─────────┘
                  │ 实现
        ┌─────────┼─────────┬─────────┬─────────┬─────────┐
        ▼         ▼         ▼         ▼         ▼         ▼
   ┌─────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐
   │SqlServer││  MySQL ││Sqlite  ││  CSV   ││ Excel  ││  JSON  │
   │ Source  ││ Source ││ Source ││ Source ││ Source ││ Source │
   └─────────┘└────────┘└────────┘└────────┘└────────┘└────────┘

         ┌──────────────────┐
         │  IDataTarget<T>  │
         └────────┬─────────┘
                  │ 实现
        ┌─────────┼─────────┬─────────┬─────────┬─────────┐
        ▼         ▼         ▼         ▼         ▼         ▼
   ┌─────────┐┌────────┐┌────────┐┌────────┐┌────────┐┌────────┐
   │SqlServer││  MySQL ││Sqlite  ││  CSV   ││ Excel  ││  JSON  │
   │ Target  ││ Target ││ Target ││ Target ││ Target ││ Target │
   └─────────┘└────────┘└────────┘└────────┘└────────┘└────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                           转换器层级                                      │
└─────────────────────────────────────────────────────────────────────────┘

         ┌───────────────────────┐
         │ IDataTransform<TIn,TOut> │
         └───────────┬───────────┘
                     │ 实现
     ┌───────────────┼───────────────┬───────────────┐
     ▼               ▼               ▼               ▼
┌─────────┐   ┌───────────┐   ┌───────────┐   ┌─────────────┐
│ Select  │   │   Where   │   │  OrderBy  │   │  Distinct   │
│Transform│   │ Transform │   │ Transform │   │  Transform  │
└─────────┘   └───────────┘   └───────────┘   └─────────────┘

     ┌───────────────────────────────────────────────┐
     ▼                                               ▼
┌─────────────┐                               ┌─────────────┐
│IGroupedTrans│                               │AggregateTrans│
│    form     │                               │    form     │
└─────────────┘                               └─────────────┘
```

---

## 扩展点设计

### 1. 自定义数据源

实现 `IDataSource<T>` 接口即可添加新的数据源类型：

```csharp
/// <summary>
/// 自定义 REST API 数据源示例
/// </summary>
public class RestApiSource<T> : IDataSource<T>
{
    public string Name { get; }
    public DataSourceType SourceType => DataSourceType.RestApi;
    
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public RestApiSource(string baseUrl, string endpoint)
    {
        Name = $"REST API: {endpoint}";
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _endpoint = endpoint;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
    
    public async IAsyncEnumerable<T> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(_endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<T>(
            stream, _jsonOptions, cancellationToken))
        {
            if (item != null)
                yield return item;
        }
    }
    
    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in ReadAsync(cancellationToken))
        {
            items.Add(item);
        }
        return items;
    }
    
    public DataSourceMetadata GetMetadata()
    {
        return new DataSourceMetadata
        {
            Name = Name,
            Type = SourceType,
            RecordCount = null, // 未知，需要查询
            Fields = null
        };
    }
}

// 在 DataForgePipeline 中注册扩展方法
public static class RestApiSourceExtensions
{
    public static IDataPipeline<RestApiSource<T>, T> FromRestApi<T>(
        string baseUrl, string endpoint)
    {
        return new DataPipeline<RestApiSource<T>, T>(
            new RestApiSource<T>(baseUrl, endpoint));
    }
}
```

### 2. 自定义转换器

实现 `IDataTransform<TIn, TOut>` 接口：

```csharp
/// <summary>
/// 自定义地址标准化转换器示例
/// </summary>
public class AddressNormalizationTransform : IDataTransform<Customer, Customer>
{
    public string Name => "地址标准化转换器";
    public int Priority => 100;
    
    public Customer? Transform(Customer input)
    {
        if (input == null) return null;
        
        return input with
        {
            Province = NormalizeProvince(input.Province),
            City = NormalizeCity(input.City),
            Address = NormalizeAddress(input.Address)
        };
    }
    
    public Task<Customer?> TransformAsync(Customer input, CancellationToken ct)
    {
        return Task.FromResult(Transform(input));
    }
    
    public async IAsyncEnumerable<Customer> TransformBatchAsync(
        IEnumerable<Customer> inputs,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var input in inputs)
        {
            var result = await TransformAsync(input, ct);
            if (result != null)
                yield return result;
        }
    }
    
    private static string NormalizeProvince(string? province)
    {
        return province switch
        {
            "北京市" or "北京" => "北京市",
            "上海市" or "上海" => "上海市",
            "广东省" or "广州" => "广东省",
            // ... 更多映射
            _ => province ?? ""
        };
    }
    
    private static string NormalizeCity(string? city) => city?.Trim() ?? "";
    private static string NormalizeAddress(string? address) => address?.Trim() ?? "";
}
```

### 3. 自定义导出目标

实现 `IDataTarget<T>` 接口：

```csharp
/// <summary>
/// 自定义 Parquet 导出目标示例
/// </summary>
public class ParquetTarget<T> : IDataTarget<T>
{
    public string Name { get; }
    public DataTargetType TargetType => DataTargetType.Parquet;
    
    private readonly string _filePath;
    private readonly ParquetSchema _schema;
    private List<T> _buffer = new();
    private readonly int _batchSize = 10000;
    
    public ParquetTarget(string filePath, ParquetSchema schema)
    {
        Name = $"Parquet: {filePath}";
        _filePath = filePath;
        _schema = schema;
    }
    
    public async Task WriteAsync(T item, CancellationToken ct)
    {
        _buffer.Add(item);
        if (_buffer.Count >= _batchSize)
        {
            await FlushBufferAsync(ct);
        }
    }
    
    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken ct)
    {
        var count = 0;
        var errors = new List<WriteError>();
        
        await foreach (var item in items.ToAsyncEnumerable().WithCancellation(ct))
        {
            try
            {
                await WriteAsync(item, ct);
                count++;
            }
            catch (Exception ex)
            {
                errors.Add(new WriteError { Item = item, Error = ex.Message });
            }
        }
        
        await CompleteAsync(ct);
        
        return new WriteResult
        {
            SuccessCount = count,
            FailedCount = errors.Count,
            Errors = errors
        };
    }
    
    public async Task CompleteAsync(CancellationToken ct)
    {
        await FlushBufferAsync(ct);
    }
    
    private async Task FlushBufferAsync(CancellationToken ct)
    {
        if (_buffer.Count == 0) return;
        
        await using var writer = new ParquetFileWriter(_filePath, _schema);
        await using var groupWriter = writer.CreateRowGroup();
        
        // 写入数据...
        
        _buffer.Clear();
    }
}
```

### 4. 自定义验证器

继承 `DataValidator<T>` 基类：

```csharp
/// <summary>
/// 订单验证器示例
/// </summary>
public class OrderValidator : DataValidator<SalesOrder>
{
    public OrderValidator()
    {
        // 基础规则
        RuleFor(o => o.OrderId)
            .NotEmpty()
            .WithMessage("订单号不能为空")
            .WithSeverity(ValidationSeverity.Critical);
        
        RuleFor(o => o.CustomerId)
            .NotEmpty()
            .WithMessage("客户ID不能为空");
        
        RuleFor(o => o.OrderDate)
            .NotEmpty()
            .Must(d => d <= DateTime.Now.AddDays(1))
            .WithMessage("订单日期不能是未来日期");
        
        // 金额规则
        RuleFor(o => o.TotalAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("订单金额不能为负数")
            .WithSeverity(ValidationSeverity.Error);
        
        RuleFor(o => o.TotalAmount)
            .Must(a => a <= 1_000_000)
            .WithMessage("单笔订单金额超过上限（100万）")
            .WithSeverity(ValidationSeverity.Warning);
        
        // 条件规则
        RuleFor(o => o.ShippingAddress)
            .NotEmpty()
            .When(o => o.RequireShipping)
            .WithMessage("需要配送的订单必须填写收货地址");
        
        // 自定义验证
        AddRule(new CustomValidationRule<SalesOrder>(
            order => order.LineItems.Count > 0,
            "订单必须包含至少一个商品明细",
            nameof(SalesOrder.LineItems)));
    }
}

/// <summary>
/// 验证器基类
/// </summary>
public abstract class DataValidator<T> : IValidator<T>
{
    private readonly List<IValidationRule<T>> _rules = new();
    
    public string Name => GetType().Name;
    public IReadOnlyList<IValidationRule<T>> Rules => _rules.AsReadOnly();
    
    protected void RuleFor<TProperty>(Expression<Func<T, TProperty>> property)
    {
        _rules.Add(new ValidationRule<T, TProperty>(property));
    }
    
    protected void AddRule(IValidationRule<T> rule)
    {
        _rules.Add(rule);
    }
    
    public ValidationResult Validate(T instance)
    {
        var errors = new List<ValidationError>();
        
        foreach (var rule in _rules)
        {
            var error = rule.Validate(instance);
            if (error != null)
            {
                errors.Add(error);
            }
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Data = instance
        };
    }
    
    public Task<ValidationResult> ValidateAsync(T instance, CancellationToken ct)
    {
        return Task.FromResult(Validate(instance));
    }
    
    public async IAsyncEnumerable<ValidationResult> ValidateBatchAsync(
        IEnumerable<T> instances,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var instance in instances)
        {
            yield return await ValidateAsync(instance, ct);
        }
    }
}
```

---

## 依赖关系

### 核心库 (DataForge.Core)

**零外部依赖** - 核心库不依赖任何第三方库

```
DataForge.Core
├── System (内置)
├── System.Collections.Generic (内置)
├── System.Linq (内置)
├── System.Threading.Tasks (内置)
└── System.IO (内置)
```

### 数据源包

```
DataForge.Core.SqlServer
├── DataForge.Core
└── Microsoft.Data.SqlClient (关系型数据库驱动)

DataForge.Core.MySql
├── DataForge.Core
└── MySqlConnector (轻量级 MySQL 驱动)

DataForge.Core.Sqlite
├── DataForge.Core
└── Microsoft.Data.Sqlite (SQLite 驱动)

DataForge.Core.Excel
├── DataForge.Core
└── ClosedXML (Excel 文件读写)

DataForge.Core.Json
├── DataForge.Core
└── System.Text.Json (内置 .NET 8)
```

### 验证集成包

```
DataForge.Core.FluentValidation
├── DataForge.Core
└── FluentValidation
```

### 包依赖图

```
                    ┌─────────────────────┐
                    │   DataForge.Core    │
                    │   (零依赖核心库)     │
                    └──────────┬──────────┘
                               │
        ┌──────────────┬───────┼───────┬──────────────┐
        │              │       │       │              │
        ▼              ▼       ▼       ▼              ▼
   ┌─────────┐   ┌─────────┐ ┌───────┐ ┌─────────┐ ┌─────────────┐
   │SqlServer│   │  MySQL  │ │Excel  │ │  JSON   │ │FluentValidation│
   └─────────┘   └─────────┘ └───────┘ └─────────┘ └─────────────┘
```

---

## 错误处理策略

### 错误类型定义

```csharp
/// <summary>
/// DataForge 异常基类
/// </summary>
public class DataForgeException : Exception
{
    public string ErrorCode { get; }
    public ErrorSeverity Severity { get; }
}

/// <summary>
/// 数据源错误
/// </summary>
public class DataSourceException : DataForgeException
{
    public string SourceName { get; }
    public Uri? SourceLocation { get; }
}

/// <summary>
/// 数据目标错误
/// </summary>
public class DataTargetException : DataForgeException
{
    public string TargetName { get; }
    public Uri? TargetLocation { get; }
}

/// <summary>
/// 验证错误（不抛出，用于收集）
/// </summary>
public class ValidationException : DataForgeException
{
    public ValidationResult Result { get; }
}

/// <summary>
/// 转换错误
/// </summary>
public class TransformException : DataForgeException
{
    public object? InputData { get; }
    public Type InputType { get; }
    public Type OutputType { get; }
}

public enum ErrorSeverity
{
    Warning,   // 可恢复，继续处理
    Error,     // 记录错误，跳过当前项
    Critical   // 不可恢复，终止处理
}
```

### 错误处理策略

```csharp
// 策略 1：遇到错误继续处理（默认）
await pipeline
    .OnErrorcontinue()
    .ToCsv("output.csv");

// 策略 2：遇到错误停止处理
await pipeline
    .OnErrorStop()
    .ToCsv("output.csv");

// 策略 3：遇到错误跳过并记录
await pipeline
    .OnErrorSkip()
    .ToCsv("output.csv");

// 策略 4：自定义错误处理
await pipeline
    .OnError((error, item) =>
    {
        Logger.LogWarning("处理失败: {Error}", error.Message);
        return ErrorAction.Skip;
    })
    .ToCsv("output.csv");

// 策略 5：记录错误但继续，并收集错误列表
var result = await pipeline
    .ContinueOnError()
    .ToCsv("output.csv");

Console.WriteLine($"处理完成，错误数: {result.ErrorCount}");
foreach (var error in result.Errors)
{
    Console.WriteLine($"- {error.Error.Message} at {error.Data}");
}
```

---

## 异步编程模型

### 核心原则

1. **所有 IO 操作异步化** - 数据源读取、目标写入均为异步
2. **流式处理优先** - 使用 `IAsyncEnumerable<T>` 避免内存溢出
3. **可取消** - 所有异步操作支持 `CancellationToken`
4. **背压支持** - 生产者可以暂停等待消费者

### 异步流使用示例

```csharp
// ✅ 推荐：使用异步流处理大文件
await foreach (var order in pipeline.WithCancellation(ct))
{
    await ProcessOrderAsync(order);
}

// ✅ 推荐：使用 ToListAsync 收集小数据集
var orders = await pipeline.ToListAsync();

// ✅ 推荐：使用异步 LINQ 操作
var highValueOrders = await pipeline
    .WhereAsync(o => CheckCustomerCreditAsync(o.CustomerId))
    .ToListAsync();

// ❌ 避免：同步等待大文件
var allOrders = pipeline.ReadAll(); // 可能 OOM
```

### 异步转换器示例

```csharp
public class AsyncEnrichmentTransform<TIn, TOut> : IDataTransform<TIn, TOut>
{
    private readonly Func<TIn, Task<TOut>> _asyncEnricher;
    
    public AsyncEnrichmentTransform(Func<TIn, Task<TOut>> asyncEnricher)
    {
        _asyncEnricher = asyncEnricher;
    }
    
    public async Task<TOut?> TransformAsync(TIn input, CancellationToken ct)
    {
        return await _asyncEnricher(input).WaitAsync(ct);
    }
    
    // 使用 ConfigureAwait(false) 避免上下文切换
    public async IAsyncEnumerable<TOut> TransformBatchAsync(
        IEnumerable<TIn> inputs,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var input in inputs)
        {
            var result = await TransformAsync(input, ct).ConfigureAwait(false);
            if (result != null)
                yield return result;
        }
    }
}
```

---

## 总结

DataForge.Core 的架构设计遵循以下原则：

1. **接口驱动** - 核心功能通过接口抽象，支持依赖注入和扩展
2. **零依赖核心** - 核心库无任何外部依赖，降低集成门槛
3. **流式优先** - 内存效率优先，支持大数据集处理
4. **类型安全** - 完整泛型支持，编译时错误检查
5. **可测试性** - 所有组件可通过接口 mock，便于单元测试

后续开发将严格按照本文档定义的接口和扩展点进行实现。
