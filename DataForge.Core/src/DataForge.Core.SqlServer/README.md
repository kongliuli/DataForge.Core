# DataForge.Core.SqlServer

为 DataForge.Core 提供 SQL Server 数据源与导出目标。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：`DataForge.Core`、`Microsoft.Data.SqlClient`
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core.SqlServer
```

## 快速示例

```csharp
using DataForge.Core.SqlServer;

var pipeline = SqlServerPipelineExtensions.FromSqlServer<Order>(
    connectionString, "Orders");

await pipeline.ToCsvAsync("orders.csv");
```
