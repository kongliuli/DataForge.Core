# DataForge.Core 调研汇总与迭代设计文档

> **版本**：v0.2 规划（决策已锁定）  
> **状态**：Accepted  
> **基线版本**：DataForge.Core 0.1.0 / .NET 8  
> **最后更新**：2026-07-08  
> **关联文档**：[architecture.md](./architecture.md) · [pipeline-guide.md](./pipeline-guide.md) · [scenarios.md](./scenarios.md)

---

## 目录

1. [文档目的与范围](#1-文档目的与范围)
2. [决策记录（已锁定）](#2-决策记录已锁定)
3. [现状评估（源码 + 使用体验）](#3-现状评估源码--使用体验)
4. [外部调研汇总](#4-外部调研汇总)
5. [竞争定位与差异化](#5-竞争定位与差异化)
6. [迭代设计原则](#6-迭代设计原则)
7. [版本路线图](#7-版本路线图)
8. [关键技术设计](#8-关键技术设计)
9. [产品化方向（库之上）](#9-产品化方向库之上)
10. [验收标准与度量](#10-验收标准与度量)
11. [风险与遗留问题](#11-风险与遗留问题)
12. [参考资料](#12-参考资料)

---

## 1. 文档目的与范围

本文档整合以下输入，形成 **v0.2 及后续版本** 的统一迭代依据：

| 输入来源 | 内容 |
|----------|------|
| 源码审读 | 管道实现、装饰器、数据源/目标、测试覆盖 |
| 架构文档 | [architecture.md](./architecture.md) 中的分层与接口设计 |
| 生态调研（GPT-5.5） | .NET 数据处理竞品、开发者痛点、2025–2026 趋势 |
| 产品调研（Opus 4.8） | 数据工具赛道形态、.NET 企业内网场景、商业化路径 |

**不在本文范围**：具体 Issue 排期、人力估算、Marketing 计划。

---

## 2. 决策记录（已锁定）

以下四项于 **2026-07-08** 评审通过，后续实现与 Issue 须与此一致。

| ID | 议题 | **决策** | 实现要点 |
|----|------|----------|----------|
| **DEC-01** | `FromExcel` 怎么处理 | **修复，而非仅抛异常** | 核心库移除「CSV 冒充 Excel」假实现；真实读写集中在 `DataForge.Core.Excel`（ClosedXML）；核心 `FromExcel` 改为明确引导安装扩展包；扩展包补齐映射/测试 |
| **DEC-02** | `OrderBy` / `ThenBy` 内存模型 | **做外部排序（磁盘 spill）** | v0.3 引入 `ExternalSortOptions`；默认小数据集仍内存排序，超过阈值自动 spill 到临时目录 |
| **DEC-03** | Sync CLI 放哪 | **Monorepo `tools/`** | 路径：`DataForge.Core/tools/DataForge.Sync/`；与库同仓 CI，独立 `dotnet tool` 打包 |
| **DEC-04** | v1.0 API 冻结范围 | **仅 `DataForge.Core` 核心包** | 扩展包（SqlServer/Excel/Http 等）独立 SemVer，不纳入 v1.0 稳定性承诺 |

### DEC-01：`FromExcel` 修复方案（摘要）

```
用户调用 FromExcel
    │
    ├─ 仅引用 DataForge.Core
    │     └─ 抛 DataForgeException（含 NuGet 安装指引），不再静默读乱码
    │
    └─ 引用 DataForge.Core.Excel
          └─ ClosedXML ExcelSource / ExcelTarget（唯一正确实现）
                ├─ 统一入口：ExcelPipelineExtensions.FromExcel<T>()
                ├─ 类型映射走 ITypeConverter
                └─ 集成测试：真实 .xlsx 读写 round-trip
```

**Breaking 说明**：仅引用核心包时，原先「能跑但结果错误」的行为改为「明确失败」——属于正确性修复，不是功能回退。

### DEC-02：外部排序方案（摘要）

- **算法**：多路归并外部排序（run 生成 → k-way merge）
- **触发**：`OrderBy`/`ThenBy`/`OrderByDescending` 链上配置 `ExternalSortOptions`，或全局 `MaxInMemorySortRows`（默认如 100_000 行）
- **临时文件**：系统 temp 目录，管道结束或取消时清理
- **ThenBy**：在 DEC-02 落地后一并修复（R-04 依赖此外部排序基础设施）

### DEC-03：Monorepo 工具布局

```
DataForge.Core/
├── src/                          # NuGet 库（不变）
├── tests/
├── tools/
│   └── DataForge.Sync/           # dotnet tool，YAML Job 驱动
│       ├── DataForge.Sync.csproj
│       ├── Program.cs
│       └── schemas/job.schema.json
└── docs/
```

### DEC-04：版本与兼容性策略

| 包 | v1.0 承诺 | 说明 |
|----|-----------|------|
| `DataForge.Core` | ✅ API 冻结 | `IDataPipeline` / `DataForgePipeline` / 验证 / 核心 Source·Target |
| `DataForge.Core.*` 扩展包 | ❌ 不冻结 | 随连接器需求迭代，主版本可 Breaking |
| `tools/DataForge.Sync` | ❌ 不冻结 | 0.x 快速迭代至 Beta |

---

## 3. 现状评估（源码 + 使用体验）

### 3.1 已有优势

| 能力 | 说明 |
|------|------|
| **零依赖核心** | 核心库仅依赖 BCL，集成门槛低 |
| **链式 API** | `FromCsv → Where → Select → ToJsonAsync`，学习曲线接近 LINQ |
| **流式模型** | 基于 `IAsyncEnumerable<T>`，适合大文件逐条处理 |
| **验证抽象** | `DataValidator<T>` + `RuleFor` 链，可接 FluentValidation |
| **扩展包模型** | SqlServer / MySql / Sqlite / Excel / Http / FluentValidation 独立 NuGet |
| **错误策略** | `OnErrorSkip / Continue / Stop` + 验证 `ContinueOnValidationError` |
| **文档骨架** | architecture、pipeline-guide、scenarios 等已具备 |

### 3.2 必须修复的问题（P0 · 正确性）

以下问题会在用户正常使用链式 API 时产生**静默错误**或**语义与文档不符**，必须在 v0.2 前解决。

#### 3.2.1 装饰器管道在后续操作后丢失

`PerformanceExtensions.cs` 中 `ProgressReportingPipeline` / `CounterReportingPipeline` / `ParallelPipeline` 将 `Where`、`Select` 等操作直接委托给 `_inner`，导致：

```csharp
// 文档/README 常见写法 — 计数器实际不会生效
await pipeline.WithCounter(counter).Where(...).ToListAsync();
```

**根因**：装饰器未在返回新管道时保留自身包装。

#### 3.2.2 `WithParallelization` 未实现真正并行

- `Task.Run` 仅原样返回 item，且 `await task` 紧跟提交，实际串行。
- `orderedItems[0].Index == orderedItems[0].Index` 恒为 true，顺序逻辑有误。
- **建议**：v0.2 删除或重写；并行应作用于用户 `Select` 委托（见 [§7.2](#72-并行-select)）。

#### 3.2.3 核心库 `FromExcel` 静默读错（DEC-01：修复）

`ExcelSource` 在核心库内用 `StreamReader` 按 CSV 读 `.xlsx` 二进制，不报错但产出乱码。

**决策 DEC-01**：不保留假实现；扩展包 ClosedXML 为唯一实现；核心库改为明确失败 + 安装指引。

#### 3.2.4 `Select` 未继承验证器与错误处理器

`DataPipeline.Select` 创建新管道时未传递 `_validator` / `_errorHandler`，导致：

```csharp
.ValidateWith(v).Select(...)  // Select 之后验证失效
```

#### 3.2.5 `ThenBy` 实现错误

当前 `ThenBy` 等同于 `OrderBy`，多级排序结果被覆盖。

**决策 DEC-02**：v0.3 外部排序落地后，在此基础设施上实现真正多级 `ThenBy`。

#### 3.2.6 关系型 Source 安全与类型映射

- `SqlServerSource`：`SELECT * FROM {tableName}` 存在表名注入面。
- 列值 `GetValue` 直接 `SetValue`，未走 `DefaultTypeConverter`，类型不匹配时运行时异常。

#### 3.2.7 文档与实现漂移

| 文档/README | 实际 API |
|-------------|----------|
| `ToJson()` / `ToCsv()` | `ToJsonAsync()` / `ToCsvAsync()` |
| `FromSqlServer(conn)` | `FromSqlServer(conn, tableName)` |
| `IDataPipeline<TIn, TOut>` 双泛型 | `IDataPipeline<T>` 单泛型 |
| Http：`DataForgePipeline.FromRestApi` | `new DataForgePipeline().FromRestApi(...)` |

### 3.3 体验缺口（P1 · 易用性）

| 缺口 | 影响 |
|------|------|
| 入口 API 风格不统一 | SqlServer 静态方法 vs Http 实例扩展，Discoverability 差 |
| `ExportResults.Errors` 仅为 `List<string>` | 导入场景难以定位坏行 |
| 无 `IEnumerable` / `IAsyncEnumerable` 扩展入口 | 与现有 LINQ 代码衔接不顺 |
| 无 DI / `ILogger` 标准集成 | ASP.NET Core 项目接入需自建包装 |
| `OrderBy` / `GroupBy` 全量物化 | 大数据 OOM；DEC-02 外部排序缓解 OrderBy/ThenBy |

### 3.4 测试覆盖缺口

- `WithCounter` 集成测试仅在链**末端**调用，未覆盖「装饰器 + 中间操作 + 终端」组合。
- 装饰器、并行、ThenBy、Select+Validate 组合缺少回归测试。

---

## 4. 外部调研汇总

### 4.1 .NET 生态竞品格局

| 项目 | 定位 | 与 DataForge 关系 |
|------|------|-------------------|
| **ETLBox** | 完整 ETL 框架，连接器多，商业支持 | 功能重叠但更重；DataForge 应做「轻量 LINQ-first」 |
| **CsvHelper** | CSV 读写事实标准 | 格式层；可借鉴映射能力，不做端到端管道 |
| **Sylvan.Data.Csv** | 高性能 CSV / DbDataReader | 格式层；未来可作可选后端 |
| **System.Linq.Async** | 异步 LINQ（Ix.NET） | .NET 10+ 平台化；管道语义可与之互操作 |
| **TPL Dataflow** | 并发/背压图 | 调度层；不负责 schema/验证/导出 |
| **Akka.NET Streams** | 分布式流处理 | 过重，非同一用户群 |
| **Microsoft.Data.Analysis** | DataFrame（长期 Preview） | GroupBy/Join 缺口大；DataForge 不正面替代 |
| **DuckDB.NET** | 嵌入式 OLAP ADO.NET | **互补**：可作 Target/Source 扩展 |
| **Parquet.Net** | Parquet 读写 | **互补**：分析场景必备 Target |

**结论**：「零依赖 + LINQ 链式 + 验证 + 多源导出」的 **micro-ETL** 位置在 .NET 生态中仍空缺。

### 4.2 开发者高频痛点（跨 Stack Overflow / GitHub / 社区）

1. 大文件 `ToList()` / 全量缓冲导致 OOM  
2. CSV 方言、culture、null、schema 漂移处理繁琐  
3. 验证、坏行报告、错误隔离需大量手写胶水  
4. 异步 IO 与 LINQ 混用时的线程/上下文问题  
5. DataFrame/Pandas 式体验在 .NET 不成熟  

DataForge 已有能力（流式 + RuleFor + ExportResults）与痛点 1–3 高度对齐，需把**正确性**和**坏行可观测性**做扎实。

### 4.3 2025–2026 趋势（适合轻量库承接的部分）

| 趋势 | DataForge 承接方式 |
|------|-------------------|
| DuckDB 嵌入式分析 | `DataForge.Core.DuckDB` 扩展包 |
| Parquet / Arrow | `ToParquetAsync` + 可选 Arrow 互操作 |
| dlt/dbt 式声明式管道 | YAML/JSON Job 描述 + CLI |
| Data quality / contract | 强化 `IValidator` + 结构化错误报告 |
| AI schema 推断 | **工具包**（采样 → POCO/Validator 生成），不进零依赖核心 |

### 4.4 产品赛道调研（库之上）

| 形态 | 代表 | 开源/商业 | 对 DataForge 启示 |
|------|------|-----------|-------------------|
| 连接器同步 | Airbyte / Fivetran | 开源+Cloud | .NET 内网难用 SaaS；做私有化/sync |
| 声明式管道 | dlt / Benthos | Apache 2.0 / fair-code | **YAML 驱动 sync 工具** 已验证 |
| 工作流 | n8n | fair-code，~$40M ARR | OEM 嵌入是可行变现 |
| 数据质量 | Soda / Great Expectations | Core 开源 + Cloud | .NET 版质量门禁有空位 |
| BI | Metabase / Evidence | 自托管+Cloud | 非近期重点 |
| AI 数据准备 | Unstructured.io | 商业+开源 | 结构化/RAG 切片可做 Target 插件 |

**.NET 企业内网特点**（ERP / MES / HIS / 政务）：

- 强私有化、数据不出内网  
- 异构源：老 SqlServer、Excel 台账、私有 REST、国产库（达梦/人大金仓）  
- 团队以 .NET 工程师为主，同栈库嵌入成本低  

**产品结论**：不与 Airbyte/Fivetran 正面竞争；聚焦 **「.NET 存量系统 · 私有化 · 配置驱动同步」** 细分。

---

## 5. 竞争定位与差异化

### 5.1 一句话定位

> **DataForge.Core = .NET 的 LINQ-first micro-ETL**  
> 在应用内完成「读 → 转 → 验 → 写」，零依赖核心 + 按需扩展包。

### 5.2 差异化矩阵

| 维度 | ETLBox | CsvHelper | DataForge（目标） |
|------|--------|-----------|-------------------|
| 端到端管道 | ✅ 重 | ❌ | ✅ 轻 |
| 零依赖核心 | ❌ | ✅（仅 CSV） | ✅ |
| 内置验证 | 部分 | ❌ | ✅ 一等公民 |
| 坏行报告 | 需自建 | ❌ | ✅ 结构化 |
| 学习曲线 | 中–高 | 低（仅 CSV） | 低（LINQ 用户） |
| 声明式配置 | 有 | ❌ | v0.3+ YAML Job |

### 5.3 不做的边界

- 不做重量级分布式流平台（Akka/Flink 类）  
- 不做完整 BI / 可视化  
- 零依赖核心不引入第三方包（含 AI/LLM SDK）  
- 非结构化文档 OCR/版面分析（Unstructured 领地）；结构化 → RAG 格式可作为 Target  

---

## 6. 迭代设计原则

1. **正确性优先于功能数量** — P0 不修不做大宣传  
2. **API 以代码为准** — 文档与实现单一真相源；或提供稳定别名并标注废弃  
3. **流式默认，排序可 spill** — 小数据内存排序；超阈值自动外部排序（DEC-02）  
4. **扩展包隔离** — 数据库/Parquet/DuckDB/Excel(ClosedXML) 不进零依赖核心  
5. **可测试** — 每个 P0 修复必须有组合链回归测试  
6. **渐进式产品化** — 库稳定后在 monorepo `tools/` 落地 Sync（DEC-03）  
7. **稳定承诺仅 Core** — v1.0 API 冻结范围限于 `DataForge.Core`（DEC-04）  

---

## 7. 版本路线图

### 7.1 总览

```
v0.1.0 (当前) ──► v0.2.0 (正确性+API统一) ──► v0.3.0 (DX+质量报告)
                                                      │
                                                      ▼
                                              v0.4.0 (分析扩展)
                                                      │
                                                      ▼
                                              v1.0.0 (稳定+Sync CLI Beta)
```

### 7.2 v0.2.0 — 正确性与信任（P0）

**主题**：让用户敢在生产环境用。

| 编号 | 工作项 | 说明 |
|------|--------|------|
| R-01 | 装饰器重构 | 用 `IAsyncEnumerable` 包装替代 30+ 方法委托；修复 WithCounter/WithProgress |
| R-02 | 移除或重写假并行 | 删除 `ParallelPipeline` 或改为 `SelectParallelAsync` |
| R-03 | 管道上下文传递 | Select/SelectAsync/SelectMany 继承 validator + errorHandler |
| R-04 | ThenBy 修复 | **依赖 v0.3 外部排序**；v0.2 可先文档标注限制，或临时内存多级排序 |
| R-05 | FromExcel 修复 **(DEC-01)** | 核心库移除假实现并抛安装指引；扩展包 ClosedXML 完善 + round-trip 测试 |
| R-06 | SqlServer 安全 | 表名白名单/标识符转义；禁止拼接不可信输入 |
| R-07 | 类型转换 | Source/Target 读取路径统一走 `ITypeConverter` |
| R-08 | 文档对齐 | README + getting-started + api-reference 与代码一致 |
| R-09 | 回归测试 | 装饰器链、Select+Validate、SqlServer 映射、Excel 扩展 round-trip |

**Breaking changes（预期）**：

- 仅引用核心包时 `FromExcel` 由「静默乱码」改为「明确失败 + 安装指引」（DEC-01）  
- 移除无效的 `WithParallelization`（或语义变更）  

### 7.3 v0.3.0 — 开发者体验 + 外部排序（P1）

**主题**：少写胶水，大数据排序不 OOM。

| 编号 | 工作项 | 说明 |
|------|--------|------|
| D-01 | 统一入口 API | 全部 `DataForgePipeline.FromXxx`；Http 改为静态扩展；废弃实例方法 |
| D-02 | 集合扩展 | `IEnumerable<T>.ToDataForge()` / `IAsyncEnumerable<T>.ToDataForge()` |
| D-03 | 结构化错误 | `RowError`（行号、字段、规则、原始值）；`ExportResults.RowErrors` |
| D-04 | 坏行导出 | `.WithBadRowOutput(path)` 一键写出错误 CSV/JSON |
| D-05 | SelectParallelAsync | Channel + 有界并发，作用于用户委托 |
| D-06 | DI 集成 | `AddDataForge()`、`IOptions`、可选 `ILogger` 挂点 |
| D-07 | 终端方法别名 | `[Obsolete]` 引导：`ToJson` → `ToJsonAsync` 兼容层（可选） |
| D-08 | Source 选项增强 | CSV：culture、null 字面量、列映射；JSON：命名策略 |
| **D-09** | **外部排序 (DEC-02)** | `ExternalSortOptions`；run 生成 + k-way merge；ThenBy 正确实现 |

### 7.4 v0.4.0 — 分析生态扩展（P2）

| 编号 | 工作项 | 包名 |
|------|--------|------|
| A-01 | Parquet 读写 | `DataForge.Core.Parquet` |
| A-02 | DuckDB 导入/查询导出 | `DataForge.Core.DuckDB` |
| A-03 | Schema 推断工具 | `DataForge.Tools.SchemaInfer`（CLI/dotnet tool） |
| A-04 | 国产库 PoC | 达梦 / 人大金仓 Source（评估后选一家） |

### 7.5 v1.0.0 — Core 稳定 + Sync 工具 Beta（DEC-03 / DEC-04）

| 编号 | 工作项 | 说明 |
|------|--------|------|
| S-01 | YAML Job 规范 | source / transforms / validate / sink / schedule |
| S-02 | `tools/DataForge.Sync` | `dotnet tool install` → `dataforge run job.yaml` **(DEC-03)** |
| S-03 | 内置调度 | 简单 cron / 单次 / watch 模式 |
| S-04 | Web 状态页（可选） | 任务历史、ExportResults、错误下载 |
| S-05 | **Core API 冻结** | **仅 `DataForge.Core` 包 SemVer 1.0 承诺 (DEC-04)** |

---

## 8. 关键技术设计

### 8.1 管道上下文（PipelineContext）

**问题**：validator、errorHandler、progress、counter 分散在 `DataPipeline` 字段中，新操作符易遗漏传递。

**设计**：

```csharp
internal sealed record PipelineContext<T>(
    Func<CancellationToken, IAsyncEnumerable<T>> SourceFactory,
    IValidator<T>? Validator = null,
    ValidationPolicy ValidationPolicy = ValidationPolicy.Fail,
    ErrorPolicy? ErrorPolicy = null,
    IReadOnlyList<IPipelineInterceptor<T>> Interceptors = null);

internal interface IPipelineInterceptor<T>
{
    IAsyncEnumerable<T> Intercept(IAsyncEnumerable<T> source, CancellationToken ct);
}
```

- `WithCounter` / `WithProgress` 实现为 `IPipelineInterceptor<T>`，不再复制整表接口方法。  
- 所有 `Select`/`Where` 仅替换 `SourceFactory`，**Context 其余字段原样保留**。

### 8.2 并行 Select

```csharp
IDataPipeline<TResult> SelectParallelAsync<TResult>(
    Func<T, Task<TResult>> selector,
    int maxDegreeOfParallelism = 4,
    bool preserveOrder = true);
```

- 有界 `Channel<T>` 输入，worker 池执行 `selector`，输出按序或无序合并。  
- **不**对「原样传递 item」做并行（无意义）。

### 8.3 结构化错误模型

```csharp
public sealed class RowError
{
    public long? RowNumber { get; init; }
    public string? SourceLocation { get; init; }  // 文件路径 / 表名 / API page
    public string? PropertyName { get; init; }
    public string? RuleName { get; init; }
    public string? RawValue { get; init; }
    public string Message { get; init; } = "";
    public ErrorKind Kind { get; init; }  // Validation | Transform | IO
}

public sealed class ExportResults
{
    // 保留现有字段
    public IReadOnlyList<RowError> RowErrors { get; } = [];
}
```

验证失败、`OnErrorSkip`、Target 写入失败均写入 `RowErrors`；`.WithBadRowOutput(path)` 在终端操作后序列化。

### 8.4 外部排序（DEC-02）

**目标**：`OrderBy` / `ThenBy` / `OrderByDescending` 在超内存阈值时 spill 到磁盘，避免 OOM。

```csharp
public sealed class ExternalSortOptions
{
    /// <summary>超过此行数启用外部排序；null = 仅内存。</summary>
    public int? MaxInMemoryRows { get; set; } = 100_000;

    public string? TempDirectory { get; set; }  // 默认 Path.GetTempPath()

    public int RunBufferRows { get; set; } = 10_000;  // 每个 sorted run 大小

    public IComparer<object>? Comparer { get; set; }   // 可选自定义比较
}
```

**流程**：

```
IAsyncEnumerable<T>
    → 按 RunBufferRows 分批读入内存
    → 每批内存 Sort(keySelector) 后序列化到 temp run 文件
    → 所有 run 就绪后 k-way merge（优先级队列）
    → 依次 yield return
    → Finally：删除 temp runs
```

**ThenBy**：维护 `SortKey[]` 链；run 文件内存储 `(T item, compositeKey)` 或多次 pass merge。v0.3 首版可先支持单键外部排序 + 内存 ThenBy（文档标注），v0.3.1 完整复合键外部 ThenBy。

**API**：

```csharp
pipeline
    .WithExternalSort(new ExternalSortOptions { MaxInMemoryRows = 50_000 })
    .OrderBy(x => x.Date)
    .ThenBy(x => x.Id)
```

### 8.5 YAML Job 规范（v1.0 草案）

```yaml
name: orders-sync
source:
  type: csv
  path: /data/orders.csv
  options:
    hasHeader: true
transforms:
  - where: "OrderDate >= @lastSync"
  - select: { OrderId: OrderId, Amount: Amount }
validate:
  validator: OrderValidator
  onError: continue
sink:
  type: sqlserver
  connection: ${DB_CONN}
  table: Fact_Orders
  mode: upsert
  keys: [OrderId]
schedule: "0 2 * * *"
```

CLI：`dotnet dataforge run tools/examples/orders-sync.yaml --var lastSync=2026-07-01`  
（工具项目位于 `tools/DataForge.Sync/`，DEC-03）

### 8.6 扩展包接入规范（不变）

新 Source/Target 继续遵循：

1. 实现 `IDataSource<T>` / `IDataTarget<T>`  
2. 提供 `XxxPipelineExtensions` 静态方法  
3. 入口：`new DataPipeline<T>(source.ReadAsync())`  
4. 出口：`target.ExportAsync(pipeline.AsAsyncEnumerable(ct), dest, ct)`  

---

## 9. 产品化方向（库之上）

按 **可行性 × 与现有代码重合度** 排序：

| 优先级 | 方向 | MVP | 变现 | 风险 |
|--------|------|-----|------|------|
| **P0** | **DataForge Sync** | `tools/DataForge.Sync` + job.yaml + cron **(DEC-03)** | fair-code + 企业连接器 + 托管控制面 | 连接器长尾维护 |
| P1 | 数据质量门禁（.NET 版 Soda 轻量） | YAML 规则 + CI 集成 + HTML 报告 | Cloud 告警/记分卡 | 市场教育 |
| P1 | OEM 嵌入 SDK | NuGet + 文档 + 白标 | License 卖给 ISV | 销售周期长 |
| P2 | 迁移工具包（ERP/HIS 切换） | 模板 Job + 断点续传 + 比对报告 | 项目制 License + 实施 | 难规模化 |
| P3 | RAG 结构化输出 Target | `ToJsonl` / `ToVectorStore` 插件 | 蹭 AI 需求，依附 Sync | Unstructured 在非结构化侧领先 |

**不建议**独立做非结构化 AI 数据准备产品；宜作为 Sync 的 `sink.type: jsonl` / `vectorstore` 插件。

---

## 10. 验收标准与度量

### 10.1 v0.2 发布门禁

- [x] 所有 P0 问题有对应单元/集成测试且 CI 绿  
- [x] `WithCounter(...).Where(...).ToListAsync()` 计数等于输出条数  
- [x] `.ValidateWith(v).Select(...).ToListAsync()` 无效行被拦截或按策略跳过  
- [x] 仅 Core 时 `FromExcel` 明确失败；引用 Excel 扩展后真实 `.xlsx` round-trip 通过 **(DEC-01)**  
- [x] README 示例可 copy-paste 编译通过（api-reference 历史章节仍标注待对齐）  
- [x] 无已知 Critical 级静默数据错误  

### 10.2 v0.3 发布门禁（外部排序）

- [x] 超 `MaxInMemoryRows` 的排序走 run 文件 spill（`SortEngine`）  
- [x] `OrderBy(...).ThenBy(...)` 结果与 LINQ 内存排序一致（回归测试）  
- [x] 取消令牌 / 异常时 temp run 文件被清理  

### 10.3 质量度量（持续）

| 度量 | 目标 |
|------|------|
| 单元 + 集成测试覆盖率 | Core ≥ 80% 行覆盖（v0.2） |
| NuGet 下载 / GitHub Star | 跟踪趋势（v0.3 起） |
| Issue：「文档与 API 不符」 | v0.2 后趋近 0 |
| 首次成功 ETL 时间（新用户） | < 15 分钟（getting-started 实测） |

---

## 11. 风险与遗留问题

| 风险 | 缓解 |
|------|------|
| 装饰器重构改动面大 | 先写失败测试，再改实现；保留行为快照测试 |
| YAML Job 表达力不足 | 支持 `csharp:` 内联脚本块或 `--plugin` 扩展 |
| 国产数据库连接器维护成本 | 先做一家 PoC，社区贡献其余 |
| fair-code 许可争议 | v1.0 前咨询 LICENSE；**核心库保持 Apache 2.0 (DEC-04)** |
| 外部排序 temp 磁盘满 | 可配置 TempDirectory；失败时抛明确 IO 异常 |
| GroupBy 仍全量物化 | v0.4 评估；文档标注与 OrderBy 不同 |

**遗留问题（非阻塞）**：

- GroupBy 是否也需要外部 spill？（当前决策未覆盖，默认 v0.4 再议）  
- Excel 扩展是否提供 `DataForgePipeline.FromExcel` 静态扩展与 Core 同名方法共存策略？（建议 Core 移除入口，仅扩展包提供）  

---

## 12. 参考资料

### 竞品与生态

- ETLBox: https://www.etlbox.net/  
- CsvHelper: https://github.com/JoshClose/CsvHelper  
- Sylvan.Data.Csv: https://github.com/MarkPflug/Sylvan  
- System.Linq.Async / .NET 10: https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/asyncenumerable  
- DuckDB.NET: https://github.com/Giorgi/DuckDB.NET/  
- Parquet.Net: https://github.com/aloneguid/parquet-dotnet  
- Microsoft.Data.Analysis issues: https://github.com/dotnet/machinelearning/issues/6144  

### 产品赛道

- Redpanda Connect (Benthos): https://github.com/redpanda-data/connect/  
- Airbyte vs Fivetran 对比: https://www.modern-datatools.com/compare/fivetran-vs-airbyte  
- n8n 增长分析: https://sacra.com/c/n8n/  
- NocoDB License: https://product-feed.nocodb.com/docs/product-docs/getting-started/license  
- Metabase Pricing: https://www.metabase.com/pricing/  
- Soda Core ELv2: https://soda.io/blog/soda-core-license-update-moving-to-elastic-license  
- Unstructured.io: https://unstructured.io/product  

### 社区痛点（示例）

- 大 CSV 内存: https://stackoverflow.com/questions/44362395/  
- 异步 CSV 处理: https://stackoverflow.com/questions/76857169/  

---

## 附录 A：P0 问题追踪表

| ID | 问题 | 文件 | 目标版本 |
|----|------|------|----------|
| P0-1 | 装饰器链丢失 | `PerformanceExtensions.cs` | v0.2 |
| P0-2 | 假并行 | `PerformanceExtensions.cs` | v0.2 |
| P0-3 | Select 丢验证器 | `DataPipeline.cs` | v0.2 |
| P0-4 | ThenBy 错误 | `DataPipeline.cs` | v0.2 |
| P0-5 | FromExcel 假实现 / 扩展修复 | `ExcelSource.cs` (Core+Excel) | v0.2 **(DEC-01)** |
| P0-6 | SQL 表名注入 | `SqlServerSource.cs` | v0.2 |
| P0-7 | 类型映射未接 Converter | `SqlServerSource.cs` 等 | v0.2 |
| P0-8 | 文档 API 漂移 | README, docs/* | v0.2 |

---

## 附录 B：调研代理摘要

| 代理 | 模型 | 产出要点 |
|------|------|----------|
| 生态调研 | GPT-5.5 | micro-ETL 空位、坏行/contract、Parquet/DuckDB、YAML 管道 |
| 产品调研 | Opus 4.8 | Sync 工具主线、.NET 内网细分、RAG 作 Target 插件、fair-code 变现 |

完整原始报告见本会话调研记录；本文档为决策用汇总版。

## 附录 C：v0.2 落地清单（2026-07-08）

| 编号 | 状态 | 说明 |
|------|------|------|
| R-01 | ✅ | 拦截器模型重写 `WithCounter` / `WithProgress` |
| R-02 | ✅ | `WithParallelization` 标记 Obsolete |
| R-03 | ✅ | `Select` 前执行验证 |
| R-04 | ✅ | `ThenBy` 多级排序 |
| R-05 | ✅ | 核心 `FromExcel` 引导 + 扩展包 ClosedXML + round-trip 测试 |
| R-06 | ✅ | SqlServer 表名校验 |
| R-07 | ✅ | SqlServer `DefaultTypeConverter` |
| R-08 | ✅ | getting-started / README / CHANGELOG 已更新；api-reference 增加 v0.2 摘要头 |
| R-09 | ✅ | `PipelineRegressionTests` + 全量 128 测试通过 |
| D-09 | ✅ | `ExternalSortOptions` + `SortEngine` |
| DEC-03 | ✅ | `tools/DataForge.Sync` v0.4：csv/json/sql + validate + watch cron |
| D-01~D-08 | 🟡 | RowError、WithBadRowOutput、SelectParallelAsync、DI 包、ToDataForge 已完成；Sync 调度/SQL sink 待 v1.0 |

---

*本文档随版本迭代更新。实现 PR 应引用对应章节编号（如 R-01、D-03）。*
