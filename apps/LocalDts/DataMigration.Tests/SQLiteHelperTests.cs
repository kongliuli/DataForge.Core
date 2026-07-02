using DataMigration.Core;
using System.IO;
using System.Threading.Tasks;
using System;

namespace DataMigration.Tests;

public class SQLiteHelperTests
{
    private string _testDbPath;

    public SQLiteHelperTests()
    {
        // 创建临时测试数据库文件路径
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.db");
        
        // 创建测试数据库并初始化表结构
        CreateTestDatabase();
    }

    ~SQLiteHelperTests()
    {
        // 清理临时数据库文件
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    private void CreateTestDatabase()
    {
        using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={_testDbPath};Version=3;");
        connection.Open();
        
        // 创建测试表
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS TestTable (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                Age INTEGER
            );
            
            INSERT INTO TestTable (Id, Name, Age) VALUES (1, 'Test1', 20);
            INSERT INTO TestTable (Id, Name, Age) VALUES (2, 'Test2', 30);
            INSERT INTO TestTable (Id, Name, Age) VALUES (3, 'Test3', 25);
        ";
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnTrueForValidConnection()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);

        // Act
        var result = await SQLiteHelper.TestConnectionAsync(connectionString);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldReturnFalseForInvalidConnection()
    {
        // Arrange
        var invalidConnectionString = "Data Source=invalid_path.db;Version=3;";

        // Act
        var result = await SQLiteHelper.TestConnectionAsync(invalidConnectionString);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetTablesAsync_ShouldReturnListOfTables()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);

        // Act
        var tables = await SQLiteHelper.GetTablesAsync(connectionString);

        // Assert
        Assert.Contains("TestTable", tables);
    }

    [Fact]
    public async Task GetTableStructureAsync_ShouldReturnTableColumns()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);

        // Act
        var columns = await SQLiteHelper.GetTableStructureAsync(connectionString, "TestTable");

        // Assert
        Assert.Contains(columns, c => c.Name == "Id" && c.Type == "INTEGER");
        Assert.Contains(columns, c => c.Name == "Name" && c.Type == "TEXT");
        Assert.Contains(columns, c => c.Name == "Age" && c.Type == "INTEGER");
    }

    [Fact]
    public async Task PreviewDataAsync_ShouldReturnTableData()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);

        // Act
        var data = await SQLiteHelper.PreviewDataAsync(connectionString, "TestTable", 2);

        // Assert
        Assert.Equal(2, data.Count);
        Assert.Contains(data, row => row["Id"]?.ToString() == "1" && row["Name"]?.ToString() == "Test1");
        Assert.Contains(data, row => row["Id"]?.ToString() == "2" && row["Name"]?.ToString() == "Test2");
    }

    [Fact]
    public async Task ExecuteQueryAsync_ShouldReturnQueryResults()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);
        var query = "SELECT * FROM TestTable WHERE Age > 25";

        // Act
        var results = await SQLiteHelper.ExecuteQueryAsync(connectionString, query);

        // Assert
        Assert.Single(results);
        Assert.Equal("2", results[0]["Id"]?.ToString());
        Assert.Equal("Test2", results[0]["Name"]?.ToString());
        Assert.Equal("30", results[0]["Age"]?.ToString());
    }

    [Fact]
    public void BuildConnectionString_ShouldReturnCorrectConnectionString()
    {
        // Arrange
        var dbPath = "test.db";

        // Act
        var connectionString = SQLiteHelper.BuildConnectionString(dbPath);

        // Assert
        Assert.Equal("Data Source=test.db;Version=3;", connectionString);
    }

    [Fact]
    public async Task TableExistsAsync_ShouldReturnTrueForExistingTable()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);

        // Act
        var result = await SQLiteHelper.TableExistsAsync(connectionString, "TestTable");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TableExistsAsync_ShouldReturnFalseForNonExistingTable()
    {
        // Arrange
        var connectionString = SQLiteHelper.BuildConnectionString(_testDbPath);

        // Act
        var result = await SQLiteHelper.TableExistsAsync(connectionString, "NonExistingTable");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DatabaseFileExists_ShouldReturnTrueForExistingFile()
    {
        // Arrange
        // _testDbPath 已在 Setup 中创建

        // Act
        var result = SQLiteHelper.DatabaseFileExists(_testDbPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DatabaseFileExists_ShouldReturnFalseForNonExistingFile()
    {
        // Arrange
        var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.db");

        // Act
        var result = SQLiteHelper.DatabaseFileExists(nonExistingPath);

        // Assert
        Assert.False(result);
    }
}