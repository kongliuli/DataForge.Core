# 数据导出指南

本文档介绍 DataForge.Core 支持的所有导出方式和配置选项。

## 目录

1. [CSV 导出](#csv-导出)
2. [Excel 导出](#excel-导出)
3. [JSON 导出](#json-导出)
4. [数据库导出](#数据库导出)
5. [自定义导出目标](#自定义导出目标)

---

## CSV 导出

### 基础用法

```csharp
// 基本导出
await pipeline.ToCsv("orders.csv");

// 指定选项
await pipeline.ToCsv("orders.csv", new CsvExportOptions
{
    IncludeHeader = true,
    Separator = ','
});
```

### 完整选项

```csharp
var options = new CsvExportOptions
{
    // ========== 格式设置 ==========
    
    /// <summary>
    /// 文件编码，默认 UTF8
    /// </summary>
    Encoding = Encoding.UTF8,
    
    /// <summary>
    /// 分隔符，默认逗号
    /// </summary>
    Separator = ',',
    
    /// <summary>
    /// 是否包含表头，默认 true
    /// </summary>
    IncludeHeader = true,
    
    /// <summary>
    /// 是否追加到文件，默认 false（覆盖）
    /// </summary>
    Append = false,
    
    /// <summary>
    /// 引号字符
    /// </summary>
    QuoteChar = '"',
    
    /// <summary>
    /// 引号模式
    /// </summary>
    QuoteMode = QuoteMode.AsNeeded,
    
    /// <summary>
    /// 空值输出，默认空字符串
    /// </summary>
    NullValue = "",
    
    /// <summary>
    /// 是否写入 BOM（字节顺序标记），默认 true
    /// </summary>
    WriteBom = true,
    
    // ========== 性能设置 ==========
    
    /// <summary>
    /// 每批次写入的行数，默认 10000
    /// </summary>
    BatchSize = 10000,
    
    /// <summary>
    /// 是否刷新流，默认 true
    /// </summary>
    FlushAfterWrite = true
};

public enum QuoteMode
{
    /// <summary>始终加引号</summary>
    Always,
    
    /// <summary>按需加引号（包含分隔符/引号/换行符时加引号）</summary>
    AsNeeded,
    
    /// <summary>从不加引号</summary>
    Never
}
```

### 编码和分隔符

```csharp
// UTF-8 带 BOM
await pipeline.ToCsv("utf8.csv");

// UTF-8 不带 BOM（兼容 Excel）
await pipeline.ToCsv("utf8-no-bom.csv", new CsvExportOptions { WriteBom = false });

// GBK 编码
await pipeline.ToCsv("gbk.csv", new CsvExportOptions
{
    Encoding = Encoding.GetEncoding("GB2312")
});

// Tab 分隔
await pipeline.ToCsv("tsv.csv", new CsvExportOptions
{
    Separator = '\t'
});

// 分号分隔
await pipeline.ToCsv("semicolon.csv", new CsvExportOptions
{
    Separator = ';'
});
```

### 表头和追加

```csharp
// 无表头
await pipeline.ToCsv("no-header.csv", new CsvExportOptions
{
    IncludeHeader = false
});

// 追加模式（不覆盖）
await pipeline.ToCsv("append.csv", new CsvExportOptions
{
    Append = true
});

// 多批次追加
foreach (var batch in batches)
{
    await batch.ToDataForge().ToCsv("accumulated.csv", new CsvExportOptions
    {
        Append = true,
        IncludeHeader = false  // 追加时通常不需要表头
    });
}
```

### 导出结果

```csharp
var result = await pipeline.ToCsv("orders.csv");

Console.WriteLine($"文件路径: {result.FilePath}");
Console.WriteLine($"写入记录数: {result.RecordsWritten}");
Console.WriteLine($"文件大小: {result.BytesWritten / 1024.0:F2} KB");
Console.WriteLine($"耗时: {result.Duration.TotalSeconds:F2}s");
```

---

## Excel 导出

### 基础用法

```csharp
// 基本导出
await pipeline.ToExcel("orders.xlsx");

// 指定 Sheet 名称
await pipeline.ToExcel("orders.xlsx", new ExcelExportOptions
{
    SheetName = "订单数据"
});
```

### 完整选项

```csharp
var options = new ExcelExportOptions
{
    // ========== Sheet 设置 ==========
    
    /// <summary>
    /// 工作表名称，默认 Sheet1
    /// </summary>
    SheetName = "Sheet1",
    
    /// <summary>
    /// 是否包含表头，默认 true
    /// </summary>
    IncludeHeader = true,
    
    /// <summary>
    /// 起始行号（从 1 开始），默认 1
    /// </summary>
    StartRow = 1,
    
    // ========== 样式设置 ==========
    
    /// <summary>
    /// 是否自动调整列宽，默认 true
    /// </summary>
    AutoFitColumns = true,
    
    /// <summary>
    /// 是否冻结首行，默认 false
    /// </summary>
    FreezeHeader = false,
    
    /// <summary>
    /// 表头背景色
    /// </summary>
    HeaderBackgroundColor = "#4472C4",
    
    /// <summary>
    /// 表头文字颜色
    /// </summary>
    HeaderFontColor = "#FFFFFF",
    
    /// <summary>
    /// 表头是否加粗
    /// </summary>
    HeaderBold = true,
    
    /// <summary>
    /// 表头字体大小
    /// </summary>
    HeaderFontSize = 11,
    
    /// <summary>
    /// 数据字体大小
    /// </summary>
    DataFontSize = 10,
    
    // ========== 性能设置 ==========
    
    /// <summary>
    /// 批量写入的行数，默认 1000
    /// </summary>
    BatchSize = 1000
};
```

### 样式自定义

```csharp
// 自定义表头样式
await pipeline.ToExcel("styled.xlsx", new ExcelExportOptions
{
    SheetName = "销售报表",
    IncludeHeader = true,
    HeaderStyle = new ExcelStyle
    {
        FontName = "微软雅黑",
        FontSize = 12,
        Bold = true,
        BackgroundColor = "#4472C4",
        FontColor = "#FFFFFF",
        Alignment = "center"
    },
    DataStyle = new ExcelStyle
    {
        FontName = "微软雅黑",
        FontSize = 10,
        Alignment = "left"
    },
    FreezeHeader = true,
    AutoFitColumns = true
});
```

### 多 Sheet 导出

```csharp
// 使用分支管道导出多个 Sheet
var regions = new[] { "华北", "华东", "华南", "西南" };

foreach (var region in regions)
{
    await DataForgePipeline
        .FromCsv<Order>("orders.csv")
        .Where(o => o.Region == region)
        .ToExcel($"{region}-orders.xlsx", new ExcelExportOptions
        {
            SheetName = $"{region}订单"
        });
}

// 合并导出到同一个文件的多个 Sheet
// 注意：需要分步执行
var wb = new ClosedXML.Excel.XLWorkbook();
wb.AddWorksheet("华北").FirstCell().InsertData(await GetNorthData());
wb.AddWorksheet("华东").FirstCell().InsertData(await GetEastData());
wb.SaveAs("all-regions.xlsx");
```

---

## JSON 导出

### 基础用法

```csharp
// 基本导出（数组）
await pipeline.ToJson("orders.json");

// 格式化输出
await pipeline.ToJson("pretty.json", new JsonExportOptions
{
    Indented = true
});
```

### 完整选项

```csharp
var options = new JsonExportOptions
{
    // ========== 格式设置 ==========
    
    /// <summary>
    /// 是否缩进格式化，默认 true
    /// </summary>
    Indented = true,
    
    /// <summary>
    /// 是否输出数组，默认 true
    /// </summary>
    OutputArray = true,
    
    /// <summary>
    /// 根属性名（设置后输出对象而非数组）
    /// </summary>
    RootPropertyName = "data",
    
    /// <summary>
    /// 日期格式，默认 ISO8601
    /// </summary>
    DateFormat = "yyyy-MM-ddTHH:mm:ss",
    
    // ========== 值处理 ==========
    
    /// <summary>
    /// 空值处理
    /// </summary>
    NullValueHandling = NullValueHandling.Ignore,
    
    /// <summary>
    /// 默认值处理
    /// </summary>
    DefaultValueHandling = DefaultValueHandling.Include,
    
    // ========== 命名策略 ==========
    
    /// <summary>
    /// 属性命名策略
    /// </summary>
    NamingStrategy = NamingStrategy.CamelCase,
    
    // ========== 枚举处理 ==========
    
    /// <summary>
    /// 枚举序列化方式
    /// </summary>
    EnumHandling = EnumHandling.AsString
};

public enum NullValueHandling
{
    /// <summary>包含空值</summary>
    Include,
    
    /// <summary>忽略空值</summary>
    Ignore
}

public enum NamingStrategy
{
    /// <summary>保持原样</summary>
    None,
    
    /// <summary>驼峰命名</summary>
    CamelCase,
    
    /// <summary>帕斯卡命名</summary>
    PascalCase,
    
    /// <summary>蛇形命名</summary>
    SnakeCase,
    
    /// <summary>短横线命名</summary>
    KebabCase
}

public enum EnumHandling
{
    /// <summary>序列化为数字</summary>
    AsNumber,
    
    /// <summary>序列化为字符串</summary>
    AsString
}
```

### JSON 结构

```csharp
// 默认输出：数组
// [
//   { "orderId": "O001", "amount": 100 },
//   { "orderId": "O002", "amount": 200 }
// ]

await pipeline.ToJson("array.json");

// 自定义根节点：对象
// {
//   "data": [
//     { "orderId": "O001", "amount": 100 }
//   ]
// }

await pipeline.ToJson("with-root.json", new JsonExportOptions
{
    RootPropertyName = "data"
});

// 蛇形命名
await pipeline.ToJson("snake_case.json", new JsonExportOptions
{
    NamingStrategy = NamingStrategy.SnakeCase
});

// 输出
// [
//   { "order_id": "O001", "customer_name": "张三", "total_amount": 100.00 }
// ]
```

---

## 数据库导出

### SQL Server

```csharp
// 基础导出
await pipeline.ToSqlServer(connectionString, "TargetTable");

// 指定选项
await pipeline.ToSqlServer(connectionString, "TargetTable", new SqlServerExportOptions
{
    BatchSize = 1000,
    InsertMode = InsertMode.Insert
});
```

### 完整选项

```csharp
var options = new SqlServerExportOptions
{
    // ========== 插入设置 ==========
    
    /// <summary>
    /// 批量插入大小，默认 1000
    /// </summary>
    BatchSize = 1000,
    
    /// <summary>
    /// 插入模式
    /// </summary>
    InsertMode = InsertMode.Insert,
    
    /// <summary>
    /// Upsert 键列（InsertMode = Upsert 时使用）
    /// </summary>
    UpsertKeyColumns = new[] { "Id", "Code" },
    
    /// <summary>
    /// 是否自动创建表，默认 false
    /// </summary>
    AutoCreateTable = false,
    
    /// <summary>
    /// 命令超时（秒），默认 300
    /// </summary>
    CommandTimeout = 300,
    
    /// <summary>
    /// 是否使用事务，默认 true
    /// </summary>
    UseTransaction = true,
    
    /// <summary>
    /// 失败时是否回滚事务，默认 true
    /// </summary>
    RollbackOnError = true
};

public enum InsertMode
{
    /// <summary>仅插入（重复则报错）</summary>
    Insert,
    
    /// <summary>插入或更新（根据键判断）</summary>
    Upsert
}
```

### Upsert（插入或更新）

```csharp
// 根据 Id 字段判断是插入还是更新
await pipeline.ToSqlServer(connectionString, "Customers", new SqlServerExportOptions
{
    InsertMode = InsertMode.Upsert,
    UpsertKeyColumns = new[] { "CustomerId" }
});

// 多键判断
await pipeline.ToSqlServer(connectionString, "OrderItems", new SqlServerExportOptions
{
    InsertMode = InsertMode.Upsert,
    UpsertKeyColumns = new[] { "OrderId", "LineNumber" }
});
```

### 自动建表

```csharp
// 自动根据类型创建表
await pipeline.ToSqlServer(connectionString, "NewTable", new SqlServerExportOptions
{
    AutoCreateTable = true,
    BatchSize = 500
});

// 自动创建的表结构基于实体属性
// string -> nvarchar(max)
// int -> int
// decimal -> decimal(18,2)
// DateTime -> datetime2
// bool -> bit
```

### MySQL

```csharp
// 基础导出
await pipeline.ToMySql(connectionString, "TargetTable");

// 带选项
await pipeline.ToMySql(connectionString, "TargetTable", new MySqlExportOptions
{
    BatchSize = 500,
    InsertMode = InsertMode.Insert,
    UpsertKeyColumns = new[] { "Id" },
    DuplicateKeyUpdate = true  // MySQL 特有的 ON DUPLICATE KEY UPDATE
});
```

### SQLite

```csharp
// 基础导出
await pipeline.ToSqlite("database.db", "TargetTable");

// 带选项
await pipeline.ToSqlite("database.db", "TargetTable", new SqliteExportOptions
{
    BatchSize = 1000,
    InsertMode = InsertMode.Insert,
    UpsertKeyColumns = new[] { "Id" },
    UseUpsert = true  // SQLite 使用 INSERT OR REPLACE
});
```

### 导出结果

```csharp
var result = await pipeline.ToSqlServer(connectionString, "Orders");

Console.WriteLine($"表名: {result.TableName}");
Console.WriteLine($"成功: {result.SuccessCount}");
Console.WriteLine($"失败: {result.FailedCount}");
Console.WriteLine($"重复: {result.DuplicateCount}");
Console.WriteLine($"耗时: {result.Duration.TotalSeconds}s");

if (result.Errors.Count > 0)
{
    Console.WriteLine("错误详情:");
    foreach (var error in result.Errors.Take(10))
    {
        Console.WriteLine($"  - {error.ErrorMessage}");
    }
}
```

### 事务控制

```csharp
// 默认使用事务（失败自动回滚）
try
{
    await pipeline.ToSqlServer(connectionString, "Orders", new SqlServerExportOptions
    {
        UseTransaction = true,
        BatchSize = 1000
    });
}
catch (Exception ex)
{
    Console.WriteLine($"导出失败: {ex.Message}");
    // 事务已自动回滚
}

// 不使用事务（逐条尝试）
await pipeline.ToSqlServer(connectionString, "Orders", new SqlServerExportOptions
{
    UseTransaction = false
});
```

---

## 自定义导出目标

### 实现 IDataTarget<T>

```csharp
/// <summary>
/// Parquet 文件导出目标
/// </summary>
public class ParquetTarget<T> : IDataTarget<T>
{
    private readonly string _filePath;
    private readonly ParquetSchema _schema;
    private readonly List<T> _buffer = new();
    private readonly int _batchSize = 10000;
    
    public string Name => $"Parquet: {_filePath}";
    public DataTargetType TargetType => DataTargetType.Parquet;
    
    public ParquetTarget(string filePath, ParquetSchema schema)
    {
        _filePath = filePath;
        _schema = schema;
    }
    
    public async Task WriteAsync(T item, CancellationToken ct)
    {
        _buffer.Add(item);
        
        if (_buffer.Count >= _batchSize)
        {
            await FlushAsync(ct);
        }
    }
    
    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken ct)
    {
        var successCount = 0;
        var errors = new List<WriteError>();
        
        foreach (var item in items)
        {
            try
            {
                await WriteAsync(item, ct);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new WriteError
                {
                    Item = item,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
            }
        }
        
        return new WriteResult
        {
            SuccessCount = successCount,
            FailedCount = errors.Count,
            Errors = errors
        };
    }
    
    public async Task CompleteAsync(CancellationToken ct)
    {
        await FlushAsync(ct);
    }
    
    private async Task FlushAsync(CancellationToken ct)
    {
        if (_buffer.Count == 0) return;
        
        await using var writer = new ParquetFileWriter(_filePath, _schema);
        await using var groupWriter = writer.CreateRowGroup();
        
        // 序列化数据
        foreach (var item in _buffer)
        {
            WriteRecord(groupWriter, item);
        }
        
        await writer.CloseAsync(ct);
        _buffer.Clear();
    }
    
    private void WriteRecord(ParquetRowGroupWriter writer, T record)
    {
        // 实现具体的 Parquet 写入逻辑
    }
}
```

### 注册扩展方法

```csharp
public static class ParquetExportExtensions
{
    public static Task<ParquetExportResult> ToParquet<T>(
        this IDataPipeline<T, T> pipeline,
        string filePath,
        ParquetExportOptions? options = null)
    {
        var schema = InferSchema<T>();
        var target = new ParquetTarget<T>(filePath, schema, options);
        return pipeline.ToTarget(target);
    }
    
    public static async Task<ParquetExportResult> ToTarget<T>(
        this IDataPipeline<T, T> pipeline,
        ParquetTarget<T> target)
    {
        await foreach (var item in pipeline)
        {
            await target.WriteAsync(item);
        }
        
        await target.CompleteAsync();
        
        return new ParquetExportResult
        {
            FilePath = target.FilePath,
            RecordsWritten = target.RecordsWritten,
            Duration = target.Duration
        };
    }
}
```

### 使用自定义目标

```csharp
// 导出到 Parquet
await pipeline.ToParquet("orders.parquet");

// 带选项
await pipeline.ToParquet("orders.parquet", new ParquetExportOptions
{
    Compression = ParquetCompression.Snappy,
    RowGroupSize = 100000
});
```

---

## 控制台和流导出

### 控制台导出

```csharp
// 默认格式化
await pipeline.ToConsole();

// 自定义格式化
await pipeline.ToConsole(order => 
    $"{order.OrderId,-10} {order.CustomerName,-20} {order.Amount,10:C}");

// 带颜色
await pipeline.ToConsole(order =>
{
    var color = order.Amount > 10000 ? ConsoleColor.Green : ConsoleColor.White;
    Console.ForegroundColor = color;
    return $"{order.OrderId} - {order.Amount}";
});
```

### 流导出

```csharp
using var stream = new MemoryStream();

// 导出到内存流
await pipeline.ToStream(stream, ExportFormat.Csv);

// 获取结果
var bytes = stream.ToArray();
await File.WriteAllBytesAsync("output.csv", bytes);

// 或者导出到 HTTP 响应
await pipeline.ToStream(context.Response.Body, ExportFormat.Json);
context.Response.ContentType = "application/json";
```
