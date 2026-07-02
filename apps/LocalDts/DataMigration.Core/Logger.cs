using System;
using System.IO;
using System.Text;

namespace DataMigration.Core;

public class Logger
{
    private static readonly object _lock = new object();
    private static Logger _instance;
    private string _logFilePath;
    
    public static Logger Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Logger();
                    }
                }
            }
            return _instance;
        }
    }
    
    private Logger()
    {
        // 默认日志文件路径
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
        
        _logFilePath = Path.Combine(logDir, $"migration_{DateTime.Now:yyyyMMdd}.log");
    }
    
    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    public void Info(string message, params object[] args)
    {
        WriteLog("INFO", message, args);
    }
    
    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    public void Warn(string message, params object[] args)
    {
        WriteLog("WARN", message, args);
    }
    
    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="ex">异常对象</param>
    /// <param name="args">格式化参数</param>
    public void Error(string message, Exception ex = null, params object[] args)
    {
        WriteLog("ERROR", message, args, ex);
    }
    
    /// <summary>
    /// 记录详细的错误信息，包括错误位置、原因和建议解决方案
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="ex">异常对象</param>
    /// <param name="component">组件名称</param>
    /// <param name="operation">操作名称</param>
    /// <param name="args">格式化参数</param>
    public void DetailedError(string message, Exception ex, string component, string operation, params object[] args)
    {
        var errorMessage = new StringBuilder();
        errorMessage.AppendLine($"Component: {component}");
        errorMessage.AppendLine($"Operation: {operation}");
        errorMessage.AppendLine($"Message: {string.Format(message, args)}");
        errorMessage.AppendLine($"Error Type: {ex.GetType().FullName}");
        errorMessage.AppendLine($"Error Message: {ex.Message}");
        errorMessage.AppendLine($"Stack Trace: {ex.StackTrace}");
        
        if (ex.InnerException != null)
        {
            errorMessage.AppendLine($"Inner Exception: {ex.InnerException.Message}");
        }
        
        // 提供建议解决方案
        errorMessage.AppendLine("Suggested Solution: " + GetSuggestedSolution(ex));
        
        WriteLog("ERROR", errorMessage.ToString());
    }
    
    /// <summary>
    /// 根据异常类型提供建议的解决方案
    /// </summary>
    /// <param name="ex">异常对象</param>
    /// <returns>建议的解决方案</returns>
    private string GetSuggestedSolution(Exception ex)
    {
        if (ex is System.Data.Common.DbException)
        {
            if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return "增加数据库连接超时时间，检查数据库服务器负载，优化查询语句";
            }
            else if (ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                return "检查数据库连接字符串是否正确，确保数据库服务器可访问，检查网络连接";
            }
            else if (ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
            {
                return "优化事务处理，减少锁持有时间，考虑使用不同的隔离级别";
            }
            return "检查数据库配置和权限，确保数据库服务正常运行";
        }
        else if (ex is System.IO.IOException)
        {
            return "检查文件路径是否正确，确保有足够的权限访问文件，检查文件是否被其他进程占用";
        }
        else if (ex is System.Net.Http.HttpRequestException)
        {
            return "检查网络连接，确保目标服务可访问，验证API端点和认证信息";
        }
        else if (ex is System.TimeoutException)
        {
            return "增加操作超时时间，检查网络连接和目标服务响应时间";
        }
        else if (ex is DataMigration.Contracts.ConfigurationException)
        {
            return "检查配置文件是否正确，确保所有必需的配置项都已设置";
        }
        else if (ex is DataMigration.Contracts.DataException)
        {
            return "检查数据源和目标源的数据格式，确保数据类型匹配，处理数据转换错误";
        }
        else if (ex is DataMigration.Contracts.PluginException)
        {
            return "检查插件是否正确安装，验证插件版本兼容性，查看插件日志获取详细错误信息";
        }
        
        return "检查系统日志获取更多信息，确保所有依赖项都已正确安装";
    }
    
    /// <summary>
    /// 写入日志到文件
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">日志消息</param>
    /// <param name="args">格式化参数</param>
    /// <param name="ex">异常对象</param>
    private void WriteLog(string level, string message, object[] args = null, Exception ex = null)
    {
        lock (_lock)
        {
            try
            {
                var formattedMessage = args != null && args.Length > 0 ? string.Format(message, args) : message;
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {formattedMessage}";
                
                if (ex != null)
                {
                    logEntry += $"\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
                }
                
                File.AppendAllText(_logFilePath, logEntry + "\n\n", Encoding.UTF8);
                
                // 同时输出到控制台
                Console.WriteLine(logEntry);
            }
            catch (Exception logEx)
            {
                // 防止日志记录本身出错
                Console.WriteLine($"Error writing to log: {logEx.Message}");
            }
        }
    }
}
