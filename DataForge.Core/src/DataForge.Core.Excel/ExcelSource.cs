using ClosedXML.Excel;
using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Excel;

public class ExcelSource<T> : IFileDataSource<T> where T : new()
{
    private readonly ExcelSourceOptions _options;

    public string FilePath { get; }
    public string Name => $"Excel: {FilePath}";
    public DataSourceType SourceType => DataSourceType.Excel;

    public ExcelSource(string filePath, ExcelSourceOptions? options = null)
    {
        FilePath = filePath;
        _options = options ?? new ExcelSourceOptions();
    }

    public async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(FilePath);
        var worksheet = string.IsNullOrWhiteSpace(_options.SheetName)
            ? workbook.Worksheet(1)
            : workbook.Worksheet(_options.SheetName);

        var range = worksheet.RangeUsed();
        if (range == null) yield break;

        var rowCount = range.RowCount();
        var colCount = range.ColumnCount();

        var firstRow = _options.HasHeaderRow ? 1 : 0;
        var startRow = _options.SkipRows + (_options.HasHeaderRow ? 2 : 1);
        var maxRows = _options.MaxRows.HasValue ? _options.MaxRows.Value + startRow - 1 : int.MaxValue;

        string[]? headers = null;
        if (_options.HasHeaderRow)
        {
            headers = new string[colCount];
            for (var col = 1; col <= colCount; col++)
            {
                headers[col - 1] = worksheet.Cell(1, col).GetString();
            }
        }

        for (var row = startRow; row <= rowCount && row <= maxRows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new string[colCount];
            for (var col = 1; col <= colCount; col++)
            {
                values[col - 1] = worksheet.Cell(row, col).GetString();
            }

            var item = MapToObject(values, headers);
            yield return item;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(FilePath);
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "Excel",
            Location = FilePath,
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(FilePath));
    }

    public async Task<IReadOnlyList<T>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in ReadAsync(cancellationToken))
        {
            results.Add(item);
        }
        return results;
    }

    private T MapToObject(string[] values, string[]? headers)
    {
        var type = typeof(T);

        if (type == typeof(string))
        {
            return (T)(object)string.Join(",", values);
        }

        if (type.IsPrimitive || type == typeof(decimal) || type == typeof(string))
        {
            return values.Length > 0 ? (T)Convert.ChangeType(values[0], typeof(T)) : default!;
        }

        var instance = Activator.CreateInstance<T>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            string? value = null;

            if (headers != null)
            {
                var headerIndex = Array.IndexOf(headers, property.Name);
                if (headerIndex >= 0 && headerIndex < values.Length)
                {
                    value = values[headerIndex];
                }
            }
            else if (i < values.Length)
            {
                value = values[i];
            }

            if (value != null)
            {
                try
                {
                    var convertedValue = Convert.ChangeType(value, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch { }
            }
        }

        return instance;
    }
}

public class ExcelSourceOptions
{
    public string SheetName { get; set; } = string.Empty;
    public bool HasHeaderRow { get; set; } = true;
    public int SkipRows { get; set; } = 0;
    public int? MaxRows { get; set; }
}
