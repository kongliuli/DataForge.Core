# DataForge.Web

Jobs Web 宿主：连接档案、Catalog、YAML 配置检查、运行、SSE 进度、历史与持久调度。

已注册适配：SQLite、PostgreSQL、SQL Server、MySQL、Excel、HTTP、Parquet、DuckDB；CSV/JSON 来自 Jobs 核心。

## 使用 CI 发布文件夹

成功的 GitHub CI 提供 `dataforge-web-net8` artifact。下载并完整解压后，安装 .NET 8 ASP.NET Core Runtime，在解压目录执行：

```bash
dotnet DataForge.Web.dll --urls http://127.0.0.1:5080
```

访问 http://127.0.0.1:5080 。发布文件夹应包含 `wwwroot`、配置文件和所有依赖 DLL，不需要源码仓库。此产物为依赖框架的发布，不包含 .NET 运行时，也不是 NuGet.org 工具包。CI 产物以该次工作流成功为准。

默认连接与调度文件写入内容根目录的 `data` 下，运行账号需要该目录写权限。升级时保留数据目录。首次使用保持回环监听；本宿主尚未提供用户认证。

## 从源码构建

在包含解决方案的内层目录执行：

```bash
dotnet run --project tools/DataForge.Web
# 或生成发布文件夹
dotnet publish tools/DataForge.Web -c Release -o web-publish
```

运行接口：`POST /api/jobs/run`，请求体 `{ "yaml": "...", "variables": {} }`。配置检查接口 `/api/jobs/validate` 不执行迁移，不能替代连接、权限、数据类型与对账检查。
