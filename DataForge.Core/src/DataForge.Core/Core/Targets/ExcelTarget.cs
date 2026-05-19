using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

internal class ExcelTarget<T> : IDataTarget<T>
{
    private readonly ExcelExportOptions _options;

    public string Name => "Excel Target";
    public DataTargetType TargetType => DataTargetType.Excel;

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
