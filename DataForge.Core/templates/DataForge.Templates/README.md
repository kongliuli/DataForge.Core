# DataForge.Templates

`dotnet new` 模板包。用于替代 LocalDts 的开箱脚手架（CLI 作业 + 指向 Web/Catalog）。

当前模板：`dataforge-showcase`。

```bash
dotnet new install ./templates/DataForge.Templates
dotnet new dataforge-showcase -n MyMigration -o ./MyMigration
```

能力：

- 跨库同构 / 拆表 / 合表 / JOIN / 批处理 YAML
- Excel↔JSON 四场景（同构 / 单表拆多表 / 多表合并 / 异构清洗；需 `--connectors all`）
- 对接 `DataForge.Jobs` Catalog（探活、列表、结构、预览）
- 与仓库 `tools/DataForge.Web`、`tools/DataForge.Sync` 同一引擎

参数：

- `-pgPort <port>`：演示默认 PostgreSQL 端口（默认 5433）
- `--connectors default|all`：`all` 额外引用并注册 sqlserver / mysql / excel / http / parquet / duckdb
