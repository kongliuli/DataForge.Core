using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace DataMigration.Core;

/// <summary>
/// SQL Server数据库帮助类
/// </summary>
public class SqlServerHelper : DatabaseHelper
{
    /// <summary>
    /// 测试SQL Server连接
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>是否连接成功</returns>
    public override async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
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
    /// 获取SQL Server数据库中的所有表名
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>表名列表</returns>
    public override async Task<List<string>> GetTablesAsync(string connectionString)
    {
        var tables = new List<string>();
        
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = DB_NAME();";
            using var command = new SqlCommand(query, connection);
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
    /// 获取SQL Server表的结构
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>表结构信息</returns>
    public override async Task<List<TableColumnInfo>> GetTableStructureAsync(string connectionString, string tableName)
    {
        var columns = new List<TableColumnInfo>();
        
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND TABLE_CATALOG = DB_NAME();";
            using var command = new SqlCommand(query, connection);
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
    /// 预览SQL Server表中的数据
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
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SELECT TOP {limit} * FROM [{tableName}];";
            using var command = new SqlCommand(query, connection);
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
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(query, connection);
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
    /// 构建SQL Server连接字符串
    /// </summary>
    /// <param name="parameters">连接参数</param>
    /// <returns>连接字符串</returns>
    public override string BuildConnectionString(Dictionary<string, string> parameters)
    {
        var builder = new SqlConnectionStringBuilder();
        
        if (parameters.TryGetValue("Server", out var server))
        {
            builder.DataSource = server;
        }
        
        if (parameters.TryGetValue("Database", out var database))
        {
            builder.InitialCatalog = database;
        }
        
        if (parameters.TryGetValue("UserID", out var userId))
        {
            builder.UserID = userId;
        }
        
        if (parameters.TryGetValue("Password", out var password))
        {
            builder.Password = password;
        }
        
        if (parameters.TryGetValue("IntegratedSecurity", out var integratedSecurity))
        {
            if (bool.TryParse(integratedSecurity, out var isIntegrated))
            {
                builder.IntegratedSecurity = isIntegrated;
            }
        }
        
        if (parameters.TryGetValue("TrustServerCertificate", out var trustServerCertificate))
        {
            if (bool.TryParse(trustServerCertificate, out var trust))
            {
                builder.TrustServerCertificate = trust;
            }
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
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            var query = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}' AND TABLE_TYPE = 'BASE TABLE' AND TABLE_CATALOG = DB_NAME();";
            using var command = new SqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();
            
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }
}