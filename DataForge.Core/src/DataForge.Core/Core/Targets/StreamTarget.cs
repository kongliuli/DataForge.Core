using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

public class StreamTarget<T> : IDataTarget<T>
{
    private readonly ExportFormat _format;

    public string Name => "Stream";
    public DataTargetType TargetType => DataTargetType.Stream;

    public StreamTarget(ExportFormat format)
    {
        _format = format;
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var _ in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
        }
        return new ExportResults { RecordsWritten = count };
    }

    public async Task<ExportResults> ExportToStreamAsync(IAsyncEnumerable<T> data, Stream stream, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }

        switch (_format)
        {
            case ExportFormat.Csv:
                await WriteCsvToStreamAsync(stream, items).ConfigureAwait(false);
                break;
            default:
                await JsonSerializer.SerializeAsync(stream, items, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;
        }

        return new ExportResults { RecordsWritten = items.Count };
    }

    private static Task WriteCsvToStreamAsync(Stream stream, List<T> items)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        writer.WriteLine(string.Join(",", properties.Select(p => p.Name)));
        foreach (var item in items)
        {
            var values = properties.Select(p => p.GetValue(item)?.ToString() ?? "");
            writer.WriteLine(string.Join(",", values));
        }
        writer.Flush();
        return Task.CompletedTask;
    }

    public Task WriteAsync(T item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default) => Task.FromResult(new WriteResult());
    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
