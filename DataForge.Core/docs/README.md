# DataForge.Core 文档

面向 **0.1.0** 读者：先分清「已实现」与「规划中」。

| 文档 | 说明 |
|---|---|
| [getting-started.md](./getting-started.md) | 快速上手 |
| [api-reference.md](./api-reference.md) | API 索引（枢纽） |
| [api-implemented.md](./api-implemented.md) | **已实现**公共 API（以 `src/` 为准） |
| [api-roadmap.md](./api-roadmap.md) | **规划中** / 历史目标签名 |
| [architecture.md](./architecture.md) | 分层与包边界（短） |
| [pipeline-guide.md](./pipeline-guide.md) | 管道编程 |
| [data-sources.md](./data-sources.md) | 数据源 |
| [transforms.md](./transforms.md) | 转换 |
| [validation.md](./validation.md) | 验证 |
| [export.md](./export.md) | 导出（真实选项 / `ExportResults`） |
| [scenarios.md](./scenarios.md) | 4 个可运行场景 |
| [scenarios-sqlite-postgresql.md](./scenarios-sqlite-postgresql.md) | SQLite ↔ PostgreSQL 跨库（同构/拆表/合表） |
| [faq.md](./faq.md) | 常见问题 |

**约定**：`using DataForge.Core;`；核心 `To*Async`；扩展包导出常无 `Async`；SQL 用 `*PipelineExtensions.From*(conn, table)`。
