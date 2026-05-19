# 数据验证指南

本文档介绍 DataForge.Core 的数据验证功能，包括内置规则、自定义验证器和 FluentValidation 集成。

## 目录

1. [内置验证规则](#内置验证规则)
2. [FluentValidation 集成](#fluentvalidation-集成)
3. [自定义验证器](#自定义验证器)
4. [验证结果收集](#验证结果收集)
5. [验证与管道集成](#验证与管道集成)

---

## 内置验证规则

### 使用 DataValidator 基类

```csharp
/// <summary>
/// 订单验证器示例
/// </summary>
public class OrderValidator : DataValidator<SalesOrder>
{
    public OrderValidator()
    {
        // ========== 必填验证 ==========
        
        // 字符串非空
        RuleFor(o => o.OrderId)
            .NotEmpty()
            .WithMessage("订单号不能为空");
        
        // 引用类型非空
        RuleFor(o => o.CustomerId)
            .NotNull()
            .WithMessage("客户ID不能为空");
        
        // ========== 数值验证 ==========
        
        // 大于
        RuleFor(o => o.Quantity)
            .GreaterThan(0)
            .WithMessage("数量必须大于0");
        
        // 大于等于
        RuleFor(o => o.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("金额不能为负数");
        
        // 小于
        RuleFor(o => o.Discount)
            .LessThan(100)
            .WithMessage("折扣不能超过100%");
        
        // 范围
        RuleFor(o => o.Amount)
            .InRange(0, 1_000_000)
            .WithMessage("单笔订单金额必须在 0 到 100 万之间");
        
        // ========== 字符串验证 ==========
        
        // 长度范围
        RuleFor(o => o.OrderId)
            .Length(8, 20)
            .WithMessage("订单号长度必须在 8 到 20 个字符之间");
        
        // 最小长度
        RuleFor(o => o.CustomerName)
            .MinLength(2)
            .WithMessage("客户名称至少需要 2 个字符");
        
        // 最大长度
        RuleFor(o => o.Address)
            .MaxLength(200)
            .WithMessage("地址不能超过 200 个字符");
        
        // 正则表达式
        RuleFor(o => o.Email)
            .Matches(@"^[\w\.]+@[\w\.]+\.\w+$")
            .WithMessage("邮箱格式不正确");
        
        RuleFor(o => o.Phone)
            .Matches(@"^1[3-9]\d{9}$")
            .WithMessage("手机号格式不正确");
        
        // 邮箱格式
        RuleFor(o => o.Email)
            .EmailAddress()
            .WithMessage("邮箱格式不正确");
        
        // ========== 日期验证 ==========
        
        // 不在将来
        RuleFor(o => o.OrderDate)
            .NotInFuture()
            .WithMessage("订单日期不能是未来日期");
        
        // 不在过去
        RuleFor(o => o.DeliveryDate)
            .NotInPast()
            .WithMessage("交货日期不能是过去日期");
        
        // 在指定范围内
        RuleFor(o => o.OrderDate)
            .InRange(DateTime.Today.AddYears(-1), DateTime.Today.AddDays(1))
            .WithMessage("订单日期必须在近一年内");
        
        // ========== 业务规则验证 ==========
        
        // 条件验证
        RuleFor(o => o.ShippingAddress)
            .NotEmpty()
            .When(o => o.RequireShipping)
            .WithMessage("需要配送的订单必须填写收货地址");
        
        // 列表非空
        RuleFor(o => o.LineItems)
            .NotEmpty()
            .WithMessage("订单必须包含至少一个商品明细");
        
        // 集合项数范围
        RuleFor(o => o.LineItems)
            .CountBetween(1, 100)
            .WithMessage("订单明细数量必须在 1 到 100 之间");
        
        // ========== 自定义规则 ==========
        
        // 自定义验证逻辑
        AddRule(new CustomValidationRule<SalesOrder>(
            order => order.LineItems.All(i => i.Price > 0),
            "所有商品价格必须大于 0",
            nameof(SalesOrder.LineItems)));
        
        // 跨字段验证
        AddRule(new CustomValidationRule<SalesOrder>(
            order => order.PaidAmount <= order.TotalAmount,
            "已付款金额不能超过订单总额",
            nameof(SalesOrder.PaidAmount)));
    }
}
```

### 内置规则列表

| 规则名称 | 说明 | 示例 |
|---------|------|------|
| `NotEmpty()` | 非空（非空字符串/集合） | `RuleFor(o => o.Name).NotEmpty()` |
| `NotNull()` | 非空引用 | `RuleFor(o => o.Customer).NotNull()` |
| `NotEmpty()` | 集合非空 | `RuleFor(o => o.Items).NotEmpty()` |
| `Required()` | 必填（组合规则） | `RuleFor(o => o.Id).Required()` |
| `Equal(value)` | 等于指定值 | `RuleFor(o => o.Status).Equal("Active")` |
| `NotEqual(value)` | 不等于指定值 | `RuleFor(o => o.Status).NotEqual("Deleted")` |
| `GreaterThan(value)` | 大于 | `RuleFor(o => o.Amount).GreaterThan(0)` |
| `GreaterThanOrEqual(value)` | 大于等于 | `RuleFor(o => o.Quantity).GreaterThanOrEqual(1)` |
| `LessThan(value)` | 小于 | `RuleFor(o => o.Discount).LessThan(100)` |
| `LessThanOrEqual(value)` | 小于等于 | `RuleFor(o => o.Amount).LessThanOrEqual(1000000)` |
| `InRange(min, max)` | 在范围内 | `RuleFor(o => o.Amount).InRange(0, 1000000)` |
| `Length(min, max)` | 字符串长度 | `RuleFor(o => o.Code).Length(8, 20)` |
| `MinLength(length)` | 最小长度 | `RuleFor(o => o.Name).MinLength(2)` |
| `MaxLength(length)` | 最大长度 | `RuleFor(o => o.Name).MaxLength(100)` |
| `Matches(pattern)` | 正则匹配 | `RuleFor(o => o.Email).Matches(@"^...")` |
| `EmailAddress()` | 邮箱格式 | `RuleFor(o => o.Email).EmailAddress()` |
| `NotInFuture()` | 不在将来 | `RuleFor(o => o.Date).NotInFuture()` |
| `NotInPast()` | 不在过去 | `RuleFor(o => o.StartDate).NotInPast()` |
| `InRange(start, end)` | 日期范围 | `RuleFor(o => o.Date).InRange(start, end)` |
| `CountBetween(min, max)` | 集合数量 | `RuleFor(o => o.Items).CountBetween(1, 100)` |
| `When(condition)` | 条件验证 | `RuleFor(...).When(o => o.IsActive)` |

---

## FluentValidation 集成

### 安装

```bash
dotnet add package DataForge.Core.FluentValidation
```

### 使用 FluentValidation 验证器

```csharp
// FluentValidation 验证器
public class CustomerValidator : FluentValidation.AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("姓名不能为空")
            .Length(2, 50).WithMessage("姓名长度在 2-50 个字符之间");
        
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("邮箱不能为空")
            .EmailAddress().WithMessage("邮箱格式不正确");
        
        RuleFor(c => c.Age)
            .InclusiveBetween(18, 120).WithMessage("年龄必须在 18-120 岁之间");
    }
}

// 在管道中使用
await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .ValidateWith(new CustomerValidator())  // 直接传入 FluentValidation 验证器
    .ToCsv("validated-customers.csv");
```

### 适配器自动转换

```csharp
// FluentValidation 验证器自动适配
public interface IValidator<T>
{
    ValidationResult Validate(T instance);
}

// DataForge.Core.FluentValidation 包含适配器
// 任何 FluentValidation.AbstractValidator<T> 自动实现 IValidator<T>
```

### FluentValidation 高级特性

```csharp
public class OrderValidator : FluentValidation.AbstractValidator<Order>
{
    public OrderValidator()
    {
        // 级联验证
        RuleForEach(o => o.LineItems).SetValidator(new LineItemValidator());
        
        // 异步规则
        RuleFor(o => o.CustomerId)
            .MustAsync(async (id, ct) => await CustomerExistsAsync(id, ct))
            .WithMessage("客户不存在");
        
        // 自定义错误码
        RuleFor(o => o.Amount)
            .GreaterThan(0)
            .WithErrorCode("ORDER_001")
            .WithMessage("订单金额必须大于 0");
        
        // 条件验证
        RuleFor(o => o.ShippingAddress)
            .NotEmpty()
            .When(o => o.IsDomestic)
            .WithMessage("国内订单必须填写收货地址");
        
        // 集合验证
        RuleForEach(o => o.LineItems)
            .SetValidator(new LineItemValidator());
        
        // 依赖属性验证
        RuleFor(o => o.EndDate)
            .GreaterThan(o => o.StartDate)
            .WithMessage("结束日期必须晚于开始日期");
    }
}
```

---

## 自定义验证器

### 继承 DataValidator<T>

```csharp
/// <summary>
/// 客户数据验证器
/// </summary>
public class CustomerValidator : DataValidator<Customer>
{
    public CustomerValidator()
    {
        // 基础规则
        RuleFor(c => c.CustomerId)
            .Required()
            .WithMessage("客户ID不能为空")
            .WithSeverity(ValidationSeverity.Critical);
        
        RuleFor(c => c.Name)
            .Required()
            .MinLength(2)
            .MaxLength(100)
            .WithMessage("客户名称必须在 2-100 个字符之间");
        
        RuleFor(c => c.Email)
            .Required()
            .EmailAddress()
            .WithMessage("邮箱格式不正确")
            .WithSeverity(ValidationSeverity.Error);
        
        RuleFor(c => c.Phone)
            .Matches(@"^1[3-9]\d{9}$")
            .When(c => !string.IsNullOrEmpty(c.Phone))
            .WithMessage("手机号格式不正确")
            .WithSeverity(ValidationSeverity.Warning);
        
        // 业务规则
        AddRule(new CustomerBusinessRule());
    }
}

/// <summary>
/// 自定义业务规则
/// </summary>
public class CustomerBusinessRule : IValidationRule<Customer>
{
    public string RuleName => "CustomerBusinessRule";
    
    public Expression<Func<Customer, object?>> PropertySelector => c => c.CustomerId;
    
    public ValidationError? Validate(Customer instance)
    {
        // VIP 客户必须有邮箱
        if (instance.IsVip && string.IsNullOrEmpty(instance.Email))
        {
            return new ValidationError
            {
                PropertyName = nameof(Customer.Email),
                ErrorMessage = "VIP客户必须提供邮箱地址",
                ErrorCode = "VIP_EMAIL_REQUIRED",
                Severity = ValidationSeverity.Error
            };
        }
        
        // 企业客户必须大于等于注册资本
        if (instance.CustomerType == CustomerType.Corporate && instance.CreditLimit < 10000)
        {
            return new ValidationError
            {
                PropertyName = nameof(Customer.CreditLimit),
                ErrorMessage = "企业客户信用额度不能低于 10000",
                ErrorCode = "CREDIT_LIMIT_TOO_LOW",
                Severity = ValidationSeverity.Warning
            };
        }
        
        return null; // 验证通过
    }
}
```

### 实现 IValidator<T> 接口

```csharp
/// <summary>
/// 跨表验证器示例
/// </summary>
public class CrossReferenceValidator : IValidator<SalesOrder>
{
    private readonly HashSet<string> _validCustomerIds;
    private readonly HashSet<string> _validProductCodes;
    
    public string Name => "跨表引用验证器";
    
    public CrossReferenceValidator(IEnumerable<string> validCustomerIds, IEnumerable<string> validProductCodes)
    {
        _validCustomerIds = new HashSet<string>(validCustomerIds);
        _validProductCodes = new HashSet<string>(validProductCodes);
    }
    
    public ValidationResult Validate(SalesOrder instance)
    {
        var errors = new List<ValidationError>();
        
        if (!_validCustomerIds.Contains(instance.CustomerId))
        {
            errors.Add(new ValidationError
            {
                PropertyName = nameof(SalesOrder.CustomerId),
                ErrorMessage = $"客户ID '{instance.CustomerId}' 不存在",
                ErrorCode = "INVALID_CUSTOMER"
            });
        }
        
        foreach (var item in instance.LineItems)
        {
            if (!_validProductCodes.Contains(item.ProductCode))
            {
                errors.Add(new ValidationError
                {
                    PropertyName = $"{nameof(SalesOrder.LineItems)}.{nameof(item.ProductCode)}",
                    ErrorMessage = $"商品编码 '{item.ProductCode}' 不存在",
                    ErrorCode = "INVALID_PRODUCT"
                });
            }
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Data = instance
        };
    }
    
    public async Task<ValidationResult> ValidateAsync(SalesOrder instance, CancellationToken ct)
    {
        // 可以在这里进行异步验证
        return await Task.Run(() => Validate(instance), ct);
    }
    
    public async IAsyncEnumerable<ValidationResult> ValidateBatchAsync(
        IEnumerable<SalesOrder> instances,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var instance in instances)
        {
            yield return await ValidateAsync(instance, ct);
        }
    }
}

// 使用跨表验证器
var validator = new CrossReferenceValidator(validCustomerIds, validProductCodes);

await DataForgePipeline
    .FromCsv<SalesOrder>("orders.csv")
    .ValidateWith(validator)
    .ContinueOnValidationError()
    .ToCsv("validated-orders.csv");
```

---

## 验证结果收集

### 收集所有验证错误

```csharp
// 收集验证结果
var results = await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .ValidateWith(new CustomerValidator())
    .ContinueOnValidationError()
    .CollectValidationResults()
    .ToListAsync();

// 分析结果
var validRecords = results.Where(r => r.IsValid).ToList();
var invalidRecords = results.Where(r => !r.IsValid).ToList();

Console.WriteLine($"总记录数: {results.Count}");
Console.WriteLine($"有效记录: {validRecords.Count}");
Console.WriteLine($"无效记录: {invalidRecords.Count}");

// 按错误类型分组
var errorGroups = invalidRecords
    .SelectMany(r => r.Errors)
    .GroupBy(e => e.ErrorCode)
    .OrderByDescending(g => g.Count());

foreach (var group in errorGroups)
{
    Console.WriteLine($"错误码 {group.Key}: {group.Count()} 次");
}

// 导出验证错误详情
var errorReport = invalidRecords.Select(r => new
{
    Record = r.Data,
    Errors = string.Join("; ", r.Errors.Select(e => e.ErrorMessage))
});

await errorReport.ToDataForge().ToJson("validation-errors.json");
```

### 验证失败时停止

```csharp
// 验证失败立即停止（抛出异常）
try
{
    await DataForgePipeline
        .FromCsv<Customer>("customers.csv")
        .ValidateWith(new CustomerValidator())
        .FailOnValidationError()  // 遇到无效记录就抛异常
        .ToCsv("validated-customers.csv");
}
catch (ValidationException ex)
{
    Console.WriteLine($"在第 {ex.FailedRecordNumber} 行发现验证错误:");
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"  - {error.PropertyName}: {error.ErrorMessage}");
    }
}
```

### 统计验证覆盖率

```csharp
var results = await DataForgePipeline
    .FromCsv<Customer>("customers.csv")
    .ValidateWith(new CustomerValidator())
    .ContinueOnValidationError()
    .CollectValidationResults()
    .ToListAsync();

// 生成数据质量报告
var report = new DataQualityReport
{
    TotalRecords = results.Count,
    ValidRecords = results.Count(r => r.IsValid),
    InvalidRecords = results.Count(r => !r.IsValid),
    
    ErrorDistribution = results
        .SelectMany(r => r.Errors)
        .GroupBy(e => e.ErrorCode)
        .ToDictionary(g => g.Key, g => g.Count()),
        
    SeverityDistribution = results
        .SelectMany(r => r.Errors)
        .GroupBy(e => e.Severity)
        .ToDictionary(g => g.Key, g => g.Count())
};
```

---

## 验证与管道集成

### 基本集成

```csharp
// 单验证器
await pipeline
    .ValidateWith(new OrderValidator())
    .ToCsv("validated.csv");

// 多验证器
await pipeline
    .ValidateWith(new OrderValidator())
    .ValidateWith(new BusinessRuleValidator())
    .ToCsv("validated.csv");

// 验证后过滤无效数据
await pipeline
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .Where(r => r.ValidationResult.IsValid)  // 只保留有效的
    .Select(r => r.Data)
    .ToCsv("valid-only.csv");
```

### 验证结果传递

```csharp
// ValidationResult 会附加到数据中传递
await pipeline
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .ForEachAsync(order =>
    {
        if (!order.ValidationResult.IsValid)
        {
            Console.WriteLine($"订单 {order.OrderId} 有验证问题");
            foreach (var error in order.ValidationResult.Errors)
            {
                Console.WriteLine($"  - {error.ErrorMessage}");
            }
        }
    });
```

### 条件验证

```csharp
// 按数据类型选择验证器
public class ConditionalValidationPipeline<T>
{
    public IDataPipeline<T, T> WithConditionalValidation(
        Func<T, IValidator<T>> validatorSelector)
    {
        return pipeline
            .TransformWith(item => 
            {
                var validator = validatorSelector(item);
                var result = validator.Validate(item);
                return item with { ValidationResult = result };
            })
            .Where(item => item.ValidationResult.IsValid);
    }
}

// 使用
await orders
    .WithConditionalValidation(order => order.OrderType switch
    {
        OrderType.Standard => new StandardOrderValidator(),
        OrderType.Vip => new VipOrderValidator(),
        OrderType.Wholesale => new WholesaleOrderValidator(),
        _ => new DefaultOrderValidator()
    })
    .ToCsv("validated.csv");
```

### 验证后处理

```csharp
// 记录验证统计
var stats = new ValidationStatistics();

await pipeline
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()
    .CollectValidationResults()
    .ForEachAsync(result =>
    {
        stats.Record(result);
    });

Console.WriteLine($"验证完成: {stats.Total} 条");
Console.WriteLine($"  通过: {stats.Passed} 条");
Console.WriteLine($"  失败: {stats.Failed} 条");
Console.WriteLine($"  成功率: {stats.PassRate:P2}");

// 导出详细报告
await stats.GenerateReport().ToJson("validation-report.json");
```
