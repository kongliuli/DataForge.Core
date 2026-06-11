---
name: dataforge-architecture-docs
overview: 为 DataForge.Core 生成架构文档和 API 契约文档，描述 8 个 NuGet 包的输出边界、核心接口契约和使用约定，供其他项目集成参考。
todos:
  - id: create-docs-dir
    content: 创建 docs/ 目录结构
    status: completed
  - id: write-architecture-doc
    content: 编写 ARCHITECTURE.md 架构设计文档（系统概述、包结构 Mermaid 图、管道架构 Mermaid 图、分层设计、扩展机制）
    status: completed
    dependencies:
      - create-docs-dir
  - id: write-api-pipeline
    content: 编写 API.md 核心管道部分（DataForgePipeline 入口、IDataPipeline 完整 API 表、链式操作、终端方法）
    status: completed
    dependencies:
      - create-docs-dir
  - id: write-api-sources-targets
    content: 编写 API.md 数据源与数据目标部分（IDataSource 体系、IDataTarget、ExportOptions 类、关键枚举）
    status: completed
    dependencies:
      - create-docs-dir
  - id: write-api-validation-transforms
    content: 编写 API.md 验证与转换器部分（IValidator、ValidationRuleBuilder、IDataTransform、CommonTransforms 预置转换器）
    status: completed
    dependencies:
      - create-docs-dir
  - id: write-api-extensions-examples
    content: 编写 API.md 扩展模块与示例部分（7 个扩展包 API、性能扩展、完整使用示例）
    status: completed
    dependencies:
      - create-docs-dir
---

## 用户需求

为 DataForge.Core 项目生成稳定的架构文档和 API 契约参考文档，描述 8 个 NuGet 包的产出边界和公共接口契约，供其他 .NET 项目集成使用。

## 产品概述

两份中文技术文档：

- **ARCHITECTURE.md**：系统架构设计文档，描述整体设计理念、包结构、管道架构、数据流、扩展机制
- **API.md**：公共 API 契约参考文档，描述所有公开接口、类型、扩展方法、配置选项

## 核心特性

- 8 个 NuGet 包的依赖关系图和输出契约
- Source → Transform → Validate → Target 管道架构说明
- IDataPipeline 完整 API 表面（链式操作 + 终端方法）
- 各扩展模块的 FromXxx / ToXxx 契约
- 验证框架和转换器体系
- 使用示例和典型场景

## 技术方案

### 文档格式

- 输出格式：Markdown (.md)
- 架构图：Mermaid
- 语言：简体中文

### 文档结构

#### ARCHITECTURE.md（架构设计文档）

1. 系统概述与设计哲学
2. 包结构与依赖关系（Mermaid 依赖图）
3. 核心架构：管道模式（Mermaid 数据流图）
4. 分层设计：入口层、管道层、数据源层、数据目标层、验证层、转换层
5. 扩展机制：NuGet 插件模式
6. 错误处理与验证策略

#### API.md（API 契约参考文档）

1. 核心入口：`DataForgePipeline` 工厂方法
2. 核心管道接口：`IDataPipeline<T>` 完整 API 表
3. 数据源接口体系：`IDataSource<T>`、`IFileDataSource<T>`、`IRelationalDataSource<T>`
4. 数据目标接口：`IDataTarget<T>` 及选项类
5. 验证框架：`IValidator<T>`、`ValidationRuleBuilder`、`ValidationResult`
6. 转换器体系：`IDataTransform` + `CommonTransforms` 预置转换器
7. 性能扩展方法：`WithProgress`、`WithCounter`、`WithParallelization`
8. 各扩展模块 API（Excel/Json/Http/MySql/Sqlite/SqlServer）
9. 关键模型类型（ExportResults、DataSourceMetadata、Options 类、枚举）
10. 完整使用示例

### 实现方式

1. 基于已有代码的公共接口和 XML 文档注释提取 API 表面
2. 使用 Mermaid 绘制包依赖图、管道数据流图
3. 每个接口方法配参数说明和返回类型描述
4. 每个扩展方法指定所属命名空间和包