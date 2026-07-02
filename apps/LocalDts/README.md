# LocalDts - 自实现数据迁移工具

## 项目介绍

LocalDts是一个基于.NET的开源数据迁移工具，支持从多种数据源（如SQLite、MySQL、SQL Server、Excel、CSV）迁移数据到不同的目标源。该工具采用插件架构，具有高度的可扩展性和灵活性。

## 核心功能

- **多数据源支持**：支持SQLite、MySQL、SQL Server、Excel、CSV、Redis、MongoDB、Kafka、Azure Blob Storage等多种数据源
- **插件架构**：采用插件式设计，可轻松扩展新的数据源、目标源和转换器
- **并行处理**：优化的并行数据转换，提高迁移性能，支持自定义并行度
- **批量处理**：支持批量数据加载，减少数据库操作次数
- **错误处理**：增强的错误处理和重试机制，提供详细的错误信息和建议解决方案
- **内存优化**：使用DataRecordPool优化内存使用，减少垃圾回收
- **日志记录**：完善的日志记录功能，支持详细的错误日志和操作日志
- **WPF界面**：提供直观的图形界面，支持实时进度显示和任务监控
- **命令行支持**：提供命令行接口，支持自动化脚本执行
- **SQLite工具**：提供SQLiteHelper类，简化SQLite数据库操作
- **插件管理**：改进的插件加载机制，支持插件版本控制和依赖管理

## 项目结构

- **DataMigration.Contracts**：核心接口和数据结构定义
- **DataMigration.Core**：核心业务逻辑，包括插件管理和迁移引擎
- **DataMigration.Wpf**：WPF图形界面
- **DataMigration.Console**：命令行界面
- **DatabaseCreator**：数据库创建工具，用于生成测试数据
- **Plugins**：插件目录，包含各种数据源、目标源和转换器插件
- **DataMigration.Tests**：单元测试和集成测试

## 安装指南

### 前提条件

- .NET 8.0 或更高版本
- Visual Studio 2022 或更高版本（开发环境）

### 安装步骤

1. 克隆代码库
   ```bash
   git clone <repository-url>
   ```

2. 打开解决方案文件
   ```
   DataMigration.slnx
   ```

3. 还原依赖项
   ```bash
   dotnet restore
   ```

4. 构建项目
   ```bash
   dotnet build
   ```

## 使用方法

### 使用WPF界面

1. 运行DataMigration.Wpf项目
2. 在界面中配置数据源、转换器和目标源
3. 点击"执行"按钮开始数据迁移
4. 查看实时进度显示和任务状态
5. 查看详细的执行日志和错误信息
6. 如有需要，点击"停止"按钮终止任务

### 使用命令行

1. 运行DataMigration.Console项目
2. 按照提示输入迁移配置
3. 等待迁移完成

## 数据库帮助类使用指南

### 概述

DataMigration.Core提供了一系列数据库帮助类，用于简化各种数据库操作，包括SQLite、MySQL、SQL Server、Excel和CSV。这些帮助类提供了统一的接口，使得不同数据库的操作变得简单一致。

### 主要功能

- **连接管理**：测试数据库连接是否成功
- **表结构获取**：获取数据库中的表名和表结构
- **数据预览**：预览表中的数据
- **SQL查询**：执行自定义SQL查询
- **连接字符串构建**：构建标准的数据库连接字符串
- **文件管理**：检查数据库文件是否存在
- **表存在性检查**：检查表是否存在于数据库中

### 支持的数据库类型

- **SQLite**：轻量级文件数据库
- **MySQL**：关系型数据库
- **SQL Server**：关系型数据库
- **Excel**：电子表格文件
- **CSV**：逗号分隔值文件

### 使用示例

#### SQLiteHelper

```csharp
using DataMigration.Core;

// 测试连接
string connectionString = SQLiteHelper.BuildConnectionString("path/to/database.db");
bool isConnected = await SQLiteHelper.TestConnectionAsync(connectionString);

// 获取表列表
List<string> tables = await SQLiteHelper.GetTablesAsync(connectionString);

// 获取表结构
List<TableColumnInfo> columns = await SQLiteHelper.GetTableStructureAsync(connectionString, "TableName");

// 预览数据
List<Dictionary<string, object?>> data = await SQLiteHelper.PreviewDataAsync(connectionString, "TableName", 10);
```

#### MySQLHelper

```csharp
using DataMigration.Core;

// 构建连接字符串
var parameters = new Dictionary<string, string>
{
    { "Server", "localhost" },
    { "Port", "3306" },
    { "Database", "test" },
    { "UserID", "root" },
    { "Password", "password" }
};
string connectionString = new MySQLHelper().BuildConnectionString(parameters);

// 测试连接
bool isConnected = await new MySQLHelper().TestConnectionAsync(connectionString);

// 获取表列表
List<string> tables = await new MySQLHelper().GetTablesAsync(connectionString);

// 获取表结构
List<TableColumnInfo> columns = await new MySQLHelper().GetTableStructureAsync(connectionString, "TableName");

// 预览数据
List<Dictionary<string, object?>> data = await new MySQLHelper().PreviewDataAsync(connectionString, "TableName", 10);
```

#### SqlServerHelper

```csharp
using DataMigration.Core;

// 构建连接字符串
var parameters = new Dictionary<string, string>
{
    { "Server", "localhost" },
    { "Database", "test" },
    { "UserID", "sa" },
    { "Password", "password" }
};
string connectionString = new SqlServerHelper().BuildConnectionString(parameters);

// 测试连接
bool isConnected = await new SqlServerHelper().TestConnectionAsync(connectionString);

// 获取表列表
List<string> tables = await new SqlServerHelper().GetTablesAsync(connectionString);

// 获取表结构
List<TableColumnInfo> columns = await new SqlServerHelper().GetTableStructureAsync(connectionString, "TableName");

// 预览数据
List<Dictionary<string, object?>> data = await new SqlServerHelper().PreviewDataAsync(connectionString, "TableName", 10);
```

#### ExcelHelper

```csharp
using DataMigration.Core;

// 构建连接字符串（文件路径）
var parameters = new Dictionary<string, string>
{
    { "FilePath", "path/to/file.xlsx" }
};
string connectionString = new ExcelHelper().BuildConnectionString(parameters);

// 测试连接
bool isConnected = await new ExcelHelper().TestConnectionAsync(connectionString);

// 获取工作表列表
List<string> sheets = await new ExcelHelper().GetTablesAsync(connectionString);

// 获取表结构
List<TableColumnInfo> columns = await new ExcelHelper().GetTableStructureAsync(connectionString, "Sheet1");

// 预览数据
List<Dictionary<string, object?>> data = await new ExcelHelper().PreviewDataAsync(connectionString, "Sheet1", 10);
```

#### CsvHelper

```csharp
using DataMigration.Core;

// 构建连接字符串（文件路径）
var parameters = new Dictionary<string, string>
{
    { "FilePath", "path/to/file.csv" }
};
string connectionString = new CsvHelper().BuildConnectionString(parameters);

// 测试连接
bool isConnected = await new CsvHelper().TestConnectionAsync(connectionString);

// 获取表列表（CSV文件视为一个表）
List<string> tables = await new CsvHelper().GetTablesAsync(connectionString);

// 获取表结构
List<TableColumnInfo> columns = await new CsvHelper().GetTableStructureAsync(connectionString, tables[0]);

// 预览数据
List<Dictionary<string, object?>> data = await new CsvHelper().PreviewDataAsync(connectionString, tables[0], 10);
```

## 插件开发

### 插件结构

每个插件应该包含以下基本结构：

```
MyPlugin/
├── MyPlugin.csproj
└── MyPlugin.cs
```

### 创建数据源插件

1. 创建一个新的类库项目
2. 引用DataMigration.Contracts
3. 实现IDataSource接口
4. 确保实现以下方法：
   - `Id`：插件唯一标识符
   - `Name`：插件名称
   - `Version`：插件版本
   - `InitializeAsync`：初始化插件
   - `ExtractAsync`：提取数据
   - `ShutdownAsync`：关闭插件
5. 将编译后的DLL放入Plugins目录

### 创建目标源插件

1. 创建一个新的类库项目
2. 引用DataMigration.Contracts
3. 实现IDataTarget接口（或IBatchDataTarget接口以支持批处理）
4. 确保实现以下方法：
   - `Id`：插件唯一标识符
   - `Name`：插件名称
   - `Version`：插件版本
   - `InitializeAsync`：初始化插件
   - `LoadAsync`：加载数据
   - `ShutdownAsync`：关闭插件
5. 将编译后的DLL放入Plugins目录

### 创建转换器插件

1. 创建一个新的类库项目
2. 引用DataMigration.Contracts
3. 实现ITransformer接口（或IParallelTransformer接口以支持并行处理）
4. 确保实现以下方法：
   - `Id`：插件唯一标识符
   - `Name`：插件名称
   - `Version`：插件版本
   - `InitializeAsync`：初始化插件
   - `TransformAsync`：转换数据
   - `ShutdownAsync`：关闭插件
5. 将编译后的DLL放入Plugins目录

### 插件配置验证

插件应该实现配置验证逻辑，确保用户提供的配置是有效的。可以在InitializeAsync方法中进行配置验证，并在配置无效时抛出ConfigurationException异常。

### 插件依赖管理

如果插件依赖于其他插件或库，应该在插件中明确声明这些依赖关系，以确保插件能够正确加载和运行。

## 测试

运行测试项目
```bash
dotnet test
```

## 贡献

欢迎提交Issue和Pull Request，共同改进项目。

## 许可证

MIT License

