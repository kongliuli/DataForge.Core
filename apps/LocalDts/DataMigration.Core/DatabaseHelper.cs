using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataMigration.Core;

/// <summary>
/// 数据库帮助类基类
/// </summary>
public abstract class DatabaseHelper
{
    /// <summary>
    /// 测试数据库连接
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>是否连接成功</returns>
    public abstract Task<bool> TestConnectionAsync(string connectionString);

    /// <summary>
    /// 获取数据库中的所有表名
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>表名列表</returns>
    public abstract Task<List<string>> GetTablesAsync(string connectionString);

    /// <summary>
    /// 获取表的结构
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>表结构信息</returns>
    public abstract Task<List<TableColumnInfo>> GetTableStructureAsync(string connectionString, string tableName);

    /// <summary>
    /// 预览表中的数据
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <param name="limit">限制行数</param>
    /// <returns>数据行列表</returns>
    public abstract Task<List<Dictionary<string, object?>>> PreviewDataAsync(string connectionString, string tableName, int limit = 100);

    /// <summary>
    /// 执行SQL查询
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="query">SQL查询语句</param>
    /// <returns>查询结果</returns>
    public abstract Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query);

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    /// <param name="parameters">连接参数</param>
    /// <returns>连接字符串</returns>
    public abstract string BuildConnectionString(Dictionary<string, string> parameters);

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="tableName">表名</param>
    /// <returns>是否存在</returns>
    public abstract Task<bool> TableExistsAsync(string connectionString, string tableName);
}

/// <summary>
/// 表列信息
/// </summary>
public class TableColumnInfo
{
    /// <summary>
    /// 列名
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 列类型
    /// </summary>
    public string Type { get; set; } = string.Empty;
}