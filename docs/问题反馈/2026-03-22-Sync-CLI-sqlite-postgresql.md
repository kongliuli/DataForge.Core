# Sync CLI 接入 SQLite / PostgreSQL（2026-03-22）

## 目标

为 DataForge.Sync YAML/CLI 增加 `sqlite` 与 `postgresql`/`postgres` source/sink，验证后提交推送。

## 代码

- Source：`JobRowSqliteSource`、`JobRowPostgreSqlSource`
- Sink：`JobRowSqliteSink`（`INSERT` / `ON CONFLICT`）、`JobRowPostgreSqlSink`（含字符串→数值 Coerce）
- `JobRunner` / `JobSinkWriter` / `JobYamlParser` 已识别新类型
- `DataForge.Sync.csproj` 引用 `DataForge.Core.Sqlite`、`DataForge.Core.PostgreSql`

## 示例

- `tools/DataForge.Sync/examples/sqlite-to-postgresql.yaml`
- README 更新为 v0.5 能力表

## 测试

`JobRunnerSqlitePostgreSqlTests`：

1. SQLite → SQLite 同构（过滤 Amount>0）
2. SQLite → PostgreSQL → SQLite 异构 select 回写（需 `dataforge-pg:5433`）

## 验收

全量 `dotnet test DataForge.Core.sln -c Release`（见提交前运行结果）。
