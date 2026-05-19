# DataForge.Core 缺失功能实现规范

## Why

当前项目核心管道已实现，但对照架构文档和场景手册，仍有 22 项功能缺失。其中 SelectMany、AggregateAsync、错误处理策略、数据库导出管道集成等是文档场景中实际用到的关键功能，需要补全以达到文档承诺的完整度。

## What Changes

- 新增管道操作：SelectMany、AggregateAsync、Zip、TransformWith、Batch
- 新增错误处理策略：IErrorHandler、OnErrorContinue/Stop/Skip/Custom
- 补全数据源接口：IDataSource.Name、SourceType、ReadAllAsync
- 补全数据目标接口：IDataTarget.Name、TargetType、WriteAsync/WriteBatchAsync/CompleteAsync
- 新增 ConsoleTarget、StreamTarget
- 数据库导出集成到 IDataPipeline 接口（ToSqlServer/ToMySql/ToSqlite）
- 补全验证系统：Must、When、MinLength、GreaterThanOrEqualTo、LessThan、ValidationSeverity、CollectValidationResults
- 新增 FromJsonString、FromJsonArray 入口方法

## Impact

- Affected code:
  - `src/DataForge.Core/Core/Pipeline/IDataPipeline.cs` — 新增方法
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs` — 实现新方法
  - `src/DataForge.Core/Core/Sources/IDataSource.cs` — 接口扩展
  - `src/DataForge.Core/Core/Targets/IDataTarget.cs` — 接口扩展
  - `src/DataForge.Core/Core/Validation/` — 新增规则和功能
  - `src/DataForge.Core/Core/Infrastructure/IErrorHandler.cs` — 新增
  - `src/DataForge.Core/Core/Targets/ConsoleTarget.cs` — 新增
  - `src/DataForge.Core/Core/Targets/StreamTarget.cs` — 新增
  - `src/DataForge.Core/DataForgePipeline.cs` — 新增入口方法
  - `tests/` — 补全测试

## ADDED Requirements

### Requirement: SelectMany 管道操作

系统 SHALL 支持 SelectMany 操作，将嵌套集合展平为单层流。

#### Scenario: SelectMany 展平嵌套集合
- **WHEN** 用户调用 `pipeline.SelectMany(x => x.Items)` 处理包含列表属性的数据
- **THEN** 结果 SHALL 为所有 Items 的扁平化序列

### Requirement: AggregateAsync 聚合操作

系统 SHALL 支持自定义聚合操作。

#### Scenario: AggregateAsync 自定义聚合
- **WHEN** 用户调用 `pipeline.AggregateAsync((acc, x) => acc + x.Amount, 0m)`
- **THEN** 结果 SHALL 为所有元素的 Amount 累加和

### Requirement: 错误处理策略

系统 SHALL 提供管道级错误处理策略。

#### Scenario: OnErrorContinue 跳过错误继续
- **WHEN** 管道配置了 `OnErrorContinue()` 且处理某条数据时抛出异常
- **THEN** 该条数据 SHALL 被跳过，管道继续处理后续数据

#### Scenario: OnErrorStop 遇到错误停止
- **WHEN** 管道配置了 `OnErrorStop()` 且处理某条数据时抛出异常
- **THEN** SHALL 抛出 DataForgeException 并停止处理

### Requirement: 数据源接口补全

IDataSource SHALL 包含 Name 和 SourceType 属性，以及 ReadAllAsync 方法。

### Requirement: 数据目标接口补全

IDataTarget SHALL 包含 Name 和 TargetType 属性，以及 WriteAsync/WriteBatchAsync/CompleteAsync 方法。

### Requirement: ConsoleTarget 控制台输出

系统 SHALL 提供 ConsoleTarget 将数据格式化输出到控制台。

### Requirement: 数据库导出管道集成

IDataPipeline SHALL 直接提供 ToSqlServer/ToMySql/ToSqlite 方法（通过扩展方法在各自包中提供）。

### Requirement: 验证系统补全

系统 SHALL 支持以下验证功能：
- Must 自定义谓词验证
- When 条件验证
- MinLength 最小长度
- GreaterThanOrEqualTo / LessThan 比较规则
- ValidationSeverity 严重级别
- CollectValidationResults 收集验证结果

### Requirement: FromJsonString / FromJsonArray 入口

系统 SHALL 支持从 JSON 字符串和 JSON 数组创建管道。

### Requirement: Batch 分批扩展

系统 SHALL 提供 Batch 操作将流数据按批次分组。

### Requirement: TransformWith 自定义转换器

系统 SHALL 支持 TransformWith 方法集成自定义 IDataTransform。

### Requirement: Zip 管道合并

系统 SHALL 支持 Zip 操作按位置合并两个管道的数据。