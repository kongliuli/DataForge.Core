# DataForge.Jobs

声明式作业引擎：YAML → source → transforms → validate → sink。

- **目标框架**：`net8.0`
- **版本**：`0.5.0`
- **依赖**：`DataForge.Core` + 常用连接器（厚核心：Sqlite / PostgreSql / SqlServer / Parquet / DuckDB）

## 安装

```bash
dotnet add package DataForge.Jobs
```

## 用法

```csharp
using DataForge.Jobs.Execution;

var result = await new JobRunner().RunAsync("job.yaml");
```

CLI 宿主见仓库 `tools/DataForge.Sync`（`dataforge` 工具）。设计说明：`docs/design-migration-nuget-foundation.md`。
