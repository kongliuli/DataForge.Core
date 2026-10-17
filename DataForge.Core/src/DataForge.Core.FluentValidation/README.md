# DataForge.Core.FluentValidation

将 FluentValidation 集成到 DataForge.Core 管道验证流程。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：`DataForge.Core`、`FluentValidation`
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core.FluentValidation
```

## 快速示例

```csharp
using DataForge.Core;
using DataForge.Core.FluentValidation;
using FluentValidation;

var adapter = new FluentValidationAdapter<Order>(new OrderValidator());

await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ValidateWith(adapter)
    .ToJsonAsync("valid-orders.json");
```
