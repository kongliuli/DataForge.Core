# 数据导出指南（0.1.0）

只记录已实现 API。规划中的能力见 [api-roadmap.md](api-roadmap.md)；完整清单见 [api-implemented.md](api-implemented.md)。

```csharp
using DataForge.Core;
```

核心管道方法（`IDataPipeline<T>`）一律带 `Async`，返回 `ExportResults`：

| 方法 | 说明 |
|---|---|
| `ToCsvAsync(path, options?)` | CSV 文件 |
| `ToJsonAsync(path, options?)` | JSON 文件 |
| `ToExcelAsync(path, options?)` | **占位**实现，非完整 `.xlsx` |
| `ToConsoleAsync(formatter?)` | 打印到控制台 |
| `ToStreamAsync(stream, format)` | 写入流；`ExportFormat`: `Csv` / `Json` / `Excel` |

---

## ExportResults

```csharp
var result = await pipeline.ToCsvAsync("orders.csv");

Console.WriteLine(result.RecordsWritten);
Console.WriteLine(result.OutputPath);
Console.WriteLine(result.OutputSize);
Console.WriteLine(result.Duration);

if (result.HasErrors)
{
    foreach (var e in result.Errors)
        Console.WriteLine(e);
}
```

字段：`RecordsWritten`、`OutputPath`、`OutputSize`、`Duration`、`Errors`、`HasErrors`。  
没有 `FilePath` / `SuccessCount`（后者在底层 `WriteResult` 上，不是 `ExportResults`）。

---

## CSV

```csharp
await pipeline.ToCsvAsync("orders.csv");

await pipeline.ToCsvAsync("orders.csv", new CsvExportOptions
{
    Delimiter = ',',          // 默认 ','
    QuoteChar = '"',          // 默认 '"'
    Encoding = Encoding.UTF8, // 默认 UTF8
    IncludeHeader = true,     // 默认 true
    BatchSize = 10000         // 可选；null 用实现默认
});
```

属性仅上述五项。没有 `Separator`（用 `Delimiter`）、`QuoteMode`、`Append`、`WriteBom`、`NullValue`。

---

## JSON（核心）

```csharp
await pipeline.ToJsonAsync("orders.json");

await pipeline.ToJsonAsync("orders.json", new JsonExportOptions
{
    Indented = true,          // 默认 true
    RootPropertyName = "data" // 可选；设置后包一层对象
});
```

属性仅 `Indented`、`RootPropertyName`。没有命名策略 / 空值策略 / 日期格式选项。

扩展包 `DataForge.Core.Json` 另提供同步命名的 `ToJson(...)`（无 `Async` 后缀），与核心 `ToJsonAsync` 并存；选项形状相同。

---

## Excel

### 核心占位

`DataForge.Core` 的 `ToExcelAsync` 是无 ClosedXML 依赖的占位写入，**不是**完整 `.xlsx`。真实 Excel 请用扩展包。

```csharp
// 核心（占位）
await pipeline.ToExcelAsync("orders.xlsx", new ExcelExportOptions
{
    SheetName = "Sheet1",     // 默认 Sheet1
    FreezeFirstRow = true,    // 默认 true
    AutoSizeColumns = true,   // 默认 true
    IncludeHeader = true      // 默认 true
});
```

### 扩展包 DataForge.Core.Excel

ClosedXML 实现；方法名是 **`ToExcel`（无 Async）**：

```csharp
using DataForge.Core.Excel;

await pipeline.ToExcel("orders.xlsx", new ExcelExportOptions
{
    SheetName = "订单",
    FreezeFirstRow = true,
    AutoSizeColumns = true,
    IncludeHeader = true
});
```

选项属性与核心同名同类。没有表头颜色 / 字体 / 多 Sheet API；多 Sheet 自行用 ClosedXML 或多次导出。

---

## 控制台与流

```csharp
await pipeline.ToConsoleAsync();
await pipeline.ToConsoleAsync(o => $"{o.Id} {o.Amount}");

await using var stream = File.Create("out.csv");
await pipeline.ToStreamAsync(stream, ExportFormat.Csv);
```

---

## 扩展包导出（简述）

统一返回 `ExportResults`。SQL 三包选项同形。

### SQL Server / MySQL / SQLite

```csharp
using DataForge.Core.SqlServer; // 或 .MySql / .Sqlite

await pipeline.ToSqlServer(conn, "Orders", new SqlServerExportOptions
{
    BatchSize = 1000,                 // 默认 1000
    InsertMode = InsertMode.Insert,   // Insert | Upsert
    UpsertKeyColumns = new[] { "Id" },
    UseTransaction = true             // 默认 true
});

await pipeline.ToMySql(conn, "Orders", new MySqlExportOptions { /* 同上四字段 */ });
await pipeline.ToSqlite(conn, "Orders", new SqliteExportOptions { /* 同上四字段 */ });
```

没有 `AutoCreateTable` / `CommandTimeout` / `RollbackOnError` / 厂商特有开关文档化进 0.1.0。

### HTTP

```csharp
using DataForge.Core.Http;

await pipeline.ToRestApi(baseUrl, "/orders", new RestApiTargetOptions
{
    BaseUrl = baseUrl,
    Endpoint = "/orders",
    BatchSize = 100,
    UseBulkEndpoint = false,
    Headers = new Dictionary<string, string>()
});

// 或传入 HttpClient
await pipeline.ToRestApi(httpClient, "/orders", options);
```

### JSON 包

见上文：`ToJson`（无 Async），包名 `DataForge.Core.Json`。

---

## 底层接口（一般不用）

`IDataTarget<T>`：`ExportAsync` / `WriteAsync` / `WriteBatchAsync` → `WriteResult`（`SuccessCount` / `FailedCount` / `Errors`）/ `CompleteAsync`。  
自定义目标与未上架格式：看路线图，不要按本文发明 API。
