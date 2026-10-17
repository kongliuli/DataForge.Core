# 跨库场景 + DataForge.Core.PostgreSql（2026-03-22）

## 需求

设计并落地 2–3 个跨库流转：SQLite / PostgreSQL 本机建库建表；同构、异构、单表拆多表、多表合单表。

## 交付

### 新包 `DataForge.Core.PostgreSql`

- `PostgreSqlSource` / `PostgreSqlTarget` / `PostgreSqlPipelineExtensions`
- `FromPostgreSql` / `ToPostgreSql` / `ToPostgreSqlAsync`
- Npgsql 8.0.5；`SqlIdentifiers.QuotePostgreSql`；`DataSourceType`/`DataTargetType.PostgreSql`
- 已加入 `DataForge.Core.sln`；PackageReadme 已写

### 本机环境

- Docker Desktop 已启动
- 容器 `dataforge-pg`：`postgres:16-alpine`，**5433→5432**（本机 5432 已被其他容器占用）
- 凭据：user/password/db = `dataforge`
- SQLite：测试用临时文件 + `Microsoft.Data.Sqlite`

### 三个场景（集成测试）

文件：`tests/DataForge.Core.IntegrationTests/SqlitePostgreSqlTransferScenariosTests.cs`

| # | 名称 | 方向 | 结果 |
|---|---|---|---|
| 1 | 同构 | SQLite orders → PG orders | 通过 |
| 2 | 1→N | SQLite sales_flat → PG headers+lines | 通过 |
| 3 | N→1 异构 | PG customers+orders → SQLite summary | 通过 |

过滤运行：3/3 通过。

文档：`DataForge.Core/docs/scenarios-sqlite-postgresql.md`

## 未做

- Sync CLI / YAML 尚未支持 sqlite、postgresql source/sink
- 未自动建表（场景测试内手工 DDL）
- 未提交/推送（等你确认）
