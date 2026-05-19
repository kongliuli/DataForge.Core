# 数据源接入指南

本文档介绍 DataForge.Core 支持的所有数据源及其配置方式。

## 目录

1. [SQL 数据源](#sql-数据源)
2. [文件数据源](#文件数据源)
3. [内存数据源](#内存数据源)
4. [自定义数据源](#自定义数据源)
5. [连接池管理](#连接池管理)

---

## SQL 数据源

### SQL Server

```csharp
// 基础用法
var orders = await DataForgePipeline
    .FromSqlServer<Order>("Server=localhost;Database=Sales;Trusted_Connection=True")
    .Where(o => o.OrderDate >= DateTime.Today.AddMonths(-1))
    .ToListAsync();

// 指定表名
var customers = await DataForgePipeline
    .FromSqlServer<Customer>(connString, "Customers")
    .Where(c => c.Status == "Active")
    .ToListAsync();

// 使用 DbConnection（支持事务）
await using var connection = new SqlConnection(connString);
await connection.OpenAsync();

var transaction = await connection.BeginTransactionAsync();
try
{
    await DataForgePipeline
        .FromSqlServer<Order>(connection, "Orders")
        .Where(o => o.Status == "Pending")
        .ToCsv("pending-orders.csv");
    
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// 带查询条件
await DataForgePipeline
    .FromSqlServer<Order>(connString, "Orders")
    .Where(o => o.Region == "华北" && o.Amount > 1000)
    .OrderBy(o => o.OrderDate)
    .ToCsv("high-value-north.csv");
```

#### SQL Server 选项

```csharp
var options = new SqlSourceOptions
{
    CommandTimeout = 300,
    EnableRetry = true,
    MaxRetryCount = 3,
    RetryDelayMs = 1000,
    Pooling = true,
    MinPoolSize = 5,
    MaxPoolSize = 100,
    ConvertEmptyValuesToNull = true
};

await DataForgePipeline
    .FromSqlServer<Order>(connString, options)
    .ToListAsync();
```

### MySQL

```csharp
// 基础用法
var orders = await DataForgePipeline
    .FromMySql<Order>("Server=localhost;Database=sales;User=root;Password=123456")
    .Where(o => o.Status == "Completed")
    .ToListAsync();

// 指定表名
await DataForgePipeline
    .FromMySql<Customer>(connString, "Customers")
    .Select(c => new { c.Id, c.Name, c.Email })
    .ToJson("customers.json");

// 使用连接
await using var connection = new MySqlConnection(connString);
await DataForgePipeline
    .FromMySql<Order>(connection, "Orders")
    .ToCsv("orders.csv");
```

### SQLite

```csharp
// 基础用法（文件路径）
var orders = await DataForgePipeline
    .FromSqlite<Order>("sales.db")
    .Where(o => o.OrderDate > DateTime.Today.AddDays(-7))
    .ToListAsync();

// 指定表名
await DataForgePipeline
    .FromSqlite<Order>("sales.db", "Orders")
    .ToJson("recent-orders.json");

// 连接字符串
await DataForgePipeline
    .FromSqlite<Order>("Data Source=sales.db;Mode=ReadOnly")
    .ToListAsync();
```

---

## 文件数据源

### CSV

```csharp
// 基础用法
var orders = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ToListAsync();

// 自定义分隔符
await DataForgePipeline
    .FromCsv<Order>("orders.tsv", new CsvSourceOptions
    {
        Separator = '\t'
    })
    .ToListAsync();

// 无表头
await DataForgePipeline
    .FromCsv<Order>("orders-no-header.csv", new CsvSourceOptions
    {
        HasHeader = false
    })
    .ToListAsync();

// 指定编码
await DataForgePipeline
    .FromCsv<Order>("orders-gbk.csv", new CsvSourceOptions
    {
        Encoding = Encoding.GetEncoding("GB2312")
    })
    .ToListAsync();

// 跳过行数
await DataForgePipeline
    .FromCsv<Order>("orders-skip.csv", new CsvSourceOptions
    {
        SkipLines = 3  // 跳过前 3 行
    })
    .ToListAsync();

// 复杂 CSV（带引号和转义）
await DataForgePipeline
    .FromCsv<Order>("orders-complex.csv", new CsvSourceOptions
    {
        Separator = ',',
        QuoteChar = '"',
        EscapeChar = '"',
        TrimFields = true,
        NullValue = "NULL"
    })
    .ToListAsync();

// 从 Stream 读取
await using var stream = File.OpenRead("orders.csv");
await DataForgePipeline
    .FromCsv<Order>(stream)
    .ToListAsync();

// 从字符串读取
var csvContent = await File.ReadAllTextAsync("orders.csv");
await DataForgePipeline
    .FromCsvString<Order>(csvContent)
    .ToListAsync();
```

### Excel

```csharp
// 基础用法（第一个 Sheet）
var orders = await DataForgePipeline
    .FromExcel<Order>("orders.xlsx")
    .ToListAsync();

// 指定 Sheet 名称
await DataForgePipeline
    .FromExcel<Order>("orders.xlsx", sheetName: "2024订单")
    .ToListAsync();

// 指定 Sheet 索引
await DataForgePipeline
    .FromExcel<Order>("orders.xlsx", new ExcelSourceOptions
    {
        SheetIndex = 2
    })
    .ToListAsync();

// 自定义表头行
await DataForgePipeline
    .FromExcel<Order>("orders.xlsx", new ExcelSourceOptions
    {
        HeaderRow = 2  // 第 2 行是表头
    })
    .ToListAsync();

// 列映射
await DataForgePipeline
    .FromExcel<dynamic>("orders.xlsx", new ExcelSourceOptions
    {
        ColumnMapping = new Dictionary<string, string>
        {
            ["订单号"] = "OrderId",
            ["客户名"] = "CustomerName",
            ["金额"] = "Amount"
        }
    })
    .Select(r => new Order
    {
        OrderId = r.OrderId,
        CustomerName = r.CustomerName,
        Amount = Convert.ToDecimal(r.Amount)
    })
    .ToListAsync();

// 从 Stream 读取
await using var stream = File.OpenRead("orders.xlsx");
await DataForgePipeline
    .FromExcel<Order>(stream)
    .ToListAsync();

// 读取所有 Sheet
var sheets = new[] { "Q1", "Q2", "Q3", "Q4" };
foreach (var sheet in sheets)
{
    await DataForgePipeline
        .FromExcel<SalesRow>("sales-2024.xlsx", new ExcelSourceOptions
        {
            SheetName = sheet
        })
        .ToCsv($"sales-{sheet}.csv");
}
```

### JSON

```csharp
// 基础用法（数组）
var orders = await DataForgePipeline
    .FromJsonArray<Order>("orders.json")
    .ToListAsync();

// 单个对象
await DataForgePipeline
    .FromJson<Order>("order-detail.json")
    .Select(o => new { o.OrderId, o.CustomerName })
    .ToJson("order-summary.json");

// 自定义选项
await DataForgePipeline
    .FromJsonArray<Order>("orders.json", new JsonSourceOptions
    {
        PropertyNameCaseInsensitive = true,
        DateFormat = "yyyy-MM-dd"
    })
    .ToListAsync();

// 从 Stream 读取
await using var stream = File.OpenRead("orders.json");
await DataForgePipeline
    .FromJsonArray<Order>(stream)
    .ToListAsync();

// 从字符串读取
var json = await File.ReadAllTextAsync("orders.json");
await DataForgePipeline
    .FromJsonString<Order>(json)
    .ToListAsync();

// 复杂嵌套 JSON
await DataForgePipeline
    .FromJson<OrderWrapper>("api-response.json")
    .SelectMany(w => w.Data.Orders)  // 展平嵌套数组
    .Where(o => o.Status == "Completed")
    .ToCsv("completed-orders.csv");
```

---

## 内存数据源

### 从集合创建

```csharp
// 从 List 创建
var orders = new List<Order>
{
    new Order { OrderId = "O001", Amount = 100 },
    new Order { OrderId = "O002", Amount = 200 }
};

await DataForgePipeline
    .FromCollection(orders)
    .Where(o => o.Amount > 150)
    .ToListAsync();

// 从数组创建
var orderArray = GetOrdersFromMemory();
await DataForgePipeline
    .FromArray(orderArray)
    .ToCsv("memory-orders.csv");

// 从 IEnumerable 创建
IEnumerable<Order> GetOrders() { /* ... */ }
await DataForgePipeline
    .FromEnumerable(GetOrders())
    .ToListAsync();

// 集合扩展方法
orders.ToDataForge()
    .Where(o => o.Amount > 100)
    .ToCsv("filtered.csv");
```

---

## 自定义数据源

### 实现 IDataSource<T>

```csharp
/// <summary>
/// REST API 数据源实现示例
/// </summary>
public class RestApiSource<T> : IDataSource<T>
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public string Name => $"REST API: {_endpoint}";
    public DataSourceType SourceType => DataSourceType.RestApi;
    
    public RestApiSource(string baseUrl, string endpoint)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _endpoint = endpoint;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
    
    public async IAsyncEnumerable<T> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(_endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
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
            RecordCount = null,
            Fields = null
        };
    }
}

/// <summary>
/// HTTP 请求头配置
/// </summary>
public class RestApiSource<T> : IDataSource<T>
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _headers = new();
    
    public RestApiSource WithHeader(string name, string value)
    {
        _headers[name] = value;
        return this;
    }
    
    public RestApiSource WithAuth(string token)
    {
        _headers["Authorization"] = $"Bearer {token}";
        return this;
    }
}
```

### 添加扩展方法

```csharp
public static class RestApiSourceExtensions
{
    public static RestApiSource<T> FromRestApi<T>(string baseUrl, string endpoint)
    {
        return new RestApiSource<T>(baseUrl, endpoint);
    }
    
    public static RestApiSource<T> FromRestApi<T>(string baseUrl, string endpoint, string authToken)
    {
        return new RestApiSource<T>(baseUrl, endpoint).WithAuth(authToken);
    }
}

// 使用
await DataForgePipeline
    .FromRestApi<Product>("https://api.example.com", "/products")
    .Where(p => p.InStock)
    .ToCsv("products.csv");
```

### 完整示例：MongoDB 数据源

```csharp
/// <summary>
/// MongoDB 数据源
/// </summary>
public class MongoDbSource<T> : IDataSource<T>
{
    private readonly IMongoCollection<T> _collection;
    private readonly FilterDefinition<T>? _filter;
    private readonly FindOptions _options;
    
    public string Name { get; }
    public DataSourceType SourceType => DataSourceType.MongoDb;
    
    public MongoDbSource(IMongoDatabase database, string collectionName, 
        FilterDefinition<T>? filter = null)
    {
        _collection = database.GetCollection<T>(collectionName);
        _filter = filter;
        _options = new FindOptions
        {
            BatchSize = 1000,
            NoCursorTimeout = true
        };
        
        Name = $"MongoDB: {collectionName}";
    }
    
    public async IAsyncEnumerable<T> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = _filter ?? Builders<T>.Filter.Empty;
        
        using var cursor = await _collection.FindAsync(filter, _options, cancellationToken);
        
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                yield return document;
            }
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
            RecordCount = _collection.EstimatedDocumentCount(),
            Fields = null
        };
    }
}

public static class MongoDbSourceExtensions
{
    public static MongoDbSource<T> FromMongoDb<T>(
        IMongoDatabase database, 
        string collectionName,
        FilterDefinition<T>? filter = null)
    {
        return new MongoDbSource<T>(database, collectionName, filter);
    }
    
    // 带 BsonDocument 过滤器
    public static MongoDbSource<T> FromMongoDb<T>(
        IMongoDatabase database,
        string collectionName,
        string filterJson)
    {
        var filter = MongoDB.Bson.Serialization.BsonSerializer
            .Deserialize<FilterDefinition<T>>(filterJson);
        return new MongoDbSource<T>(database, collectionName, filter);
    }
}
```

---

## 连接池管理

### 自动连接池

```csharp
// SQL Server 连接池（默认启用）
var options = new SqlSourceOptions
{
    Pooling = true,
    MinPoolSize = 5,
    MaxPoolSize = 50
};

// 在高性能场景下使用连接池
await DataForgePipeline
    .FromSqlServer<Order>(connString, options)
    .Where(o => o.Status == "Pending")
    .ForEachAsync(async order =>  // 多次执行
    {
        await ProcessOrderAsync(order);
    });
```

### 手动连接管理

```csharp
/// <summary>
/// 推荐：使用 using 语句管理连接生命周期
/// </summary>
public async Task ProcessOrders()
{
    await using var connection = new SqlConnection(connString);
    await connection.OpenAsync();
    
    var orders = await DataForgePipeline
        .FromSqlServer<Order>(connection, "Orders")
        .Where(o => o.Status == "Pending")
        .ToListAsync();
    
    foreach (var order in orders)
    {
        await ProcessOrderAsync(order);
    }
} // 连接自动关闭和释放
```

### 重用连接

```csharp
/// <summary>
/// 在多个管道中重用同一个连接
/// </summary>
public async Task ExportMultipleReports()
{
    await using var connection = new SqlConnection(connString);
    await connection.OpenAsync();
    
    // 报表 1
    await DataForgePipeline
        .FromSqlServer<Order>(connection, "Orders")
        .Where(o => o.Region == "华北")
        .ToCsv("north-orders.csv");
    
    // 报表 2（复用连接）
    await DataForgePipeline
        .FromSqlServer<Customer>(connection, "Customers")
        .Where(c => c.Region == "华北")
        .ToCsv("north-customers.csv");
    
    // 报表 3（复用连接）
    await DataForgePipeline
        .FromSqlServer<Product>(connection, "Products")
        .ToCsv("all-products.csv");
} // 连接在 using 结束时释放
```

### 长连接 vs 短连接

```csharp
// 短连接模式（每次创建新连接）
// 适用于：一次性任务、定时批处理
await DataForgePipeline
    .FromSqlServer<Order>(connString, "Orders")
    .ToCsv("orders.csv");  // 完成后自动关闭连接

// 长连接模式（保持连接复用）
// 适用于：大量数据处理、实时数据流
await using var connection = new SqlConnection(connString);
await connection.OpenAsync();

for (var i = 0; i < 100; i++)
{
    await DataForgePipeline
        .FromSqlServer<Order>(connection, "Orders")
        .Where(o => o.BatchId == i)
        .ToCsv($"batch-{i}.csv");
} // 复用同一个连接
```
