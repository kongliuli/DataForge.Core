# DataForge.Core.PostgreSql

为 DataForge.Core 提供 PostgreSQL（Npgsql）数据源与导出目标。

- **目标框架**：`net8.0`
- **版本**：`0.1.0`
- **依赖**：`DataForge.Core`、`Npgsql`
- **仓库**：[kongliuli/DataForge.Core](https://github.com/kongliuli/DataForge.Core)

## 安装

```bash
dotnet add package DataForge.Core.PostgreSql
```

## 用法

```csharp
using DataForge.Core;
using DataForge.Core.PostgreSql;

var conn = "Host=localhost;Port=5432;Database=dataforge;Username=dataforge;Password=dataforge";

var result = await PostgreSqlPipelineExtensions
    .FromPostgreSql<Order>(conn, "orders")
    .Where(o => o.Amount > 0)
    .ToPostgreSqlAsync(conn, "orders_copy");
```

表名/列名经 `SqlIdentifiers` 校验后按 PostgreSQL 双引号引用；建议目标表事先建好。
