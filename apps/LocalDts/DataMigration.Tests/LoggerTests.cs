using DataMigration.Core;
using System.IO;
using System;
using System.Reflection;

namespace DataMigration.Tests;

public class LoggerTests
{
    private string _logDirectory;

    public LoggerTests()
    {
        // 使用当前目录作为测试目录，Logger会在其中创建logs文件夹
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        
        // 清理旧的日志目录
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, true);
        }
        
        // 重置Logger实例
        var loggerField = typeof(Logger).GetField("_instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (loggerField != null)
        {
            loggerField.SetValue(null, null);
        }
    }

    ~LoggerTests()
    {
        // 清理临时日志目录
        if (Directory.Exists(_logDirectory))
        {
            try
            {
                Directory.Delete(_logDirectory, true);
            }
            catch { }
        }
    }

    [Fact]
    public void Instance_ShouldReturnSameInstance()
    {
        // Act
        var instance1 = Logger.Instance;
        var instance2 = Logger.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Info_ShouldWriteInfoLog()
    {
        // Arrange
        var testMessage = "Test info message";
        var logger = Logger.Instance;

        // Act
        logger.Info(testMessage);

        // Assert
        var logFiles = Directory.GetFiles(_logDirectory, "migration_*.log");
        Assert.NotEmpty(logFiles);
        
        var logContent = File.ReadAllText(logFiles[0]);
        Assert.Contains("INFO", logContent);
        Assert.Contains(testMessage, logContent);
    }

    [Fact]
    public void Warn_ShouldWriteWarnLog()
    {
        // Arrange
        var testMessage = "Test warning message";
        var logger = Logger.Instance;

        // Act
        logger.Warn(testMessage);

        // Assert
        var logFiles = Directory.GetFiles(_logDirectory, "migration_*.log");
        Assert.NotEmpty(logFiles);
        
        var logContent = File.ReadAllText(logFiles[0]);
        Assert.Contains("WARN", logContent);
        Assert.Contains(testMessage, logContent);
    }

    [Fact]
    public void Error_ShouldWriteErrorLog()
    {
        // Arrange
        var testMessage = "Test error message";
        var testException = new Exception("Test exception");
        var logger = Logger.Instance;

        // Act
        logger.Error(testMessage, testException);

        // Assert
        var logFiles = Directory.GetFiles(_logDirectory, "migration_*.log");
        Assert.NotEmpty(logFiles);
        
        var logContent = File.ReadAllText(logFiles[0]);
        Assert.Contains("ERROR", logContent);
        Assert.Contains(testMessage, logContent);
        Assert.Contains(testException.Message, logContent);
        if (testException.StackTrace != null)
        {
            Assert.Contains(testException.StackTrace, logContent);
        }
    }

    [Fact]
    public void DetailedError_ShouldWriteDetailedErrorLog()
    {
        // Arrange
        var testMessage = "Test detailed error message";
        var testException = new Exception("Test exception");
        var component = "TestComponent";
        var operation = "TestOperation";
        var logger = Logger.Instance;

        // Act
        logger.DetailedError(testMessage, testException, component, operation);

        // Assert
        var logFiles = Directory.GetFiles(_logDirectory, "migration_*.log");
        Assert.NotEmpty(logFiles);
        
        var logContent = File.ReadAllText(logFiles[0]);
        Assert.Contains("ERROR", logContent);
        Assert.Contains(testMessage, logContent);
        Assert.Contains(component, logContent);
        Assert.Contains(operation, logContent);
        Assert.Contains(testException.Message, logContent);
        Assert.Contains("Suggested Solution:", logContent);
    }
}
