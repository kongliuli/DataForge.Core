# 管道编程指南

本指南详细介绍 DataForge.Core 管道编程的完整用法，包括链式 API、执行模型、错误处理和常用模式。

## 目录

1. [管道设计理念](#管道设计理念)
2. [链式 API 完整方法列表](#链式-api-完整方法列表)
3. [管道执行模型](#管道执行模型)
4. [管道组合](#管道组合)
5. [错误处理](#错误处理)
6. [性能优化建议](#性能优化建议)
7. [常用编程模式](#常用编程模式)

---

## 管道设计理念

DataForge.Core 的管道设计借鉴了函数式编程和 LINQ 的优点：

1. **链式调用** - 方法返回管道本身，支持链式调用
2. **延迟执行** - 管道在枚举时才真正执行
3. **无副作用** - 管道操作不修改原始数据
4. **类型安全** - 完整的泛型支持

```
数据源 ──▶ [过滤] ──▶ [映射] ──▶ [排序] ──▶ [分组] ──▶ 数据目标
           Where     Select    OrderBy   GroupBy     ToXxx
```

### 设计原则

```
┌─────────────────────────────────────────────────────────────┐
│                     管道操作分类                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  转换操作（返回新管道）                                        │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Where, Select, SelectMany, Distinct, OrderBy...       │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  执行操作（触发实际执行）                                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ ToList, ToCsv, ToJson, Count, First...               │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  组合操作（返回合并/分支管道）                                  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Merge, Concat, Zip, Branch...                        │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 链式 API 完整方法列表

### 创建管道

```csharp
// 从各种数据源创建
var p1 = DataForgePipeline.FromCsv<Order>("orders.csv");
var p2 = DataForgePipeline.FromExcel<Order>("orders.xlsx");
var p3 = DataForgePipeline.FromSqlServer<Order>(conn, "Orders");
var p4 = DataForgePipeline.FromJson<Order>("order.json");
var p5 = DataForgePipeline.FromCollection(myList);

// 从已有集合扩展
var p6 = myList.ToDataForge();
var p7 = asyncStream.ToDataForge();
```

### 过滤操作 (Where)

```csharp
// 基本过滤
pipeline.Where(o => o.Status == "Completed")

// 带索引过滤
pipeline.Where((o, index) => index < 100)

// 异步过滤
pipeline.WhereAsync(async o => await CheckStatusAsync(o.Id))

// 组合条件
pipeline
    .Where(o => o.Amount > 1000)
    .Where(o => o.OrderDate > DateTime.Today.AddMonths(-1))
```

### 投影操作 (Select)

```csharp
// 基本投影
pipeline.Select(o => new { o.OrderId, o.Amount })

// 带索引投影
pipeline.Select((o, index) => new { RowNum = index + 1, o.OrderId })

// 异步投影
pipeline.SelectAsync(async o => await EnrichDataAsync(o))

// 条件投影
pipeline.Select(o => new
{
    o.OrderId,
    StatusText = o.Status switch
    {
        "P" => "待付款",
        "C" => "已完成",
        "R" => "已退款",
        _ => "未知"
    }
})

// 展平操作
pipeline.SelectMany(o => o.LineItems)

// 条件展平
pipeline.SelectMany(
    o => o.LineItems,
    (order, item) => new { order.OrderId, item.ProductName, item.Quantity })
```

### 去重操作 (Distinct)

```csharp
// 全部去重（需要类型可比较）
pipeline.Distinct()

// 按键去重
pipeline.DistinctBy(o => o.CustomerId)

// 多键去重
pipeline.DistinctBy(o => new { o.CustomerId, o.ProductId })

// 保留最后一条
pipeline.DistinctBy(o => o.CustomerId, keepLast: true)
```

### 排序操作 (OrderBy)

```csharp
// 单字段升序
pipeline.OrderBy(o => o.OrderDate)

// 单字段降序
pipeline.OrderByDescending(o => o.Amount)

// 多级排序
pipeline
    .OrderBy(o => o.Region)
    .ThenBy(o => o.OrderDate)
    .ThenByDescending(o => o.Amount)

// 复合键排序
pipeline.OrderBy(o => new { o.Region, o.OrderDate })

// 条件排序
pipeline.OrderBy(o => o.IsPriority ? 0 : 1)
    .ThenByDescending(o => o.OrderDate)
```

### 分页操作 (Skip/Take)

```csharp
// 取前 N 条
pipeline.Take(100)

// 跳过 N 条
pipeline.Skip(100)

// 分页：第 2 页，每页 50 条
pipeline.Skip(50).Take(50)

// 条件分页
pipeline.TakeWhile(o => o.Rank <= 100)
pipeline.SkipWhile(o => o.Status == "Draft")
```

### 分组操作 (GroupBy)

```csharp
// 基本分组
var grouped = pipeline.GroupBy(o => o.Region)

// 分组后聚合
grouped.Select(g => new
{
    Region = g.Key,
    OrderCount = g.Count(),
    TotalAmount = g.Sum(o => o.Amount),
    AvgAmount = g.Average(o => o.Amount)
})

// 多级分组
pipeline.GroupBy(o => new { o.Year, o.Month })

// 分组后排序
grouped.Select(g => g.OrderByDescending(x => x.Amount).First())
```

### 验证操作 (ValidateWith)

```csharp
// 添加验证器
pipeline.ValidateWith(new OrderValidator())

// 验证失败继续执行
pipeline
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .ToCsv("all-orders.csv")

// 验证失败停止
pipeline
    .ValidateWith(new OrderValidator())
    .FailOnValidationError()
    .ToCsv("valid-orders.csv")

// 获取验证结果
var result = await pipeline
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .CollectValidationResults()
    .ToListAsync();

var errors = result.Where(r => !r.IsValid);
```

### 自定义转换 (TransformWith)

```csharp
// 自定义同步转换
pipeline.TransformWith(o => NormalizeOrder(o))

// 自定义异步转换
pipeline.TransformWithAsync(async o => await EnrichAsync(o))

// 添加转换器实例
pipeline.TransformWith(new AddressNormalizationTransform())

// 链式多个转换
pipeline
    .TransformWith(new TrimTransform())
    .TransformWith(new UpperCaseTransform())
    .TransformWith(o => CalculateDiscount(o))
```

---

## 管道执行模型

### 延迟执行 vs 立即执行

DataForge.Core 采用**延迟执行**模式：

```csharp
var pipeline = DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)    // 管道定义（不执行）
    .Select(o => new { o.OrderId, o.Amount });  // 管道定义（不执行）

// 此时尚未读取任何数据
// 管道只是描述了处理流程

var result = await pipeline.ToListAsync();  // 触发执行
```

### 触发执行的操作

```csharp
// 导出操作 - 触发完整执行
await pipeline.ToCsv("output.csv");
await pipeline.ToJson("output.json");
await pipeline.ToExcel("output.xlsx");
await pipeline.ToSqlServer(conn, "Orders");

// 收集操作 - 触发完整执行
await pipeline.ToListAsync();
await pipeline.ToArrayAsync();
await pipeline.FirstOrDefaultAsync();

// 计数操作 - 触发完整执行
await pipeline.CountAsync();

// 聚合操作 - 触发完整执行
await pipeline.AggregateAsync((sum, item) => sum + item.Amount, 0m);

// 遍历操作 - 触发流式执行
await foreach (var item in pipeline)
{
    Process(item);
}
```

### 执行流程图

```
┌─────────────────────────────────────────────────────────────────────┐
│                        管道执行流程                                   │
└─────────────────────────────────────────────────────────────────────┘

     管道定义阶段                    管道执行阶段
     (延迟，无IO)                    (触发IO)
          │                              │
          ▼                              ▼
   ┌─────────────┐              ┌─────────────────┐
   │ FromCsv()   │              │                 │
   │     ↓       │              │  1. 打开文件    │
   │ Where()     │              │  2. 读取行      │
   │     ↓       │              │  3. 过滤         │
   │ Select()    │              │  4. 映射         │
   │     ↓       │              │  5. 写入目标      │
   │ OrderBy()   │              │  6. 关闭文件      │
   └─────────────┘              └─────────────────┘
          │                              │
          ▼                              ▼
   ┌─────────────────────────────────────────────┐
   │              ToCsv("output.csv")            │
   │                   ↑ 触发执行 ↑                │
   └─────────────────────────────────────────────┘
```

### 多次执行

每次触发执行都会重新读取数据：

```csharp
var pipeline = DataForgePipeline.FromCsv<Order>("orders.csv");

// 执行 1：CSV → 内存
var list1 = await pipeline.ToListAsync();

// 执行 2：CSV → JSON（重新读取）
await pipeline.ToJson("orders.json");  // 会再次读取 CSV

// 执行 3：CSV → Excel（再次重新读取）
await pipeline.ToExcel("orders.xlsx"); // 又会再次读取 CSV
```

### 缓存执行结果

```csharp
// 使用 ToListAsync 缓存结果
var orders = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)
    .ToListAsync();  // 执行并缓存到内存

// 从缓存创建新管道
orders.ToDataForge()
    .Select(o => new { o.OrderId, o.Amount })
    .ToJson("filtered.json");

orders.ToDataForge()
    .GroupBy(o => o.Region)
    .Select(g => new { Region = g.Key, Count = g.Count() })
    .ToCsv("regional-count.csv");
```

---

## 管道组合

### 合并多个管道 (Merge)

```csharp
// 合并同类型数据源
var allOrders = DataForgePipeline.Merge(
    DataForgePipeline.FromCsv<Order>("orders-2023.csv"),
    DataForgePipeline.FromCsv<Order>("orders-2024.csv"),
    DataForgePipeline.FromExcel<Order>("orders-2025.xlsx")
);

// 合并后统一处理
await allOrders
    .Where(o => o.Status == "Completed")
    .GroupBy(o => o.Region)
    .Select(g => new { Region = g.Key, Total = g.Sum(o => o.Amount) })
    .ToCsv("consolidated-report.csv");

// 不同类型合并（需投影到相同类型）
await DataForgePipeline.Merge(
    DataForgePipeline.FromSqlServer<Customer>(conn, "Customers")
        .Select(c => new { Id = c.CustomerId, Name = c.Name, Source = "DB" }),
    DataForgePipeline.FromExcel<CustomerRow>("new-customers.xlsx")
        .Select(r => new { Id = r.Id, Name = r.Name, Source = "Excel" })
)
.DistinctBy(c => c.Id)
.ToJson("all-customers.json");
```

### 连接两个管道 (Zip)

```csharp
// Zip 合并（需要相同长度）
var orders = DataForgePipeline.FromCsv<Order>("orders.csv");
var shipments = DataForgePipeline.FromCsv<Shipment>("shipments.csv");

await orders.Zip(shipments, (order, ship) => new
{
    order.OrderId,
    order.Amount,
    ship.TrackingNumber,
    ship.ShipDate
})
.ToCsv("order-shipment.csv");

// Zip + 过滤
orders.Zip(shipments, (o, s) => (Order: o, Shipment: s))
    .Where(x => x.Order.Amount > 1000)
    .Select(x => new { x.Order.OrderId, x.Shipment.TrackingNumber })
    .ToJson("high-value-shipments.json");
```

### 条件管道 (Branch)

```csharp
// 根据条件选择管道
var pipeline = DataForgePipeline.FromCsv<Order>("orders.csv");

// 策略模式
if (exportFormat == "csv")
{
    await pipeline.ToCsv("output.csv");
}
else if (exportFormat == "json")
{
    await pipeline.ToJson("output.json");
}

// 动态管道构建
var basePipeline = DataForgePipeline.FromCsv<Order>("orders.csv");

IDataPipeline pipeline = basePipeline;
if (includeDetails)
{
    pipeline = pipeline.Select(o => new { o.OrderId, o.Amount, Details = GetDetails(o) });
}
if (filterByDate)
{
    pipeline = pipeline.Where(o => o.OrderDate >= startDate);
}
await pipeline.ToExcel("report.xlsx");
```

### 管道嵌套

```csharp
// 子管道
var orderValidation = DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError();

// 主管道使用子管道结果
await orderValidation
    .Where(o => o.Amount > 0)  // 过滤掉验证失败的（无金额）
    .GroupBy(o => o.Region)
    .ToCsv("validated-report.csv");
```

---

## 错误处理

### 错误处理策略

```csharp
// 策略 1：遇到错误继续（默认）
var result1 = await pipeline
    .OnErrorContinue()
    .ToCsv("output.csv");

// 策略 2：遇到错误停止
try
{
    await pipeline.OnErrorStop().ToCsv("output.csv");
}
catch (DataForgeException ex)
{
    Console.WriteLine($"处理在第 {ex.LineNumber} 行失败: {ex.Message}");
}

// 策略 3：跳过错误项
var result3 = await pipeline
    .OnErrorSkip()
    .ToCsv("output.csv");
Console.WriteLine($"跳过 {result3.SkippedCount} 条错误数据");

// 策略 4：自定义错误处理
var result4 = await pipeline
    .OnError((error, item) =>
    {
        Logger.LogError("处理失败: {@Item}, Error: {Error}", item, error.Message);
        return ErrorAction.Skip;
    })
    .ToCsv("output.csv");
```

### 错误收集模式

```csharp
// 收集所有错误
var result = await pipeline
    .ContinueOnError()
    .CollectErrors()
    .ToCsv("output.csv");

Console.WriteLine($"成功: {result.SuccessCount}, 失败: {result.ErrorCount}");
foreach (var error in result.Errors)
{
    Console.WriteLine($"行 {error.LineNumber}: {error.Exception.Message}");
    Console.WriteLine($"  数据: {JsonSerializer.Serialize(error.Data)}");
}
```

### 验证失败处理

```csharp
// 分离有效和无效数据
var result = await pipeline
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .CollectValidationResults()
    .ToListAsync();

var validOrders = result.Where(r => r.IsValid).Select(r => (Order)r.Data!);
var invalidOrders = result.Where(r => !r.IsValid);

await validOrders.ToDataForge().ToCsv("valid-orders.csv");
await invalidOrders.Select(r => new { Error = r.Errors[0].ErrorMessage, Data = r.Data })
    .ToJson("validation-errors.json");
```

### 重试策略

```csharp
// 自动重试配置
var result = await DataForgePipeline
    .FromSqlServer<Order>(conn, options: new SqlSourceOptions
    {
        EnableRetry = true,
        MaxRetryCount = 3,
        RetryDelayMs = 1000
    })
    .Where(o => o.Status == "Pending")
    .ToCsv("pending-orders.csv");
```

---

## 性能优化建议

### 1. 尽早过滤

```csharp
// ❌ 低效：先读取所有数据再过滤
DataForgePipeline
    .FromCsv<Order>("orders.csv")    // 读取 100 万行
    .ToListAsync()                    // 全部加载到内存
    .Where(o => o.Amount > 1000)      // 再过滤

// ✅ 高效：尽早过滤
DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)      // 只读取满足条件的行
    .ToCsv("filtered.csv");
```

### 2. 使用流式处理

```csharp
// ❌ 内存压力大
var allData = await pipeline.ToListAsync();  // 全部加载到内存
foreach (var item in allData)
{
    await ProcessAsync(item);
}

// ✅ 流式处理
await foreach (var item in pipeline)
{
    await ProcessAsync(item);
}
```

### 3. 选择需要的字段

```csharp
// ❌ 传输不必要的数据
DataForgePipeline
    .FromSqlServer<Order>(conn, "Orders")  // SELECT *
    .Where(o => o.Status == "Completed")
    .ToCsv("output.csv");

// ✅ 只选择需要的字段
DataForgePipeline
    .FromSqlServer<Order>(conn, "Orders")
    .Select(o => new { o.OrderId, o.CustomerName, o.Amount, o.OrderDate })
    .Where(o => o.Amount > 1000)
    .ToCsv("output.csv");
```

### 4. 批量写入优化

```csharp
// CSV 导出批量配置
await pipeline.ToCsv("output.csv", new CsvExportOptions
{
    BatchSize = 50000  // 增大批量减少 IO
});

// 数据库批量插入
await pipeline.ToSqlServer(conn, "Orders", new SqlServerExportOptions
{
    BatchSize = 5000   // 增大批量减少网络往返
});
```

### 5. 避免不必要的排序

```csharp
// ❌ 排序大数据集很慢
DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)
    .OrderBy(o => o.OrderDate)  // 全部排序后再分页
    .Take(100)
    .ToCsv("top-100.csv");

// ✅ 分页后再排序
DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)
    .Take(100)  // 先取 100 条
    .OrderBy(o => o.OrderDate)  // 只排序 100 条
    .ToCsv("top-100.csv");
```

---

## 常用编程模式

### 模式 1：ETL 流水线

```csharp
/// <summary>
/// 典型的 ETL 模式：Extract → Transform → Load
/// </summary>
public async Task ExtractTransformLoad(
    string sourcePath,
    string targetPath)
{
    await DataForgePipeline
        .FromExcel<SalesRow>(sourcePath, "SalesData")     // Extract
        .Select(row => new SalesRecord                    // Transform
        {
            Id = Guid.NewGuid(),
            ProductCode = row.ProductCode?.Trim(),
            Quantity = int.TryParse(row.Quantity, out var q) ? q : 0,
            UnitPrice = decimal.TryParse(row.Price, out var p) ? p : 0,
            SaleDate = DateTime.TryParse(row.Date, out var d) ? d : DateTime.MinValue,
            Region = NormalizeRegion(row.Region)
        })
        .Where(r => r.Quantity > 0 && r.UnitPrice > 0)   // More Transform
        .ValidateWith(new SalesRecordValidator())        // Validate
        .ToJson(targetPath);                              // Load
}
```

### 模式 2：多数据源聚合

```csharp
/// <summary>
/// 聚合来自多个数据源的数据
/// </summary>
public async Task AggregateMultipleSources()
{
    var customers = await DataForgePipeline.Merge(
        DataForgePipeline.FromSqlServer<Customer>(conn, "Customers")
            .Select(c => new CustomerInfo { Id = c.Id, Name = c.Name, Source = "DB" }),
        DataForgePipeline.FromCsv<CustomerCsv>("new-customers.csv")
            .Select(c => new CustomerInfo { Id = c.Id, Name = c.Name, Source = "CSV" }),
        DataForgePipeline.FromJson<CustomerJson>("api-customers.json")
            .Select(c => new CustomerInfo { Id = c.Id, Name = c.Name, Source = "API" })
    )
    .DistinctBy(c => c.Id)
    .OrderBy(c => c.Name)
    .ToListAsync();
    
    return customers;
}
```

### 模式 3：数据清洗管道

```csharp
/// <summary>
/// 多阶段数据清洗
/// </summary>
public async Task<DataCleaningResult> CleanData(string inputPath, string outputPath)
{
    var result = await DataForgePipeline
        .FromCsv<DirtyRow>("dirty-data.csv")
        // 阶段 1：基础清理
        .TransformWith(row => row with 
        { 
            Name = row.Name?.Trim(), 
            Email = row.Email?.Trim()?.ToLower(),
            Phone = NormalizePhone(row.Phone)
        })
        // 阶段 2：格式标准化
        .TransformWith(row => row with
        {
            Amount = decimal.TryParse(row.Amount, out var a) ? a : 0,
            Date = DateTime.TryParse(row.Date, out var d) ? d : DateTime.MinValue
        })
        // 阶段 3：业务规则过滤
        .Where(row => row.Amount > 0 && row.Date != DateTime.MinValue)
        // 验证
        .ValidateWith(new CleanDataValidator())
        .ContinueOnValidationError()
        .CollectValidationResults()
        .ToCsv(outputPath);
    
    return new DataCleaningResult
    {
        TotalProcessed = result.TotalProcessed,
        SuccessCount = result.RecordsWritten,
        FailedCount = result.Errors.Count,
        Errors = result.Errors
    };
}
```

### 模式 4：增量同步

```csharp
/// <summary>
/// 基于时间戳的增量同步
/// </summary>
public async Task IncrementalSync(
    string connectionString,
    DateTime lastSyncTime)
{
    var currentMaxTime = DateTime.MinValue;
    
    await DataForgePipeline
        .FromSqlServer<Order>(connectionString, "Orders")
        .Where(o => o.UpdatedAt > lastSyncTime)
        .ForEachAsync(async order =>  // 自定义操作
        {
            await SyncToTargetAsync(order);
            if (order.UpdatedAt > currentMaxTime)
                currentMaxTime = order.UpdatedAt;
        });
    
    return currentMaxTime;
}
```

### 模式 5：分批处理

```csharp
/// <summary>
/// 大文件分批处理
/// </summary>
public async Task ProcessInBatches(string filePath)
{
    const int batchSize = 10000;
    var batchNumber = 0;
    
    await foreach (var batch in DataForgePipeline
        .FromCsv<Order>(filePath)
        .Batch(batchSize))  // 分批
    {
        batchNumber++;
        await ProcessBatchAsync(batch, batchNumber);
        
        Console.WriteLine($"处理批次 {batchNumber}，{batch.Count} 条记录");
    }
}

/// <summary>
/// 分页导出
/// </summary>
public async Task PaginatedExport(int pageSize = 1000)
{
    var page = 0;
    while (true)
    {
        var records = await DataForgePipeline
            .FromSqlServer<Order>(conn, "Orders")
            .OrderBy(o => o.OrderId)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        if (records.Count == 0) break;
        
        await records.ToDataForge()
            .ToCsv($"orders-page-{page}.csv");
        
        page++;
    }
}
```

### 模式 6：条件处理

```csharp
/// <summary>
/// 根据数据内容条件处理
/// </summary>
public async Task ConditionalProcess()
{
    var orders = await DataForgePipeline
        .FromCsv<Order>("orders.csv")
        .Select(o => new
        {
            o.OrderId,
            o.Amount,
            Category = o.Amount switch
            {
                < 100 => "小额订单",
                < 1000 => "中等订单",
                < 10000 => "大额订单",
                _ => "超大额订单"
            },
            Priority = o.Amount > 5000 ? "高优先级" : "普通"
        })
        .ToListAsync();
}
```

### 模式 7：Lookup 关联

```csharp
/// <summary>
/// 使用字典进行 Lookup
/// </summary>
public async Task EnrichWithLookup()
{
    // 加载产品信息到字典
    var products = await DataForgePipeline
        .FromCsv<Product>("products.csv")
        .ToDictionaryAsync(p => p.ProductCode);
    
    // 关联查询
    var orderDetails = await DataForgePipeline
        .FromCsv<OrderItem>("order-items.csv")
        .Select(item => new
        {
            item.OrderId,
            item.ProductCode,
            ProductName = products.TryGet(item.ProductCode, out var p) ? p.Name : "未知",
            item.Quantity,
            UnitPrice = products.TryGet(item.ProductCode, out var p2) ? p2.Price : 0,
            TotalAmount = item.Quantity * (products.TryGet(item.ProductCode, out var p3) ? p3.Price : 0)
        })
        .ToCsv("order-details.csv");
}
```

### 模式 8：数据质量报告

```csharp
/// <summary>
/// 生成数据质量报告
/// </summary>
public async Task<DataQualityReport> GenerateQualityReport(string filePath)
{
    var allRecords = await DataForgePipeline
        .FromCsv<Customer>("customers.csv")
        .ToListAsync();
    
    var report = new DataQualityReport
    {
        TotalRecords = allRecords.Count,
        NullEmailCount = allRecords.Count(c => string.IsNullOrEmpty(c.Email)),
        InvalidPhoneCount = allRecords.Count(c => !IsValidPhone(c.Phone)),
        DuplicateEmailCount = allRecords
            .GroupBy(c => c.Email)
            .Count(g => g.Count() > 1),
        UniqueCustomerCount = allRecords.DistinctBy(c => c.Email).Count()
    };
    
    return report;
}
```

### 模式 9：并行处理

```csharp
/// <summary>
/// 并行处理独立任务
/// </summary>
public async Task ParallelExport()
{
    var tasks = new[]
    {
        DataForgePipeline.FromSqlServer<Order>(conn, "Orders")
            .Where(o => o.Region == "华北")
            .ToCsvAsync("orders-north.csv"),
            
        DataForgePipeline.FromSqlServer<Order>(conn, "Orders")
            .Where(o => o.Region == "华东")
            .ToCsvAsync("orders-east.csv"),
            
        DataForgePipeline.FromSqlServer<Order>(conn, "Orders")
            .Where(o => o.Region == "华南")
            .ToCsvAsync("orders-south.csv")
    };
    
    var results = await Task.WhenAll(tasks);
    
    Console.WriteLine($"导出完成，共 {results.Sum(r => r.RecordsWritten)} 条记录");
}
```

### 模式 10：事务性导出

```csharp
/// <summary>
/// 数据库事务性导出
/// </summary>
public async Task<bool> TransactionalExport(string filePath)
{
    var options = new SqlServerExportOptions
    {
        UseTransaction = true,
        BatchSize = 1000,
        InsertMode = InsertMode.Upsert,
        UpsertKeyColumns = new[] { "OrderId" }
    };
    
    try
    {
        await DataForgePipeline
            .FromCsv<Order>(filePath)
            .ValidateWith(new OrderValidator())
            .ToSqlServer(connString, "Orders", options);
        
        return true;
    }
    catch
    {
        // 事务自动回滚
        return false;
    }
}
```

---

## 总结

掌握以下要点即可熟练使用 DataForge.Core 管道：

1. **理解延迟执行** - 管道定义 ≠ 数据执行
2. **善用链式 API** - 方法组合实现复杂逻辑
3. **选择合适操作** - Where > Select > OrderBy（按此优先级）
4. **处理错误** - 了解错误处理策略和验证机制
5. **性能意识** - 尽早过滤、流式处理、避免不必要操作
