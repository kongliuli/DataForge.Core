# DataForge.Core.Json

DataForge.Core 的 JSON 扩展包。核心库已内置基础 `FromJson` / `ToJsonAsync`；本包提供额外选项与扩展方法。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：`DataForge.Core`
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core.Json
```

## 快速示例

```csharp
using DataForge.Core;
using DataForge.Core.Json;

await new DataForgePipeline()
    .FromJson<Order>("orders.json")
    .ToJson("out.json");
```
