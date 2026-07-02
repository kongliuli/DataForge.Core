using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;

namespace DataMigration.Core;

/// <summary>
/// MySQL数据库帮助类
/// </summary>
public class MySQLHelper : DatabaseHelper
{
    /// <summary>
    /// 测试MySQL连接
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>是否连接成功</returns>
    public override async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
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
    /// 获取MySQL数据库中的所有表名
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>表名列表</returns>
    public override async Task<List<string>> GetTablesAsync(string connectionString)
    {
        var tables = new List<string>();
        
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = "SHOW TABLES;";
            using var command = new MySqlCommand(query, connection);
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
    /// 获取MySQL表的结构
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>表结构信息</returns>
    public override async Task<List<TableColumnInfo>> GetTableStructureAsync(string connectionString, string tableName)
    {
        var columns = new List<TableColumnInfo>();
        
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"DESCRIBE {tableName};";
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var columnInfo = new TableColumnInfo
                {
                    Name = reader.GetString(0),
                    Type = reader.GetString(1)
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
    /// 预览MySQL表中的数据
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <param name="limit">限制行数</param>
    /// <returns>数据行列表</returns>
    public override async Task<List<Dictionary<string, object?>>> PreviewDataAsync(string connectionString, string tableName, int limit = 100)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SELECT * FROM {tableName} LIMIT {limit};";
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object? value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
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
    public override async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object? value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
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
    /// 构建MySQL连接字符串
    /// </summary>
    /// <param name="parameters">连接参数</param>
    /// <returns>连接字符串</returns>
    public override string BuildConnectionString(Dictionary<string, string> parameters)
    {
        var builder = new MySqlConnectionStringBuilder();
        
        if (parameters.TryGetValue("Server", out var server))
        {
            builder.Server = server;
        }
        
        if (parameters.TryGetValue("Port", out var port))
        {
            if (int.TryParse(port, out var portNumber))
            {
                builder.Port = (uint)portNumber;
            }
        }
        
        if (parameters.TryGetValue("Database", out var database))
        {
            builder.Database = database;
        }
        
        if (parameters.TryGetValue("UserID", out var userId))
        {
            builder.UserID = userId;
        }
        
        if (parameters.TryGetValue("Password", out var password))
        {
            builder.Password = password;
        }
        
        if (parameters.TryGetValue("SSLMode", out var sslMode))
        {
            builder.SslMode = sslMode switch
            {
                "None" => MySqlSslMode.None,
                "Preferred" => MySqlSslMode.Preferred,
                "Required" => MySqlSslMode.Required,
                "VerifyCA" => MySqlSslMode.VerifyCA,
                "VerifyFull" => MySqlSslMode.VerifyFull,
                _ => MySqlSslMode.Preferred
            };
        }
        
        return builder.ToString();
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>是否存在</returns>
    public override async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SHOW TABLES LIKE '{tableName}';";
            using var command = new MySqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            
            return result != null;
        }
        catch
        {
            return false;
        }
    }
}