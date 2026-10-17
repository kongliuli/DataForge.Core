# 抽出 DataForge.Jobs（2026-03-22）

## 目标

P1：从 `tools/DataForge.Sync` 抽出可打包引擎库，Sync 变薄宿主。

## 交付

### 新包 `src/DataForge.Jobs`

- 厚核心：引用 Core + Sqlite / PostgreSql / SqlServer / Parquet / DuckDB + YamlDotNet
- 迁入：JobRow、Models、Parsing、Validation、Execution（Runner/Sinks）、Sources
- 命名空间：`DataForge.Jobs.*`
- PackageId `DataForge.Jobs` 0.5.0；InternalsVisibleTo Sync / Sync.Tests
- Core 增加 `InternalsVisibleTo DataForge.Jobs`（`FinalizeExportResultsAsync`）

### Sync CLI

- 仅保留 `Program.cs` + `JobScheduler`（Cronos）
- ProjectReference → `DataForge.Jobs`；去掉直连连接器 / YamlDotNet

### 测试

`dotnet test DataForge.Core.sln -c Release`：

- Core.Tests 164
- IntegrationTests 14
- Sync.Tests 15
- SchemaInfer.Tests 1

## 未做（后续）

- 薄核心 + `DataForge.Jobs.*` 适配包拆分
- `DataForge.Templates` / `dotnet new`
- Jobs 独立测试项目（现仍用 Sync.Tests）
