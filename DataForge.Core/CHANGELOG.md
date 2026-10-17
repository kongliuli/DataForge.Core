# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

---

## [0.1.0] - 2024-01-01

> 说明：版本日期沿用仓库既有发布记录；接口与终端方法名称已按当前源码对齐修订。

### Added

- **Core Pipeline**
  - `DataForgePipeline` entry class（工厂方法为 static；类本身可被扩展方法扩展）
  - `IDataPipeline<T>` interface with chainable API
  - `Where`, `Select`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`
  - `Skip`, `Take`, `Distinct`, `DistinctBy`
  - `GroupBy` with aggregation support

- **Data Sources**
  - `IDataSource<T>` interface
  - SQL Server source (`FromSqlServer<T>`，扩展包)
  - MySQL source (`FromMySql<T>`，扩展包)
  - SQLite source (`FromSqlite<T>`，扩展包)
  - CSV source (`FromCsv<T>`)
  - Excel source (`FromExcel<T>`；核心为占位实现，真实 xlsx 见 `DataForge.Core.Excel`)
  - JSON source (`FromJson<T>`, `FromJsonArray<T>`；扩展能力见 `DataForge.Core.Json`)
  - Memory source (`FromMemory<T>`)

- **Data Targets**
  - `IDataTarget<T>` interface
  - CSV export (`ToCsvAsync`)
  - Excel export (`ToExcelAsync`)
  - JSON export (`ToJsonAsync`)
  - SQL Server export (`ToSqlServer`，扩展包)
  - MySQL export (`ToMySql`，扩展包)
  - SQLite export (`ToSqlite`，扩展包)
  - Console export (`ToConsoleAsync`)

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
