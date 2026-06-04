using ClosedXML.Excel;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Targets;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Excel;

public class ExcelTarget<T> : IDataTarget<T>
{
    private readonly ExcelExportOptions _options;

    public string Name => "Excel Target";
    public DataTargetType TargetType => DataTargetType.Excel;

    public ExcelTarget(ExcelExportOptions? options = null)
    {
        _options = options ?? new ExcelExportOptions();
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(_options.SheetName) ? "Sheet1" : _options.SheetName);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var colCount = properties.Length;

        // 写入标题
        if (_options.IncludeHeader)
        {
            for (var col = 1; col <= colCount; col++)
            {
                worksheet.Cell(1, col).Value = properties[col - 1].Name;
            }
        }

        // 写入数据
        var startRow = _options.IncludeHeader ? 2 : 1;
        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = startRow + i;
            var item = items[i];

            for (var col = 1; col <= colCount; col++)
            {
                var value = properties[col - 1].GetValue(item);
                worksheet.Cell(row, col).Value = value ?? string.Empty;
            }
        }

        // 格式化
        if (_options.AutoSizeColumns)
        {
            worksheet.Columns().AdjustToContents();
        }

        if (_options.FreezeFirstRow && _options.IncludeHeader)
        {
            worksheet.SheetView.FreezeRows(1);
        }

        // 保存文件
        workbook.SaveAs(destination);

        return new ExportResults
        {
            RecordsWritten = items.Count,
            OutputPath = destination,
            OutputSize = new FileInfo(destination).Length
        };
    }

    public async Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await ExportAsync(ToAsyncEnumerable(item), "", cancellationToken).ConfigureAwait(false);
    }

    public async Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var result = await ExportAsync(ToAsyncEnumerable(items), "", cancellationToken).ConfigureAwait(false);
        return new WriteResult { SuccessCount = result.RecordsWritten };
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(T item)
    {
        yield return item;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }
}

public class ExcelExportOptions
{
    public string SheetName { get; set; } = "Sheet1";
    public bool FreezeFirstRow { get; set; } = true;
    public bool AutoSizeColumns { get; set; } = true;
    public bool IncludeHeader { get; set; } = true;
}
