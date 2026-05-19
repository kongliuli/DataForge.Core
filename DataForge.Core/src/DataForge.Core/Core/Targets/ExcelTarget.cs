using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

internal class ExcelTarget<T> : IDataTarget<T>
{
    private readonly ExcelExportOptions _options;

    public ExcelTarget(ExcelExportOptions options)
    {
        _options = options;
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        using var stream = new MemoryStream();
        
        WriteToStream(stream, items, properties);
        
        await File.WriteAllBytesAsync(destination, stream.ToArray(), cancellationToken).ConfigureAwait(false);

        return new ExportResults
        {
            RecordsWritten = items.Count,
            OutputPath = destination,
            OutputSize = new FileInfo(destination).Length
        };
    }

    private void WriteToStream(MemoryStream stream, List<T> items, PropertyInfo[] properties)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);
        
        if (_options.IncludeHeader)
        {
            var headers = properties.Select(p => p.Name);
            writer.WriteLine(string.Join(",", headers));
        }

        foreach (var item in items)
        {
            var values = properties.Select(p => p.GetValue(item)?.ToString() ?? string.Empty);
            writer.WriteLine(string.Join(",", values));
        }
        
        writer.Flush();
    }
}