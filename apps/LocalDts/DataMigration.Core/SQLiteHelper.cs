using System.Data.SQLite;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataMigration.Core;

public class SQLiteHelper
{
    /// <summary>
    /// 测试SQLite连接
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>是否连接成功</returns>
    public static async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            await connection.CloseAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取SQLite数据库中的所有表名
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>表名列表</returns>
    public static async Task<List<string>> GetTablesAsync(string connectionString)
    {
        var tables = new List<string>();
        
        try
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            
            var query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                string tableName = reader.GetString(0);
                tables.Add(tableName);
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return tables;
    }

    /// <summary>
    /// 获取SQLite表的结构
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>表结构信息</returns>
    public static async Task<List<TableColumnInfo>> GetTableStructureAsync(string connectionString, string tableName)
    {
        var columns = new List<TableColumnInfo>();
        
        try
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"PRAGMA table_info({tableName});";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var columnInfo = new TableColumnInfo
                {
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
                };
                columns.Add(columnInfo);
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return columns;
    }

    /// <summary>
    /// 预览SQLite表中的数据
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <param name="limit">限制行数</param>
    /// <returns>数据行列表</returns>
    public static async Task<List<Dictionary<string, object?>>> PreviewDataAsync(string connectionString, string tableName, int limit = 100)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SELECT * FROM {tableName} LIMIT {limit};";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value;
                }
                data.Add(row);
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return data;
    }

    /// <summary>
    /// 执行SQL查询
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="query">SQL查询语句</param>
    /// <returns>查询结果</returns>
    public static async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value;
                }
                data.Add(row);
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return data;
    }

    /// <summary>
    /// 构建SQLite连接字符串
    /// </summary>
    /// <param name="databasePath">数据库文件路径</param>
    /// <returns>连接字符串</returns>
    public static string BuildConnectionString(string databasePath)
    {
        return $"Data Source={databasePath};Version=3;";
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>是否存在</returns>
    public static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        try
        {
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";
            using var command = new SQLiteCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查数据库文件是否存在
    /// </summary>
    /// <param name="databasePath">数据库文件路径</param>
    /// <returns>是否存在</returns>
    public static bool DatabaseFileExists(string databasePath)
    {
        return System.IO.File.Exists(databasePath);
    }
}