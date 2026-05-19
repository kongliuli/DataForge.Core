# DataForge.Core 核心管道实现规范

## Why

当前项目已搭建骨架结构，但核心管道存在严重架构缺陷：Where/Distinct/Skip/Take 转换器使用 `default!` 标记被过滤项，导致无法区分"被过滤的数据"和"实际的默认值"；OrderBy 转换器仅提取键但从未执行排序；Select 创建新管道时丢失已有转换链。此外，文档中定义的大量核心功能（验证系统、GroupBy、数据库源/目标、类型转换器、错误处理）均未实现。需要系统性修复和补全，使项目达到可编译、可测试、可使用的 v0.1.0 状态。

## What Changes

- **BREAKING**: 重构管道核心架构，使用 `IAsyncEnumerable` 函数式组合替代有状态的 Transform 模式
- **BREAKING**: 重构 `IDataPipeline<T>` 接口，移除 `out` 协变修饰符以支持 Where 等同类型操作
- 实现基础设施层：`ITypeConverter`、`DefaultTypeConverter`、异常层次结构
- 实现验证系统：`DataValidator<T>` 基类、`IValidationRule<T>`、内置规则（Required、Length、Range）
- 实现 `GroupBy` 分组管道操作
- 实现数据库数据源/目标：SqlServer、MySql、Sqlite
- 实现 FluentValidation 适配器
- 补全单元测试和集成测试

## Impact

- Affected specs: 管道核心、转换器、验证器、数据源、数据目标
- Affected code:
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs` — 完全重写
  - `src/DataForge.Core/Core/Pipeline/IDataPipeline.cs` — 接口调整
  - `src/DataForge.Core/Core/Transforms/*` — 全部重写为函数式管道步骤
  - `src/DataForge.Core/Core/Validation/*` — 扩展验证系统
  - `src/DataForge.Core/Core/Infrastructure/*` — 新增
  - `src/DataForge.Core/DataForgePipeline.cs` — 新增数据库入口方法
  - `src/DataForge.Core.SqlServer/*` — 实现
  - `src/DataForge.Core.MySql/*` — 实现
  - `src/DataForge.Core.Sqlite/*` — 实现
  - `src/DataForge.Core.FluentValidation/*` — 实现
  - `tests/*` — 补全测试

## ADDED Requirements

### Requirement: 管道核心架构重构

系统 SHALL 使用函数式管道组合模式实现 `DataPipeline<T>`，每个管道操作返回新的 `IAsyncEnumerable<T>` 而非依赖有状态的 Transform 对象。

#### Scenario: Where 过滤正确工作
- **WHEN** 用户调用 `pipeline.Where(x => x > 2)` 处理数据 `[1, 2, 3, 4, 5]`
- **THEN** 结果 SHALL 为 `[3, 4, 5]`，不产生任何 `default!` 值

#### Scenario: OrderBy 排序正确工作
- **WHEN** 用户调用 `pipeline.OrderByDescending(x => x)` 处理数据 `[3, 1, 4, 1, 5]`
- **THEN** 结果 SHALL 为 `[5, 4, 3, 1, 1]`

#### Scenario: 链式操作正确组合
- **WHEN** 用户调用 `pipeline.Where(x => x > 2).Select(x => x * 10).Take(2)` 处理数据 `[1, 2, 3, 4, 5]`
- **THEN** 结果 SHALL 为 `[30, 40]`

### Requirement: 基础设施层

系统 SHALL 提供类型转换器和异常层次结构。

#### Scenario: DefaultTypeConverter 转换字符串到数值
- **WHEN** 调用 `converter.Convert<int>("42")`
- **THEN** 结果 SHALL 为 `42`

#### Scenario: 异常层次结构
- **WHEN** 数据源读取失败
- **THEN** SHALL 抛出 `DataSourceException`，继承自 `DataForgeException`

### Requirement: 验证系统

系统 SHALL 提供 `DataValidator<T>` 基类，支持流畅的规则定义 API。

#### Scenario: 使用 RuleFor 定义验证规则
- **WHEN** 用户定义 `RuleFor(o => o.OrderId).NotEmpty()` 并验证空 OrderId 的对象
- **THEN** 验证结果 SHALL 包含 `ValidationError`，ErrorCode 为 `Required`

#### Scenario: ContinueOnValidationError 跳过无效数据
- **WHEN** 管道配置了 `ContinueOnValidationError()` 且数据验证失败
- **THEN** 该条数据 SHALL 被跳过，管道继续处理后续数据

#### Scenario: FailOnValidationError 抛出异常
- **WHEN** 管道配置了 `FailOnValidationError()`（默认行为）且数据验证失败
- **THEN** SHALL 抛出 `ValidationException`

### Requirement: GroupBy 分组操作

系统 SHALL 支持 `GroupBy` 操作，返回 `IGroupedDataPipeline<TKey, TElement>`。

#### Scenario: GroupBy 后聚合
- **WHEN** 用户调用 `pipeline.GroupBy(x => x.Category).Select(g => new { Category = g.Key, Count = g.Count() })`
- **THEN** 结果 SHALL 为按 Category 分组后的聚合数据

### Requirement: 数据库数据源和目标

系统 SHALL 提供 SQL Server、MySQL、SQLite 三种数据库的数据源和目标实现。

#### Scenario: FromSqlServer 读取数据
- **WHEN** 用户调用 `DataForgePipeline.FromSqlServer<Order>(connectionString, "Orders")`
- **THEN** SHALL 从 SQL Server 的 Orders 表读取数据并返回 `IDataPipeline<Order>`

#### Scenario: ToSqlServer 写入数据
- **WHEN** 用户调用 `pipeline.ToSqlServer(connectionString, "Orders", options)` 且 options.InsertMode 为 Upsert
- **THEN** SHALL 批量写入数据，对已存在的记录执行更新

### Requirement: FluentValidation 集成

系统 SHALL 提供 `FluentValidationAdapter<T>` 将 FluentValidation 的 `AbstractValidator<T>` 适配为 `IValidator<T>`。

#### Scenario: 使用 FluentValidation 验证器
- **WHEN** 用户创建 `FluentValidationAdapter(new OrderFluentValidator())` 并传入管道
- **THEN** SHALL 正确执行 FluentValidation 验证规则

## MODIFIED Requirements

### Requirement: IDataPipeline 接口

原接口使用 `out T` 协变修饰符，导致 `Where` 等 `T -> T` 操作无法正确返回 `IDataPipeline<T>`（因为接口实例不可替换）。移除 `out` 修饰符。

新增方法：
- `IDataPipeline<T> GroupBy<TKey>(Func<T, TKey> keySelector)` → 返回 `IGroupedDataPipeline<TKey, T>`
- `Task<T?> FirstOrDefaultAsync(CancellationToken ct)`
- `IDataPipeline<T> FailOnValidationError()`

### Requirement: ExportResults 模型

`ExportResults` 新增 `Duration` 自动计时功能，导出目标 SHALL 在 `ExportAsync` 中自动计算耗时。

## REMOVED Requirements

### Requirement: 有状态的 IDataTransform 模式
**Reason**: 当前 `IDataTransform<TIn, TOut>` 模式要求转换器维护状态（如 SkipTransform 的计数器），在异步流中不可靠且无法正确处理过滤逻辑（用 `default!` 标记被过滤项）。
**Migration**: 转换器接口保留用于自定义扩展点，但内部管道实现改用函数式组合（每个操作直接返回新的 `IAsyncEnumerable<T>`）。
