# API 稳定性策略（DEC-04）

> 生效版本：**DataForge.Core 0.3.0** · 目标：**1.0.0 API 冻结**

## 1. 范围

| 包 | v1.0 稳定承诺 | 说明 |
|----|---------------|------|
| **DataForge.Core** | ✅ 是 | 唯一纳入 SemVer 1.0 冻结的包 |
| DataForge.Core.* 扩展 | ❌ 否 | 独立 SemVer，可随连接器演进 |
| DataForge.Sync / SchemaInfer | ❌ 否 | CLI 工具，YAML schema 可扩展 |

## 2. 公共 API 面（Core 1.0 候选）

### 入口
- `DataForgePipeline` 静态工厂：`FromCsv`, `FromJson`, `FromMemory`, `Merge`
- `FromExcel` 仅抛出 `EXCEL_EXTENSION_REQUIRED`（引导至扩展包）

### 管道 `IDataPipeline<T>`
- 变换：`Select`, `SelectAsync`, `SelectParallelAsync`, `Where`, `OrderBy`/`ThenBy`, `WithExternalSort`
- 验证：`ValidateWith`, `ContinueOnValidationError`, `FailOnValidationError`, `WithBadRowOutput`
- 终端：`ToJsonAsync`, `ToCsvAsync`, `ToListAsync`, `AsAsyncEnumerable`

### 模型
- `ExportResults`, `RowError`, `ExternalSortOptions`

## 3. 破坏性变更政策

- **0.x**：允许小幅 API 调整；每次 minor 在 CHANGELOG 标注
- **1.0.0**：上述 API 冻结；仅 patch 修 bug
- **2.0.0**：需 RFC 或 roadmap 显式 DEC 编号

## 4. 扩展包约定

扩展包通过 **静态 `*PipelineExtensions`** 接入，不修改 Core 接口：

```csharp
using DataForge.Core.Parquet;
await pipeline.ToParquetAsync("out.parquet");
```

## 5. 1.0 发布门禁

- [ ] 全量测试 CI 绿（146+）
- [ ] `docs/api-reference.md` 摘要与源码一致
- [ ] 无已知 Critical 级静默数据错误
- [ ] Sync Beta 可运行 roadmap §8.5 示例 job
- [ ] 本文件与 CHANGELOG 同步

## 6. 非目标（1.0 不承诺）

- GroupBy 外部 spill
- Parquet row-group 流式读
- Sync Web UI（S-04 完整版；`--report` JSON 为最小替代）

---

参见：`docs/roadmap-and-iteration.md` DEC-04 · `docs/blueprint-v0.5.md`
