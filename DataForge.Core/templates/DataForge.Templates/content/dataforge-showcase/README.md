# DataForgeShowcase

展示三类跨库作业（SQLite ↔ PostgreSQL）。引擎是 `DataForge.Jobs`，连接器是适配包。

## 场景

| 文件 | 含义 |
|---|---|
| `jobs/01-homo.yaml` | 同构：SQLite `orders` → PostgreSQL `orders` |
| `jobs/02-split-headers.yaml` + `02-split-lines.yaml` | 单表拆多表（两个 job） |
| `jobs/03-merge.yaml` | 异构投影：列重命名后写入 SQLite 摘要表 |
| `jobs/04-join.yaml` | 多源等值 JOIN（N→1）+ `createTable` + upsert |
| `jobs/05-batch.yaml` | `jobs:` 顺序编排，失败即停 |

## 准备

```bash
docker start dataforge-pg
# 按 sql/postgres-init.sql 与 sql/sqlite-init.sql 建表
dotnet run -- run jobs/01-homo.yaml
```

连接串里的端口默认 `5433`（`dotnet new` 可用 `-pgPort`）。
