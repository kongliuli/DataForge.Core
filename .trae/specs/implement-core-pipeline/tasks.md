# Tasks

- [x] Task 1: 重构管道核心架构 — 修复 Where/OrderBy/Skip/Take/Distinct 的根本性缺陷
  - [x] SubTask 1.1: 重写 `IDataPipeline<T>` 接口，移除 `out` 协变，新增 `GroupBy`、`FirstOrDefaultAsync`、`FailOnValidationError` 方法
  - [x] SubTask 1.2: 重写 `DataPipeline<T>` 实现，使用函数式管道组合（每个操作返回新的 `IAsyncEnumerable<T>`），消除 `default!` 过滤标记
  - [x] SubTask 1.3: 重写 `IGroupedDataPipeline<TKey, TElement>` 接口和 `GroupedDataPipeline` 实现
  - [x] SubTask 1.4: 更新 `DataForgePipeline` 入口类，新增 `FromSqlServer`、`FromMySql`、`FromSqlite` 占位方法

- [x] Task 2: 实现基础设施层 — 类型转换器和异常体系
  - [x] SubTask 2.1: 实现 `ITypeConverter` 接口和 `DefaultTypeConverter` 类
  - [x] SubTask 2.2: 实现异常层次结构：`DataForgeException`、`DataSourceException`、`DataTargetException`、`TransformException`
  - [x] SubTask 2.3: 实现 `DataSourceType` 和 `DataTargetType` 枚举

- [x] Task 3: 实现验证系统 — DataValidator 基类和内置规则
  - [x] SubTask 3.1: 实现 `IValidationRule<T>` 接口和 `ValidationRuleBuilder<T, TProperty>` 流畅 API
  - [x] SubTask 3.2: 实现 `DataValidator<T>` 基类，包含 `RuleFor` 方法和 `Validate`/`ValidateAsync` 实现
  - [x] SubTask 3.3: 实现内置验证规则：`RequiredRule`、`LengthRule`、`RangeRule`、`GreaterThanRule`
  - [x] SubTask 3.4: 更新 `DataPipeline<T>` 中的验证逻辑，支持 `FailOnValidationError` 和 `ContinueOnValidationError`

- [x] Task 4: 实现 SQL Server 数据源和目标
  - [x] SubTask 4.1: 实现 `SqlServerSource<T>`，支持 `ReadAsync` 和 `QueryAsync`
  - [x] SubTask 4.2: 实现 `SqlServerTarget<T>`，支持批量写入和 Upsert 模式
  - [x] SubTask 4.3: 实现 `SqlServerExportOptions` 配置类
  - [x] SubTask 4.4: 在 `DataForgePipeline` 中实现 `FromSqlServer` 和 `ToSqlServer` 方法

- [x] Task 5: 实现 MySQL 数据源和目标
  - [x] SubTask 5.1: 实现 `MySqlSource<T>`
  - [x] SubTask 5.2: 实现 `MySqlTarget<T>` 和 `MySqlExportOptions`
  - [x] SubTask 5.3: 在 `DataForgePipeline` 中实现 `FromMySql` 和 `ToMySql` 方法

- [x] Task 6: 实现 SQLite 数据源和目标
  - [x] SubTask 6.1: 实现 `SqliteSource<T>`
  - [x] SubTask 6.2: 实现 `SqliteTarget<T>` 和 `SqliteExportOptions`
  - [x] SubTask 6.3: 在 `DataForgePipeline` 中实现 `FromSqlite` 和 `ToSqlite` 方法

- [x] Task 7: 实现 FluentValidation 适配器
  - [x] SubTask 7.1: 实现 `FluentValidationAdapter<T>`，将 `IValidator<T>` (FluentValidation) 适配为 `IValidator<T>` (DataForge)

- [x] Task 8: 补全单元测试
  - [x] SubTask 8.1: 管道核心测试：Where、Select、OrderBy、Skip、Take、Distinct、GroupBy、链式操作
  - [x] SubTask 8.2: 验证系统测试：DataValidator、内置规则、ContinueOnValidationError、FailOnValidationError
  - [x] SubTask 8.3: 数据源测试：CsvSource、JsonSource、MemorySource
  - [x] SubTask 8.4: 导出目标测试：CsvTarget、JsonTarget

- [x] Task 9: 编译验证和最终提交
  - [x] SubTask 9.1: 确保所有项目编译通过（`dotnet build`）
  - [x] SubTask 9.2: 确保所有单元测试通过（`dotnet test`）
  - [x] SubTask 9.3: 提交并推送到 GitHub

# Task Dependencies

- [Task 2] depends on [Task 1] — 基础设施层需要新的异常类型
- [Task 3] depends on [Task 1] — 验证系统需要重构后的管道接口
- [Task 4] depends on [Task 1, Task 2] — SQL Server 实现需要管道接口和异常体系
- [Task 5] depends on [Task 1, Task 2] — MySQL 实现需要管道接口和异常体系
- [Task 6] depends on [Task 1, Task 2] — SQLite 实现需要管道接口和异常体系
- [Task 7] depends on [Task 3] — FluentValidation 适配器需要验证系统接口
- [Task 8] depends on [Task 1, Task 2, Task 3] — 测试需要核心功能完成
- [Task 9] depends on [Task 8] — 最终验证需要测试通过
- [Task 4, Task 5, Task 6] 可并行执行