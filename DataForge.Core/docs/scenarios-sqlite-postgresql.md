# 跨库流转场景：SQLite ↔ PostgreSQL

> 依赖：`DataForge.Core.Sqlite` + `DataForge.Core.PostgreSql`（Npgsql）  
> 本机 PostgreSQL：Docker 容器 `dataforge-pg`，端口 **5433**，库/用户/密码均为 `dataforge`

```bash
docker start dataforge-pg
# 或首次：
docker run -d --name dataforge-pg \
  -e POSTGRES_PASSWORD=dataforge -e POSTGRES_USER=dataforge -e POSTGRES_DB=dataforge \
  -p 5433:5432 postgres:16-alpine
```

可执行验收：`SqlitePostgreSqlTransferScenariosTests`（3 个 Fact）。

## 场景 1 — 同构表跨库

**流**：SQLite `orders` → PostgreSQL `orders`（列名/类型对齐）

```csharp
await SqlitePipelineExtensions
    .FromSqlite<OrderRow>(sqliteConn, "orders")
    .ToPostgreSqlAsync(pgConn, "orders");
```

验证：目标表 3 行，金额与源一致。

## 场景 2 — 单表拆多表

**流**：SQLite 宽表 `sales_flat` → PostgreSQL `order_headers` + `order_lines`

1. 读出 flat  
2. 按 `OrderId` 聚合写 headers  
3. 逐行写 lines

验证：2 个 header、4 个 line。

## 场景 3 — 多表合单表 + 异构映射

**流**：PostgreSQL `customers` ⟕ `orders` → SQLite `customer_order_summary`

目标列与源不同：`Name→Buyer`、`Region→RegionCode`、`Amount→OrderTotal` 等。

验证：3 行摘要，金额合计与订单一致。

## 说明

- 目标表需预先 DDL（测试里用脚本建表）；包本身不做自动建表。
- 标识符经 `SqlIdentifiers` 校验；PostgreSQL 用双引号引用。
- Sync CLI 尚未接入 SQLite/PostgreSQL，本场景走管道 API。
