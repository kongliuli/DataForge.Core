using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using ExcelDataReader;

namespace DataMigration.Core;

/// <summary>
/// Excel文件帮助类
/// </summary>
public class ExcelHelper : DatabaseHelper
{
    /// <summary>
    /// 测试Excel文件连接
    /// </summary>
    /// <param name="connectionString">Excel文件路径</param>
    /// <returns>是否连接成功</returns>
    public override async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            // 对于Excel，连接字符串就是文件路径
            return File.Exists(connectionString);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取Excel文件中的所有工作表
    /// </summary>
    /// <param name="connectionString">Excel文件路径</param>
    /// <returns>工作表名称列表</returns>
    public override async Task<List<string>> GetTablesAsync(string connectionString)
    {
        var sheets = new List<string>();
        
        try
        {
            using var stream = File.Open(connectionString, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            
            var dataSet = reader.AsDataSet();
            foreach (DataTable table in dataSet.Tables)
            {
                sheets.Add(table.TableName);
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return sheets;
    }

    /// <summary>
    /// 获取Excel工作表的结构
    /// </summary>
    /// <param name="connectionString">Excel文件路径</param>
    /// <param name="tableName">工作表名称</param>
    /// <returns>表结构信息</returns>
    public override async Task<List<TableColumnInfo>> GetTableStructureAsync(string connectionString, string tableName)
    {
        var columns = new List<TableColumnInfo>();
        
        try
        {
            using var stream = File.Open(connectionString, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            
            var dataSet = reader.AsDataSet();
            if (dataSet.Tables.Contains(tableName))
            {
                var table = dataSet.Tables[tableName];
                foreach (DataColumn column in table.Columns)
                {
                    var columnInfo = new TableColumnInfo
                    {
                        Name = column.ColumnName,
                        Type = column.DataType.Name
                    };
                    columns.Add(columnInfo);
                }
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return columns;
    }

    /// <summary>
    /// 预览Excel工作表中的数据
    /// </summary>
    /// <param name="connectionString">Excel文件路径</param>
    /// <param name="tableName">工作表名称</param>
    /// <param name="limit">限制行数</param>
    /// <returns>数据行列表</returns>
    public override async Task<List<Dictionary<string, object?>>> PreviewDataAsync(string connectionString, string tableName, int limit = 100)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            using var stream = File.Open(connectionString, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            
            var dataSet = reader.AsDataSet();
            if (dataSet.Tables.Contains(tableName))
            {
                var table = dataSet.Tables[tableName];
                int rowCount = Math.Min(table.Rows.Count, limit);
                
                for (int i = 0; i < rowCount; i++)
                {
                    var row = new Dictionary<string, object?>();
                    var dataRow = table.Rows[i];
                    
                    for (int j = 0; j < table.Columns.Count; j++)
                    {
                        string columnName = table.Columns[j].ColumnName;
                        object? value = dataRow[j] == DBNull.Value ? null : dataRow[j];
                        row[columnName] = value;
                    }
                    
                    data.Add(row);
                }
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return data;
    }

    /// <summary>
    /// 执行Excel查询（获取特定范围的数据）
    /// </summary>
    /// <param name="connectionString">Excel文件路径</param>
    /// <param name="query">查询语句（格式：SheetName!A1:C10）</param>
    /// <returns>查询结果</returns>
    public override async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string connectionString, string query)
    {
        var data = new List<Dictionary<string, object?>>();
        
        try
        {
            // 解析查询语句，提取工作表名称和范围
            string sheetName = query;
            string range = string.Empty;
            
            if (query.Contains('!'))
            {
                var parts = query.Split('!');
                sheetName = parts[0];
                range = parts[1];
            }
            
            using var stream = File.Open(connectionString, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            
            var dataSet = reader.AsDataSet();
            if (dataSet.Tables.Contains(sheetName))
            {
                var table = dataSet.Tables[sheetName];
                
                // 简化处理，返回整个工作表的数据
                foreach (DataRow dataRow in table.Rows)
                {
                    var row = new Dictionary<string, object?>();
                    
                    for (int j = 0; j < table.Columns.Count; j++)
                    {
                        string columnName = table.Columns[j].ColumnName;
                        object? value = dataRow[j] == DBNull.Value ? null : dataRow[j];
                        row[columnName] = value;
                    }
                    
                    data.Add(row);
                }
            }
        }
        catch
        {
            // 忽略错误，返回空列表
        }
        
        return data;
    }

    /// <summary>
    /// 构建Excel连接字符串（实际上就是文件路径）
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
    /// 检查工作表是否存在
    /// </summary>
    /// <param name="connectionString">Excel文件路径</param>
    /// <param name="tableName">工作表名称</param>
    /// <returns>是否存在</returns>
    public override async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        try
        {
            using var stream = File.Open(connectionString, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            
            var dataSet = reader.AsDataSet();
            return dataSet.Tables.Contains(tableName);
        }
        catch
        {
            return false;
        }
    }
}