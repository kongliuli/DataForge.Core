# 数据转换指南

本文档介绍 DataForge.Core 内置的转换器和自定义转换器的实现方式。

## 目录

1. [内置转换器](#内置转换器)
2. [自定义转换器](#自定义转换器)
3. [类型转换系统](#类型转换系统)
4. [条件转换](#条件转换)

---

## 内置转换器

### Select — 映射/投影

```csharp
// 基本映射
pipeline.Select(o => new { o.OrderId, o.Amount })

// 复杂计算
pipeline.Select(o => new 
{
    o.OrderId,
    o.CustomerName,
    o.Amount,
    TaxAmount = o.Amount * 0.13m,
    TotalWithTax = o.Amount * 1.13m,
    DiscountAmount = o.Amount > 10000 ? o.Amount * 0.1m : 0
})

// 字符串处理
pipeline.Select(o => new
{
    o.OrderId,
    CustomerName = o.CustomerName.Trim().ToUpper(),
    OrderDateText = o.OrderDate.ToString("yyyy-MM-dd")
})

// 条件映射
pipeline.Select(o => new
{
    o.OrderId,
    StatusText = o.Status switch
    {
        "P" => "待付款",
        "C" => "已完成",
        "R" => "已退款",
        _ => "未知"
    },
    AmountLevel = o.Amount switch
    {
        < 1000 => "小额",
        < 10000 => "中等",
        < 50000 => "大额",
        _ => "超大额"
    }
})

// 嵌套对象
pipeline.Select(o => new
{
    o.OrderId,
    Customer = new
    {
        o.CustomerName,
        o.CustomerPhone,
        o.CustomerEmail
    },
    Items = o.LineItems.Select(i => new { i.ProductName, i.Quantity })
})
```

### Where — 过滤

```csharp
// 单条件
pipeline.Where(o => o.Amount > 1000)

// 多条件 AND
pipeline.Where(o => o.Amount > 1000 && o.Status == "Completed")

// 多条件 OR
pipeline.Where(o => o.Status == "Completed" || o.Status == "Shipped")

// 复杂条件
pipeline.Where(o => 
    o.OrderDate >= startDate &&
    o.OrderDate <= endDate &&
    (o.Region == "华北" || o.Region == "华东") &&
    o.LineItems.Count > 0)

// 带索引
pipeline.Where((o, index) => index < 100)

// 排除特定值
pipeline.Where(o => o.Status != "Cancelled")

// 字符串匹配
pipeline.Where(o => o.CustomerName.Contains("有限公司"))
pipeline.Where(o => o.OrderId.StartsWith("SO2024"))
pipeline.Where(o => Regex.IsMatch(o.Email, @"^[\w\.]+@[\w\.]+\.\w+$"))

// 集合操作
pipeline.Where(o => o.LineItems.Any(i => i.Quantity > 10))
pipeline.Where(o => o.LineItems.All(i => i.Price > 0))
```

### OrderBy — 排序

```csharp
// 单字段升序
pipeline.OrderBy(o => o.OrderDate)

// 单字段降序
pipeline.OrderByDescending(o => o.Amount)

// 多级排序
pipeline
    .OrderBy(o => o.Region)           // 先按区域
    .ThenBy(o => o.OrderDate)         // 再按日期
    .ThenByDescending(o => o.Amount)   // 最后按金额降序

// 复合键
pipeline.OrderBy(o => new { o.Region, o.OrderDate })

// 空值处理
pipeline.OrderBy(o => o.ShippingDate ?? DateTime.MaxValue)

// 条件排序
pipeline
    .OrderBy(o => o.IsPriority ? 0 : 1)    // VIP 优先
    .ThenByDescending(o => o.Amount)        // 再按金额

// 自定义比较
pipeline.OrderBy(o => o.CustomerName, StringComparer.OrdinalIgnoreCase)
```

### GroupBy — 分组

```csharp
// 基本分组
var grouped = pipeline.GroupBy(o => o.Region);

// 分组后聚合
grouped.Select(g => new
{
    Region = g.Key,
    OrderCount = g.Count(),
    TotalAmount = g.Sum(o => o.Amount),
    AvgAmount = g.Average(o => o.Amount),
    MaxAmount = g.Max(o => o.Amount),
    MinAmount = g.Min(o => o.Amount)
})

// 分组后取 Top
grouped.Select(g => g
    .OrderByDescending(o => o.Amount)
    .Take(3)
    .Select(o => o))

// 多级分组
pipeline
    .GroupBy(o => new { o.Year, o.Month })
    .Select(g => new
    {
        g.Key.Year,
        g.Key.Month,
        OrderCount = g.Count(),
        TotalAmount = g.Sum(o => o.Amount)
    })

// 分组后筛选
grouped
    .Where(g => g.Count() > 10)  // 只保留订单数 > 10 的区域
    .Select(g => new
    {
        Region = g.Key,
        OrderCount = g.Count(),
        TotalAmount = g.Sum(o => o.Amount)
    })
```

### Distinct — 去重

```csharp
// 全部去重（需要类型 IEquatable 或重写 Equals）
pipeline.Distinct()

// 按单键去重
pipeline.DistinctBy(o => o.CustomerId)

// 按多键去重
pipeline.DistinctBy(o => new { o.CustomerId, o.ProductId })

// 保留最后一条
pipeline.DistinctBy(o => o.CustomerId, keepLast: true)

// 去重后取其他字段
pipeline
    .DistinctBy(o => o.CustomerId)
    .Select(o => new { o.CustomerId, o.CustomerName, o.Region })

// 先排序再去重（保留优先级最高的）
pipeline
    .OrderBy(o => o.Priority)
    .DistinctBy(o => o.CustomerId)  // 保留优先级最高（最小值）的记录
```

### Skip/Take — 分页

```csharp
// 取前 N 条
pipeline.Take(100)

// 跳过 N 条
pipeline.Skip(100)

// 分页
var page = 0;
var pageSize = 50;

pipeline
    .OrderBy(o => o.OrderId)
    .Skip(page * pageSize)
    .Take(pageSize)

// 条件跳过
pipeline.SkipWhile(o => o.Status == "Draft")

// 条件取
pipeline.TakeWhile(o => o.OrderDate >= DateTime.Today.AddMonths(-1))
```

### SelectMany — 展平

```csharp
// 展平嵌套集合
pipeline.SelectMany(o => o.LineItems)

// 带索引展平
pipeline.SelectMany((o, index) => o.LineItems.Select(item => new
{
    RowIndex = index,
    OrderId = o.OrderId,
    item.ProductName,
    item.Quantity
}))

// 展平后聚合
pipeline
    .SelectMany(o => o.LineItems)
    .GroupBy(item => item.ProductName)
    .Select(g => new
    {
        ProductName = g.Key,
        TotalQuantity = g.Sum(i => i.Quantity),
        OrderCount = g.Count()
    })

// 带条件展平
pipeline
    .SelectMany(o => o.LineItems.Where(i => i.Quantity > 0))
    .Select(item => new { item.ProductName, item.Quantity })
```

---

## 自定义转换器

### 实现 IDataTransform

```csharp
/// <summary>
/// 字符串去空格转换器
/// </summary>
public class TrimTransform<T> : IDataTransform<T, T>
{
    public string Name => "字符串去空格转换";
    public int Priority => 100;
    
    public T? Transform(T input)
    {
        if (input == null) return default;
        
        // 使用反射处理字符串属性
        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.PropertyType == typeof(string))
            {
                var value = prop.GetValue(input) as string;
                if (value != null)
                {
                    prop.SetValue(input, value.Trim());
                }
            }
        }
        
        return input;
    }
    
    public Task<T?> TransformAsync(T input, CancellationToken ct)
    {
        return Task.FromResult(Transform(input));
    }
    
    public async IAsyncEnumerable<T> TransformBatchAsync(
        IEnumerable<T> inputs,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var input in inputs)
        {
            yield return await TransformAsync(input, ct);
        }
    }
}

/// <summary>
/// 日期标准化转换器
/// </summary>
public class DateNormalizationTransform<T> : IDataTransform<T, T>
{
    private readonly string _inputFormat;
    private readonly string _outputFormat;
    
    public string Name => "日期标准化转换";
    public int Priority => 110;
    
    public DateNormalizationTransform(string inputFormat = "yyyy/MM/dd", string outputFormat = "yyyy-MM-dd")
    {
        _inputFormat = inputFormat;
        _outputFormat = outputFormat;
    }
    
    public T? Transform(T input)
    {
        if (input == null) return default;
        
        foreach (var prop in typeof(T).GetProperties())
        {
            if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
            {
                var value = prop.GetValue(input);
                if (value is DateTime dt && dt != DateTime.MinValue)
                {
                    var normalized = DateTime.ParseExact(dt.ToString(_inputFormat), _inputFormat, null);
                    prop.SetValue(input, normalized);
                }
            }
            else if (prop.PropertyType == typeof(string))
            {
                var value = prop.GetValue(input) as string;
                if (!string.IsNullOrEmpty(value) && DateTime.TryParseExact(value, _inputFormat, null, DateTimeStyles.None, out var date))
                {
                    prop.SetValue(input, date.ToString(_outputFormat));
                }
            }
        }
        
        return input;
    }
}

/// <summary>
/// 金额计算转换器
/// </summary>
public class AmountCalculationTransform<T> : IDataTransform<T, T>
{
    private readonly decimal _taxRate;
    private readonly decimal _discountThreshold;
    private readonly decimal _discountRate;
    
    public string Name => "金额计算转换";
    public int Priority => 120;
    
    public AmountCalculationTransform(decimal taxRate = 0.13m, 
        decimal discountThreshold = 10000, decimal discountRate = 0.1m)
    {
        _taxRate = taxRate;
        _discountThreshold = discountThreshold;
        _discountRate = discountRate;
    }
    
    public T? Transform(T input)
    {
        if (input == null) return default;
        
        var amountProp = typeof(T).GetProperty("Amount");
        if (amountProp != null && decimal.TryParse(amountProp.GetValue(input)?.ToString(), out var amount))
        {
            var discount = amount >= _discountThreshold ? amount * _discountRate : 0;
            var taxableAmount = amount - discount;
            var tax = taxableAmount * _taxRate;
            var total = taxableAmount + tax;
            
            typeof(T).GetProperty("Discount")?.SetValue(input, discount);
            typeof(T).GetProperty("Tax")?.SetValue(input, tax);
            typeof(T).GetProperty("TotalAmount")?.SetValue(input, total);
        }
        
        return input;
    }
}
```

### 链式使用自定义转换器

```csharp
// 单个转换器
pipeline.TransformWith(new TrimTransform<Order>())

// 链式多个转换器
pipeline
    .TransformWith(new TrimTransform<Order>())           // 先去空格
    .TransformWith(new DateNormalizationTransform<Order>())  // 再标准化日期
    .TransformWith(new AmountCalculationTransform<Order>()) // 最后计算金额

// 带条件的转换
pipeline.TransformWith(o =>
{
    o.Status = o.Status?.Trim().ToUpper();
    return o;
})

// 异步转换
pipeline.TransformWithAsync(async o =>
{
    o.EnrichedData = await FetchEnrichmentDataAsync(o.Id);
    return o;
})

// 使用 Select 实现简单转换（推荐）
pipeline.Select(o => new Order
{
    OrderId = o.OrderId.Trim(),
    Amount = o.Amount,
    // ...
})
```

---

## 类型转换系统

### 内置类型转换

```csharp
// 字符串到数值
pipeline.Select(o => new
{
    Id = int.Parse(o.IdString),
    Amount = decimal.Parse(o.AmountString),
    Quantity = int.TryParse(o.QtyString, out var q) ? q : 0
})

// 字符串到日期
pipeline.Select(o => new
{
    OrderDate = DateTime.Parse(o.DateString),
    OrderDateOnly = DateOnly.Parse(o.DateString),
    OrderTime = TimeOnly.Parse(o.TimeString)
})

// 枚举转换
public enum OrderStatus { Pending, Completed, Cancelled }

pipeline.Select(o => new
{
    Status = Enum.Parse<OrderStatus>(o.StatusString),
    StatusInt = (int)Enum.Parse<OrderStatus>(o.StatusString)
})

// 可空类型
pipeline.Select(o => new
{
    Amount = decimal.TryParse(o.AmountString, out var a) ? a : null,
    Date = DateTime.TryParse(o.DateString, out var d) ? d : null
})
```

### 自定义类型转换器

```csharp
/// <summary>
/// 自定义类型转换器
/// </summary>
public class CustomTypeConverter : ITypeConverter
{
    public bool CanConvert(Type sourceType, Type targetType)
    {
        // 自定义转换规则
        if (sourceType == typeof(string) && targetType == typeof(Money))
            return true;
        if (sourceType == typeof(string) && targetType == typeof(PhoneNumber))
            return true;
            
        return false;
    }
    
    public object? Convert(object? value, Type targetType)
    {
        if (value == null) return null;
        
        if (targetType == typeof(Money) && value is string s)
        {
            // 解析 "$1,234.56" -> Money
            var amount = decimal.Parse(s.Replace("$", "").Replace(",", ""));
            var currency = s.StartsWith("$") ? "USD" : "CNY";
            return new Money(amount, currency);
        }
        
        if (targetType == typeof(PhoneNumber) && value is string phone)
        {
            // 标准化手机号
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 11 && digits.StartsWith("1"))
                return new PhoneNumber(digits, "CN");
        }
        
        return Convert.ChangeType(value, targetType);
    }
}

/// <summary>
/// Money 值对象
/// </summary>
public record Money(decimal Amount, string Currency);

/// <summary>
/// 电话号码值对象
/// </summary>
public record PhoneNumber(string Number, string CountryCode);

// 注册转换器
DataForgeOptions.DefaultTypeConverter = new CustomTypeConverter();
```

### 类型转换选项

```csharp
var options = new CsvSourceOptions
{
    // 自定义类型转换器
    TypeConverter = new CustomTypeConverter(),
    
    // 数值格式
    NumberFormat = new NumberFormatInfo
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = ","
    },
    
    // 日期格式
    DateFormat = "yyyy-MM-dd",
    
    // 布尔值格式
    TrueValue = "Y",
    FalseValue = "N"
};
```

---

## 条件转换

### 条件投影

```csharp
// 条件赋值
pipeline.Select(o => new
{
    o.OrderId,
    o.Amount,
    Level = o.Amount switch
    {
        < 1000 => "普通",
        < 5000 => "银卡",
        < 10000 => "金卡",
        _ => "钻石"
    },
    DiscountRate = o.Amount switch
    {
        < 1000 => 0.0m,
        < 5000 => 0.05m,
        < 10000 => 0.10m,
        _ => 0.15m
    }
})

// 条件转换
pipeline.Select(o => new Order
{
    OrderId = o.OrderId,
    Status = o.Status switch
    {
        "P" => OrderStatus.Pending,
        "C" => OrderStatus.Completed,
        "R" => OrderStatus.Refunded,
        _ => OrderStatus.Unknown
    },
    Priority = o.Amount > 10000 ? Priority.High : Priority.Normal
})

// 条件字段
pipeline.Select(o => new
{
    o.OrderId,
    o.Amount,
    // 只在满足条件时计算
    PremiumDiscount = o.IsMember && o.Amount > 5000 ? o.Amount * 0.1m : 0,
    // 条件对象
    CustomerInfo = o.IsVip ? new { o.VipLevel, o.VipPoints } : null
})
```

### 多阶段转换管道

```csharp
/// <summary>
/// 多阶段数据转换
/// </summary>
var result = await pipeline
    // 阶段 1：数据清洗
    .TransformWith(o => o with
    {
        OrderId = o.OrderId?.Trim(),
        CustomerName = o.CustomerName?.Trim(),
        Amount = decimal.TryParse(o.AmountRaw, out var a) ? a : 0
    })
    
    // 阶段 2：数据标准化
    .TransformWith(o => o with
    {
        Region = NormalizeRegion(o.Region),
        Status = NormalizeStatus(o.Status)
    })
    
    // 阶段 3：业务计算
    .Select(o => new
    {
        o.OrderId,
        o.CustomerName,
        o.Amount,
        Tax = o.Amount * 0.13m,
        TotalAmount = o.Amount * 1.13m,
        Discount = o.Amount > 10000 ? o.Amount * 0.05m : 0
    })
    
    // 阶段 4：分类标记
    .Select(o => new
    {
        o.OrderId,
        o.CustomerName,
        o.Amount,
        o.Tax,
        o.TotalAmount,
        o.Discount,
        Category = ClassifyOrder(o.Amount)
    })
    
    .ToCsv("transformed-orders.csv");
```
