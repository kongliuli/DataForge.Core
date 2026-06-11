# DataForge.Core — 全线改进工作计划

## TL;DR

> **Quick Summary**: 基于深度项目分析报告中的 12 项改进建议，制定分三个 Waves 的全线执行计划。Wave 1（今明天）修复 2 个 P0 正确性 Bug + 补齐 CI/CD + 补充关键测试；Wave 2（1-2周）重构数据库扩展包/代码分析/性能基准/内存优化；Wave 3（1-3月）文档国际化/高级特性/社区建设/Excel 完整实现。
>
> **Deliverables**:
> - Wave 1: 正确的 CSV 解析器（支持跨行字段/转义引号）、安全的内存源元数据、GitHub Actions CI、6+ 新测试文件
> - Wave 2: 抽象基类 `RelationalSource<T>` / `RelationalTarget<T>`、`.editorconfig` + Roslyn 分析器、BenchmarkDotNet 项目、内存优化注释+分区
> - Wave 3: `docs/en/` 英文文档目录、并行管道/LINQ to SQL 原型、NuGet 发布流水线、ClosedXML 实现的 ExcelSource
>
> **Estimated Effort**: XLarge（跨 1 天到 3 个月的渐进式工作）
> **Parallel Execution**: YES — 3 Waves，Wave 内最大化并行
> **Critical Path**:
> ```
> Wave 1: Task 1.1 (CSV) + Task 1.2 (MemorySource) → Task 1.4.1-1.4.4 (扩展测试)
>         Task 1.3 (CI/CD) 独立
> Wave 2: Task 2.1 (Db重构) → Task 2.3 (Benchmark) → Task 2.4 (内存优化)
>         Task 2.2 (代码分析) 独立
> Wave 3: 全部可并行
> ```

---

## Context

### 原始请求
基于 `dataforge-core-analysis.md` 中的 12 项改进建议，制定完整工作计划并对短期 4 项进行详细拆解。

### 分析报告摘要
**项目状态**: DataForge.Core v0.1.0，.NET 8.0 轻量级数据管道库。47 个源文件/3153 行，6 个测试文件/759 行，10 篇文档/7522 行。Apache 2.0 许可证。15 次 Git 提交。

**核心发现**:
| P0 问题 | P1 问题 | P2 缺失 | P3 长期 |
|---------|---------|---------|---------|
| CSV 解析不支持跨行字段 | 数据库扩展包 95% 代码重复 | 数据目标层无测试 | 文档全中文无英文版 |
| MemorySource 多次枚举导致数据丢失 | Excel 源是冒牌 CSV 实现 | 集成测试目录存在但为空 | 无并行/LINQ to SQL |
| | 核心库无 CI/CD | 无代码分析器 | 无 NuGet 发布流水线 |
| | | 无性能基准测试 | Excel 扩展包仅空壳 |

---

## Work Objectives

### Core Objective
系统性提升 DataForge.Core 的正确性、工程化程度、测试覆盖率和扩展完整性，使其从 v0.1.0 原型阶段进入 v0.2.0 稳定可发布阶段。

### Concrete Deliverables
- **Wave 1**（今明天）: 
  - `src/DataForge.Core/Core/Sources/Implementations/CsvSource.cs` — 正确的 CSV 解析
  - `src/DataForge.Core/Core/Sources/Implementations/MemorySource.cs` — 安全的元数据
  - `.github/workflows/ci.yml` — CI/CD 配置
  - 新建: `tests/DataForge.Core.Tests/Targets/CsvTargetTests.cs`
  - 新建: `tests/DataForge.Core.Tests/Targets/JsonTargetTests.cs`
  - 新建: `tests/DataForge.Core.Tests/Targets/ConsoleTargetTests.cs`
  - 新建: `tests/DataForge.Core.Tests/Pipeline/ErrorHandlingTests.cs`
  - 新建: `tests/DataForge.Core.Tests/Pipeline/EdgeCaseTests.cs`
- **Wave 2**（1-2周）: 抽象基类 × 2，`.editorconfig` + `Directory.Build.props`，Benchmark 项目，内存注释
- **Wave 3**（1-3月）: `docs/en/` × 10，并行管道原型，NuGet 发布配置，ClosedXML ExcelSource

### Definition of Done
- [x] （预填）所有 P0 正确性问题已修复，dt test 全绿
- [ ] Wave 1 完成：GitHub Actions CI 绿灯
- [ ] Wave 2 完成：Roslyn 分析器零错误
- [ ] Wave 3 完成：NuGet 0.2.0 发布成功

### Must Have
- 不再有 CSV 数据解析错误
- MemorySource 元数据查询不破坏数据流
- CI/CD 可运行

### Must NOT Have (Guardrails)
- **不**修改公开 API 签名（保持向后兼容）
- **不**引入新 NuGet 依赖到核心库（保持零外部依赖原则）
- **不**删除现有测试用例
- **不**在核心库中使用数据库驱动（数据库通过扩展包引入）

---

## Verification Strategy

> **ZERO HUMAN INTERVENTION** - ALL verification is agent-executed.

### Test Decision
- **Infrastructure exists**: YES — xUnit + FluentAssertions + Microsoft.NET.Test.Sdk
- **Automated tests**: Tests-after — 核心库已有测试框架，新代码需配套测试
- **Framework**: xUnit 2.9.0 + FluentAssertions 7.0.0
- **TDD**: No — 修复类任务先改代码再验证；新建类任务代码+测试同时

### QA Policy
Every task MUST include agent-executed QA scenarios. Evidence saved to `.sisyphus/evidence/task-{N}-{scenario-slug}.{ext}`.
- **Frontend/UI**: N/A（纯后端库）
- **TUI/CLI**: interactive_bash — 运行 `dotnet test` 验证测试结果
- **API/Backend**: Bash (dotnet test) — 运行测试套件，检查通过/失败
- **Library/Module**: Bash (dotnet build) — 编译通过，无警告

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Start Immediately — P0修复 + 工程化基础):
├── Task 1.1: CSV 解析器重写 [deep]
├── Task 1.2: MemorySource 元数据修复 [quick]
├── Task 1.3: GitHub Actions CI/CD [quick]
├── Task 1.4.1: 新建 CsvTarget 测试 [quick]
├── Task 1.4.2: 新建 JsonTarget 测试 [quick]
├── Task 1.4.3: 新建 ConsoleTarget 测试 [quick]
├── Task 1.4.4: 新建 ErrorHandling 测试 [deep]
└── Task 1.4.5: 新建 EdgeCase 测试 [deep]

Wave 2 (After Wave 1 — 架构优化 + 性能):
├── Task 2.1a: 提取 RelationalSource 抽象基类 [deep]
├── Task 2.1b: 提取 RelationalTarget 抽象基类 [deep]
├── Task 2.1c: 重构 SqlServer 扩展 [quick]
├── Task 2.1d: 重构 MySql 扩展 [quick]
├── Task 2.1e: 重构 Sqlite 扩展 [quick]
├── Task 2.2: 引入代码分析工具 [quick]
├── Task 2.3: BenchmarkDotNet 性能基准 [unspecified-high]
└── Task 2.4: 内存优化注释+预留 [quick]

Wave 3 (After Wave 2 — 长期战略):
├── Task 3.1: 文档国际化框架 [writing]
├── Task 3.2a: 高级特性 — 并行管道原型 [deep]
├── Task 3.2b: 高级特性 — LINQ to SQL 原型 [deep]
├── Task 3.3: 社区建设 + NuGet 发布 [quick]
└── Task 3.4: Excel 源 ClosedXML 实现 [deep]

Wave FINAL (After ALL waves):
├── Task F1: Plan Compliance Audit (oracle)
├── Task F2: Code Quality Review (unspecified-high)
├── Task F3: Real Manual QA (unspecified-high)
└── Task F4: Scope Fidelity Check (deep)
```

---

## TODOs

---

### Wave 1: P0 修复 + 工程化基础（今明天 — 详细拆解）

---

- [x] 1.1 重写 CSV 解析器 — 支持跨行字段、转义引号、行缓冲区

  **What to do**:
  
  **A) 新增 `ReadCsvLineAsync` 方法**（`CsvSource.cs` 内部）:
  - 替换现有的 `reader.ReadLineAsync()` + 单行状态机方案
  - 实现字符级读取循环（`reader.Read()` 逐字符），跟踪 `inQuotes` 状态
  - 当 `ch == '\n' && !inQuotes` 时结束一行，返回完整行字符串
  - 当 `inQuotes == true` 时跨越多行继续读取
  - 处理 `\r\n` 换行（Windows 风格）
  - 处理文件末尾 EOF 情况

  **B) 修复 `ParseCsvLine` 方法**（`CsvSource.cs:95-123`）:
  - 修复转义引号处理：`""` 在引号内表示一个字面引号
  - 当前逻辑 `inQuotes = !inQuotes` 遇到 `""` 时翻转两次但未保留内容
  - 需要：检测当前引号后是否紧跟第二个引号，若是则追加一个字面引号并跳过
  - 需要用 `charIndex` 提前读取下一个字符（`line[charIndex + 1]`）

  **C) 修改 `ReadAsync` 调用方**（`CsvSource.cs:28-66`）:
  - 将 `reader.ReadLineAsync()` 替换为 `ReadCsvLineAsync(reader, ct)`
  - 调整行号计数器逻辑

  **D) 补充 XML 文档注释**:
  - `ReadCsvLineAsync`: 说明支持 RFC 4180 风格的跨行字段

  **Must NOT do**:
  - ❌ 不引入第三方 CSV 库（保持零外部依赖）
  - ❌ 不修改 `MapToObject` / `ConvertValue`（与解析无关）
  - ❌ 不修改 CSV 导出（CsvTarget.cs）— 导出已有正确的 `EscapeValue`

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: CSV 解析涉及状态机、字符级处理、边界条件，需要仔细的逻辑推理
  - **Skills**: `[]`
  - **Skills Evaluated but Omitted**:
    - None needed — 纯算法逻辑，不涉及外部库

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Task 1.2, 1.3)
  - **Blocks**: Task 1.4.5 (EdgeCase tests include CSV parsing scenarios)
  - **Blocked By**: None (can start immediately)

  **References**:
  - `src/DataForge.Core/Core/Sources/Implementations/CsvSource.cs:95-123` — 现有 ParseCsvLine 状态机（需修复）
  - `src/DataForge.Core/Core/Sources/Implementations/CsvSource.cs:28-66` — 现有 ReadAsync 调用链（需修改调用方）
  - `src/DataForge.Core/Core/Sources/Options/CsvSourceOptions.cs:1-20` — CSV 选项（Delimiter, QuoteChar 等）

  **Acceptance Criteria**:
  - [ ] `dotnet build src/DataForge.Core/DataForge.Core.csproj` → PASS, 零警告
  - [ ] 新建 `CsvSourceAdvancedTests` 测试通过:
    - [ ] 字段含逗号: `"Smith, John",30` → Name="Smith, John", Age=30
    - [ ] 跨行字段: `"Hello\nWorld",42` → 单条记录
    - [ ] 转义引号: `"He said ""Hello""",42` → Name=`He said "Hello"`
    - [ ] 空字段处理
    - [ ] 注释行跳过（`CommentPrefix` 选项）

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: CSV with quoted field containing delimiter
    Tool: Bash (dotnet test)
    Preconditions: Test file created with content `"Smith, John",30`
    Steps:
      1. Write test in CsvSourceAdvancedTests.cs: using MemorySource pattern + CsvSource mapped to Person
      2. Run `dotnet test --filter "QuotedFieldWithDelimiter"`
      3. Assert `Person.Name == "Smith, John"` AND `Person.Age == 30`
    Expected Result: Test passes, Name equals "Smith, John" (not "Smith" or " John")
    Evidence: .sisyphus/evidence/task-1.1-quoted-delimiter.txt

  Scenario: CSV with multi-line field (quoted field contains newline)
    Tool: Bash (dotnet test)
    Preconditions: Test CSV file with content: `"Line1\nLine2",42`
    Steps:
      1. Create temp file with multi-line quoted field
      2. Read via CsvSource<Person>.ReadAsync()
      3. Collect all items
      4. Assert exactly 1 item returned, Name="Line1\nLine2", Age=42
    Expected Result: Single record with Name containing newline
    Failure Indicators: Record count != 1 OR Name doesn't contain newline OR Age wrong
    Evidence: .sisyphus/evidence/task-1.1-multiline-field.txt

  Scenario: CSV with escaped quotes inside quoted field
    Tool: Bash (dotnet test)
    Preconditions: Test CSV: `"He said ""Hello""",42`
    Steps:
      1. Parse via CsvSource<Person>
      2. Assert `Person.Name == 'He said "Hello"'`
    Expected Result: Name = He said "Hello" (quote marks preserved)
    Failure Indicators: Name contains "" (two quotes) or missing quotes entirely
    Evidence: .sisyphus/evidence/task-1.1-escaped-quotes.txt

  Scenario: Existing simple CSV tests still pass (regression guard)
    Tool: Bash (dotnet test)
    Preconditions: Existing CsvSourceTests.cs
    Steps:
      1. Run `dotnet test --filter "CsvSourceTests"`
      2. Assert ALL 3 existing tests pass
    Expected Result: 3 tests pass, 0 fail
    Failure Indicators: Any existing test regression
    Evidence: .sisyphus/evidence/task-1.1-regression.txt
  ```

  **Commit**: YES
  - Message: `fix(csv): rewrite CSV parser with RFC 4180 support for multiline fields and escaped quotes`
  - Files: `src/DataForge.Core/Core/Sources/Implementations/CsvSource.cs`, `tests/DataForge.Core.Tests/Sources/CsvSourceAdvancedTests.cs`

---

- [x] 1.2 修复 MemorySource 元数据多次枚举

  **What to do**:
  
  **A) 添加缓存字段**（`MemorySource.cs` 构造函数）:
  - 新增 `private readonly int? _cachedCount;` 字段
  - 在构造函数中检测 `_data` 的类型:
    - `is ICollection<T>` → 使用 `.Count`（O(1) 操作）
    - `is IReadOnlyCollection<T>` → 使用 `.Count`
    - `is T[]` → 使用 `.Length`
    - 其他情况 → `_cachedCount = null`（无法预估）
  
  **B) 重写 `GetMetadataAsync` 方法**（`MemorySource.cs:32-40`）:
  - 将 `_data.Count() * 1024L` 替换为有条件逻辑
  - 如果 `_cachedCount != null` → 使用缓存值
  - 否则 → 返回 `-1L`（表示无法估算）
  
  **C) 补充测试**（`tests/DataForge.Core.Tests/Infrastructure/InfrastructureTests.cs` 或新文件）:
  - 测试 `IEnumerable<T>`（yield return）调用元数据后数据仍可正确读取
  - 测试 `ICollection<T>`（List）元数据返回正确 Size
  - 测试 `T[]` 元数据返回正确 Size

  **Must NOT do**:
  - ❌ 不修改 `ReadAsync` 方法
  - ❌ 不在 `MemorySource` 中调用 `.ToList()` 强制缓存（破坏延迟特性）

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 单一文件小改动，逻辑简单明确
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (with Task 1.1, 1.3)
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Sources/Implementations/MemorySource.cs:17-51` — 完整文件
  - `src/DataForge.Core/Core/Models/DataSourceMetadata.cs:3-15` — Size 字段定义

  **Acceptance Criteria**:
  - [ ] `dotnet build` → PASS
  - [ ] 测试：IEnumerable（yield return）→ Metadata 不消耗数据 → ReadAsync 仍返回全部数据
  - [ ] 测试：List<int> → GetMetadataAsync → Size > 0（有意义的估计值）
  - [ ] 测试：int[] → GetMetadataAsync → Size > 0
  - [ ] 现有测试 `dotnet test` → 全部通过

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: IEnumerable with deferred execution, metadata query preserves data
    Tool: Bash (dotnet test)
    Preconditions: Create data using iterator method (yield return) — not a List/array
    Steps:
      1. var source = new MemorySource<int>(YieldNumbers())
      2. var metadata = await source.GetMetadataAsync()  // first enumeration trigger
      3. var items = new List<int>()
      4. await foreach (item in ReadAsync()) items.Add(item)  // second enumeration
      5. Assert items.Count == expected (e.g., 5)
    Expected Result: items contains all 5 numbers despite metadata being called first
    Failure Indicators: items is empty or has fewer than 5 items
    Evidence: .sisyphus/evidence/task-1.2-deferred-ok.txt

  Scenario: ICollection source returns correct metadata size
    Tool: Bash (dotnet test)
    Preconditions: new MemorySource<int>(new List<int> { 1, 2, 3, 4, 5 })
    Steps:
      1. var metadata = await source.GetMetadataAsync()
      2. Assert metadata.Size > 0 (not -1)
    Expected Result: Size > 0, collection count detected
    Evidence: .sisyphus/evidence/task-1.2-collection-size.txt

  Scenario: Raw IEnumerable (non-collection) returns -1 size
    Tool: Bash (dotnet test)
    Preconditions: new MemorySource<int>(Enumerable.Range(1, 100))
    Steps:
      1. var metadata = await source.GetMetadataAsync()
      2. Assert metadata.Size == -1L
    Expected Result: Size == -1L (unknown)
    Evidence: .sisyphus/evidence/task-1.2-unknown-size.txt
  ```

  **Commit**: YES
  - Message: `fix(memory): prevent multiple enumeration in GetMetadataAsync by adding size caching`
  - Files: `src/DataForge.Core/Core/Sources/Implementations/MemorySource.cs`, `tests/DataForge.Core.Tests/Infrastructure/MemorySourceMetadataTests.cs`

---

- [x] 1.3 配置 GitHub Actions CI/CD

  **What to do**:
  
  **A) 新建 `DataForge.Core/.github/workflows/ci.yml`**:
  - 触发器：`push` 到 `main` 和 `develop` 分支，`pull_request` 到 `main`
  - Job: `build-and-test`，运行在 `ubuntu-latest`
  - Steps:
    1. `actions/checkout@v4`
    2. `actions/setup-dotnet@v4` — 指定 `dotnet-version: 8.0.x`
    3. `dotnet restore DataForge.Core.sln`（working-directory: DataForge.Core）
    4. `dotnet build --no-restore --configuration Release`
    5. `dotnet test --no-build --configuration Release --verbosity normal`
    6. `dotnet pack --no-build --configuration Release --output nupkg/`（dry-run 包生成）

  **B) 新建 `DataForge.Core/.github/workflows/publish.yml`**（NuGet 发布准备）:
  - 触发器：`release` published
  - Job: `publish-nuget`
  - Steps: restore → build → pack → `dotnet nuget push`（用 GitHub Secrets `NUGET_API_KEY`）

  **Must NOT do**:
  - ❌ 不在 CI 中配置 NuGet 正式推送（仅 pack 验证）
  - ❌ 不在 CI 中添加外部依赖

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 标准 YAML 配置，无复杂逻辑
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 1 (独立任务，无依赖)
  - **Blocks**: None
  - **Blocked By**: None

  **References**:
  - `DataForge.Core/.github/ISSUE_TEMPLATE/` — 现有模板位置参考
  - `DataForge.Core/.github/PULL_REQUEST_TEMPLATE.md` — 现有 PR 模板
  - `DataForge.Core/DataForge.Core.sln` — 解决方案文件路径
  - `DataForge.Core/src/DataForge.Core/DataForge.Core.csproj` — NuGet 包元数据已有，无需修改

  **Acceptance Criteria**:
  - [ ] `.github/workflows/ci.yml` 文件存在且语法正确
  - [ ] `.github/workflows/publish.yml` 文件存在
  - [ ] 本地模拟 CI 步骤：`dotnet restore && dotnet build && dotnet test` → PASS

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: CI file references correct paths and build succeeds
    Tool: Bash (dotnet restore + build + test)
    Preconditions: ci.yml and publish.yml created
    Steps:
      1. Working-directory: DataForge.Core, run `dotnet restore DataForge.Core.sln` → success
      2. Run `dotnet build DataForge.Core.sln --configuration Release` → success
      3. Run `dotnet test DataForge.Core.sln --configuration Release` → all pass
      4. Verify ci.yml exists: `Test-Path .github/workflows/ci.yml` → True
      5. Verify publish.yml exists: `Test-Path .github/workflows/publish.yml` → True
    Expected Result: All dotnet commands succeed locally (simulating CI)
    Failure Indicators: Wrong working-directory path, missing project refs in build
    Evidence: .sisyphus/evidence/task-1.3-ci-local.txt
  ```

  **Commit**: YES
  - Message: `ci: add GitHub Actions CI and NuGet publish workflows`
  - Files: `.github/workflows/ci.yml`, `.github/workflows/publish.yml`

---

- [ ] 1.4.1 新建 CsvTarget 导出测试

  **What to do**:
  - 新建 `tests/DataForge.Core.Tests/Targets/CsvTargetTests.cs`
  - 复用 `TemporaryFile` 辅助类（从 CsvSourceTests.cs 提取或原地复用）
  - 测试场景：
    1. **基础导出** — Person[] → 导出 CSV → 验证文件内容
    2. **字段含分隔符** — Name="Smith, John" → 验证引号转义
    3. **字段含引号** — Name=`John "JD" Doe` → 验证引号转义
    4. **空数据集** — 空数组 → 仅输出表头行
    5. **禁用表头** — `IncludeHeader = false`
    6. **自定义分隔符** — 用 Tab 分隔
    7. **大数分片** — 多余 `BatchSize` 条记录 → 验证全量写出

  **Must NOT do**:
  - ❌ 不修改 CsvTarget.cs 源码（除非发现转义 Bug）

  **Recommended Agent Profile**:
  - **Category**: `quick` — Reason: 标准 xUnit 测试编写
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 1 (with Task 1.4.2, 1.4.3)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Targets/CsvTarget.cs` — 被测试对象
  - `src/DataForge.Core/Core/Targets/IDataTarget.cs:39-50` — CsvExportOptions
  - `tests/DataForge.Core.Tests/Sources/CsvSourceTests.cs:55-72` — TemporaryFile helper 复用

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter "CsvTargetTests"` → 7 tests pass, 0 fail

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: CsvTarget exports with quote-escaped delimiter in field
    Tool: Bash (dotnet test)
    Preconditions: Person data: Name="Smith, John", Age=30
    Steps:
      1. Export to temp CSV via CsvTarget<Person>.ExportAsync()
      2. Read file content
      3. Assert content contains `"Smith, John"` (quoted field)
    Expected Result: Delimiter in value is safely quoted
    Evidence: .sisyphus/evidence/task-1.4.1-quoted-delimiter.txt

  Scenario: CsvTarget exports empty dataset with header only
    Tool: Bash (dotnet test)
    Preconditions: Empty Person[], options: IncludeHeader=true
    Steps:
      1. Export empty collection
      2. Read file
      3. Assert content.Trim() == "Name,Age" (only header, no data rows)
    Expected Result: Single header line only
    Evidence: .sisyphus/evidence/task-1.4.1-empty-dataset.txt
  ```

  **Commit**: YES (与 1.4.2-1.4.5 组合提交)
  - Message: `test: add CsvTarget, JsonTarget, ConsoleTarget, ErrorHandling, EdgeCase tests`
  - Files: `tests/DataForge.Core.Tests/Targets/CsvTargetTests.cs` + 其他新测试

---

- [ ] 1.4.2 新建 JsonTarget 导出测试

  **What to do**:
  - 新建 `tests/DataForge.Core.Tests/Targets/JsonTargetTests.cs`
  - 测试场景：
    1. **基础导出** — 对象数组 → JSON → 反序列化后验证属性完整
    2. **缩进模式** — `Indented = true` → 验证 JSON 含换行
    3. **压缩模式** — `Indented = false` → 验证单行 JSON
    4. **根属性名** — `RootPropertyName = "items"` → 验证嵌套结构
    5. **空数据集** — 空数组 → `[]`
    6. **特殊字符** — Name 含 Unicode → 序列化后正确

  **Must NOT do**:
  - ❌ 不修改 JsonTarget.cs 源码

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 1 (with Task 1.4.1, 1.4.3)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Targets/JsonTarget.cs` — 被测试对象
  - `src/DataForge.Core/Core/Targets/IDataTarget.cs:52-57` — JsonExportOptions

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter "JsonTargetTests"` → 6 tests pass

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: JsonTarget exports valid JSON round-trip
    Tool: Bash (dotnet test)
    Steps:
      1. Export Person[] to temp JSON file
      2. Deserialize: JsonSerializer.Deserialize<List<Person>>(content)
      3. Assert deserialized count == original count, values equal
    Expected Result: Round-trip preserves all data
    Evidence: .sisyphus/evidence/task-1.4.2-roundtrip.txt

  Scenario: JsonTarget with RootPropertyName produces wrapped JSON
    Tool: Bash (dotnet test)
    Steps:
      1. Export with JsonExportOptions { RootPropertyName = "employees" }
      2. Parse as JsonDocument
      3. Assert root has property "employees" → array of 2 items
    Expected Result: JSON wrapped in named root property
    Evidence: .sisyphus/evidence/task-1.4.2-root-property.txt
  ```

  **Commit**: YES (merged into test batch)

---

- [ ] 1.4.3 新建 ConsoleTarget 输出测试

  **What to do**:
  - 新建 `tests/DataForge.Core.Tests/Targets/ConsoleTargetTests.cs`
  - 测试场景：
    1. **基础输出** — Person[] → Console 输出 → 验证 `Console.WriteLine` 被调用 N 次
    2. **格式化器** — 自定义 `Func<T, string>` → 验证输出格式
    3. **null 处理** — 值为 null → 输出空字符串（不抛异常）
    4. **空数据集** — 空数组 → 零次输出
    5. **ExportResults** — 验证返回值的 RecordsWritten 正确

  **实现提示**: 使用 `StringWriter` + `Console.SetOut()` 捕获控制台输出：
  ```csharp
  var sw = new StringWriter();
  var originalOut = Console.Out;
  Console.SetOut(sw);
  // ... export ...
  Console.SetOut(originalOut);
  var output = sw.ToString();
  ```

  **Must NOT do**:
  - ❌ 不修改 ConsoleTarget.cs 源码

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 1 (with Task 1.4.1, 1.4.2)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Targets/ConsoleTarget.cs` — 被测试对象
  - `tests/DataForge.Core.Tests/Sources/CsvSourceTests.cs:55-72` — 测试辅助模式参考

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter "ConsoleTargetTests"` → 5 tests pass

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: ConsoleTarget outputs formatted lines
    Tool: Bash (dotnet test)
    Steps:
      1. Redirect Console.Out to StringWriter
      2. Export Person[] { {Name="Alice", Age=30}, {Name="Bob", Age=25} }
      3. Capture output, split by newline
      4. Assert 2 non-empty lines, first contains "Alice"
    Expected Result: Output contains both Person strings
    Evidence: .sisyphus/evidence/task-1.4.3-basic-output.txt
  ```

  **Commit**: YES (merged into test batch)

---

- [ ] 1.4.4 新建管道错误处理测试

  **What to do**:
  - 新建 `tests/DataForge.Core.Tests/Pipeline/ErrorHandlingTests.cs`
  - 测试场景：
    1. **OnErrorContinue** — Select 中抛异常 → 后续项继续处理
    2. **OnErrorSkip** — Where 中抛异常 → 跳过该项
    3. **OnErrorStop** — 第一个异常 → 管道停止
    4. **OnError(自定义 handler)** — 返回 Continue/Skip/Stop/Throw
    5. **验证异常 ContinueOnValidationError** — 无效项跳过，有效项保留
    6. **验证异常 FailOnValidationError** — 无效项抛异常

  **Must NOT do**:
  - ❌ 不修改 DataPipeline.cs 中的错误处理逻辑（除非发现 Bug）

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: 错误处理涉及多路径逻辑推断，需要仔细设计测试数据
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 1 (with Task 1.4.5)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs:210-380` — 错误处理核心逻辑
  - `src/DataForge.Core/Core/Infrastructure/IErrorHandler.cs:1-17` — ErrorAction 枚举
  - `src/DataForge.Core/Core/Validation/IValidator.cs:13-81` — ValidationResult / ValidationException

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter "ErrorHandlingTests"` → 6 tests pass

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: OnErrorContinue skips failing items, continues processing
    Tool: Bash (dotnet test)
    Preconditions: Data [1, 2, 3], Select throws on item == 2
    Steps:
      1. Pipeline: FromMemory → OnErrorContinue → Select(x => x==2? throw: x*10) → ToListAsync
      2. Assert result == [10, 30] (item 2 skipped)
    Expected Result: Failing item 2 is skipped, others processed
    Evidence: .sisyphus/evidence/task-1.4.4-continue.txt

  Scenario: FailOnValidationError throws on first invalid item
    Tool: Bash (dotnet test)
    Preconditions: Data [valid, invalid, valid], validator rejects empty Name
    Steps:
      1. Pipeline: FromMemory → ValidateWith → FailOnValidationError → ToListAsync
      2. Assert ValidationException thrown, message contains error count
    Expected Result: ValidationException thrown (not silently continuing)
    Evidence: .sisyphus/evidence/task-1.4.4-failfast.txt
  ```

  **Commit**: YES (merged into test batch)

---

- [ ] 1.4.5 新建边缘条件测试

  **What to do**:
  - 新建 `tests/DataForge.Core.Tests/Pipeline/EdgeCaseTests.cs`
  - 测试场景：
    1. **空数据流** — 空 `IAsyncEnumerable` → 所有操作返回空结果
    2. **CancellationToken 取消** — 预先取消的 token → TaskCanceledException
    3. **超大 Take 值** — `Take(int.MaxValue)` → 不抛异常
    4. **负/零 Skip/Take** — `Skip(-1)` / `Take(0)` → 合理处理
    5. **Select 换类型后多次操作** — int → string → Where → ToList
    6. **DistinctBy 自定义键** — Person 按 City 去重
    7. **Batch 边界** — 记录数 = batchSize 倍数 / 余数 = 1
    8. **CSV 源读取空文件** — 空 CSV → 零条记录

  **Must NOT do**:
  - ❌ 不修改 DataPipeline.cs 的边界处理（除非发现 Bug）

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: 边界条件覆盖广，需系统性枚举并验证
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 1 (with Task 1.4.4)
  **Blocks**: None
  **Blocked By**: Task 1.1 (CSV 空文件测试依赖 CSV 解析器修复)

  **References**:
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs:575-604` — Skip/Take 内部实现
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs:560-572` — DistinctBy 实现
  - `src/DataForge.Core/Core/Sources/Implementations/CsvSource.cs` — CSV 读取

  **Acceptance Criteria**:
  - [ ] `dotnet test --filter "EdgeCaseTests"` → 8 tests pass

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Empty async enumerable produces empty result
    Tool: Bash (dotnet test)
    Steps:
      1. Create pipeline from empty enumerable
      2. Apply Where+Select chain
      3. ToListAsync → Assert result.Count == 0
    Expected Result: Empty list, no exceptions
    Evidence: .sisyphus/evidence/task-1.4.5-empty-stream.txt

  Scenario: Pre-cancelled token throws immediately
    Tool: Bash (dotnet test)
    Steps:
      1. var cts = new CancellationTokenSource()
      2. cts.Cancel()
      3. await pipeline.ToListAsync(cts.Token)
      4. Assert TaskCanceledException (or OperationCanceledException)
    Expected Result: OperationCanceledException thrown
     Evidence: .sisyphus/evidence/task-1.4.5-cancelled.txt
  ```

  **Commit**: YES (merged into test batch)

---

### Wave 2: 架构优化 + 性能（1-2周 — 详细拆解）

---

- [ ] 2.1a 提取 RelationalSource<T> 抽象基类

  **What to do**:
  
  **A) 新建 `src/DataForge.Core/Core/Sources/RelationalSource.cs`**:
  - 命名空间: `DataForge.Core.Core.Sources`
  - 类型: `public abstract class RelationalSource<T> : IRelationalDataSource<T> where T : new()`
  
  **B) 提取共享的 ReadAsync 逻辑**（从 SqlServerSource.cs:27-51 / MySqlSource.cs:27-51）:
  ```csharp
  public async IAsyncEnumerable<T> ReadAsync([EnumeratorCancellation] CancellationToken ct)
  {
      await using var connection = CreateConnection();
      await connection.OpenAsync(ct).ConfigureAwait(false);
      using var command = connection.CreateCommand();
      command.CommandText = $"SELECT * FROM {TableName}";
      using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
      
      var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
      while (await reader.ReadAsync(ct).ConfigureAwait(false))
      {
          ct.ThrowIfCancellationRequested();
          var item = new T();
          foreach (var prop in properties)
          {
              var ordinal = reader.GetOrdinal(prop.Name);
              if (!reader.IsDBNull(ordinal))
                  prop.SetValue(item, reader.GetValue(ordinal));
          }
          yield return item;
      }
  }
  ```

  **C) 提取共享的 QueryAsync 逻辑**（从 SqlServerSource.cs:53-82 / MySqlSource.cs:53-82）:
  - 带参数绑定的自定义 SQL 查询
  - `foreach (var prop in parameters.GetType().GetProperties(...))` 遍历参数绑定

  **D) 抽象成员定义**:
  - `protected abstract DbConnection CreateConnection();` — 子类提供具体连接
  - `protected abstract string TableName { get; }` — 表名
  - `public abstract string Name { get; }` — 显示名
  - `public abstract DataSourceType SourceType { get; }` — 源类型

  **Must NOT do**:
  - ❌ 不在核心库中引用 `Microsoft.Data.SqlClient` / `MySqlConnector`
  - ❌ 不使用 `System.Data.SqlClient`（已弃用），统一使用 `System.Data.Common.DbConnection`
  - ❌ 不在基类中硬编码 SQL 方言

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: 抽象基类提取涉及模板方法模式、3 个现有实现的差异分析、回退兼容性
  - **Skills**: `[]`

  **Parallelization**:
  - **Can Run In Parallel**: YES
  - **Parallel Group**: Wave 2 (with Task 2.1b)
  - **Blocks**: 2.1c, 2.1d, 2.1e
  - **Blocked By**: None

  **References** (CRITICAL):
  - `src/DataForge.Core.SqlServer/SqlServerSource.cs:27-82` — ReadAsync + QueryAsync 完整实现（模板来源）
  - `src/DataForge.Core.MySql/MySqlSource.cs:27-82` — MySql 版本（差异对比：仅 Connection 类型不同）
  - `src/DataForge.Core/Core/Sources/IDataSource.cs:18-21` — IRelationalDataSource<T> 接口定义
  - `src/DataForge.Core/Core/Infrastructure/DataSourceType.cs:3-13` — DataSourceType 枚举

  **Acceptance Criteria**:
  - [ ] RelationalSource.cs 编译通过，`dotnet build src/DataForge.Core/DataForge.Core.csproj` → PASS
  - [ ] 不依赖任何数据库驱动 NuGet 包（仅 `System.Data.Common`）
  - [ ] 抽象方法签名正确（子类可无缝继承）

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: RelationalSource abstraction compiles with zero external DB dependencies
    Tool: Bash (dotnet build)
    Preconditions: RelationalSource.cs written
    Steps:
      1. `dotnet build src/DataForge.Core/DataForge.Core.csproj` → PASS
      2. Verify DataForge.Core.csproj contains NO new PackageReference (especially no SqlClient/MySqlConnector)
    Expected Result: Core library builds clean, only System.Data.Common used
    Failure Indicators: Build error from missing DB types, or new PackageReference added
    Evidence: .sisyphus/evidence/task-2.1a-build.txt

  Scenario: All three existing DB extension projects still compile
    Tool: Bash (dotnet build)
    Preconditions: RelationalSource.cs in place
    Steps:
      1. `dotnet build src/DataForge.Core.SqlServer/DataForge.Core.SqlServer.csproj` → PASS
      2. `dotnet build src/DataForge.Core.MySql/DataForge.Core.MySql.csproj` → PASS
    Expected Result: No breaking changes to existing extensions
    Evidence: .sisyphus/evidence/task-2.1a-extensions.txt
  ```

  **Commit**: YES
  - Message: `refactor(db): extract RelationalSource<T> abstract base from duplicate SQL source implementations`
  - Files: `src/DataForge.Core/Core/Sources/RelationalSource.cs`

---

- [ ] 2.1b 提取 RelationalTarget<T> 抽象基类

  **What to do**:

  **A) 新建 `src/DataForge.Core/Core/Targets/RelationalTarget.cs`**:
  - 类型: `public abstract class RelationalTarget<T> : IDataTarget<T>`
  - 从 `SqlServerTarget.cs` 提取共享的批量 INSERT + 事务逻辑

  **B) 提取 ExportAsync 核心逻辑**（从 SqlServerTarget.cs:26-55）:
  ```csharp
  public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken ct)
  {
      var parts = destination.Split('|');
      var connectionString = parts[0];
      var tableName = parts.Length > 1 ? parts[1] : string.Empty;
      
      var sw = Stopwatch.StartNew();
      var count = 0;
      var batch = new List<T>(BatchSize);
      
      await foreach (var item in data.WithCancellation(ct))
      {
          batch.Add(item);
          if (batch.Count >= BatchSize)
          {
              await WriteBatchAsync(batch, connectionString, tableName, ct);
              count += batch.Count;
              batch.Clear();
          }
      }
      // ... 剩余批次处理 + 返回 ExportResults
  }
  ```

  **C) 提取 WriteBatchAsync — 参数化 INSERT**（从 SqlServerTarget.cs:57-90）:
  - `var columns = string.Join(", ", properties.Select(p => p.Name))`
  - `var valuePlaceholders = string.Join(", ", properties.Select((_, i) => $"@p{i}"))`
  - 事务：`if (UseTransaction) { transaction = await connection.BeginTransactionAsync(ct); }`

  **D) 抽象成员**:
  - `protected abstract DbConnection CreateConnection(string connectionString);`
  - `protected abstract int BatchSize { get; }` — 可被子类覆盖默认值
  - `protected abstract bool UseTransaction { get; }`

  **Must NOT do**:
  - ❌ 不在基类中写死特定数据库的参数前缀（`@` 是通用标准，其他方言另议）

  **Recommended Agent Profile**:
  - **Category**: `deep`
    - Reason: 同 2.1a，模板方法提取 + 事务边界处理需谨慎
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (with 2.1a)
  **Blocks**: 2.1c, 2.1d, 2.1e
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core.SqlServer/SqlServerTarget.cs:26-90` — ExportAsync + WriteBatchAsync 完整实现
  - `src/DataForge.Core/Core/Targets/IDataTarget.cs:9-16` — IDataTarget<T> 接口
  - `src/DataForge.Core/Core/Models/DataSourceMetadata.cs:18-31` — ExportResults 定义

  **Acceptance Criteria**:
  - [ ] `dotnet build` → PASS
  - [ ] 不依赖数据库驱动包

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: RelationalTarget abstraction compiles clean
    Tool: Bash (dotnet build)
    Steps:
      1. `dotnet build src/DataForge.Core/DataForge.Core.csproj` → PASS
      2. Verify no new PackageReference
    Expected Result: Clean build, zero DB driver dependencies
    Evidence: .sisyphus/evidence/task-2.1b-build.txt
  ```

  **Commit**: YES
  - Message: `refactor(db): extract RelationalTarget<T> abstract base from duplicate SQL target implementations`
  - Files: `src/DataForge.Core/Core/Targets/RelationalTarget.cs`

---

- [ ] 2.1c 重构 SqlServer 扩展使用新基类

  **What to do**:

  **A) 修改 `SqlServerSource<T>`**（当前 68 行 → 目标 ~18 行）:
  ```csharp
  public class SqlServerSource<T> : RelationalSource<T> where T : new()
  {
      private readonly string _connectionString;
      
      public SqlServerSource(string connectionString, string tableName)
      {
          _connectionString = connectionString;
          TableName = tableName;  // 赋值给基类属性
      }
      
      protected override string TableName { get; }
      public override string Name => $"SQL Server: {TableName}";
      public override DataSourceType SourceType => DataSourceType.SqlServer;
      
      protected override DbConnection CreateConnection()
          => new SqlConnection(_connectionString);
  }
  ```
  - 删除现有的 `ReadAsync`、`QueryAsync`、`ReadAllAsync` 实现（已在基类中）

  **B) 修改 `SqlServerTarget<T>`**（当前 ~90 行 → 目标 ~20 行）:
  ```csharp
  public class SqlServerTarget<T> : RelationalTarget<T>
  {
      public SqlServerTarget(SqlServerExportOptions? options = null)
      {
          BatchSize = options?.BatchSize ?? 1000;
          UseTransaction = options?.UseTransaction ?? false;
      }
      
      public override string Name => "SQL Server Target";
      public override DataTargetType TargetType => DataTargetType.SqlServer;
      
      protected override DbConnection CreateConnection(string connectionString)
          => new SqlConnection(connectionString);
  }
  ```
  - 删除现有的 `ExportAsync`、`WriteBatchAsync` 实现

  **Must NOT do**:
  - ❌ 不修改 `SqlServerPipelineExtensions.cs`（扩展方法保持不变）
  - ❌ 不删除 `SqlServerExportOptions.cs`

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 简单的继承重构，模式明确
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (with 2.1d, 2.1e)
  **Blocks**: None
  **Blocked By**: 2.1a, 2.1b

  **References**:
  - `src/DataForge.Core.SqlServer/SqlServerSource.cs` — 当前实现（对比删减目标）
  - `src/DataForge.Core.SqlServer/SqlServerTarget.cs` — 当前实现
  - `src/DataForge.Core.SqlServer/SqlServerPipelineExtensions.cs` — 不应修改

  **Acceptance Criteria**:
  - [ ] `dotnet build src/DataForge.Core.SqlServer/DataForge.Core.SqlServer.csproj` → PASS
  - [ ] SqlServerSource.cs 行数 < 25 行
  - [ ] SqlServerTarget.cs 行数 < 25 行
  - [ ] 现有测试全部通过（无回归）

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: SqlServer extension builds and existing tests pass after refactor
    Tool: Bash (dotnet build + test)
    Steps:
      1. `dotnet build src/DataForge.Core.SqlServer/DataForge.Core.SqlServer.csproj` → PASS
      2. `dotnet test DataForge.Core.sln` → all tests pass
    Expected Result: No regression, code reduced significantly
    Evidence: .sisyphus/evidence/task-2.1c-build-test.txt
  ```

  **Commit**: YES (与 2.1d, 2.1e 合并)
  - Message: `refactor(db): migrate SqlServer/MySql/Sqlite to use RelationalSource/RelationalTarget bases`
  - Files: `src/DataForge.Core.SqlServer/SqlServerSource.cs`, `src/DataForge.Core.SqlServer/SqlServerTarget.cs`
  - Pre-commit: `dotnet test`

---

- [ ] 2.1d 重构 MySql 扩展使用新基类

  **What to do**:
  - 同 2.1c 模式，将 `MySqlSource<T>` 和 `MySqlTarget<T>` 改为继承 `RelationalSource<T>` / `RelationalTarget<T>`
  - `CreateConnection()` → `return new MySqlConnection(_connectionString);`
  - 删除冗余实现，保留仅 ~18 行 / ~20 行

  **Must NOT do**: ❌ 不修改 `MySqlPipelineExtensions.cs`

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (with 2.1c, 2.1e)
  **Blocked By**: 2.1a, 2.1b

  **References**:
  - `src/DataForge.Core.MySql/MySqlSource.cs` — 当前实现
  - `src/DataForge.Core.MySql/MySqlTarget.cs` — 当前实现

  **Acceptance Criteria**:
  - [ ] `dotnet build src/DataForge.Core.MySql/DataForge.Core.MySql.csproj` → PASS
  - [ ] MySqlSource.cs < 25 行, MySqlTarget.cs < 25 行
  - [ ] `dotnet test` → all pass

  **QA Scenarios**:

  ```
  Scenario: MySql extension builds after refactor
    Tool: Bash (dotnet build + test)
    Steps:
      1. `dotnet build src/DataForge.Core.MySql/DataForge.Core.MySql.csproj` → PASS
      2. `dotnet test` → all pass
    Expected Result: Clean build, no regression
    Evidence: .sisyphus/evidence/task-2.1d-build-test.txt
  ```

  **Commit**: YES (merged with 2.1c, 2.1e)

---

- [ ] 2.1e 重构 Sqlite 扩展使用新基类

  **What to do**:
  - 如果 Sqlite 扩展尚未实现（仅 csproj 存在），则**新建** `SqliteSource.cs` 和 `SqliteTarget.cs`
  - 直接继承 `RelationalSource<T>` / `RelationalTarget<T>`
  - `CreateConnection()` → `return new SqliteConnection(...);`

  **Must NOT do**: ❌ 不修改核心库

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (with 2.1c, 2.1d)
  **Blocked By**: 2.1a, 2.1b

  **Acceptance Criteria**:
  - [ ] `dotnet build src/DataForge.Core.Sqlite/DataForge.Core.Sqlite.csproj` → PASS
  - [ ] SqliteSource.cs 和 SqliteTarget.cs 存在
  - [ ] `dotnet test` → all pass

  **QA Scenarios**:

  ```
  Scenario: Sqlite extension builds after implementation/refactor
    Tool: Bash (dotnet build)
    Steps:
      1. `dotnet build src/DataForge.Core.Sqlite/DataForge.Core.Sqlite.csproj` → PASS
    Expected Result: Clean build
    Evidence: .sisyphus/evidence/task-2.1e-build.txt
  ```

  **Commit**: YES (merged with 2.1c, 2.1d)

---

- [ ] 2.2 引入代码分析工具

  **What to do**:

  **A) 新建 `DataForge.Core/Directory.Build.props`**（位于解决方案根目录，影响所有项目）:
  ```xml
  <Project>
    <PropertyGroup>
      <TargetFramework>net8.0</TargetFramework>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <AnalysisLevel>latest-recommended</AnalysisLevel>
      <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
      <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    </PropertyGroup>
  </Project>
  ```

  **B) 新建 `DataForge.Core/.editorconfig`**:
  ```ini
  [*.cs]
  # 可空性检查
  dotnet_diagnostic.CA1062.severity = suggestion  # 参数空检查（建议级，避免阻塞现有代码）
  dotnet_diagnostic.CA2007.severity = suggestion  # ConfigureAwait
  
  # 代码风格
  csharp_style_var_elsewhere = true:suggestion
  csharp_style_var_for_built_in_types = true:suggestion
  csharp_style_prefer_auto_properties = true:suggestion
  
  # 命名规范
  dotnet_naming_rule.interface_should_be_prefixed.severity = suggestion
  ```

  **C) 清理各 `.csproj`**（从各项目删除 `Directory.Build.props` 已覆盖的重复属性）:
  - 每个 `.csproj` 查找 `<TargetFramework>`, `<Nullable>`, `<ImplicitUsings>` → 删除（已在 Build.props 中）
  - 保留项目特有的 `<PackageId>`, `<PackageVersion>` 等

  **D) 运行编译检查**:
  - `dotnet build DataForge.Core.sln` → 如果有分析器警告，记录但不阻塞

  **Must NOT do**:
  - ❌ 不在核心库中引用 `StyleCop.Analyzers`（太严格，会产生大量现有代码警告）
  - ❌ 不将警告设置为 error 级别
  - ❌ 不修改公开 API 签名

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 标准配置文件编写，无复杂逻辑
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (独立任务)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/DataForge.Core.csproj:1-6` — 当前重复属性示例
  - 所有 7 个 `.csproj` 文件 — 需要清理

  **Acceptance Criteria**:
  - [ ] `Directory.Build.props` 存在且语法正确
  - [ ] `.editorconfig` 存在
  - [ ] `dotnet build DataForge.Core.sln` → PASS（无阻塞性错误）
  - [ ] 各 `.csproj` 中无 `<TargetFramework>` 重复定义

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Full solution builds with analyzers enabled
    Tool: Bash (dotnet build)
    Steps:
      1. `dotnet build DataForge.Core/DataForge.Core.sln` → PASS
      2. Check exit code = 0
    Expected Result: Clean build, zero errors
    Failure Indicators: Build fails due to missing TargetFramework, or analyzer errors
    Evidence: .sisyphus/evidence/task-2.2-build.txt

  Scenario: All projects use centralized properties
    Tool: Bash (grep)
    Steps:
      1. `Select-String -Path "src/**/*.csproj" -Pattern "<TargetFramework>"` → should return 0 matches (all removed)
      2. Or: verify Directory.Build.props contains `<TargetFramework>net8.0</TargetFramework>`
    Expected Result: No individual csproj defines TargetFramework
    Evidence: .sisyphus/evidence/task-2.2-centralized.txt
  ```

  **Commit**: YES
  - Message: `build: add Directory.Build.props and .editorconfig with Roslyn analyzers`
  - Files: `DataForge.Core/Directory.Build.props`, `DataForge.Core/.editorconfig`, may modify 7 `.csproj` files

---

- [ ] 2.3 添加 BenchmarkDotNet 性能基准项目

  **What to do**:

  **A) 新建 `DataForge.Core/perf/DataForge.Core.Benchmarks/DataForge.Core.Benchmarks.csproj`**:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <OutputType>Exe</OutputType>
      <TargetFramework>net8.0</TargetFramework>
      <ImplicitUsings>enable</ImplicitUsings>
      <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
      <PackageReference Include="BenchmarkDotNet" Version="0.14.0" />
      <ProjectReference Include="..\..\src\DataForge.Core\DataForge.Core.csproj" />
    </ItemGroup>
  </Project>
  ```

  **B) 新建 `PipelineBenchmarks.cs`**:
  - 测试数据：100K 条 `Order` 记录（PreSetup 中生成）
  - 基准场景：
    1. `WhereSelectToList` — `.Where().Select().ToListAsync()`
    2. `OrderByToList` — `.OrderBy().ToListAsync()`
    3. `DistinctToList` — `.Distinct().ToListAsync()`
    4. `ChainOfFive` — `.Where().Select().OrderBy().Skip().Take().ToListAsync()`
    5. `NoOpToList` — 纯 `.ToListAsync()`（基准对照）

  **C) 新建 `CsvSourceBenchmarks.cs`**:
  - 生成 10K/100K 行临时 CSV
  - 基准：`ReadAndCollect` — CsvSource → ToList

  **D) 添加 `README.md`**: 说明如何运行：`dotnet run -c Release -- --filter *`

  **Must NOT do**:
  - ❌ 不在核心库中引用 BenchmarkDotNet
  - ❌ Benchmark 项目不参与 CI 自动化测试（手动运行）

  **Recommended Agent Profile**:
  - **Category**: `unspecified-high`
    - Reason: 需要 BenchmarkDotNet 知识 + 数据生成逻辑 + 结果验证
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (独立任务)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs` — 管道操作实现
  - `src/DataForge.Core/Core/Sources/Implementations/MemorySource.cs` — 数据生成参考
  - BenchmarkDotNet 官方文档: `https://benchmarkdotnet.org/articles/overview.html`

  **Acceptance Criteria**:
  - [ ] `dotnet build perf/DataForge.Core.Benchmarks/DataForge.Core.Benchmarks.csproj -c Release` → PASS
  - [ ] `dotnet run -c Release --project perf/DataForge.Core.Benchmarks -- --filter * --job short` → 输出 Markdown 表格
  - [ ] 至少 5 个 Pipeline 基准 + 1 个 CSV 基准

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Benchmark project builds and runs successfully
    Tool: Bash (dotnet build + run)
    Preconditions: Benchmarks.csproj and .cs files written
    Steps:
      1. `dotnet build perf/DataForge.Core.Benchmarks/DataForge.Core.Benchmarks.csproj -c Release` → PASS
      2. `dotnet run --project perf/DataForge.Core.Benchmarks/DataForge.Core.Benchmarks.csproj -c Release -- --filter * --job short --exporters markdown`
      3. Check exit code = 0
      4. Verify output directory contains .md result files
    Expected Result: Benchmark produces result table without runtime errors
    Failure Indicators: Build error, runtime crash, or empty results
    Evidence: .sisyphus/evidence/task-2.3-benchmark-run.txt
  ```

  **Commit**: YES
  - Message: `perf: add BenchmarkDotNet project with pipeline and source benchmarks`
  - Files: `perf/DataForge.Core.Benchmarks/`

---

- [ ] 2.4 内存优化注释 + 预留接口

  **What to do**:

  **A) 在 `DataPipeline.cs` 中的 `OrderByInternal` 方法上方添加文档注释**（行 524 前）:
  ```csharp
  /// <summary>
  /// Sorts items by the specified key selector.
  /// </summary>
  /// <remarks>
  /// <para>Current implementation loads ALL data into memory before sorting.
  /// Suitable for small to medium datasets (< 100K records).</para>
  /// <para>For very large datasets, consider using an external sort or
  /// database-level ORDER BY instead.</para>
  /// <para>Future version may add <c>OrderByStreaming</c> with external merge sort support.</para>
  /// </remarks>
  ```

  **B) 在 `GroupedDataPipeline.cs` 中的 `GetGroupsAsync` 方法上方添加注释**（行 52 前）:
  ```csharp
  /// <remarks>
  /// Current implementation accumulates all items into memory before grouping.
  /// For streaming group-by on large datasets (> 500K records), consider
  /// using hash-partitioned grouping or database-level GROUP BY.
  /// </remarks>
  ```

  **C) 在 `IDataPipeline.cs` 中的 `OrderBy` 方法添加接口级注释**（行 28 前）:
  ```csharp
  /// <summary>Sorts items in ascending order by the specified key.</summary>
  /// <remarks>This operation buffers all data in memory. For large datasets, prefer pre-sorted sources.</remarks>
  ```

  **Must NOT do**:
  - ❌ 不修改任何实现逻辑（纯注释）
  - ❌ 不在注释中给出绝对的数字建议（"<100K" 是估算，环境不同）

  **Recommended Agent Profile**:
  - **Category**: `quick`
    - Reason: 纯文档注释，无代码逻辑变更
  - **Skills**: `[]`

  **Parallelization**: YES — Wave 2 (独立任务)
  **Blocks**: None
  **Blocked By**: None

  **References**:
  - `src/DataForge.Core/Core/Pipeline/DataPipeline.cs:524-544` — OrderByInternal 方法
  - `src/DataForge.Core/Core/Pipeline/GroupedDataPipeline.cs:52-60` — GetGroupsAsync 方法
  - `src/DataForge.Core/Core/Pipeline/IDataPipeline.cs:28-29` — OrderBy 接口声明

  **Acceptance Criteria**:
  - [ ] `dotnet build` → PASS（XML 注释不影响编译）
  - [ ] 注释内容准确描述了内存使用限制
  - [ ] 注释不包含模糊或错误的性能声明

  **QA Scenarios (MANDATORY)**:

  ```
  Scenario: Build passes with XML doc comments, no warnings generated
    Tool: Bash (dotnet build)
    Steps:
      1. `dotnet build src/DataForge.Core/DataForge.Core.csproj` → PASS
      2. Check for no CS1591 warnings (missing XML comments — we only add to specific methods)
    Expected Result: Clean build with docs
    Evidence: .sisyphus/evidence/task-2.4-build.txt
  ```

  **Commit**: YES
  - Message: `docs: add memory usage notes to OrderBy/GroupBy implementations`
  - Files: `src/DataForge.Core/Core/Pipeline/DataPipeline.cs`, `src/DataForge.Core/Core/Pipeline/GroupedDataPipeline.cs`, `src/DataForge.Core/Core/Pipeline/IDataPipeline.cs`



---

### Wave 3: 长期战略（1-3个月）

---

- [ ] 3.1 文档国际化框架

  **What to do**: 新建 `docs/en/` + `docs/zh-CN/`。先翻译 `getting-started.md`（196 行最短）。README.md 添加语言切换链接。

  **Category**: `writing` | **Parallel**: YES | **Commit**: YES

---

- [ ] 3.2a 高级特性 — 并行管道原型

  **What to do**: `IDataPipeline<T>` 添加 `AsParallel(int maxDop)` 方法，`[Experimental]` 标记。内部使用 `Parallel.ForEachAsync`。

  **Category**: `deep` | **Parallel**: YES (with 3.2b) | **Commit**: YES

---

- [ ] 3.2b 高级特性 — LINQ to SQL 原型

  **What to do**: SQL 扩展包添加 `Where(Expression<Func<T, bool>>)` 重载，表达式树 → SQL WHERE 翻译。

  **Category**: `deep` | **Parallel**: YES | **Commit**: YES

---

## Final Verification Wave (MANDATORY — after EACH Wave)

> 每个 Wave 完成后运行，4 个审查代理并行执行。

- [ ] F1. **Plan Compliance Audit** — `oracle`
  验证 Wave 内所有 Must Have 已实现、Must NOT Have 未违反、evidence 文件存在。
  Output: `Must Have [N/N] | Must NOT Have [N/N] | Tasks [N/N] | VERDICT: APPROVE/REJECT`

- [ ] F2. **Code Quality Review** — `unspecified-high`
  运行 `dotnet build` + `dotnet test`。审查 `as any`/`@ts-ignore`、空 catch、注释代码、AI slop。
  Output: `Build [PASS/FAIL] | Tests [N pass/N fail] | VERDICT`

- [ ] F3. **Real Manual QA** — `unspecified-high`
  从干净状态执行所有 QA 场景。验证跨任务集成。测试边界条件。
  Evidence: `.sisyphus/evidence/final-qa/`

- [ ] F4. **Scope Fidelity Check** — `deep`
  对比 diff 与计划：无遗漏、无范围蔓延。检查交叉污染。
  Output: `Tasks [N/N compliant] | Contamination [CLEAN/N issues] | VERDICT`

---

## Commit Strategy

| Wave | Commit | Files |
|------|--------|-------|
| 1.1 | `fix(csv): rewrite CSV parser with RFC 4180 support for multiline fields and escaped quotes` | `CsvSource.cs` |
| 1.2 | `fix(memory): prevent multiple enumeration in GetMetadataAsync by adding size caching` | `MemorySource.cs` |
| 1.3 | `ci: add GitHub Actions CI and NuGet publish workflows` | `ci.yml`, `publish.yml` |
| 1.4 | `test: add CsvTarget, JsonTarget, ConsoleTarget, ErrorHandling, EdgeCase tests` | 新测试文件 ×5 |
| 2.1a-b | `refactor(db): extract RelationalSource<T> and RelationalTarget<T> abstract bases` | `RelationalSource.cs`, `RelationalTarget.cs` |
| 2.1c-e | `refactor(db): migrate SqlServer/MySql/Sqlite to use abstract bases` | 各扩展包源文件 |
| 2.2 | `build: add Directory.Build.props and .editorconfig with Roslyn analyzers` | 配置文件 |
| 2.3 | `perf: add BenchmarkDotNet project with pipeline and source benchmarks` | `perf/` 目录 |
| 2.4 | `docs: add memory usage notes to OrderBy/GroupBy implementations` | `DataPipeline.cs`, `GroupedDataPipeline.cs` |
| 3.1 | `docs: add English version of getting-started guide` | `docs/en/` |
| 3.2 | `feat(pipeline): add experimental AsParallel and LINQ to SQL support` | 接口+实现 |
| 3.3 | `chore: add issue templates and prepare v0.2.0 release` | 模板+配置 |
| 3.4 | `feat(excel): implement ClosedXML-based ExcelSource and ExcelTarget` | Excel 扩展包 |

---

## Success Criteria

### Wave 1 Verification Commands
```bash
# 编译 + 全量测试
dotnet build DataForge.Core/DataForge.Core.sln
# Expected: BUILD SUCCEEDED (0 errors, 0 warnings)

dotnet test DataForge.Core/DataForge.Core.sln
# Expected: All ~90 tests pass (68 existing + 22+ new)

# CSV 特定测试
dotnet test --filter "CsvSourceAdvancedTests"
# Expected: 5 tests pass (quoted delimiter, multiline, escaped quotes, empty, comments)

dotnet test --filter "CsvTargetTests"
# Expected: 7 tests pass

dotnet test --filter "JsonTargetTests"
# Expected: 6 tests pass

dotnet test --filter "ErrorHandlingTests"
# Expected: 6 tests pass
```

### Wave 2 Verification Commands
```bash
# 编译（含分析器）
dotnet build DataForge.Core/DataForge.Core.sln -warnaserror
# Expected: BUILD SUCCEEDED (0 errors)

dotnet test DataForge.Core/DataForge.Core.sln
# Expected: All tests pass (无回归)

# 基准测试
dotnet run --project perf/DataForge.Core.Benchmarks -c Release -- --filter * --job short
# Expected: Outputs Markdown benchmark table
```

### Wave 3 Verification Commands
```bash
# 文档存在
Test-Path docs/en/getting-started.md
# Expected: True

# Excel 扩展编译
dotnet build src/DataForge.Core.Excel/DataForge.Core.Excel.csproj
# Expected: BUILD SUCCEEDED

# NuGet 包生成
dotnet pack src/DataForge.Core/DataForge.Core.csproj -c Release -o nupkg/
# Expected: .nupkg file created
```

### Final Checklist
- [ ] All P0 correctness bugs fixed (CSV + MemorySource)
- [ ] CI/CD green on GitHub Actions
- [ ] Test coverage expanded from 6→11 files, ~68→90+ tests
- [ ] No new NuGet dependencies in core library
- [ ] All Must HAVE met
- [ ] All Must NOT HAVE absent
- [ ] Wave FINAL all VERDICT: APPROVE

- [ ] 3.3 社区建设 + NuGet 发布

  **What to do**: 填充 ISSUE_TEMPLATE 模板、添加 GitHub Topics、验证 NuGet 推送、发布 v0.2.0。

  **Category**: `quick` | **Parallel**: YES | **Commit**: YES

---

- [ ] 3.4 Excel 源 ClosedXML 完整实现

  **What to do**: `DataForge.Core.Excel` 包中用 ClosedXML 实现真正的 `.xlsx` 解析。支持多 Sheet。核心库中 `ExcelSource.cs` 标记 `[Obsolete]`。

  **Category**: `deep` | **Parallel**: YES | **Commit**: YES



