# 场景实战手册

本文档提供 10 个真实业务场景的完整解决方案，每个场景包含业务背景、代码实现、关键点说明和常见问题。

## 目录

1. [订单数据月报生成](#场景1-订单数据月报生成)
2. [Excel 数据清洗入库](#场景2-excel-数据清洗入库)
3. [多系统客户数据合并](#场景3-多系统客户数据合并)
4. [数据库表结构迁移](#场景4-数据库表结构迁移)
5. [日志文件分析报表](#场景5-日志文件分析报表)
6. [定时数据同步](#场景6-定时数据同步)
7. [数据质量检测报告](#场景7-数据质量检测报告)
8. [接口数据 ETL](#场景8-接口数据-etl)
9. [大文件分批处理](#场景9-大文件分批处理)
10. [实时数据流处理](#场景10-实时数据流处理)

---

## 场景 1: 订单数据月报生成

### 业务背景

财务部门每月需要生成销售月报，从 SQL Server 读取订单数据，按区域汇总，生成 CSV 报表用于财务对账。

### 代码实现

```csharp
/// <summary>
/// 月报生成器
/// </summary>
public async Task GenerateMonthlyReport(int year, int month)
{
    var connectionString = Configuration.GetConnectionString("SalesDb");
    var outputPath = $"reports/sales-report-{year}{month:D2}.csv";
    
    var report = await DataForgePipeline
        .FromSqlServer<SalesOrder>(connectionString, "SalesOrders")
        .Where(o => o.OrderDate.Year == year && o.OrderDate.Month == month)
        .Where(o => o.Status == "Completed")  // 只统计已完成的订单
        .GroupBy(o => o.Region)
        .Select(g => new MonthlySalesReport
        {
            Year = year,
            Month = month,
            Region = g.Key,
            OrderCount = g.Count(),
            TotalSales = g.Sum(o => o.Amount),
            TotalCost = g.Sum(o => o.Cost),
            GrossProfit = g.Sum(o => o.Amount - o.Cost),
            GrossMargin = g.Sum(o => o.Amount) == 0 
                ? 0 
                : Math.Round((g.Sum(o => o.Amount - o.Cost) / g.Sum(o => o.Amount) * 100), 2),
            AvgOrderValue = Math.Round(g.Average(o => o.Amount), 2),
            MaxOrderValue = g.Max(o => o.Amount),
            MinOrderValue = g.Min(o => o.Amount)
        })
        .OrderByDescending(r => r.TotalSales)
        .ToCsv(outputPath, new CsvExportOptions
        {
            IncludeHeader = true,
            Encoding = Encoding.UTF8
        });
    
    Console.WriteLine($"月报生成完成");
    Console.WriteLine($"  文件: {report.FilePath}");
    Console.WriteLine($"  区域数: {report.RecordsWritten}");
    Console.WriteLine($"  耗时: {report.Duration.TotalSeconds:F2}s");
}

/// <summary>
/// 月报实体
/// </summary>
public record MonthlySalesReport
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Region { get; init; } = "";
    public int OrderCount { get; init; }
    public decimal TotalSales { get; init; }
    public decimal TotalCost { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal GrossMargin { get; init; }
    public decimal AvgOrderValue { get; init; }
    public decimal MaxOrderValue { get; init; }
    public decimal MinOrderValue { get; init; }
}
```

### 关键点

1. **分组聚合** - 使用 `GroupBy` 按区域分组，再在 Select 中对每个分组执行聚合
2. **条件过滤** - 多层 Where 确保只统计有效数据
3. **排序** - 按销售额降序，便于查看重点区域
4. **decimal 精度** - 金额计算使用 decimal 类型，保持精度

### 常见问题

| 问题 | 解决方案 |
|-----|---------|
| 日期范围查询慢 | 在 OrderDate 上建立索引 |
| 浮点数精度丢失 | 使用 decimal 而非 double |
| 大数据量 OOM | 使用 `ToCsv` 流式写入，不占用大量内存 |

---

## 场景 2: Excel 数据清洗入库

### 业务背景

运营人员从各个渠道导出 Excel 格式的订单数据，格式不统一（表头不一致、日期格式混乱、金额单位不同），需要清洗后入库。

### 代码实现

```csharp
/// <summary>
/// Excel 订单数据清洗器
/// </summary>
public class OrderDataCleaner
{
    private readonly string _connectionString;
    
    public OrderDataCleaner(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    /// <summary>
    /// 清洗并导入 Excel 数据
    /// </summary>
    public async Task<ImportResult> CleanAndImport(string excelPath, string sheetName)
    {
        // 定义验证器
        var validator = new CleanedOrderValidator();
        
        // 执行清洗流程
        var result = await DataForgePipeline
            // Step 1: 从 Excel 读取原始数据
            .FromExcel<DirtyOrderRow>(excelPath, new ExcelSourceOptions
            {
                SheetName = sheetName,
                HeaderRow = 1,
                TrimFields = true
            })
            
            // Step 2: 类型转换和数据清洗
            .Select(row => new CleanedOrder
            {
                // 字符串清洗
                OrderId = row.OrderID?.Trim(),
                CustomerName = row.Customer?.Trim(),
                Region = NormalizeRegion(row.Region?.Trim()),
                Status = NormalizeStatus(row.Status?.Trim()),
                
                // 金额转换（处理 "¥1,234.56" 格式）
                Amount = ParseAmount(row.Amount),
                Discount = ParseAmount(row.Discount),
                
                // 日期转换（支持多种格式）
                OrderDate = ParseDate(row.OrderDate),
                
                // 数量转换
                Quantity = int.TryParse(row.Quantity, out var q) ? q : 0,
                
                // 原始数据保留（用于问题追溯）
                RawData = row
            })
            
            // Step 3: 过滤无效数据
            .Where(o => !string.IsNullOrEmpty(o.OrderId))  // 必须有订单号
            .Where(o => o.Amount > 0)                       // 金额必须大于 0
            .Where(o => o.OrderDate != DateTime.MinValue)   // 必须有有效日期
            
            // Step 4: 业务验证
            .ValidateWith(validator)
            
            // Step 5: 配置错误处理
            .ContinueOnValidationError()
            .CollectValidationResults()
            
            // Step 6: 导出到数据库
            .ToSqlServer(_connectionString, "ImportedOrders", new SqlServerExportOptions
            {
                BatchSize = 500,
                InsertMode = InsertMode.Upsert,
                UpsertKeyColumns = new[] { "OrderId" }
            });
        
        return result;
    }
    
    /// <summary>
    /// 标准化区域名称
    /// </summary>
    private static string NormalizeRegion(string? region)
    {
        if (string.IsNullOrEmpty(region)) return "未知";
        
        return region.Trim() switch
        {
            "华北" or "北京" or "天津" => "华北",
            "华东" or "上海" or "江苏" => "华东",
            "华南" or "广州" or "深圳" => "华南",
            "西南" or "成都" or "重庆" => "西南",
            "东北" or "沈阳" or "大连" => "东北",
            _ => region
        };
    }
    
    /// <summary>
    /// 标准化订单状态
    /// </summary>
    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrEmpty(status)) return "待处理";
        
        return status.ToUpper().Trim() switch
        {
            "P" or "PENDING" or "待支付" => "待付款",
            "C" or "COMPLETED" or "已完成" => "已完成",
            "S" or "SHIPPED" or "已发货" => "已发货",
            "R" or "REFUNDED" or "已退款" => "已退款",
            _ => status
        };
    }
    
    /// <summary>
    /// 解析金额（支持 ¥、$、逗号等）
    /// </summary>
    private static decimal ParseAmount(string? amountStr)
    {
        if (string.IsNullOrEmpty(amountStr)) return 0;
        
        // 移除非数字字符
        var cleaned = new string(amountStr
            .Where(c => char.IsDigit(c) || c == '.' || c == '-')
            .ToArray());
        
        return decimal.TryParse(cleaned, out var amount) ? amount : 0;
    }
    
    /// <summary>
    /// 解析日期（支持多种格式）
    /// </summary>
    private static DateTime ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;
        
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
            "MM/dd/yyyy", "dd/MM/yyyy", "M/d/yyyy",
            "yyyy年MM月dd日"
        };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr.Trim(), format, null, 
                DateTimeStyles.None, out var date))
            {
                return date;
            }
        }
        
        return DateTime.MinValue;
    }
}

/// <summary>
/// 清洗后订单验证器
/// </summary>
public class CleanedOrderValidator : DataValidator<CleanedOrder>
{
    public CleanedOrderValidator()
    {
        RuleFor(o => o.OrderId)
            .Required()
            .Length(8, 20)
            .WithMessage("订单号格式不正确");
        
        RuleFor(o => o.CustomerName)
            .Required()
            .MinLength(2)
            .WithMessage("客户名称不能为空");
        
        RuleFor(o => o.Amount)
            .InRange(0, 10_000_000)
            .WithMessage("订单金额超出合理范围");
        
        RuleFor(o => o.Quantity)
            .GreaterThan(0)
            .WithMessage("订单数量必须大于0");
        
        RuleFor(o => o.OrderDate)
            .InRange(DateTime.Today.AddYears(-2), DateTime.Today.AddDays(1))
            .WithMessage("订单日期超出合理范围");
    }
}
```

### 关键点

1. **类型转换** - Select 中完成从脏数据到干净数据的映射
2. **多格式兼容** - 日期、金额支持多种输入格式
3. **字符串标准化** - 区域、状态等字段统一映射
4. **验证规则** - 清洗后的数据再次验证确保质量
5. **Upsert** - 使用订单号作为键，支持重复导入

### 常见问题

| 问题 | 解决方案 |
|-----|---------|
| Excel 格式不标准 | 配置 `ColumnMapping` 映射列 |
| 日期格式混乱 | 使用 `ParseDate` 多种格式尝试 |
| 金额单位不同 | 统一转换为 decimal 存储 |
| 重复数据 | 使用 Upsert 模式 |

---

## 场景 3: 多系统客户数据合并

### 业务背景

公司有三个系统（CRM、电商、财务系统），各有一套客户数据，需要合并去重，生成统一的客户主数据。

### 代码实现

```csharp
/// <summary>
/// 客户数据合并服务
/// </summary>
public class CustomerMerger
{
    /// <summary>
    /// 合并多系统客户数据
    /// </summary>
    public async Task<List<MergedCustomer>> MergeAllCustomers(
        string crmConnection,
        string ecommercePath,
        string financeJson)
    {
        // 定义数据源
        var crmSource = DataForgePipeline
            .FromSqlServer<CustomerCrm>(crmConnection, "Customers")
            .Select(c => new CustomerSource
            {
                SourceId = c.CustomerId,
                Source = "CRM",
                Name = c.Name?.Trim() ?? "",
                Email = c.Email?.ToLower()?.Trim(),
                Phone = NormalizePhone(c.Phone),
                Address = c.Address?.Trim(),
                Company = c.Company?.Trim(),
                CreatedAt = c.CreatedDate,
                UpdatedAt = c.ModifiedDate
            });
        
        var ecommerceSource = DataForgePipeline
            .FromExcel<CustomerEcom>(ecommercePath, "Customers")
            .Select(c => new CustomerSource
            {
                SourceId = c.CustomerGuid,
                Source = "E-commerce",
                Name = c.CustomerName?.Trim() ?? "",
                Email = c.Email?.ToLower()?.Trim(),
                Phone = NormalizePhone(c.Mobile),
                Address = c.DeliveryAddress?.Trim(),
                Company = null,
                CreatedAt = c.RegisterTime,
                UpdatedAt = c.LastOrderTime
            });
        
        var financeSource = DataForgePipeline
            .FromJsonArray<CustomerFin>(financeJson)
            .Select(c => new CustomerSource
            {
                SourceId = c.AccountCode,
                Source = "Finance",
                Name = c.AccountName?.Trim() ?? "",
                Email = c.ContactEmail?.ToLower()?.Trim(),
                Phone = NormalizePhone(c.ContactPhone),
                Address = c.RegisteredAddress?.Trim(),
                Company = c.CompanyName?.Trim(),
                CreatedAt = c.AccountOpenDate,
                UpdatedAt = c.LastTransactionDate
            });
        
        // 合并所有数据源
        var allCustomers = await DataForgePipeline
            .Merge(crmSource, ecommerceSource, financeSource)
            
            // 按邮箱去重（邮箱是最可靠的唯一标识）
            .DistinctBy(c => c.Email?.ToLower())
            
            // 过滤无效数据
            .Where(c => !string.IsNullOrEmpty(c.Email))
            .Where(c => c.Email.Contains("@"))  // 基本格式检查
            
            // 合并字段（多个来源的数据取最优）
            .GroupBy(c => c.Email?.ToLower()!)
            .Select(g => MergeCustomerData(g))
            
            // 标准化处理
            .TransformWith(StandardizeCustomer)
            
            .ToListAsync();
        
        return allCustomers;
    }
    
    /// <summary>
    /// 合并同一条客户的多来源数据
    /// </summary>
    private MergedCustomer MergeCustomerData(IGroupResult<string, CustomerSource> group)
    {
        var sources = group.ToList();
        var primary = sources.First();  // 主来源（优先取 CRM）
        var crmData = sources.FirstOrDefault(s => s.Source == "CRM");
        var ecomData = sources.FirstOrDefault(s => s.Source == "E-commerce");
        var finData = sources.FirstOrDefault(s => s.Source == "Finance");
        
        return new MergedCustomer
        {
            Id = Guid.NewGuid().ToString(),
            
            // 名称：CRM > E-commerce > Finance
            Name = crmData?.Name ?? ecomData?.Name ?? finData?.Name ?? "未知",
            
            // 邮箱：各来源一致，取任意一个
            Email = primary.Email!,
            
            // 电话：优先取手机号
            Phone = ecomData?.Phone ?? crmData?.Phone ?? finData?.Phone,
            
            // 地址：优先取详细地址
            Address = ecomData?.Address ?? crmData?.Address ?? finData?.Address,
            
            // 公司：优先取企业名称
            Company = crmData?.Company ?? finData?.Company,
            
            // 来源追踪
            SourceSystems = sources.Select(s => s.Source).Distinct().ToList(),
            SourceIds = sources.Select(s => $"{s.Source}:{s.SourceId}").ToList(),
            
            // 时间取最新
            CreatedAt = sources.Min(s => s.CreatedAt),
            UpdatedAt = sources.Max(s => s.UpdatedAt)
        };
    }
    
    /// <summary>
    /// 标准化客户数据
    /// </summary>
    private MergedCustomer StandardizeCustomer(MergedCustomer customer)
    {
        return customer with
        {
            Name = customer.Name.Trim(),
            Email = customer.Email?.Trim()?.ToLower(),
            Phone = NormalizePhone(customer.Phone),
            Address = customer.Address?.Trim(),
            Company = customer.Company?.Trim()
        };
    }
    
    /// <summary>
    /// 标准化手机号
    /// </summary>
    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return null;
        
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        
        // 11 位中国手机号
        if (digits.Length == 11 && digits.StartsWith("1"))
            return $"+86-{digits[..3]}-{digits[3..7]}-{digits[7..]}";
        
        // 其他格式原样返回
        return phone.Trim();
    }
}

/// <summary>
/// 中间数据模型
/// </summary>
public record CustomerSource
{
    public string SourceId { get; init; } = "";
    public string Source { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? Company { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 合并后客户
/// </summary>
public record MergedCustomer
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? Company { get; init; }
    public List<string> SourceSystems { get; init; } = new();
    public List<string> SourceIds { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

### 关键点

1. **Merge 合并** - 多个不同结构的数据源合并为一个流
2. **Select 投影** - 每个数据源投影到统一的中间格式
3. **DistinctBy 去重** - 按邮箱去重，确保唯一性
4. **GroupBy 再次分组** - 去重后的数据按邮箱分组，合并多来源信息
5. **优先策略** - 多来源数据有优先级，取最优值

### 常见问题

| 问题 | 解决方案 |
|-----|---------|
| 同一人多个邮箱 | 使用手机号辅助去重 |
| 同一公司不同人 | 按个人去重而非公司 |
| 命名格式不一致 | 标准化处理（去空格、大小写） |

---

## 场景 4: 数据库表结构迁移

### 业务背景

需要将 MySQL 中的订单表迁移到 SQL Server，包括数据类型转换、字段映射和历史数据迁移。

### 代码实现

```csharp
/// <summary>
/// 数据库迁移服务
/// </summary>
public class DatabaseMigrationService
{
    /// <summary>
    /// 从 MySQL 迁移到 SQL Server
    /// </summary>
    public async Task<MigrationResult> MigrateOrders(
        string sourceConnection,
        string targetConnection)
    {
        // 定义字段映射
        var fieldMappings = new Dictionary<string, string>
        {
            ["order_id"] = "OrderId",
            ["customer_name"] = "CustomerName",
            ["order_amount"] = "Amount",
            ["order_date"] = "OrderDate",
            ["status_code"] = "Status",
            ["created_at"] = "CreatedAt",
            ["updated_at"] = "UpdatedAt"
        };
        
        var result = await DataForgePipeline
            // Step 1: 从 MySQL 读取
            .FromMySql<MysqlOrder>(sourceConnection, "orders")
            
            // Step 2: 字段映射和类型转换
            .Select(row => new SqlServerOrder
            {
                OrderId = row.OrderId,
                CustomerName = row.CustomerName?.Trim(),
                Amount = row.OrderAmount,
                OrderDate = row.OrderDate,
                
                // 状态码转换
                Status = row.StatusCode switch
                {
                    0 => "Pending",
                    1 => "Processing",
                    2 => "Completed",
                    3 => "Cancelled",
                    _ => "Unknown"
                },
                
                CreatedAt = row.CreatedAt ?? DateTime.Now,
                UpdatedAt = row.UpdatedAt ?? DateTime.Now,
                
                // 保留原始 ID 用于追溯
                LegacyId = row.OrderId
            })
            
            // Step 3: 数据清洗
            .Where(o => !string.IsNullOrEmpty(o.OrderId))
            .Where(o => o.Amount >= 0)
            .Where(o => o.OrderDate != DateTime.MinValue)
            
            // Step 4: 迁移到 SQL Server
            .ToSqlServer(targetConnection, "Orders", new SqlServerExportOptions
            {
                BatchSize = 1000,
                InsertMode = InsertMode.Insert,
                UseTransaction = true,
                AutoCreateTable: false  // 表已存在
            });
        
        return new MigrationResult
        {
            TotalRecords = result.TotalProcessed,
            SuccessCount = result.SuccessCount,
            FailedCount = result.FailedCount,
            Duration = result.Duration
        };
    }
    
    /// <summary>
    /// 创建目标表（如果不存在）
    /// </summary>
    public async Task CreateTargetTable(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        var sql = @"
            CREATE TABLE IF NOT EXISTS Orders (
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                OrderId NVARCHAR(50) NOT NULL,
                CustomerName NVARCHAR(100),
                Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
                OrderDate DATETIME2 NOT NULL,
                Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                LegacyId NVARCHAR(50),
                UNIQUE (OrderId)
            );
            
            CREATE INDEX IX_Orders_CustomerName ON Orders(CustomerName);
            CREATE INDEX IX_Orders_OrderDate ON Orders(OrderDate);
            CREATE INDEX IX_Orders_Status ON Orders(Status);";
        
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

public record MysqlOrder
{
    public string OrderId { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public decimal OrderAmount { get; init; }
    public DateTime OrderDate { get; init; }
    public int StatusCode { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record SqlServerOrder
{
    public string OrderId { get; init; } = "";
    public string? CustomerName { get; init; }
    public decimal Amount { get; init; }
    public DateTime OrderDate { get; init; }
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? LegacyId { get; init; }
}
```

---

## 场景 5: 日志文件分析报表

### 业务背景

分析服务器日志文件，统计各接口的访问量、响应时间、错误率，生成可视化报表。

### 代码实现

```csharp
/// <summary>
/// 日志分析服务
/// </summary>
public class LogAnalysisService
{
    /// <summary>
    /// 分析日志并生成报表
    /// </summary>
    public async Task AnalyzeLogs(string logPath, string reportPath)
    {
        await DataForgePipeline
            // Step 1: 读取日志文件
            .FromCsv<LogEntry>(logPath, new CsvSourceOptions
            {
                Separator = '|',
                HasHeader = false
            })
            
            // Step 2: 解析日志行
            .Select(line => ParseLogLine(line.RawData))
            
            // Step 3: 过滤有效请求
            .Where(e => e.IsValid)
            .Where(e => e.StatusCode >= 100)  // 排除心跳检测
            
            // Step 4: 统计分析
            .GroupBy(e => new { e.ApiPath, e.StatusCode })
            .Select(g => new ApiStatistics
            {
                ApiPath = g.Key.ApiPath,
                StatusCode = g.Key.StatusCode,
                RequestCount = g.Count(),
                AvgResponseTime = Math.Round(g.Average(e => e.ResponseTime), 2),
                MaxResponseTime = g.Max(e => e.ResponseTime),
                MinResponseTime = g.Min(e => e.ResponseTime),
                P95ResponseTime = CalculatePercentile(g, e => e.ResponseTime, 0.95),
                P99ResponseTime = CalculatePercentile(g, e => e.ResponseTime, 0.99),
                ErrorRate = g.Key.StatusCode >= 400 
                    ? Math.Round((decimal)g.Count() / g.Count() * 100, 2) 
                    : 0
            })
            
            // Step 5: 排序
            .OrderByDescending(s => s.RequestCount)
            
            // Step 6: 导出报表
            .ToExcel(reportPath, new ExcelExportOptions
            {
                SheetName = "API统计",
                IncludeHeader = true,
                FreezeHeader = true,
                AutoFitColumns = true
            });
    }
    
    /// <summary>
    /// 解析日志行
    /// </summary>
    private LogEntry ParseLogLine(string line)
    {
        var parts = line.Split('|');
        
        return new LogEntry
        {
            Timestamp = DateTime.TryParse(parts.ElementAtOrDefault(0), out var ts) ? ts : DateTime.MinValue,
            Level = parts.ElementAtOrDefault(1)?.Trim() ?? "INFO",
            ApiPath = parts.ElementAtOrDefault(2)?.Trim() ?? "",
            StatusCode = int.TryParse(parts.ElementAtOrDefault(3), out var code) ? code : 0,
            ResponseTime = int.TryParse(parts.ElementAtOrDefault(4), out var time) ? time : 0,
            Message = parts.ElementAtOrDefault(5),
            RawData = line,
            IsValid = !string.IsNullOrEmpty(parts.ElementAtOrDefault(2))
        };
    }
    
    private static int CalculatePercentile<T>(IGroup group, Func<T, int> selector, double percentile)
    {
        var values = group.OrderBy(selector).ToList();
        var index = (int)Math.Ceiling(percentile * values.Count) - 1;
        return values[Math.Max(0, index)];
    }
}

public record LogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "";
    public string ApiPath { get; init; } = "";
    public int StatusCode { get; init; }
    public int ResponseTime { get; init; }
    public string? Message { get; init; }
    public string RawData { get; init; } = "";
    public bool IsValid { get; init; }
}

public record ApiStatistics
{
    public string ApiPath { get; init; } = "";
    public int StatusCode { get; init; }
    public int RequestCount { get; init; }
    public double AvgResponseTime { get; init; }
    public int MaxResponseTime { get; init; }
    public int MinResponseTime { get; init; }
    public int P95ResponseTime { get; init; }
    public int P99ResponseTime { get; init; }
    public decimal ErrorRate { get; init; }
}
```

---

## 场景 6: 定时数据同步

### 业务背景

定时任务从源库同步订单数据到数据仓库，支持增量同步（只同步上次同步后的新增和修改数据）。

### 代码实现

```csharp
/// <summary>
/// 数据同步服务
/// </summary>
public class DataSyncService
{
    private readonly string _sourceConnection;
    private readonly string _targetConnection;
    
    public DataSyncService(string sourceConnection, string targetConnection)
    {
        _sourceConnection = sourceConnection;
        _targetConnection = targetConnection;
    }
    
    /// <summary>
    /// 增量同步订单数据
    /// </summary>
    public async Task<SyncResult> IncrementalSyncOrders(DateTime lastSyncTime)
    {
        Console.WriteLine($"开始增量同步，上次同步时间: {lastSyncTime:yyyy-MM-dd HH:mm:ss}");
        
        var syncTime = DateTime.Now;
        
        var result = await DataForgePipeline
            // Step 1: 从源库读取增量数据
            .FromSqlServer<Order>(_sourceConnection, "Orders")
            .Where(o => o.UpdatedAt > lastSyncTime)  // 增量条件
            
            // Step 2: 准备同步数据
            .Select(o => new SyncedOrder
            {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                CustomerName = o.CustomerName,
                Amount = o.Amount,
                OrderDate = o.OrderDate,
                Status = o.Status,
                UpdatedAt = o.UpdatedAt,
                SyncedAt = syncTime
            })
            
            // Step 3: 同步到目标库
            .ToSqlServer(_targetConnection, "Fact_Orders", new SqlServerExportOptions
            {
                BatchSize = 2000,
                InsertMode = InsertMode.Upsert,
                UpsertKeyColumns = new[] { "OrderId" }
            });
        
        var syncResult = new SyncResult
        {
            LastSyncTime = syncTime,
            InsertedCount = result.SuccessCount,
            UpdatedCount = result.DuplicateCount,
            FailedCount = result.FailedCount,
            Duration = result.Duration
        };
        
        Console.WriteLine($"同步完成: 新增 {syncResult.InsertedCount}, 更新 {syncResult.UpdatedCount}");
        
        return syncResult;
    }
    
    /// <summary>
    /// 获取上次同步时间（从配置或数据库）
    /// </summary>
    public async Task<DateTime> GetLastSyncTime()
    {
        await using var connection = new SqlConnection(_targetConnection);
        await connection.OpenAsync();
        
        var sql = "SELECT MAX(UpdatedAt) FROM Fact_Orders";
        var result = await connection.ExecuteScalarAsync<DateTime?>(sql);
        
        return result ?? DateTime.MinValue;
    }
    
    /// <summary>
    /// 保存同步时间
    /// </summary>
    public async Task SaveSyncTime(DateTime syncTime)
    {
        await using var connection = new SqlConnection(_targetConnection);
        await connection.OpenAsync();
        
        var sql = @"
            INSERT INTO SyncLog (SyncType, LastSyncTime, SyncTime, RecordCount)
            VALUES ('Orders', @LastSyncTime, @SyncTime, @RecordCount)";
        
        // 保存同步日志
    }
}

public record SyncedOrder
{
    public string OrderId { get; init; } = "";
    public string CustomerId { get; init; } = "";
    public string? CustomerName { get; init; }
    public decimal Amount { get; init; }
    public DateTime OrderDate { get; init; }
    public string Status { get; init; } = "";
    public DateTime UpdatedAt { get; init; }
    public DateTime SyncedAt { get; init; }
}

public record SyncResult
{
    public DateTime LastSyncTime { get; init; }
    public long InsertedCount { get; init; }
    public long UpdatedCount { get; init; }
    public long FailedCount { get; init; }
    public TimeSpan Duration { get; init; }
}
```

---

## 场景 7: 数据质量检测报告

### 业务背景

对导入的客户数据进行全面质量检测，生成详细的质检报告，包括完整性、准确性、一致性检查。

### 代码实现

```csharp
/// <summary>
/// 数据质量检测服务
/// </summary>
public class DataQualityService
{
    /// <summary>
    /// 生成数据质量报告
    /// </summary>
    public async Task<DataQualityReport> GenerateQualityReport(string dataPath)
    {
        var records = await DataForgePipeline
            .FromCsv<Customer>(dataPath)
            .ToListAsync();
        
        var report = new DataQualityReport
        {
            ReportTime = DateTime.Now,
            TotalRecords = records.Count,
            
            // 完整性检查
            CompletenessCheck = AnalyzeCompleteness(records),
            
            // 准确性检查
            AccuracyCheck = AnalyzeAccuracy(records),
            
            // 一致性检查
            ConsistencyCheck = AnalyzeConsistency(records),
            
            // 唯一性检查
            UniquenessCheck = AnalyzeUniqueness(records),
            
            // 详细问题记录
            Issues = CollectIssues(records)
        };
        
        return report;
    }
    
    /// <summary>
    /// 完整性分析
    /// </summary>
    private CompletenessResult AnalyzeCompleteness(List<Customer> records)
    {
        var total = records.Count;
        
        return new CompletenessResult
        {
            FieldCompleteness = new Dictionary<string, decimal>
            {
                ["CustomerId"] = CalculateCompleteness(records, c => !string.IsNullOrEmpty(c.CustomerId)),
                ["Name"] = CalculateCompleteness(records, c => !string.IsNullOrEmpty(c.Name)),
                ["Email"] = CalculateCompleteness(records, c => !string.IsNullOrEmpty(c.Email)),
                ["Phone"] = CalculateCompleteness(records, c => !string.IsNullOrEmpty(c.Phone)),
                ["Address"] = CalculateCompleteness(records, c => !string.IsNullOrEmpty(c.Address))
            },
            OverallCompleteness = records.Count(r => 
                !string.IsNullOrEmpty(r.CustomerId) &&
                !string.IsNullOrEmpty(r.Name) &&
                !string.IsNullOrEmpty(r.Email)
            ) / (decimal)total * 100
        };
    }
    
    /// <summary>
    /// 收集所有问题记录
    /// </summary>
    private List<DataQualityIssue> CollectIssues(List<Customer> records)
    {
        var issues = new List<DataQualityIssue>();
        
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            
            if (string.IsNullOrEmpty(record.Email))
            {
                issues.Add(new DataQualityIssue
                {
                    RowNumber = i + 2,  // +2 因为有表头且从 1 开始
                    FieldName = "Email",
                    IssueType = "Missing",
                    IssueDescription = "邮箱字段为空"
                });
            }
            else if (!record.Email.Contains("@"))
            {
                issues.Add(new DataQualityIssue
                {
                    RowNumber = i + 2,
                    FieldName = "Email",
                    IssueType = "Invalid",
                    IssueDescription = $"邮箱格式无效: {record.Email}"
                });
            }
            
            if (!string.IsNullOrEmpty(record.Phone) && record.Phone.Length != 11)
            {
                issues.Add(new DataQualityIssue
                {
                    RowNumber = i + 2,
                    FieldName = "Phone",
                    IssueType = "Invalid",
                    IssueDescription = $"手机号长度异常: {record.Phone}"
                });
            }
        }
        
        return issues;
    }
    
    private decimal CalculateCompleteness(List<Customer> records, Func<Customer, bool> predicate)
    {
        return records.Count(predicate) / (decimal)records.Count * 100;
    }
}

public record DataQualityReport
{
    public DateTime ReportTime { get; init; }
    public int TotalRecords { get; init; }
    public CompletenessResult CompletenessCheck { get; init; } = new();
    public AccuracyResult AccuracyCheck { get; init; } = new();
    public ConsistencyResult ConsistencyCheck { get; init; } = new();
    public UniquenessResult UniquenessCheck { get; init; } = new();
    public List<DataQualityIssue> Issues { get; init; } = new();
    public decimal OverallScore => CalculateOverallScore();
    
    private decimal CalculateOverallScore()
    {
        return (CompletenessCheck.OverallCompleteness + 
                AccuracyCheck.AccuracyRate + 
                ConsistencyCheck.ConsistencyRate +
                UniquenessCheck.UniquenessRate) / 4;
    }
}

public record DataQualityIssue
{
    public int RowNumber { get; init; }
    public string FieldName { get; init; } = "";
    public string IssueType { get; init; } = "";
    public string IssueDescription { get; init; } = "";
}
```

---

## 场景 8: 接口数据 ETL

### 业务背景

定时从第三方 REST API 获取订单数据，清洗验证后存入本地数据库。

### 代码实现

```csharp
/// <summary>
/// API ETL 服务
/// </summary>
public class ApiEtlService
{
    private readonly HttpClient _httpClient;
    
    public ApiEtlService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    /// <summary>
    /// 从 API 获取数据并 ETL
    /// </summary>
    public async Task<EtlResult> ExtractFromApiAndLoad(string apiUrl, string apiToken)
    {
        // Step 1: 从 API 获取 JSON 数据
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", apiToken);
        
        var response = await _httpClient.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        
        // Step 2: 解析并转换
        var result = await DataForgePipeline
            .FromJsonString<ApiOrderResponse>(json)
            .SelectMany(r => r.Data)  // 展平嵌套数据
            .Select(r => new Order
            {
                // API 返回的字段映射到本地字段
                ExternalId = r.Id,
                OrderNo = r.OrderNumber,
                CustomerName = r.Customer.Name,
                CustomerEmail = r.Customer.Email,
                TotalAmount = r.Payment.Amount,
                Currency = r.Payment.Currency,
                Status = r.Status,
                CreatedAt = DateTime.Parse(r.CreatedAt),
                UpdatedAt = DateTime.Parse(r.UpdatedAt),
                
                // 原始 JSON 保留
                RawJson = JsonSerializer.Serialize(r)
            })
            
            // Step 3: 数据验证
            .ValidateWith(new ApiOrderValidator())
            .ContinueOnValidationError()
            
            // Step 4: 入库
            .ToSqlServer(
                Configuration.GetConnectionString("LocalDb"),
                "Orders",
                new SqlServerExportOptions
                {
                    BatchSize = 500,
                    InsertMode = InsertMode.Upsert,
                    UpsertKeyColumns = new[] { "ExternalId" }
                });
        
        return new EtlResult
        {
            ProcessedCount = result.TotalProcessed,
            SuccessCount = result.SuccessCount,
            FailedCount = result.FailedCount,
            Duration = result.Duration
        };
    }
}

/// <summary>
/// API 响应模型
/// </summary>
public class ApiOrderResponse
{
    public List<ApiOrder> Data { get; init; } = new();
    public int TotalCount { get; init; }
    public string? NextPage { get; init; }
}

public class ApiOrder
{
    public string Id { get; init; } = "";
    public string OrderNumber { get; init; } = "";
    public ApiCustomer Customer { get; init; } = new();
    public ApiPayment Payment { get; init; } = new();
    public string Status { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; init; } = "";
}

public class ApiCustomer
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
}

public class ApiPayment
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "CNY";
}
```

---

## 场景 9: 大文件分批处理

### 业务背景

处理一个 100GB 的大文件，需要分批读取、分批处理、分批写入，避免内存溢出。

### 代码实现

```csharp
/// <summary>
/// 大文件处理服务
/// </summary>
public class LargeFileProcessingService
{
    private const int BatchSize = 100_000;
    
    /// <summary>
    /// 分批处理大文件
    /// </summary>
    public async Task ProcessLargeFile(string inputPath, string outputPath)
    {
        var batchNumber = 0;
        var totalProcessed = 0;
        
        await using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync("OrderId,CustomerName,Amount");  // 写入表头
        
        await foreach (var batch in DataForgePipeline
            .FromCsv<OrderRecord>(inputPath, new CsvSourceOptions
            {
                HasHeader = true,
                BatchSize = BatchSize
            })
            .Batch(BatchSize))  // 分批
        {
            batchNumber++;
            Console.WriteLine($"处理批次 {batchNumber}，{batch.Count} 条记录...");
            
            // 处理当前批次
            var processedBatch = await ProcessBatchAsync(batch);
            
            // 写入当前批次
            foreach (var record in processedBatch)
            {
                await writer.WriteLineAsync($"{record.OrderId},{record.CustomerName},{record.Amount}");
            }
            
            totalProcessed += batch.Count;
            Console.WriteLine($"批次 {batchNumber} 完成，累计处理: {totalProcessed}");
        }
        
        Console.WriteLine($"处理完成，总计 {batchNumber} 批次，{totalProcessed} 条记录");
    }
    
    /// <summary>
    /// 处理单个批次
    /// </summary>
    private async Task<List<ProcessedRecord>> ProcessBatchAsync(List<OrderRecord> batch)
    {
        return await batch.ToDataForge()
            .Select(r => new ProcessedRecord
            {
                OrderId = r.OrderId,
                CustomerName = r.CustomerName?.Trim().ToUpper(),
                Amount = decimal.TryParse(r.Amount, out var a) ? a : 0,
                ProcessedAt = DateTime.Now
            })
            .Where(r => r.Amount > 0)
            .ToListAsync();
    }
}

/// <summary>
/// 分批扩展
/// </summary>
public static class BatchExtensions
{
    public static async IAsyncEnumerable<List<T>> Batch<T>(
        this IDataPipeline pipeline,
        int batchSize)
    {
        var batch = new List<T>(batchSize);
        
        await foreach (var item in pipeline)
        {
            batch.Add(item);
            
            if (batch.Count >= batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }
        
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
}
```

---

## 场景 10: 实时数据流处理

### 业务背景

处理 Kafka 消息流，实时清洗、聚合后输出到下游系统。

### 代码实现

```csharp
/// <summary>
/// 实时数据流处理服务
/// </summary>
public class StreamProcessingService
{
    /// <summary>
    /// 处理实时订单流
    /// </summary>
    public async Task ProcessOrderStream(
        IAsyncEnumerable<OrderMessage> orderStream,
        IAsyncEnumerable<PaymentMessage> paymentStream,
        CancellationToken ct)
    {
        // 合并订单流和支付流
        var mergedStream = MergeStreams(orderStream, paymentStream);
        
        // 窗口聚合（每 5 分钟输出一次）
        await foreach (var window in mergedStream
            .Window(TimeSpan.FromMinutes(5), ct))
        {
            var report = await window
                .GroupBy(m => m.Region)
                .Select(g => new WindowReport
                {
                    WindowStart = window.WindowStart,
                    WindowEnd = window.WindowEnd,
                    Region = g.Key,
                    OrderCount = g.Count(m => m is OrderMessage),
                    PaymentCount = g.Count(m => m is PaymentMessage),
                    TotalAmount = g
                        .OfType<PaymentMessage>()
                        .Sum(m => m.Amount)
                })
                .ToListAsync();
            
            // 输出到下游
            await OutputToDownstreamAsync(report);
        }
    }
    
    /// <summary>
    /// 合并两个流
    /// </summary>
    private async IAsyncEnumerable<Message> MergeStreams(
        IAsyncEnumerable<OrderMessage> orders,
        IAsyncEnumerable<PaymentMessage> payments)
    {
        await foreach (var order in orders)
        {
            yield return order;
        }
        
        await foreach (var payment in payments)
        {
            yield return payment;
        }
    }
}

public abstract record Message
{
    public DateTime Timestamp { get; init; }
    public string Region { get; init; } = "";
}

public record OrderMessage : Message
{
    public string OrderId { get; init; } = "";
    public decimal Amount { get; init; }
}

public record PaymentMessage : Message
{
    public string PaymentId { get; init; } = "";
    public decimal Amount { get; init; }
}

public record WindowReport
{
    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd { get; init; }
    public string Region { get; init; } = "";
    public int OrderCount { get; init; }
    public int PaymentCount { get; init; }
    public decimal TotalAmount { get; init; }
}
```

---

## 总结

以上 10 个场景涵盖了 DataForge.Core 的主要使用场景：

| 场景 | 核心能力 |
|-----|---------|
| 月报生成 | GroupBy 聚合、ToCsv |
| Excel清洗 | FromExcel、Select 转换、ValidateWith |
| 数据合并 | Merge、DistinctBy |
| 表迁移 | FromMySql、ToSqlServer |
| 日志分析 | FromCsv、GroupBy、ToExcel |
| 定时同步 | Where 增量过滤、Upsert |
| 质量检测 | ValidateWith、自定义聚合 |
| API ETL | FromJsonString、SelectMany |
| 大文件 | Batch 分批、流式处理 |
| 实时流 | IAsyncEnumerable、Window |
