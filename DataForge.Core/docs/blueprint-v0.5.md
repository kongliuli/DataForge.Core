# DataForge v0.5 总结蓝图

> 2026-07-08 · 覆盖 v0.2 → v0.5 迭代成果与 v1.0 路径

---

## 1. 产品定位

**DataForge.Core** 是 .NET 8 流式 ETL 库；**DataForge.Sync** 是其 YAML 驱动的同步 CLI（DEC-03）。核心库零额外依赖，数据库 / 分析格式通过扩展包接入。

```
┌─────────────────────────────────────────────────────────────┐
│                    应用 / Sync CLI / ISV 嵌入                  │
├─────────────────────────────────────────────────────────────┤
│  DataForge.Sync (YAML jobs)  │  DataForge.SchemaInfer (A-03) │
├─────────────────────────────────────────────────────────────┤
│  扩展包：SqlServer · Excel · Http · Json · Parquet · DuckDB   │
├─────────────────────────────────────────────────────────────┤
│  DataForge.Core — IDataPipeline<T> 链式 API（DEC-04 冻结目标）│
└─────────────────────────────────────────────────────────────┘
```

---

## 2. 版本里程碑

| 版本 | 主题 | 关键交付 |
|------|------|----------|
| **v0.2.0** | 质量修复 | 拦截器、ThenBy、外部排序、FromExcel 扩展分离 |
| **v0.2.1** | DX | RowError、WithBadRowOutput、SelectParallelAsync、DI |
| **v0.3.0** | Sync 初版 | `dataforge run` — csv/json + where/select |
| **v0.4.0** | Sync 完整 job | SQL 源/汇、YAML validate、cron watch |
| **v0.5.0** | 分析 + 闭环 | Parquet/DuckDB 扩展 + Sync 接入 + SchemaInfer |
| **v1.0.0** | 稳定 | Core API 冻结 (DEC-04) + Sync Beta + 可选 Web 状态页 |

---

## 3. 核心 API（DataForge.Core）

```csharp
// 入口
DataForgePipeline.FromCsv<T>(path)
DataForgePipeline.FromJson<T>(path)
DataForgePipeline.FromMemory(data)

// 链式
pipeline
    .Where(...)
    .Select(...)
    .OrderBy(...).ThenBy(...).WithExternalSort(options)
    .ValidateWith(validator).ContinueOnValidationError()
    .WithBadRowOutput("errors.ndjson")

// 终端
await pipeline.ToJsonAsync(path);
await pipeline.ToCsvAsync(path);
await pipeline.ToListAsync();
```

**已锁定决策 (DEC-01~04)**：Excel 仅在扩展包；OrderBy 可磁盘 spill；Sync 在 monorepo `tools/`；v1.0 稳定承诺仅 Core 包。

---

## 4. 扩展包矩阵

| 包 | 读 | 写 | Sync YAML |
|----|----|----|-----------|
| SqlServer | ✅ | ✅ | `source/sink.type: sqlserver` |
| Excel | ✅ | ✅ | — |
| Http | ✅ | ✅ | — |
| Json | ✅ | ✅ | `csv/json`（Sync 内置 JobRow） |
| **Parquet** | ✅ | ✅ | `source/sink.type: parquet` |
| **DuckDB** | ✅ | ✅ | `source/sink.type: duckdb` + `query` |

---

## 5. Sync YAML 规范（v0.5）

```yaml
name: orders-sync
schedule: "0 2 * * *"          # dataforge watch

source:
  type: parquet                # csv | json | sqlserver | parquet | duckdb
  path: ./orders.parquet
  # duckdb 额外:
  # query: "SELECT * FROM sales"

transforms:
  - where: "Amount > 0"
  - select: { OrderId: OrderId, Amount: Amount }

validate:
  onError: continue
  badRowOutput: ./errors.ndjson
  rules:
    - field: Amount
      min: 0

sink:
  type: duckdb
  path: ./analytics.duckdb
  table: fact_orders
```

**CLI**

```bash
dataforge run job.yaml --var lastSync=2026-07-01
dataforge watch job.yaml
schema-infer csv sample.csv    # → 生成 validate.rules
```

---

## 6. 测试与质量

| 类别 | 数量 | 说明 |
|------|------|------|
| 单元测试 | 122 | Core 管道、验证、Source/Target |
| 集成测试 | 11+ | Excel/Parquet/DuckDB round-trip |
| Sync 测试 | 11+ | YAML 解析、validate、调度 |
| **合计** | **144+** | CI 全绿 |

---

## 7. 开放 PR 栈

| PR | 状态 | 内容 |
|----|------|------|
| #7~#9 | 可关闭 | 已并入 #10 |
| **#10** | **待合并 → main** | v0.5 全量（Sync + Parquet/DuckDB + SchemaInfer + 发布准备） |

合并后打 tag：`v0.5.0`

---

## 8. v1.0 剩余工作

| 优先级 | 项 | 说明 |
|--------|-----|------|
| P0 | 合并 PR #7~#10 → main | 发布 NuGet 0.5.x |
| P1 | Core API 审查 + 1.0.0 | DEC-04 文档化 breaking 承诺 |
| P1 | Sync `validator: ClassName` 插件 | 加载 FluentValidation / 自定义 IValidator |
| P2 | S-04 Web 状态页 | 任务历史、ExportResults、错误下载 |
| P2 | A-04 国产库 PoC | 达梦 / 人大金仓评估 |
| P3 | Parquet row-group 流式读 | 替代整文件反序列化 |
| P3 | GroupBy 外部 spill | 与 OrderBy 对称 |

---

## 9. 架构原则（Ponytail 备忘）

1. **Core 零依赖** — 新格式进扩展包，不进 Core  
2. **一处修复，全部受益** — 拦截器 / SortEngine / SqlIdentifier 共享  
3. **Sync JobRow** — YAML 作业用动态行；扩展包 typed API 供代码集成  
4. **测试不弹窗** — 无 Process.Start；SQL/DuckDB 用 mock 或 `:memory:`  
5. **文档跟代码** — api-reference 摘要头 + roadmap 附录 C 同步  

---

## 10. 快速上手路径（< 15 分钟）

```bash
# 1. 库 ETL
dotnet add package DataForge.Core
# FromCsv → Where → ToJsonAsync

# 2. Parquet 分析链
dotnet add package DataForge.Core.Parquet
dotnet add package DataForge.Core.DuckDB

# 3. Sync 作业
cd tools/DataForge.Sync/examples
dotnet run --project .. -- run example-job.yaml

# 4. 推断验证规则
dotnet run --project ../DataForge.SchemaInfer -- csv orders.csv
```

---

*本文档随 v0.5 发布；详细决策见 `docs/roadmap-and-iteration.md`。*
