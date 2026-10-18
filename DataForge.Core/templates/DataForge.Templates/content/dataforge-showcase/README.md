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

## 无数据库快速开始

在生成的项目目录执行：

```bash
dotnet run -- run jobs/00-file-batch.yaml
```

模板自带 `sample.csv`（Alice、Bob 两行），批次生成 `output/sample.json` 和 `output/sample.csv`。先用此场景验证安装，再配置真实数据库。

## 数据库场景 CLI

```bash
docker start dataforge-pg
# 执行 sql/sqlite-init.sql 与 sql/postgres-init.sql
dotnet run -- run jobs/01-homo.yaml
# 同一入口也支持完整批次
dotnet run -- run jobs/05-batch.yaml
```

入口调用 `JobBatch.RunFileAsync`，单作业与批次统一执行；逐项输出结果，任一执行结果失败时退出码为 1。

默认 PostgreSQL 端口 `5433`（`dotnet new` 可用 `-pgPort`）。

## Web（仓库内工具）

生成的模板不包含 Web 宿主。若已克隆 DataForge.Core 仓库，在包含解决方案的内层目录执行：

```bash
dotnet run --project tools/DataForge.Web
```

浏览器打开启动日志中的地址，可管理连接档案、探活、列表、结构、预览和运行 YAML。此命令不适用于独立生成的模板目录；独立 Web 分发仍待交付。

## NuGet 目录 API（可嵌入你的程序）

```csharp
JobSqliteRegistration.AddDataForgeJobsSqlite();
var ok = await JobCatalog.TestConnectionAsync("sqlite", conn);
var tables = await JobCatalog.ListTablesAsync("sqlite", conn);
var cols = await JobCatalog.DescribeTableAsync("sqlite", conn, "orders");
var rows = await JobCatalog.PreviewAsync("sqlite", conn, "orders", 20);
```

本模板默认引用并注册 `sqlite` / `postgresql`。其他 Catalog（例如 sqlserver、mysql、excel、parquet）需要额外引用对应 Jobs 适配包并调用其注册方法；它们并未默认包含在本模板中。
