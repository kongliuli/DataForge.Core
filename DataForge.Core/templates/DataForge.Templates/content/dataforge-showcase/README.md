# DataForgeShowcase — LocalDts 替代脚手架

用 `DataForge.Jobs` + 连接器 NuGet 替代 LocalDts 的本地数据迁移工作流。
UI 走 Web/CLI，不走 WPF。

## LocalDts 能力对照

| LocalDts | DataForge |
|---|---|
| 连接探活 / 列库表 / 表结构 / 预览 | NuGet `JobCatalog.*` + Web `/api/catalog/*`（含 excel/parquet 文件路径） |
| MigrationEngine 作业 + 进度 | YAML + `JobRunner`/`JobBatch` `IProgress<JobProgress>` / `dataforge run` / Web |
| 同构/拆表/合表 | `jobs/01`–`05` |
| 连接档案 | Web `ConnectionStore`（`data/connections.json`） |

## 场景作业

| 文件 | 含义 |
|---|---|
| `jobs/01-homo.yaml` | 同构：SQLite `orders` → PostgreSQL `orders` |
| `jobs/02-split-*.yaml` | 单表拆多表 |
| `jobs/03-merge.yaml` | 异构投影后写入摘要表 |
| `jobs/04-join.yaml` | 多源等值 JOIN + `createTable` + upsert |
| `jobs/05-batch.yaml` | `jobs:` 顺序编排，失败即停 |
| `jobs/06-parallel.yaml` | `execution.parallelism` 并行批 |

批处理选项写在文件根 `execution:`：`parallelism`（默认 1）、`stopOnError`（默认 true）、`progressEvery`、`batchSize`。

## CLI

```bash
docker start dataforge-pg
# 执行 sql/sqlite-init.sql 与 sql/postgres-init.sql
dotnet run -- run jobs/01-homo.yaml
```

默认 PostgreSQL 端口 `5433`（`dotnet new` 可用 `-pgPort`）。

## Web（仓库内工具）

```bash
dotnet run --project ../../tools/DataForge.Web
# 浏览器打开首页：连接档案 / 探活 / 列表 / 结构 / 预览 / 跑 YAML
```

## NuGet 目录 API（可嵌入你的程序）

```csharp
JobSqliteRegistration.AddDataForgeJobsSqlite();
var ok = await JobCatalog.TestConnectionAsync("sqlite", conn);
var tables = await JobCatalog.ListTablesAsync("sqlite", conn);
var cols = await JobCatalog.DescribeTableAsync("sqlite", conn, "orders");
var rows = await JobCatalog.PreviewAsync("sqlite", conn, "orders", 20);
```

已注册 Catalog：`sqlite` / `postgresql` / `sqlserver` / `mysql` / `excel` / `parquet`。文件类 Catalog 的 connection 是文件路径，table 是工作表名或 parquet 文件名。
