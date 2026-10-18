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

## 配置检查（不执行迁移）

```csharp
using DataForge.Jobs.Parsing;

var report = JobConfigurationValidator.Validate(yaml);
// report.Valid / JobCount / Error / Scope / ChecksNotPerformed
```

检查复用作业解析器，覆盖 YAML 结构和必填配置。不会打开连接、读取源数据或写入目标。`Valid=true` 不代表迁移能够成功：连接、权限、类型兼容及数据对账未检查；错误摘要不会回显 YAML 片段。变量替换和 provider 注册状态也不在此 API 的检查范围。

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
- Web 自动保存注册与删除，启动时恢复并重新调度。默认存储为内容根目录下的 `data/schedules.json`，可通过配置 `Schedules:Path`（环境变量 `Schedules__Path`）覆盖。一个存储文件只供一个 Web 实例使用；损坏或重复注册会阻止启动，不会静默丢弃。
- 注册导出不含运行历史；恢复后的 `RunOnStart` 会再次执行，停机期间错过的 cron 时段不会补跑。导出可能含连接凭据，应保存在受保护的位置。

已支持：`sqlite` / `postgresql` / `sqlserver` / `mysql`。

CLI：`tools/DataForge.Sync`　Web：`tools/DataForge.Web`　模板：`templates/DataForge.Templates`。
