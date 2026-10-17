# 合并 origin/main（v0.5）与本地加固（2026-03-22）

## 背景

本地 `main` ahead 5 / behind 14。rebase 在 P0/P1 加固提交上冲突过重，按用户要求改为 **拉取后 merge**，再提交推送。

## 策略

1. **文档 / 管道骨架**：优先 `origin/main`（SortEngine、v0.5 文档、DuckDB/Parquet/DI 等）
2. **删除核心 ExcelSource 占位**：跟随远端
3. **补回本地独有加固**：
   - `TransformWithAsync` + OnError 安全路径
   - `WhereAsync` / `SelectAsync` / `SelectMany` / `TransformWith` OnError
   - `Batch` / `GroupBy` / `Zip` 走 `GetValidatedEnumerable`
   - `SqlServerSource` 用 `SqlIdentifiers` 校验+引用，元数据不再泄漏连接串
   - `SqlIdentifier` 兼容包装到 `SqlIdentifiers`
   - Http `EnumerateAndDispose` + `ToRestApiAsync` / HttpClient 扩展重载
   - Excel `ToExcelAsync` 别名

## 验收

`dotnet test DataForge.Core.sln -c Release`：

- DataForge.Core.Tests: 164
- IntegrationTests: 11
- Sync.Tests: 13
- SchemaInfer.Tests: 1

构建 0 error（保留既有 Obsolete / CS8425 警告）。
