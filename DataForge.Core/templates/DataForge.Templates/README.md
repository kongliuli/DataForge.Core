# DataForge.Templates

`dotnet new` 模板包。用于替代 LocalDts 的开箱脚手架（CLI 作业 + 指向 Web/Catalog）。

当前模板：`dataforge-showcase`。

```bash
dotnet new install ./templates/DataForge.Templates
dotnet new dataforge-showcase -n MyMigration -o ./MyMigration
```

能力：

- 跨库同构 / 拆表 / 合表 / JOIN / 批处理 YAML
- 对接 `DataForge.Jobs` Catalog（探活、列表、结构、预览）
- 与仓库 `tools/DataForge.Web`、`tools/DataForge.Sync` 同一引擎
