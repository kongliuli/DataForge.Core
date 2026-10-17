# DataForge 最终目标：引擎底座 + Local DTS 控制台

> 状态：目标梳理（2026-03-22）  
> 对照：阿里云 [数据传输服务 DTS](https://www.alibabacloud.com/help/en/dts/product-overview/what-is-dts) · 本地参考 `V:\代码库\A0100-活跃项目\数据迁移与报表\LocalDts`  
> 产品定位（已确认）：**NuGet 引擎底座 + 本机/在线控制台**，不是二选一。

## 1. 一句话目标

做一套 **可嵌入的迁移引擎**（`DataForge.Jobs` + 适配包），并在其上提供 **可直接用的 Local DTS**：CLI + Web（后续可加桌面），跑在本机/内网，覆盖异构库表的全量迁移、作业编排、校验与可观测，而不追求云上 DTS 的托管 CDC/跨云专线能力。

## 2. 对标对象分别贡献什么

### 2.1 阿里云 DTS（能力地图，不是照搬实现）

公开能力大致分三块：

| DTS 能力 | 含义 | DataForge 是否要对齐 |
|---|---|---|
| **数据迁移（Migration）** | 库/表结构 + 全量数据，一次性或可重跑 | **核心要对齐**（结构可分阶段） |
| **数据同步（Synchronization）** | 源→目标持续一致，常含增量 | **中期可选**；v1 不做实时 CDC |
| **变更订阅（Change tracking）** | 把增量变更流给下游消费 | **远期/非目标**（除非明确要做） |

阿里云侧还强调：多源异构、对象选择（库/表/列）、预检查、进度与告警、增量位点等（见 [产品概述](https://www.alibabacloud.com/help/en/dts/product-overview/what-is-dts)、[系统架构](https://www.alibabacloud.com/help/zh/dts/product-overview/system-architecture-and-design-concepts)）。

**我们学什么**

- 任务是一等公民：创建 → 配置源/目标/对象 → 预检 → 运行 → 进度/历史 → 失败可重试
- 迁移 ≠ 同步 ≠ 订阅，产品文案和路线图必须拆开
- 连接器可插拔；控制台只编排，不把驱动逻辑写死在 UI

**我们刻意不学什么（避免变成第二个云 DTS）**

- 云账号/VPC/专线/跨地域链路
- 托管吞吐与商业 SLA
- 开箱即用的全库实时同步（依赖 binlog/WAL 解码与运维）

### 2.2 LocalDts（同仓库生态的前代产品）

路径：`LocalDts/main`（Beta ~55%，插件 30+，WPF + Blazor，TPL Dataflow）。

| 维度 | LocalDts | DataForge 现状 |
|---|---|---|
| 运行时 | TPL Dataflow + 插件 DLL | `IAsyncEnumerable` 管道 + Jobs Provider |
| 合同 | `IDataSource` / `ITransformer` / `IDataTarget` + `MigrationTask` | Core 管道接口 + `JobDefinition` YAML |
| 宿主 | WPF、Console、WebDts.Blazor | Sync CLI、`DataForge.Web`、Templates |
| 连接器 | SQL Server/MySQL/Oracle/达梦/Mongo/Kafka/Redis/… | Sqlite/PG/SqlServer/MySql/Excel/Http/Parquet/DuckDB… |
| 测试 | 覆盖率低（文档自评 ~4.7%） | 解决方案测试已绿且持续加 |
| 痛点（其 README） | 空 catch、与 Web_Worker 重复造引擎 | 已定方向：Jobs 为唯一作业引擎 |

**我们继承什么**

- 「本地 DTS」产品直觉：选源选目标、配转换、跑任务、看进度
- 插件/适配边界：Contracts ↔ Core ↔ Host
- 双 UI 思路 → 我们优先 **Web + CLI**（桌面可后置）

**我们不直接合并代码的原因**

- LocalDts 与 DataForge 是两套运行时；合并插件模型成本高
- DataForge 已有 Jobs YAML、薄适配包、跨库场景与 Web MVP——应以 DataForge 为演进主线，LocalDts 作需求与交互参考

## 3. 最终产品形态（双轨）

```
                 ┌──────────────────────────────────────┐
                 │  DataForge Console（Local DTS UI）     │
                 │  Web 优先 · CLI · （可选）桌面壳         │
                 └──────────────────┬───────────────────┘
                                    │ 调 Jobs API / 存任务元数据
                                    ▼
┌──────────────┐    ┌──────────────┴──────────────┐    ┌────────────────┐
│ Templates    │───▶│  DataForge.Jobs （引擎 NuGet）│───▶│ DataForge.Core │
│ dotnet new   │    │  YAML·Runner·Join·Batch·…   │    │ + 连接器包      │
└──────────────┘    └──────────────┬──────────────┘    └────────────────┘
                                   │
                    ┌──────────────▼──────────────┐
                    │ DataForge.Jobs.* 适配包       │
                    │ Sqlite / PostgreSql / MySql / │
                    │ Excel / Http / …              │
                    └─────────────────────────────┘
```

1. **引擎底座（可嵌入）**  
   任何 .NET 程序引用 `DataForge.Jobs` + 所需适配包，即可解析 YAML/JSON 作业并执行。这是「给别人做迁移工具」的售卖面。

2. **Local DTS 控制台（可直接用）**  
   当前 `DataForge.Web` + `dataforge` CLI 演进为任务控制台：连接管理、对象选择、作业编辑、运行、历史、预检报告。这是「给迁移同学直接用」的产品面。

两者共享同一引擎；禁止再长出第二套 Runner。

## 4. 能力分层（最终要有什么）

### P0 — 已有或接近（巩固）

- 流式管道与多连接器
- Jobs：单源、`sources[]` JOIN、有序 `jobs:`、createTable、upsert
- 适配：sqlite / postgresql / sqlserver / mysql / excel / http / parquet / duckdb
- CLI + Web 跑 YAML + 运行历史
- 跨库场景（同构 / 1→N / N→1）与模板骨架

### P1 — Local DTS 最小可用（下一步产品化）

| 能力 | 说明 | 对标 |
|---|---|---|
| 连接档案 | 安全保存连接串/探活 | DTS 源目标配置；LocalDts Helper |
| 对象浏览器 | 列库表、预览样例行、选表/列 | DTS 对象选择；LocalDts GetTables/Preview |
| 作业可视化编辑 | 表单/向导生成 YAML，仍可看源码 | LocalDts WPF 配置流 |
| 预检查 | 连通性、表存在、类型粗检、权限 | DTS 预检 |
| 运行可观测 | 进度%、行数、耗时、错误行导出 | DTS/LocalDts Progress |
| 任务元数据 | 作业列表、版本、最近运行、重跑 | DTS 任务生命周期 |

### P2 — 迁移深度

- Schema：推断 → 生成 DDL →（可选）依赖排序迁表
- 校验：行数对账、抽样 checksum、坏行隔离
- 大批量：分批/断点续跑（checkpoint）、限流
- 多作业 DAG：比线性 `jobs:` 更完整的依赖与失败策略

### P3 — 同步（明确为可选产品线）

- 仅当有真实需求时做「定时增量」或「基于水位列的准实时」
- 完整 CDC/订阅不对齐阿里云 DTS，除非单独立项

## 5. 非目标（写死，避免范围膨胀）

- 不做云托管 DTS（账号、VPC、跨地域链路）
- Jobs 核心不做 GUI；GUI 只在 Console 宿主
- 不把 LocalDts 插件二进制硬塞进 DataForge
- 不以 Oracle/达梦/Kafka 插件数量竞赛为 KPI；按场景加适配
- 不默认自动毁掉目标库数据；危险操作需显式确认

## 6. 与当前仓库的落点

| 仓库组件 | 在最终目标中的角色 |
|---|---|
| `DataForge.Core` + 连接器 | 管道与驱动底层 |
| `DataForge.Jobs` + `Jobs.*` | **唯一作业引擎 / NuGet 售卖面** |
| `DataForge.Templates` | 场景脚手架 |
| `tools/DataForge.Sync` | 薄 CLI 宿主 |
| `tools/DataForge.Web` | Local DTS Web 控制台雏形 |
| `docs/design-migration-nuget-foundation.md` | 包分层设计（已采纳） |
| LocalDts 仓库 | 需求/交互参考，不作为运行时依赖 |

## 7. 成功标准（怎么算「做成了」）

1. **嵌入**：外部项目只引 NuGet，能用 YAML 完成 Sqlite→PostgreSQL 同构迁移并拿到报告  
2. **开箱**：本机起 Web，无需写代码即可配置连接、选表、跑通一次跨库任务并在历史中回看  
3. **可信**：预检 + 对账至少覆盖行数；失败有坏行/错误信息，无静默吞异常  
4. **边界清晰**：文档明确「迁移已交付 / 同步未做 / 订阅不做」  
5. **不分裂**：CLI / Web / 模板全部调用同一 `JobRunner`

## 8. 建议的近期执行顺序

1. 把本文收为产品北极星；路线图与 issue 按 P1 拆  
2. Web：连接档案 + 表浏览器 + 预检（先 Sqlite/PG）  
3. Jobs：进度回调 / checkpoint（支撑控制台进度条与续跑）  
4. Schema 包或 `DataForge.Migrate` 草案（DDL 生成，仍可选）  
5. 再评估是否做水位增量；默认仍以全量迁移打磨体验

## 9. 总结

- **对标阿里云 DTS**：学任务生命周期与迁移能力分层，不学云托管与完整 CDC。  
- **参照 LocalDts**：学本地插件式迁移产品形态与双 UI，引擎以 DataForge.Jobs 为准，避免双引擎。  
- **最终形态**：`Jobs` NuGet 底座 + Local DTS 控制台（Web/CLI）；模板降低起步成本。  
- **当前阶段**：引擎与多源适配已具备雏形；下一跃迁是 **控制台产品化（连接/对象/预检/进度）**，而不是继续堆连接器数量。
