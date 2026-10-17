# DataForge.Jobs

声明式作业引擎（薄核心）：YAML → source → transforms → validate → sink。
另提供 LocalDts 风格的 **Catalog** API（探活 / 列表 / 结构 / 预览）。

- **目标框架**：`net8.0`
- **版本**：`0.5.0`
- **内置 provider**：`csv` / `json`
- **适配包**：`DataForge.Jobs.Sqlite` / `.PostgreSql` / `.SqlServer` / `.MySql` / `.Excel` / `.Http` / `.Parquet` / `.DuckDB`

## 作业

```csharp
using DataForge.Jobs.Execution;
using DataForge.Jobs.Sqlite;
using DataForge.Jobs.PostgreSql;

JobSqliteRegistration.AddDataForgeJobsSqlite();
JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();

var result = await new JobRunner().RunAsync("job.yaml");
```

## Catalog（替代 LocalDts DatabaseHelper）

关系库适配包在 `AddDataForgeJobs*()` 时自动注册 Catalog：

```csharp
using DataForge.Jobs.Catalog;

var ok = await JobCatalog.TestConnectionAsync("sqlite", conn);
var tables = await JobCatalog.ListTablesAsync("sqlite", conn);
var cols = await JobCatalog.DescribeTableAsync("sqlite", conn, "orders");
var rows = await JobCatalog.PreviewAsync("sqlite", conn, "orders", 20);
```

## 调度（清单一并进 NuGet）

```csharp
using DataForge.Jobs.Scheduling;

var scheduler = new JobScheduler();          // 默认本机时区
scheduler.OnTick += tick => Console.WriteLine(tick.Success ? "ok" : tick.Error);
await scheduler.WatchFileAsync("job.yaml", variables, cancellationToken);
```

- `JobScheduler.GetNextOccurrence("0 2 * * *", DateTimeOffset.UtcNow, TimeZoneInfo.Local)` 用标准 5 段 cron（Cronos）
- `MaxRuns` 便于测试/一次性试跑；`OnTick` 上报每次结果与下次时间
- CLI `dataforge watch` 与 Web `POST /api/jobs/schedule` 复用同一实现
- `JobScheduleService.ExportRegistrations()` 返回可 JSON 序列化的原始 YAML、变量、绝对作业路径、标识及启动运行选项；新服务逐项调用 `Restore(registration)` 后调用 `Start()` 即可重新调度。
- `ScheduleFileStore.SaveAsync(path, service.ExportRegistrations())` 将注册写入同目录临时文件再替换目标；`LoadAsync(path)` 读取注册列表，文件不存在返回空列表，损坏 JSON 抛出异常。并发写入由宿主串行化。
- 注册导出不含运行历史，也不会由 Web 自动恢复；宿主负责调用文件存储和恢复，重复标识会被拒绝。导出可能含连接凭据，应保存在受保护的位置。

已支持：`sqlite` / `postgresql` / `sqlserver` / `mysql`。

CLI：`tools/DataForge.Sync`　Web：`tools/DataForge.Web`　模板：`templates/DataForge.Templates`。
