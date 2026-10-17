# DataForge.Jobs

声明式作业引擎（薄核心）：YAML → source → transforms → validate → sink。

- **目标框架**：`net8.0`
- **版本**：`0.5.0`
- **内置 provider**：`csv` / `json`
- **适配包**：`DataForge.Jobs.Sqlite` / `.PostgreSql` / `.SqlServer` / `.Parquet` / `.DuckDB`

使用数据库或 Parquet 前，引用对应适配包并调用 `AddDataForgeJobs*()` 注册 provider。

```csharp
using DataForge.Jobs.Execution;
using DataForge.Jobs.Sqlite;
using DataForge.Jobs.PostgreSql;

JobSqliteRegistration.AddDataForgeJobsSqlite();
JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();

var result = await new JobRunner().RunAsync("job.yaml");
```

CLI 宿主：`tools/DataForge.Sync`（已注册全部适配包）。
