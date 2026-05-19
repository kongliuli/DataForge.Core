using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

public interface IDataTarget<in T>
{
    Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default);
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