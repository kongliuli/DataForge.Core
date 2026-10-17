# Jobs MySQL/Excel/Http + Web 运行历史（2026-03-22）

## 新增包

- `DataForge.Jobs.MySql`：`mysql` source/sink，`createTable` + upsert（`ON DUPLICATE KEY UPDATE`）
- `DataForge.Jobs.Excel`：`excel` source/sink（ClosedXML，sink.path 为 xlsx）
- `DataForge.Jobs.Http`：`http`/`rest` source/sink（path 为 URL，sink 为 POST JSON 数组）

YAML 解析器已允许这三类 source/sink。

## 宿主

- `dataforge` CLI 与 `DataForge.Web` 都注册了上述适配器
- Web：`JobProviders.RegisterAll()` 在 `WebApplication` 创建时调用（测试宿主也会执行）
- `GET /api/jobs/history`、`GET /api/jobs/history/{id}`；运行结果写入 `data/job-history`
- 页面增加历史列表与 Excel 示例

## 测试

- `JobMySqlExcelHttpTests`：Excel 往返 + Http loopback POST
- `JobApiTests`：3/3（含 Excel 往返写历史）
- 全量 `dotnet test DataForge.Core.sln -c Release`：
  - Core.Tests 164
  - IntegrationTests 14
  - Sync.Tests 19
  - Web.Tests 3
  - SchemaInfer.Tests 1

## 未做

- MySQL 本机集成测试（无服务）
- Http source 分页/鉴权选项未暴露到 YAML（仅 URL）
