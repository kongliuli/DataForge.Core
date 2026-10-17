# 数据源（0.1.0）

入口：`using DataForge.Core;`。签名见 [api-implemented.md](./api-implemented.md)；规划见 [api-roadmap.md](./api-roadmap.md)。

核心工厂：`DataForgePipeline` 的 `public static` 方法。

---

## 核心入口

```csharp
DataForgePipeline.FromCsv<T>(string filePath, CsvSourceOptions? options = null);
DataForgePipeline.FromJson<T>(string filePath, JsonSourceOptions? options = null);
DataForgePipeline.FromMemory<T>(IEnumerable<T> data);
DataForgePipeline.FromExcel<T>(string filePath, string sheetName = "Sheet1"); // 占位
DataForgePipeline.FromJsonString<T>(string jsonContent);
DataForgePipeline.FromJsonArray<T>(string filePath); // 当前等同 FromJson
DataForgePipeline.Merge<T>(params IDataSource<T>[] sources);
```

### FromCsv + `CsvSourceOptions`

```csharp
var p = DataForgePipeline.FromCsv<Order>("orders.csv", new CsvSourceOptions
{
    Delimiter = ',',
    QuoteChar = '"',
    Encoding = Encoding.UTF8,
    HasHeaderRow = true,
    SkipRows = 0,
    MaxRows = null,
    CommentPrefix = "#"
});
```

真实属性（`DataForge.Core.Core.Sources.Options`）：`Delimiter`、`QuoteChar`、`Encoding`、`HasHeaderRow`、`SkipRows`、`MaxRows`、`CommentPrefix`。

### FromJson / FromJsonString / FromJsonArray

```csharp
var file = DataForgePipeline.FromJson<Order>("orders.json", new JsonSourceOptions
{
    AllowTrailingCommas = true,
    ReadCommentHandling = true,
    UseStreaming = true
    // SerializerOptions = ...
});
var text = DataForgePipeline.FromJsonString<Order>("[{\"Id\":1}]");
var alias = DataForgePipeline.FromJsonArray<Order>("orders.json"); // = FromJson
```

`JsonSourceOptions`：`SerializerOptions`、`AllowTrailingCommas`、`ReadCommentHandling`、`UseStreaming`。

### FromMemory / Merge

```csharp
var mem = DataForgePipeline.FromMemory(orders);
var merged = DataForgePipeline.Merge(sourceA, sourceB); // 按参数顺序依次枚举 IDataSource<T>
```

### FromExcel（核心占位）

```csharp
// 核心按文本/CSV 方式读，不是完整 xlsx
var placeholder = DataForgePipeline.FromExcel<Order>("orders.xlsx", "Sheet1");
```

生产请用 **`DataForge.Core.Excel`**。

---

## 扩展包（摘要）

只列已存在的 `From*`，不编造 Options。

### DataForge.Core.Excel

```csharp
using DataForge.Core.Excel;

var p = new DataForgePipeline().FromExcel<Order>("orders.xlsx", new ExcelSourceOptions
{
    SheetName = "Sheet1",
    HasHeaderRow = true,
    SkipRows = 0,
    MaxRows = null
});
await p.ToExcel("out.xlsx"); // 扩展名；核心接口另有 ToExcelAsync
```

`ExcelSourceOptions`：`SheetName`、`HasHeaderRow`、`SkipRows`、`MaxRows`。

### DataForge.Core.Json

```csharp
using DataForge.Core.Json;
var p = new DataForgePipeline().FromJson<Order>("orders.json", options);
await p.ToJson("out.json");
```

### DataForge.Core.Http

```csharp
using DataForge.Core.Http;

var p = new DataForgePipeline().FromRestApi<Order>(baseUrl, endpoint, new RestApiSourceOptions
{
    BaseUrl = baseUrl,
    Endpoint = endpoint,
    QueryParams = "",
    PageSize = 100,
    PageParam = "page",
    StartPage = 1,
    MaxPages = 0,
    UseStreaming = true
});
var paged = new DataForgePipeline().FromRestApiWithPagination<Order>(baseUrl, endpoint, options);
```

另有 `HttpClient` 重载。

### SQL（SqlServer / MySql / Sqlite）

**静态**入口（不是 `DataForgePipeline.FromSqlServer`）：

```csharp
using DataForge.Core.SqlServer;
var p = SqlServerPipelineExtensions.FromSqlServer<Order>(connectionString, "Orders");
// MySqlPipelineExtensions.FromMySql<T>(...)
// SqlitePipelineExtensions.FromSqlite<T>(...)
```

约束 `where T : new()`。写出：`ToSqlServer` / `ToMySql` / `ToSqlite`。

---

## 注意

1. 业务代码用 `using DataForge.Core;`；扩展命名空间按包引用。
2. 核心 `FromExcel` ≠ 完整 xlsx。
3. Options 以源码为准。
4. 导出结果：`ExportResults.RecordsWritten` / `OutputPath` / `Duration` / `Errors`（无 `FilePath`）。
