# DataForge.Core

面向 .NET 8 的轻量级数据处理（ETL）核心库：统一数据源、链式管道、验证与导出。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：无第三方依赖
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core
```

## 快速示例

```csharp
using DataForge.Core;

var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 0)
    .ToJsonAsync("orders.json");
```

核心提供 `FromCsv` / `FromJson` / `FromMemory`，以及 `ToCsvAsync` / `ToJsonAsync` / `ToExcelAsync` / `ToConsoleAsync`。
`FromExcel` / `ToExcelAsync` 在核心中为**无依赖占位**（按文本分隔读写）；真实 `.xlsx` 请安装 `DataForge.Core.Excel`。
