# 设计：模板化迁移工具的 NuGet 基底

> 状态：提案（2026-03-22）  
> 目标：把现有流水线能力抽象成可发布 NuGet，支撑后续「引擎包 + 模板包」形态的模板化迁移工具。

## 1. 结论（先说能不能）

**能，而且现在就该按这个方向拆。**

当前仓库已经具备基底所需的三层能力：

| 层 | 现状 | 是否适合做 NuGet |
|---|---|---|
| 流式管道 | `DataForge.Core` + 各 `*.SqlServer/Sqlite/PostgreSql/...` | 已是 NuGet |
| 作业执行 | `tools/DataForge.Sync` 内嵌 JobRunner / YAML / source·sink | **应抽出为库包** |
| 模板/脚手架 | 仅有 examples YAML | **应新增模板包** |

不要把「迁移工具」做成又一个单体 CLI 塞满逻辑；CLI 只做宿主，可复用逻辑进 NuGet。

## 2. 产品形态（已选）

**引擎包 + 模板包**（双轨）：

1. **引擎**：可编程 API + 声明式 Job 模型（YAML/JSON）→ 执行 source→transforms→validate→sink  
2. **模板**：`dotnet new` / 预置 job 模板（如 `sqlite→postgresql`、同构表、1→N、N→1）→ 生成可改的作业与项目壳

管道库继续做「一行数据怎么流」；引擎做「一次迁移作业怎么描述与跑」；模板做「常见迁移怎么开箱」。

## 3. 推荐包分层

```
                    ┌─────────────────────────────┐
                    │  DataForge.Templates        │  dotnet new / job 样例包
                    │  (模板包，非运行时依赖)        │
                    └──────────────┬──────────────┘
                                   │ 生成
                                   ▼
┌──────────────┐    ┌──────────────┴──────────────┐    ┌────────────────┐
│ dataforge    │───▶│  DataForge.Jobs             │───▶│ DataForge.Core*│
│ CLI 宿主     │    │  Job 模型·解析·Runner·插件   │    │ + 连接器扩展包  │
└──────────────┘    └──────────────┬──────────────┘    └────────────────┘
                                   │
                    ┌──────────────▼──────────────┐
                    │ DataForge.Jobs.Sqlite       │
                    │ DataForge.Jobs.PostgreSql   │  （可选：按连接器拆的 Job 适配）
                    │ DataForge.Jobs.SqlServer    │
                    └─────────────────────────────┘
```

### 3.1 已有且保持不变的运行时包

- `DataForge.Core`：管道、`IDataSource` / `IDataPipeline` / `IDataTarget`、标识符、错误模型  
- 连接器：`DataForge.Core.Sqlite` / `PostgreSql` / `SqlServer` / `MySql` / `Excel` / `Http` / `Parquet` / `DuckDB` / …  
- `DataForge.Core.DependencyInjection`：可选 DI 注册

这些继续服务「代码里写管道」的用户，也作为 Jobs 引擎的底层依赖。

### 3.2 新建：`DataForge.Jobs`（引擎核心库）

从 `tools/DataForge.Sync` **上移**到 `src/` 并 pack：

| 模块 | 职责 | 公开 API 方向 |
|---|---|---|
| 模型 | `JobDefinition` / Source / Sink / Transform / Validate | 稳定 DTO，版本化 |
| 解析 | YAML（及后续 JSON）→ JobDefinition | `JobParser.Parse` |
| 变量 | `${ENV}` / `--var` / `@var` | `VariableResolver` |
| 执行 | `JobRunner.RunAsync` | 返回 `JobRunResult` |
| 插件点 | Source/Sink 注册表 | `IJobSourceProvider` / `IJobSinkProvider` |
| 行模型 | `JobRow`（字符串字典袋） | 保持简单；类型 coerce 放 sink 侧 |

**故意不放进 Jobs 核心的：**

- cron watch UI / 进程托管（留在 CLI）  
- 具体驱动（Npgsql、Microsoft.Data.Sqlite）——通过 Jobs.* 适配包或 Provider 注册引入  
- Schema 推断、自动建表（后续可选包，不挡 v1）

### 3.3 新建：`DataForge.Jobs.*` 适配包（可选拆分）

两种实现策略（择一写进路线图）：

**A. 厚核心（先快）**  
`DataForge.Jobs` 直接 ProjectReference/PackageReference 常用连接器，内置 sqlite/postgresql/sqlserver provider。  
适合内部工具快速迭代。

**B. 薄核心 + 适配包（更像平台）**  
- `DataForge.Jobs` 只含抽象与 csv/json 等无重依赖 provider  
- `DataForge.Jobs.Sqlite`、`DataForge.Jobs.PostgreSql`… 注册 provider  
- CLI / 模板按需引用  

**建议**：对外平台走 **B**；仓库内可以先 A 抽出，再切 B（用同一套 `IJobSourceProvider` 接口，避免二次撕裂）。

### 3.4 改建：`DataForge.Sync` CLI

- 变成 **薄宿主**：解析 argv、加载 Jobs、可选加载适配包、打印报告  
- `PackAsTool` 继续；版本与 `DataForge.Jobs` 对齐（建议同主版本）  
- 不再承载业务实现源文件（Source/Sink 实现迁出）

### 3.5 新建：`DataForge.Templates`

- 类型：`dotnet new` 模板包（`Microsoft.TemplateEngine`）  
- 内容示例：
  - `dataforge-job`：单 YAML 作业 + README  
  - `dataforge-migration`：小控制台项目，引用 `DataForge.Jobs` + 选定连接器，内含 2–3 个场景 YAML  
- 参数：`--source sqlite` `--sink postgresql` `--scenario homo|split|merge`  
- **不**依赖运行时去「执行模板」；模板只生成文件

## 4. 与「模板化迁移」的映射

你已验证的跨库场景，对应模板参数而不是新引擎概念：

| 场景 | 引擎能力 | 模板怎么给 |
|---|---|---|
| 同构表跨库 | sqlite/postgresql source+sink + insert/upsert | `scenario=homo` YAML |
| 单表拆多表 | 一次 Job 不够时：多 Job 或后续 `steps:` | v1 用两个 job 文件；v2 再加编排 |
| 多表合单表 / 异构 | `select` 投影 + where | `scenario=merge` + 列映射表 |
| 本机建库建表 | **不在引擎内默认做** | 模板附带 `init.sql` / 文档步骤 |

原则：**引擎保证「搬得动、可校验、可报告」；模板保证「常见路径零起步」。** 自动 DDL / 外键图 / 依赖排序属于下一层 `DataForge.Migrate`（可选），不要塞进 Jobs v1。

## 5. 公开 API 边界（Jobs v1）

最小稳定面：

```csharp
// 解析 + 执行
JobDefinition job = JobYaml.Parse(text);
JobRunResult result = await new JobRunner(providers).RunAsync(job, vars, ct);

// 扩展
public interface IJobSourceProvider
{
    bool CanHandle(string type);
    IAsyncEnumerable<JobRow> ReadAsync(SourceDefinition source, CancellationToken ct);
}

public interface IJobSinkProvider
{
    bool CanHandle(string type);
    Task<ExportResults> WriteAsync(IAsyncEnumerable<JobRow> rows, SinkDefinition sink, CancellationToken ct);
}
```

YAML 类型名保持与现网一致：`csv|json|sqlite|postgresql|postgres|sqlserver|parquet|duckdb`。

## 6. 版本与依赖策略

- 管道包：`0.1.x` / 随仓库 `0.5` 文档叙事可继续，但 **Jobs 建议独立从 `0.5.0` 或 `1.0.0-preview` 起**，避免和历史 Core 文档漂移绑死。  
- Jobs 依赖：`DataForge.Core`（必选）+ 连接器（按策略 A/B）。  
- Templates 依赖：无运行时依赖；README 写明需安装的 Jobs / 连接器版本。  
- 破坏性变更：YAML 字段走 `API-STABILITY` / changelog；provider 接口变更需次版本说明。

## 7. 实施路线（建议）

| 阶段 | 工作 | 验收 |
|---|---|---|
| P0 | 本文档合入；标注 Sync 迁出清单 | 评审通过 |
| P1 | 新建 `src/DataForge.Jobs`，从 Sync 搬模型/Runner/csv·json·sqlite·postgresql provider | 现有 Sync 测试改引用后全绿 |
| P2 | Sync CLI 瘦身，只引用 Jobs | `dataforge run` 行为不变 |
| P3 | `DataForge.Templates` 最小 `dataforge-job` + sqlite→pg 场景 | `dotnet new` 生成可跑 |
| P4 |（可选）Jobs 适配包拆分、多 step 编排、`DataForge.Migrate` DDL | 单独立项 |

## 8. 风险与非目标

**风险**

- `JobRow` 全字符串 + PG 强类型：已在 sink coerce，需文档化并单测边界  
- Sync 与管道扩展双入口（`FromSqlite` vs YAML `type: sqlite`）会并存——文档写清「代码管道 vs 作业引擎」  
- 拆包过细导致用户要装 6 个包——模板用「推荐引用集合」缓解

**非目标（Jobs v1）**

- 通用 schema migration / flyway 替代品  
- GUI  
- 分布式调度（cron watch 仅本机）  
- 自动发现表依赖并拆任务

## 9. 对现有仓库的直接含义

- **现在就可以**把 Sync 里的执行栈视为「未打包的 Jobs」。  
- **PostgreSql / Sqlite 连接器包**已经是模板化迁移的正确底层，不必再造一套 ADO 封装。  
- **跨库场景测试**应下沉为 Jobs 包的集成测试，模板只消费同等 YAML。

## 10. 下一步

文档评审通过后，优先 P1：抽出 `DataForge.Jobs` 骨架（不改 YAML 语义），再动 Templates。
