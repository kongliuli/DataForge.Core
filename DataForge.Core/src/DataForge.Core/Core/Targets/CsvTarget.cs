using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Targets;

internal class CsvTarget<T> : IDataTarget<T>
{
    private readonly CsvExportOptions _options;

    public string Name => "CSV Target";
    public DataTargetType TargetType => DataTargetType.Csv;

    public CsvTarget(CsvExportOptions options)
    {
        _options = options;
    }

    public async Task<ExportResults> ExportAsync(IAsyncEnumerable<T> data, string destination, CancellationToken cancellationToken = default)
    {
        var rowCount = 0;
        var batchSize = _options.BatchSize ?? 1000;
        var batch = new List<string>(batchSize);

        using var writer = new StreamWriter(destination, false, _options.Encoding);

        if (_options.IncludeHeader)
        {
            var headers = GetHeaders();
            await writer.WriteLineAsync(string.Join(_options.Delimiter.ToString(), headers)).ConfigureAwait(false);
        }

        await foreach (var item in data.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var line = ConvertToCsvLine(item);
            batch.Add(line);

            if (batch.Count >= batchSize)
            {
                await WriteBatchToWriterAsync(writer, batch).ConfigureAwait(false);
                rowCount += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await WriteBatchToWriterAsync(writer, batch).ConfigureAwait(false);
            rowCount += batch.Count;
        }

        return new ExportResults { RecordsWritten = rowCount, OutputPath = destination };
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

    private string[] GetHeaders()
    {
        return typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();
    }

    private string ConvertToCsvLine(T item)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var values = new string[properties.Length];

        for (var i = 0; i < properties.Length; i++)
        {
            var value = properties[i].GetValue(item)?.ToString() ?? string.Empty;
            values[i] = EscapeValue(value);
        }

        return string.Join(_options.Delimiter.ToString(), values);
    }

    private string EscapeValue(string value)
    {
        if (value.Contains(_options.Delimiter) || value.Contains(_options.QuoteChar))
        {
            return _options.QuoteChar + value.Replace(_options.QuoteChar.ToString(), _options.QuoteChar.ToString() + _options.QuoteChar) + _options.QuoteChar;
        }
        return value;
    }

    private async Task WriteBatchToWriterAsync(StreamWriter writer, List<string> batch)
    {
        foreach (var line in batch)
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
