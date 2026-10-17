# DataForge.Core.Http

为 DataForge.Core 提供 REST API 数据源与导出目标。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：`DataForge.Core`
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core.Http
```

## 快速示例

```csharp
using DataForge.Core;
using DataForge.Core.Http;

var pipeline = new DataForgePipeline()
    .FromRestApi<Order>("https://api.example.com", "/orders");

await pipeline.ToJsonAsync("orders.json");
```
