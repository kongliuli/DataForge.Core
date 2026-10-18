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
| `jobs/10-homo-excel-json.yaml` | Excel↔JSON 同构：`samples/sales-flat.xlsx` → JSON |
| `jobs/11-split-excel-json.yaml` | Excel 单表拆多表 → 两个 JSON |
| `jobs/12-merge-json-excel.yaml` | 多表合并：两个 JSON JOIN → Excel |
| `jobs/13-hetero-excel-json.yaml` | 异构清洗：Excel 过滤+改名 → JSON |

批处理选项写在文件根 `execution:`：`parallelism`（默认 1）、`stopOnError`（默认 true）、`progressEvery`、`batchSize`。

## 无数据库快速开始

在生成的项目目录执行：

```bash
dotnet run -- run jobs/00-file-batch.yaml
```

模板自带 `sample.csv`（Alice、Bob 两行），批次生成 `output/sample.json` 和 `output/sample.csv`。先用此场景验证安装，再配置真实数据库。

## Excel ↔ JSON 四种场景（无需数据库）

生成模板时加 `--connectors all`（会引用并注册 `DataForge.Jobs.Excel`），样例文件在 `samples/`：

| 模式 | 作业 | 输入 | 输出 |
|---|---|---|---|
| 同构迁移 | `jobs/10-homo-excel-json.yaml` | `samples/sales-flat.xlsx` | `output/excel-json/homo-sales.json` |
| 单表拆多表 | `jobs/11-split-excel-json.yaml` | 同上 | `order-headers.json` + `order-lines.json` |
| 多表合并 | `jobs/12-merge-json-excel.yaml` | `customers.json` + `orders.json` | `customer-orders.xlsx` |
| 异构清洗 | `jobs/13-hetero-excel-json.yaml` | `sales-flat.xlsx`（过滤 cancelled + 列改名） | `sales-clean.json` |

```bash
dotnet new dataforge-showcase -n ExcelJsonDemo -o ./ExcelJsonDemo --connectors all
cd ExcelJsonDemo
dotnet run -- run jobs/10-homo-excel-json.yaml
dotnet run -- run jobs/11-split-excel-json.yaml
dotnet run -- run jobs/12-merge-json-excel.yaml
dotnet run -- run jobs/13-hetero-excel-json.yaml
```

默认 `--connectors default` 只有 sqlite/postgresql，跑 10–13 会报 `excel` 未注册；换 `--connectors all` 或自行引用 `DataForge.Jobs.Excel` 并调用 `JobExcelRegistration.AddDataForgeJobsExcel()`。纯 CLI 也可用全局工具：`dataforge run jobs/10-homo-excel-json.yaml`（`DataForge.Sync` 已内置注册 Excel）。

## 数据库场景 CLI

```bash
docker start dataforge-pg
# 执行 sql/sqlite-init.sql 与 sql/postgres-init.sql
dotnet run -- run jobs/01-homo.yaml
# 同一入口也支持完整批次
dotnet run -- run jobs/05-batch.yaml
```

入口调用 `JobBatch.RunFileAsync`，单作业与批次统一执行；逐项输出结果，任一执行结果失败时退出码为 1。运行时 Ctrl+C 请求协作取消，取消异常返回 130；已完成的写入不自动回滚。终端信号端到端验收仍待完成，目前已验证模板构建与引擎取消测试。

## PostgreSQL 凭据与 `-pgPort`

YAML 作业**不再内嵌** Host/Port/Username/Password/Database 字面量，统一写成：

```text
Host=${PG_HOST};Port=${PG_PORT};Username=${PG_USER};Password=${PG_PASSWORD};Database=${PG_DATABASE}
```

解析优先级（由引擎 `VariableResolver` + 本模板 `Program.cs` 共同保证）：

1. CLI `--var KEY=VALUE`
2. 非空环境变量 `KEY`
3. 本模板 `Program.cs` 里仅为开箱演示注入的默认值（`PG_HOST=127.0.0.1`、`PG_PORT=5433`、`PG_USER=dataforge`、`PG_PASSWORD=dataforge`、`PG_DATABASE=dataforge`）

`dotnet new` 的 `-pgPort` 会替换 `Program.cs` 中演示默认端口字面量 `5433`；作业实际端口随之变化。环境变量 / `--var` 仍可覆盖：

```powershell
$env:PG_HOST='db.example'; $env:PG_PORT='5432'; $env:PG_USER='app'; $env:PG_PASSWORD='secret'; $env:PG_DATABASE='appdb'
dotnet run -- run jobs/01-homo.yaml
```

```bash
export PG_HOST=db.example PG_PORT=5432 PG_USER=app PG_PASSWORD=secret PG_DATABASE=appdb
dotnet run -- run jobs/01-homo.yaml
# 或
dotnet run -- run jobs/01-homo.yaml --var PG_HOST=db.example --var PG_PORT=5432
```

YAML 中的其他 `${KEY}` 同样可通过重复的 `--var KEY=VALUE` 参数替换：

```bash
dotnet run -- run jobs/custom.yaml --var SRC=/data/input.csv --var DST=/data/output.json
```

值含空格时将整个 `KEY=VALUE` 参数加引号；值中的等号会保留。缺失值或空变量名会返回退出码 1。

## 连接器选择（`--connectors`）

本模板默认引用并注册 `sqlite` / `postgresql`（`--connectors default`，与历史行为一致）。

生成时传入 `--connectors all` 还会额外引用并注册：`sqlserver`、`mysql`、`excel`、`http`、`parquet`、`duckdb`：

```bash
dotnet new dataforge-showcase -n MyMigration -o ./MyMigration --connectors all
```

## 纯 NuGet 命令行工具

模板 `Program.cs` 只是最小的 `run` 子集。完整 CLI（`run` / `watch` / `catalog`）请安装全局工具：

```bash
dotnet tool install --global DataForge.Sync
```

`DataForge.*` / `DataForge.Jobs.*` 目前尚未发布到 nuget.org；本地验收可用仓库 `DataForge.Core/artifacts/packages` 作为附加源（例如在 `nuget.config` 里指向该绝对路径）。

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

默认生成物只注册 sqlite / postgresql；需要更多 Catalog 时请用 `--connectors all`，或自行引用对应 Jobs 适配包并调用其 `AddDataForgeJobs*` 注册方法。
