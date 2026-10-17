# LocalDts Catalog 交付说明（2026-03-22）

提交：`bf75d76`（Catalog NuGet + Web）及本轮 CLI 增强。

## 你可验收的路径

### 1. NuGet

```csharp
JobSqliteRegistration.AddDataForgeJobsSqlite();
await JobCatalog.TestConnectionAsync("sqlite", conn);
await JobCatalog.ListTablesAsync("sqlite", conn);
await JobCatalog.DescribeTableAsync("sqlite", conn, "orders");
await JobCatalog.PreviewAsync("sqlite", conn, "orders", 20);
```

类型：`sqlite` / `postgresql` / `sqlserver` / `mysql`。

### 2. Web

```bash
dotnet run --project DataForge.Core/tools/DataForge.Web
```

- 连接档案 CRUD（`data/connections.json`）
- 探活 / 列表 / 结构 / 预览（支持 `connectionId`）
- YAML 运行 + 历史

### 3. CLI

```bash
dotnet run --project DataForge.Core/tools/DataForge.Sync -- catalog types
dotnet run --project DataForge.Core/tools/DataForge.Sync -- catalog test --type sqlite --connection "Data Source=./demo.db"
dotnet run --project DataForge.Core/tools/DataForge.Sync -- catalog tables --type sqlite --connection "..."
dotnet run --project DataForge.Core/tools/DataForge.Sync -- catalog describe --type sqlite --connection "..." --table orders
dotnet run --project DataForge.Core/tools/DataForge.Sync -- catalog preview --type sqlite --connection "..." --table orders --limit 20
```

### 4. Templates

`dotnet new dataforge-showcase` README 已按 LocalDts 替代说明改写。

## 测试

- `JobCatalogTests` 2 通过
- 排除 live PG 的全量：Core 164 + Integration 14 + Sync 19 + Web 3 + SchemaInfer 1

## 明确未做（下一轮）

- 作业进度回调（IProgress）
- YAML 并行度选项
- 文件类 Catalog（Excel/Parquet）
- LocalDts 30+ 专用驱动按需移植
