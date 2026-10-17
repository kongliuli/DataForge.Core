# DataForge.Core 架构（外层索引）

> 版本：0.1.0 | net8.0

详细短文与接口清单在内层文档，避免两份长文漂移：

| 文档 | 内容 |
|---|---|
| [DataForge.Core/docs/architecture.md](../DataForge.Core/docs/architecture.md) | 分层、包边界、执行模型（短） |
| [DataForge.Core/docs/api-implemented.md](../DataForge.Core/docs/api-implemented.md) | **已实现** API |
| [DataForge.Core/docs/api-roadmap.md](../DataForge.Core/docs/api-roadmap.md) | 规划 / 历史误写 |
| [DataForge.Core/docs/README.md](../DataForge.Core/docs/README.md) | 文档目录 |

## 一句话结构

`DataForgePipeline` / `*PipelineExtensions` → `IDataSource<T>` → `IDataPipeline<T>` → `IDataTarget<T>` → `ExportResults`。

核心零依赖；Excel 真 `.xlsx`、HTTP、SQL、FluentValidation 在扩展包。
