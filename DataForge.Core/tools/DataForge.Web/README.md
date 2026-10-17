# DataForge.Web

最小 Web 宿主：页面贴 YAML，`POST /api/jobs/run` 调用 `DataForge.Jobs`。

已引用现有适配：Sqlite、PostgreSql、SqlServer、Parquet、DuckDB。csv/json 在 Jobs 核心。MySQL / Excel / Http 的 Jobs 适配包后补。

```bash
dotnet run --project tools/DataForge.Web
```

打开站点后点「运行」。请求体：`{ "yaml": "...", "variables": { } }`。
