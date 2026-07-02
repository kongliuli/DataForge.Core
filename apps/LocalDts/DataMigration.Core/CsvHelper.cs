using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace DataMigration.Core;

/// <summary>
/// CSV文件帮助类
/// </summary>
public class CsvHelper : DatabaseHelper
{
    /// <summary>
    /// 测试CSV文件连接
    /// </summary>
    /// <param name="connectionString">CSV文件路径</param>
    /// <returns>是否连接成功</returns>
    public override async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            // 对于CSV，连接字符串就是文件路径
            return File.Exists(connectionString);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取CSV文件中的表（对于CSV，整个文件视为一个表）
    /// </summary>
    /// <param name="connectionString">CSV文件路径</param>
    /// <returns>表名列表</returns>
    public override async Task<List<string>> GetTablesAsync(string connectionString)
    {
        var tables = new List<string>();
        
        try
        {
            if (File.Exists(connectionString))
            {
                // 将文件名作为表名
                string fileName = Path.GetFileNameWithoutExtension(connectionString);
                tables.Add(fileName);
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return tables;
    }

    /// <summary>
    /// 获取CSV文件的结构
    /// </summary>
    /// <param name="connectionString">CSV文件路径</param>
    /// <param name="tableName">表名（对于CSV，忽略此参数）</param>
    /// <returns>表结构信息</returns>
    public override async Task<List<TableColumnInfo>> GetTableStructureAsync(string connectionString, string tableName)
    {
        var columns = new List<TableColumnInfo>();
        
        try
        {
            using var reader = new StreamReader(connectionString);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            
            await csv.ReadAsync();
            csv.ReadHeader();
            
            foreach (var header in csv.HeaderRecord ?? Enumerable.Empty<string>())
            {
                var columnInfo = new TableColumnInfo
                {
                    Name = header,
                    Type = "string"
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
    /// 预览CSV文件中的数据
    /// </summary>
    /// <param name="connectionString">CSV文件路径</param>
    /// <param name="tableName">表名（对于CSV，忽略此参数）</param>
    /// <param name="limit">限制行数</param>
    /// <returns>数据行列表</returns>
    public override async Task<List<Dictionary<string, object?>>> PreviewDataAsync(string connectionString, string tableName, int limit = 100)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            using var reader = new StreamReader(connectionString);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
            
            await csv.ReadAsync();
            csv.ReadHeader();
            
            int rowCount = 0;
            while (await csv.ReadAsync() && rowCount < limit)
            {
                var row = new Dictionary<string, object?>();
                var headerNames = csv.HeaderRecord ?? Enumerable.Range(0, csv.ColumnCount).Select(i => $"Column{i}");
                
                foreach (var header in headerNames)
                {
                    var value = csv.GetField(header);
                    row[header] = value;
                }
                
                data.Add(row);
                rowCount++;
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return data;
    }

    /// <summary>
    /// 执行CSV查询（获取特定范围的数据）
    /// </summary>
    /// <param name="connectionString">CSV文件路径</param>
    /// <param name="query">查询语句（格式： LIMIT 10）</param>
    /// <returns>查询结果</returns>
    public override async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            int limit = 100; // 默认限制
            
            // 解析查询语句，提取限制行数
            if (query.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            {
                var parts = query.Split(new[] { "LIMIT" }, StringSplitOptions.None);
                if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var parsedLimit))
                {
                    limit = parsedLimit;
                }
            }
            
            // 调用PreviewDataAsync获取数据
            data = await PreviewDataAsync(connectionString, string.Empty, limit);
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return data;
    }

    /// <summary>
    /// 构建CSV连接字符串（实际上就是文件路径）
    /// </summary>
    /// <param name="parameters">连接参数</param>
    /// <returns>连接字符串（文件路径）</returns>
    public override string BuildConnectionString(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("FilePath", out var filePath))
        {
            return filePath;
        }
        
        return string.Empty;
    }

    /// <summary>
    /// 检查CSV文件是否存在
    /// </summary>
    /// <param name="connectionString">CSV文件路径</param>
    /// <param name="tableName">表名（对于CSV，忽略此参数）</param>
    /// <returns>是否存在</returns>
    public override async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        try
        {
            return File.Exists(connectionString);
        }
        catch
        {
            return false;
        }
    }
}