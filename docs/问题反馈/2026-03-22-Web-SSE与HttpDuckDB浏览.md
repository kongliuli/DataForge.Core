# Web SSE 进度 + Http/DuckDB 目录（2026-03-22）

## 做了什么

- `POST /api/jobs/run/stream`：`text/event-stream`，事件 `progress` / `result` / `error`。进度来自 `JobBatch` 的 `IProgress<JobProgress>`。
- Web 页面改走 SSE，并在连接档案下拉增加 excel / parquet / http / duckdb。
- `DataForge.Jobs.Http` 注册 `HttpCatalogProvider`：连接串为绝对 http(s) URL，GET 测连接，JSON 数组预览。
- `DataForge.Jobs.DuckDB` 注册 `DuckDbCatalogProvider`：information_schema 列表/列，LIMIT 预览。

## 验收

- `DataForge.Web.Tests` JobApiTests：4/4（含 `RunStream_EmitsProgressAndResult`）
- Sync 测试排除 PostgreSqlLive：本轮一并跑

## 还没做

- LocalDts 全量功能对照仍未收完（调度、断点续传、映射编辑器、真并行子作业等）
- Http/DuckDB catalog 没有单独集成测试；DuckDB 连接串解析复制自适配包（Core 里的 helper 是 internal）
