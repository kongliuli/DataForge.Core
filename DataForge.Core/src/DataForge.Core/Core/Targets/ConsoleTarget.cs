using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

public class ConsoleTarget<T> : IDataTarget<T>
{
    private readonly Func<T, string>? _formatter;

    public string Name => "Console";
    public DataTargetType TargetType => DataTargetType.Console;

    public ConsoleTarget(Func<T, string>? formatter = null)
    {
        _formatter = formatter;
    }

    public Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        return ExportInternalAsync(data, cancellationToken);
    }

    private async Task<ExportResults> ExportInternalAsync(IAsyncEnumerable<T> data, CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var line = _formatter != null ? _formatter(item) : item?.ToString() ?? "";
            System.Console.WriteLine(line);
            count++;
        }
        return new ExportResults { RecordsWritten = count };
    }

    public Task WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        var line = _formatter != null ? _formatter(item) : item?.ToString() ?? "";
        System.Console.WriteLine(line);
        return Task.CompletedTask;
    }

    public Task<WriteResult> WriteBatchAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var item in items)
        {
            var line = _formatter != null ? _formatter(item) : item?.ToString() ?? "";
            System.Console.WriteLine(line);
            count++;
        }
        return Task.FromResult(new WriteResult { SuccessCount = count });
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
