# DataForge.Core.Excel

为 DataForge.Core 提供基于 ClosedXML 的真实 Excel（`.xlsx`）读写支持。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：`DataForge.Core`、`ClosedXML`
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core.Excel
```

> 核心包内的 `FromExcel` / `ToExcelAsync` 仅为占位实现；生产环境请使用本扩展包。

## 快速示例

```csharp
using DataForge.Core;
using DataForge.Core.Excel;

await new DataForgePipeline()
    .FromExcel<Order>("sales.xlsx")
    .ToExcel("out.xlsx");
```
