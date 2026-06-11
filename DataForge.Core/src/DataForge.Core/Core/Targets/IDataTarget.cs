using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

public interface IDataTarget<in T>
{
    string Name { get; }
    DataTargetType TargetType { get; }
    Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default);
    Task WriteAsync(T item, CancellationToken cancellationToken = default);
    Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);
    Task CompleteAsync(CancellationToken cancellationToken = default);
}

public class WriteResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<WriteError> Errors { get; set; } = [];
}

public class WriteError
{
    public object? Item { get; set; }
    public string Error { get; set; } = "";
}

public enum ExportFormat
{
    Csv,
    Json,
    Excel
}

public class CsvExportOptions
{
    public char Delimiter { get; set; } = ',';
    
    public char QuoteChar { get; set; } = '"';
    
    public System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;
    
    public bool IncludeHeader { get; set; } = true;
    
    public int? BatchSize { get; set; }
}

public class JsonExportOptions
{
    public bool Indented { get; set; } = true;
    
    public string? RootPropertyName { get; set; }
}

public class ExcelExportOptions
{
    public string SheetName { get; set; } = "Sheet1";
    
    public bool FreezeFirstRow { get; set; } = true;
    
    public bool AutoSizeColumns { get; set; } = true;
    
    public bool IncludeHeader { get; set; } = true;
}