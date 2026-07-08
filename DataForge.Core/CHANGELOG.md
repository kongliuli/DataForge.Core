# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] - 2026-07-08

### Added

- **DataForge.Core.Parquet (A-01)** — `FromParquet<T>()` / `ToParquetAsync()`
- **DataForge.Core.DuckDB (A-02)** — `FromDuckDb<T>()` / `ToDuckDbAsync()`
- **DataForge.Sync v0.5** — YAML `parquet` / `duckdb` source & sink
- **DataForge.SchemaInfer (A-03)** — `schema-infer csv|json <file>`
- `dataforge run --report run.json` — JSON 运行报告（S-04 最小）
- `docs/blueprint-v0.5.md`、`docs/API-STABILITY.md`（DEC-04 1.0 路径）
- Parquet/DuckDB/Sync/SchemaInfer 测试（146+）

### Changed

- **DataForge.Core** 0.2.1 → **0.3.0**
- **DataForge.Core.DependencyInjection** 0.2.0 → **0.3.0**

## [0.4.0] - 2026-07-08

### Added

- **DataForge.Sync v0.4** — v1.0 job subset
  - SQL Server source/sink (`insert` / `upsert` with `keys`)
  - YAML `validate.rules` (required, min, max, pattern) + `onError` + `badRowOutput`
  - `schedule` field + `dataforge watch` cron runner (Cronos)
  - 5 new Sync unit tests (validation, scheduler, SQL YAML)

### Changed

- Sync tool version 0.3.0 → 0.4.0; references `DataForge.Core.SqlServer`

## [0.3.0] - 2026-07-08

### Added

- **DataForge.Sync v0.3** — YAML job execution: `dataforge run job.yaml [--var key=value]`
  - Source: `csv` | `json`
  - Transforms: `where`, `select`
  - Sink: `csv` | `json`
  - Variables: `${VAR}` (CLI / env), `@var` in `where` expressions

## [0.2.1] - 2026-07-08

### Added

- `RowError` + `ExportResults.RowErrors` 结构化错误（D-03）
- `.WithBadRowOutput(path)` 验证失败行导出 NDJSON（D-04）
- `SelectParallelAsync(selector, maxDop)` 有界并行映射（D-05）
- `DataForge.Core.DependencyInjection` 包：`AddDataForge()`（D-06）
- Excel 扩展 `.xlsx` round-trip 集成测试（DEC-01）

## [0.2.0] - 2026-07-08

### Added

- `ExternalSortOptions` + `WithExternalSort()` — 外部排序（磁盘 spill，DEC-02）
- `PipelineCollectionExtensions.ToDataForge()` — 从集合 / 异步流创建管道
- `tools/DataForge.Sync/` — YAML 同步 CLI 脚手架（DEC-03）
- `SqlIdentifier.ValidateTableName()` — SQL 表名校验
- 回归测试：`WithCounter`+`Where`、`ThenBy`、`ValidateWith`+`Select`、`FromExcel` 引导

### Changed

- `WithCounter` / `WithProgress` 改为拦截器模型，链式中间操作不再丢失装饰器（R-01）
- `OrderBy` / `ThenBy` 使用统一排序引擎；`ThenBy` 支持真正的多级排序（R-04 / D-09）
- `Select` 在映射前先执行验证（R-03）
- 核心库 `FromExcel` 改为抛出 `EXCEL_EXTENSION_REQUIRED`，真实实现仅在 `DataForge.Core.Excel`（DEC-01）
- `HttpPipelineExtensions` / `ExcelPipelineExtensions` 提供静态入口，与 SqlServer 风格一致
- `WithParallelization` 标记 `[Obsolete]`（原实现未并行化，R-02）

### Fixed

- SqlServer Source 表名白名单校验 + `DefaultTypeConverter` 类型映射（R-06 / R-07）

### Removed

- 核心库内「CSV 冒充 Excel」的 `ExcelSource` 假实现

### Security

- SqlServer `ReadAsync` 不再直接拼接未校验的表名

---

## [0.1.0] - 2024-01-01

### Added

- **Core Pipeline**
  - `DataForgePipeline` static entry class
  - `IDataPipeline<TIn, TOut>` interface with chainable API
  - `Where`, `Select`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`
  - `Skip`, `Take`, `Distinct`, `DistinctBy`
  - `GroupBy` with aggregation support

- **Data Sources**
  - `IDataSource<T>` interface
  - SQL Server source (`FromSqlServer<T>`)
  - MySQL source (`FromMySql<T>`)
  - SQLite source (`FromSqlite<T>`)
  - CSV source (`FromCsv<T>`)
  - Excel source (`FromExcel<T>`)
  - JSON source (`FromJson<T>`, `FromJsonArray<T>`)
  - Memory source (`FromCollection<T>`, `FromEnumerable<T>`)

- **Data Targets**
  - `IDataTarget<T>` interface
  - CSV export (`ToCsv`)
  - Excel export (`ToExcel`)
  - JSON export (`ToJson`)
  - SQL Server export (`ToSqlServer`)
  - MySQL export (`ToMySql`)
  - SQLite export (`ToSqlite`)
  - Console export (`ToConsole`)

- **Validation**
  - `IValidator<T>` interface
  - `DataValidator<T>` abstract base class
  - Built-in validation rules (NotEmpty, Required, GreaterThan, LessThan, etc.)
  - `ValidateWith` pipeline method
  - `ContinueOnValidationError` and `FailOnValidationError` options

- **Transforms**
  - `IDataTransform<TIn, TOut>` interface
  - Built-in transforms (Select, Where, OrderBy, GroupBy, etc.)
  - Custom transform support via `TransformWith`

- **Multi-source Operations**
  - `Merge` for combining multiple data sources
  - `Zip` for joining two pipelines

- **Documentation**
  - Complete API documentation
  - Getting started guide
  - Architecture documentation
  - Pipeline programming guide
  - Data sources guide
  - Transforms guide
  - Validation guide
  - Export guide
  - Scenarios handbook
  - FAQ

### Changed

### Deprecated

### Removed

### Fixed

### Security
