# Tasks

- [ ] Task 1: 扩展管道操作 — SelectMany、AggregateAsync、Batch、Zip、TransformWith
  - [ ] SubTask 1.1: 在 IDataPipeline 中新增 SelectMany、AggregateAsync、Batch、Zip、TransformWith 方法签名
  - [ ] SubTask 1.2: 在 DataPipeline 中实现 SelectMany（展平嵌套集合）
  - [ ] SubTask 1.3: 在 DataPipeline 中实现 AggregateAsync（自定义聚合）
  - [ ] SubTask 1.4: 在 DataPipeline 中实现 Batch（分批分组）
  - [ ] SubTask 1.5: 实现 Zip 管道合并操作
  - [ ] SubTask 1.6: 实现 TransformWith 自定义转换器集成

- [ ] Task 2: 错误处理策略 — IErrorHandler、OnErrorContinue/Stop/Skip/Custom
  - [ ] SubTask 2.1: 实现 IErrorHandler 接口和 ErrorAction 枚举
  - [ ] SubTask 2.2: 实现 PipelineErrorContext 模型
  - [ ] SubTask 2.3: 在 IDataPipeline 新增 OnErrorContinue/OnErrorStop/OnErrorSkip/OnError 方法
  - [ ] SubTask 2.4: 在 DataPipeline 中实现错误处理逻辑

- [ ] Task 3: 补全数据源和数据目标接口
  - [ ] SubTask 3.1: 扩展 IDataSource 接口，新增 Name、SourceType、ReadAllAsync
  - [ ] SubTask 3.2: 更新所有数据源实现（CsvSource、JsonSource、ExcelSource、MemorySource）
  - [ ] SubTask 3.3: 扩展 IDataTarget 接口，新增 Name、TargetType、WriteAsync/WriteBatchAsync/CompleteAsync
  - [ ] SubTask 3.4: 更新所有目标实现（CsvTarget、JsonTarget、ExcelTarget）

- [ ] Task 4: 新增 ConsoleTarget 和 StreamTarget
  - [ ] SubTask 4.1: 实现 ConsoleTarget（格式化输出到控制台）
  - [ ] SubTask 4.2: 实现 StreamTarget（输出到 Stream）
  - [ ] SubTask 4.3: 在 IDataPipeline 新增 ToConsole/ToStream 方法

- [ ] Task 5: 数据库导出管道集成
  - [ ] SubTask 5.1: 在 DataForge.Core.SqlServer 中实现 ToSqlServer 管道扩展方法
  - [ ] SubTask 5.2: 在 DataForge.Core.MySql 中实现 ToMySql 管道扩展方法
  - [ ] SubTask 5.3: 在 DataForge.Core.Sqlite 中实现 ToSqlite 管道扩展方法

- [ ] Task 6: 补全验证系统
  - [ ] SubTask 6.1: ValidationRuleBuilder 新增 Must、When、MinLength、GreaterThanOrEqualTo、LessThan
  - [ ] SubTask 6.2: 实现 ValidationSeverity 严重级别
  - [ ] SubTask 6.3: 实现 CollectValidationResults（收集验证结果而非跳过/抛出）
  - [ ] SubTask 6.4: 更新 DataPipeline 验证逻辑支持 CollectValidationResults

- [ ] Task 7: 新增 FromJsonString / FromJsonArray 入口
  - [ ] SubTask 7.1: 在 DataForgePipeline 中实现 FromJsonString
  - [ ] SubTask 7.2: 在 DataForgePipeline 中实现 FromJsonArray

- [ ] Task 8: 补全单元测试
  - [ ] SubTask 8.1: 新增管道操作测试：SelectMany、AggregateAsync、Batch、Zip、TransformWith
  - [ ] SubTask 8.2: 新增错误处理测试：OnErrorContinue、OnErrorStop
  - [ ] SubTask 8.3: 新增验证系统测试：Must、When、MinLength、ValidationSeverity、CollectValidationResults
  - [ ] SubTask 8.4: 新增 ConsoleTarget、StreamTarget 测试
  - [ ] SubTask 8.5: 新增 FromJsonString、FromJsonArray 测试

- [ ] Task 9: 编译验证和最终提交
  - [ ] SubTask 9.1: 确保所有项目编译通过
  - [ ] SubTask 9.2: 确保所有单元测试通过
  - [ ] SubTask 9.3: 提交并推送到 GitHub

# Task Dependencies

- [Task 2] depends on [Task 1] — 错误处理需要管道接口
- [Task 3] depends on [Task 1] — 接口扩展需要管道接口稳定
- [Task 4] depends on [Task 3] — ConsoleTarget/StreamTarget 需要新接口
- [Task 5] depends on [Task 1] — 数据库导出需要管道接口
- [Task 6] depends on [Task 1] — 验证系统需要管道接口
- [Task 7] depends on [Task 3] — 入口方法需要数据源接口
- [Task 8] depends on [Task 1-7] — 测试需要所有功能完成
- [Task 9] depends on [Task 8]
- [Task 4, Task 5, Task 6, Task 7] 可并行执行