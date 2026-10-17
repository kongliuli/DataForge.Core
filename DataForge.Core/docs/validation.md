# 验证（0.1.0）

管道用 `ValidateWith(IValidator<T>)`。清单 → [api-implemented.md](./api-implemented.md)。

## 内置：`DataValidator<T>`

```csharp
using DataForge.Core;
using DataForge.Core.Core.Validation;

public class OrderValidator : DataValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Amount).GreaterThan(0m);
        RuleFor(o => o.CustomerId).NotEmpty();
        RuleFor(o => o.Note).Length(0, 200);
    }
}

await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ValidateWith(new OrderValidator())
    .ContinueOnValidationError()   // 或 FailOnValidationError()
    .ToCsvAsync("out.csv");
```

常用规则（`ValidationRuleBuilder`）：`NotEmpty` / `Required` / `Length` / `MinLength` / `InRange` / `GreaterThan` / `GreaterThanOrEqualTo` / `Must` / `When`。

## FluentValidation

必须适配，不能把 `AbstractValidator<T>` 直接丢进 `ValidateWith`：

```csharp
using DataForge.Core.FluentValidation;

var adapter = new FluentValidationAdapter<Order>(new MyFluentValidator());
await pipeline.ValidateWith(adapter).FailOnValidationError().ToListAsync();
```

## 结果怎么看

0.1.0 **不会**把 `ValidationResult` 挂到业务对象上，也没有 `CollectValidationResults`。

- 管道内：靠 `ContinueOnValidationError` / `FailOnValidationError` 决定是否继续。
- 需要明细：管道外 `validator.Validate(item)`，读 `ValidationResult.Errors`（`PropertyName` / `Message`）。

```csharp
var vr = new OrderValidator().Validate(order);
if (!vr.IsValid)
    foreach (var e in vr.Errors)
        Console.WriteLine($"{e.PropertyName}: {e.Message}");
```

## 不做

`ValidateWith<TValidator>()` 泛型约束重载、验证结果自动投影、管道内收集全部错误的专用 API → [api-roadmap.md](./api-roadmap.md)。
