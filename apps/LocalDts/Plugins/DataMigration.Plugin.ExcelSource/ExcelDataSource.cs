using DataMigration.Contracts;
using ExcelDataReader;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;

namespace DataMigration.Plugin.ExcelSource;

public class ExcelDataSource : IDataSource
{
    public string Id => "Standard.ExcelSource";
    public string Name => "Excel File Source";
    public Version Version => new(1, 0, 0);

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        // 可在此注入日志、配置中心等依赖
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(
        SourceConfig config,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var filePath = config["FilePath"];
        var sheetName = config.TryGetValue("SheetName", out var name) ? name : null;
        var hasHeader = config.TryGetValue("HasHeader", out var header) && bool.Parse(header);

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = hasHeader
            }
        });

        DataTable? table = null;
        if (!string.IsNullOrEmpty(sheetName) && dataSet.Tables.Contains(sheetName))
        {
            table = dataSet.Tables[sheetName];
        }
        else if (dataSet.Tables.Count > 0)
        {
            table = dataSet.Tables[0];
        }

        if (table != null)
        {
            foreach (DataRow row in table.Rows)
            {
                ct.ThrowIfCancellationRequested();

                var record = new DataRecord();
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var columnName = table.Columns[i].ColumnName;
                    var value = row[i] == DBNull.Value ? null : row[i];
                    record[columnName] = value;
                }
                yield return record;
            }
        }
    }

    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct) => Task.CompletedTask;
}
