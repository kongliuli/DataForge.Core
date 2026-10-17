# PackageReadme 补充与内层 docs 审计（2026-03-22）

## 目标

1. 为 8 个 NuGet 包补充 `README.md` 并接入 `PackageReadmeFile`
2. 审计并修复 `DataForge.Core/docs` 内层文档与源码的明显漂移
3. 保持构建与测试通过

## PackageReadme

每个包目录新增 `README.md`，并在对应 `.csproj` 中增加：

```xml
<PackageReadmeFile>README.md</PackageReadmeFile>
<None Include="README.md" Pack="true" PackagePath="\" />
```

涉及包：

- `DataForge.Core`
- `DataForge.Core.Excel`
- `DataForge.Core.Json`
- `DataForge.Core.Http`
- `DataForge.Core.FluentValidation`
- `DataForge.Core.SqlServer`
- `DataForge.Core.MySql`
- `DataForge.Core.Sqlite`

## 内层文档修复

目录：`DataForge.Core/docs/`

批量对齐：

- `.ToCsv(` / `.ToJson(` / `.ToExcel(` / `.ToConsole(` → `*Async(`
- `FromCollection` / `FromEnumerable` → `FromMemory`
- `IDataPipeline<TIn, TOut>` → `IDataPipeline<T>`（主接口声明）

手工修正：

- `getting-started.md`：`includeHeader` 命名参数改为 `CsvExportOptions`；结果字段改为 `RecordsWritten` / `Errors.Count`；补充 Excel 占位与扩展包说明
- `api-reference.md`：内存入口改为 `FromMemory`；文首增加「与当前实现对齐」说明
- `architecture.md`：文首增加同样对齐说明

## 验收

```text
dotnet build DataForge.Core.sln -c Release
# 0 错误 / 0 警告（原先 NuGet 缺少 README 的 NU512 类警告已消除）

dotnet test DataForge.Core.sln -c Release --no-build
# 单元 116 + 集成 8 = 124 通过
```

## 后续补充

PackageReadme 子代理在各扩展包 README 中追加了用法片段。对照源码后确认：

- Excel/Json 扩展为 `this DataForgePipeline` 实例扩展，导出方法名为 `ToExcel` / `ToJson`（非 `*Async`），示例使用 `new DataForgePipeline().FromXxx().ToXxx()` 与扩展签名一致
- Http 的 `FromRestApi` 同为实例扩展；SQL 包的 `FromSqlServer` / `FromMySql` / `FromSqlite` 为静态工厂，示例用类型名调用正确
- FluentValidation 示例使用 `FluentValidationAdapter<T>` + `ValidateWith`，与适配器实现一致

## 残留说明

- `api-reference.md` / `architecture.md` 中仍有大量**规划型**签名（如 `IDataPipeline<TIn, TResult>`、完整 SQL 工厂、`IGroupedPipeline` 等），已用文首 callout 标明；未整篇重写目标架构
- 示例中的 `using DataForge;`、`DataForgeVersion`、部分验证器基类名等仍可能与当前命名空间不完全一致，建议后续单独做命名空间/类型名对齐
- 核心 Excel 占位 vs `DataForge.Core.Excel` 边界已在核心包 README 与 getting-started 中提示，其它长文仍可能混用
- 扩展包导出方法名（`ToExcel`/`ToJson`）与核心 `To*Async` 并存，文档读者需注意包边界
