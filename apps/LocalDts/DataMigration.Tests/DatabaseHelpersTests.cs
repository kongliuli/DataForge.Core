using DataMigration.Core;
using CsvHelperClass = DataMigration.Core.CsvHelper;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataMigration.Tests;

public class DatabaseHelpersTests
{
    // MySQLHelper 测试
    public class MySQLHelperTests
    {
        [Fact]
        public async Task TestConnectionAsync_ShouldReturnFalseForInvalidConnection()
        {
            // Arrange
            var connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;";

            // Act
            var result = await new MySQLHelper().TestConnectionAsync(connectionString);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetTablesAsync_ShouldReturnEmptyListForInvalidConnection()
        {
            // Arrange
            var connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;";

            // Act
            var tables = await new MySQLHelper().GetTablesAsync(connectionString);

            // Assert
            Assert.Empty(tables);
        }

        [Fact]
        public async Task TableExistsAsync_ShouldReturnFalseForInvalidConnection()
        {
            // Arrange
            var connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;";

            // Act
            var result = await new MySQLHelper().TableExistsAsync(connectionString, "test_table");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void BuildConnectionString_ShouldReturnCorrectConnectionString()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "Server", "localhost" },
                { "Port", "3306" },
                { "Database", "test" },
                { "UserID", "root" },
                { "Password", "password" },
                { "SSLMode", "None" }
            };

            // Act
            var connectionString = new MySQLHelper().BuildConnectionString(parameters);

            // Assert
            Assert.Contains("Server=localhost", connectionString);
            Assert.Contains("Port=3306", connectionString);
            Assert.Contains("Database=test", connectionString);
            Assert.Contains("User ID=root", connectionString);
            Assert.Contains("Password=password", connectionString);
        }
    }

    // SqlServerHelper 测试
    public class SqlServerHelperTests
    {
        [Fact]
        public async Task TestConnectionAsync_ShouldReturnFalseForInvalidConnection()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;";

            // Act
            var result = await new SqlServerHelper().TestConnectionAsync(connectionString);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetTablesAsync_ShouldReturnEmptyListForInvalidConnection()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;";

            // Act
            var tables = await new SqlServerHelper().GetTablesAsync(connectionString);

            // Assert
            Assert.Empty(tables);
        }

        [Fact]
        public async Task TableExistsAsync_ShouldReturnFalseForInvalidConnection()
        {
            // Arrange
            var connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;";

            // Act
            var result = await new SqlServerHelper().TableExistsAsync(connectionString, "test_table");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void BuildConnectionString_ShouldReturnCorrectConnectionString()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "Server", "localhost" },
                { "Database", "test" },
                { "UserID", "sa" },
                { "Password", "password" },
                { "IntegratedSecurity", "false" },
                { "TrustServerCertificate", "true" }
            };

            // Act
            var connectionString = new SqlServerHelper().BuildConnectionString(parameters);

            // Assert
            Assert.Contains("Data Source=localhost", connectionString);
            Assert.Contains("Initial Catalog=test", connectionString);
            Assert.Contains("User ID=sa", connectionString);
            Assert.Contains("Password=password", connectionString);
        }
    }

    // ExcelHelper 测试
    public class ExcelHelperTests
    {
        [Fact]
        public async Task TestConnectionAsync_ShouldReturnFalseForNonExistingFile()
        {
            // Arrange
            var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.xlsx");

            // Act
            var result = await new ExcelHelper().TestConnectionAsync(nonExistingPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetTablesAsync_ShouldReturnEmptyListForNonExistingFile()
        {
            // Arrange
            var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.xlsx");

            // Act
            var tables = await new ExcelHelper().GetTablesAsync(nonExistingPath);

            // Assert
            Assert.Empty(tables);
        }

        [Fact]
        public async Task TableExistsAsync_ShouldReturnFalseForNonExistingFile()
        {
            // Arrange
            var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.xlsx");

            // Act
            var result = await new ExcelHelper().TableExistsAsync(nonExistingPath, "Sheet1");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void BuildConnectionString_ShouldReturnFilePath()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "FilePath", "C:\\path\\to\\file.xlsx" }
            };

            // Act
            var connectionString = new ExcelHelper().BuildConnectionString(parameters);

            // Assert
            Assert.Equal("C:\\path\\to\\file.xlsx", connectionString);
        }
    }

    // CsvHelper 测试
    public class CsvHelperTests
    {
        [Fact]
        public async Task TestConnectionAsync_ShouldReturnFalseForNonExistingFile()
        {
            // Arrange
            var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.csv");

            // Act
            var result = await new CsvHelperClass().TestConnectionAsync(nonExistingPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetTablesAsync_ShouldReturnEmptyListForNonExistingFile()
        {
            // Arrange
            var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.csv");

            // Act
            var tables = await new CsvHelperClass().GetTablesAsync(nonExistingPath);

            // Assert
            Assert.Empty(tables);
        }

        [Fact]
        public async Task TableExistsAsync_ShouldReturnFalseForNonExistingFile()
        {
            // Arrange
            var nonExistingPath = Path.Combine(Path.GetTempPath(), "non_existing.csv");

            // Act
            var result = await new CsvHelperClass().TableExistsAsync(nonExistingPath, "test_table");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void BuildConnectionString_ShouldReturnFilePath()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "FilePath", "C:\\path\\to\\file.csv" }
            };

            // Act
            var connectionString = new CsvHelperClass().BuildConnectionString(parameters);

            // Assert
            Assert.Equal("C:\\path\\to\\file.csv", connectionString);
        }
    }
}