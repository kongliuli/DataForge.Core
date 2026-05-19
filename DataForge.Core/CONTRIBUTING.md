# 贡献指南

感谢你考虑为 DataForge.Core 做出贡献！本文档将帮助你开始参与项目开发。

## 目录

1. [开始之前](#开始之前)
2. [开发环境](#开发环境)
3. [开发流程](#开发流程)
4. [代码规范](#代码规范)
5. [提交规范](#提交规范)
6. [测试要求](#测试要求)
7. [文档要求](#文档要求)

---

## 开始之前

在开始贡献之前，请确保：

1. 你已经阅读了 [README](../README.md) 和 [架构文档](./docs/architecture.md)
2. 你有一个 [GitHub 账号](https://github.com/join)
3. 你了解基本的 Git 工作流程

## 开发环境

### 环境要求

- .NET 8.0 SDK 或更高版本
- Visual Studio 2022 17.8+ / VS Code / JetBrains Rider
- Git

### 克隆仓库

```bash
git clone https://github.com/dataforge-team/dataforge-core.git
cd dataforge-core
```

### 还原依赖

```bash
dotnet restore
```

### 构建项目

```bash
dotnet build
```

### 运行测试

```bash
dotnet test
```

## 开发流程

### 1. Fork 仓库

点击 GitHub 页面右上角的 **Fork** 按钮，创建你自己的 Fork。

### 2. 克隆你的 Fork

```bash
git clone https://github.com/YOUR_USERNAME/dataforge-core.git
cd dataforge-core
```

### 3. 添加上游仓库

```bash
git remote add upstream https://github.com/dataforge-team/dataforge-core.git
```

### 4. 创建功能分支

```bash
git checkout -b feature/your-feature-name
# 或
git checkout -b fix/your-bug-fix
```

分支命名规范：
- `feature/` - 新功能
- `fix/` - Bug 修复
- `docs/` - 文档更新
- `refactor/` - 代码重构
- `test/` - 测试相关

### 5. 开发

编写代码，遵循本文档的代码规范。

### 6. 运行测试

```bash
# 运行所有测试
dotnet test

# 运行特定测试
dotnet test --filter "FullyQualifiedName~YourTestClass"
```

### 7. 提交代码

```bash
git add .
git commit -m "feat: 添加新功能描述"
```

提交信息遵循 [Conventional Commits](https://www.conventionalcommits.org/) 规范。

### 8. 保持 Fork 同步

```bash
git fetch upstream
git rebase upstream/main
```

### 9. Push 并创建 Pull Request

```bash
git push origin feature/your-feature-name
```

然后在 GitHub 上创建 Pull Request。

---

## 代码规范

### 命名规范

| 类型 | 命名规范 | 示例 |
|-----|---------|------|
| 类 | PascalCase | `DataForgePipeline` |
| 接口 | I + PascalCase | `IDataSource<T>` |
| 方法 | PascalCase | `ReadAsync` |
| 属性 | PascalCase | `RecordsWritten` |
| 字段 | _camelCase | `_connectionString` |
| 参数 | camelCase | `connectionString` |
| 局部变量 | camelCase | `orderList` |
| 常量 | PascalCase | `DefaultBatchSize` |
| 枚举值 | PascalCase | `ValidationSeverity.Error` |

### 文件结构

```
src/
├── DataForge.Core/
│   ├── Core/
│   │   ├── Pipeline/
│   │   │   ├── DataPipeline.cs
│   │   │   └── ...
│   │   ├── Sources/
│   │   │   ├── IDataSource.cs
│   │   │   └── ...
│   │   └── ...
│   ├── DataForge.cs              # 入口类
│   └── DataForge.Core.csproj
├── DataForge.Core.SqlServer/
│   └── ...
```

### 代码风格

**使用 C# 12 的新特性：**

```csharp
// ✅ 推荐：使用 file-scoped namespace
namespace DataForge.Core.Pipeline;

// ✅ 推荐：使用 record 类型
public record CsvExportResult
{
    public string FilePath { get; init; }
    public long RecordsWritten { get; init; }
}

// ✅ 推荐：使用 primary constructor（C# 12）
public class OrderValidator : DataValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.OrderId).NotEmpty();
    }
}

// ✅ 推荐：使用 pattern matching
var status = order.Amount switch
{
    < 1000 => "小额",
    < 10000 => "中等",
    _ => "大额"
};
```

**避免：**

```csharp
// ❌ 避免：匈牙利命名
string strOrderId;
int iCount;

// ❌ 避免：缩写
var lst = new List<Order>();
var cfg = configuration;

// ❌ 避免： magic number
if (amount > 1000000) { }

// ❌ 避免： 过短的变量名
var x = orders.Where(o => o.A > 0);
```

### 注释规范

```csharp
/// <summary>
/// 从 CSV 文件创建数据管道
/// </summary>
/// <param name="filePath">CSV 文件路径</param>
/// <param name="options">CSV 读取选项，可选</param>
/// <returns>数据管道实例</returns>
/// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
/// <remarks>
/// 支持多种编码格式，包括 UTF-8、GB2312 等。
/// </remarks>
public static CsvSourcePipeline<T> FromCsv<T>(string filePath, CsvSourceOptions? options = null)
{
    // ...
}
```

---

## 提交规范

### 提交信息格式

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type 类型

| Type | 说明 |
|------|------|
| feat | 新功能 |
| fix | Bug 修复 |
| docs | 文档更新 |
| style | 代码格式（不影响功能） |
| refactor | 重构（不是新功能或修复） |
| perf | 性能优化 |
| test | 测试相关 |
| build | 构建相关 |
| ci | CI 相关 |
| chore | 其他更改 |

### 示例

```bash
# 功能提交
git commit -m "feat(pipeline): 添加 GroupBy 支持聚合操作

- 支持按指定键分组
- 支持分组后 Sum、Count、Average 等聚合
- 支持 Having 条件筛选

Closes #123"

# 修复提交
git commit -m "fix(csv): 修复 GBK 编码读取问题

当 CSV 文件使用 GBK 编码时，正确解析中文字符。
修复了 Issue #456 中报告的乱码问题。"

# 文档提交
git commit -m "docs: 更新 README 添加性能基准数据"
```

---

## 测试要求

### 测试覆盖

- 所有公共 API 必须有单元测试
- 核心功能需要有集成测试
- 边缘情况和异常情况需要测试

### 测试命名

```csharp
[Fact]
public void FromCsv_WithValidFile_ReturnsPipeline()
{
    // Arrange
    var filePath = "test-data.csv";
    
    // Act
    var pipeline = DataForgePipeline.FromCsv<TestEntity>(filePath);
    
    // Assert
    Assert.NotNull(pipeline);
}

[Theory]
[InlineData("orders.csv", 100)]
[InlineData("customers.csv", 50)]
public void FromCsv_WithValidFiles_ReadsCorrectCount(string fileName, int expectedCount)
{
    // ...
}
```

### 测试隔离

- 单元测试不依赖外部资源（文件、数据库）
- 集成测试使用临时文件或内存数据库
- 测试之间相互独立

---

## 文档要求

### 新功能文档

新增功能必须包含：

1. **API 文档** - 在 `docs/api-reference.md` 中添加 API 说明
2. **使用示例** - 至少包含一个完整的代码示例
3. **更新场景文档** - 如果适用，在 `docs/scenarios.md` 中添加使用场景

### 文档位置

| 文档类型 | 位置 |
|---------|------|
| README | `README.md` |
| 快速上手 | `docs/getting-started.md` |
| 架构设计 | `docs/architecture.md` |
| API 参考 | `docs/api-reference.md` |
| 使用指南 | `docs/` 目录 |
| 场景实战 | `docs/scenarios.md` |

---

## 获取帮助

- 📖 阅读 [文档](./docs/)
- 💬 加入 [GitHub Discussion](https://github.com/dataforge-team/dataforge-core/discussions)
- 🐛 报告 [Bug](https://github.com/dataforge-team/dataforge-core/issues/new?template=bug_report.md)
- ⭐ 提交 [功能请求](https://github.com/dataforge-team/dataforge-core/issues/new?template=feature_request.md)

---

感谢你的贡献！
