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
