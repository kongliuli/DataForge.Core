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
                await WriteBatchAsync(writer, batch).ConfigureAwait(false);
                rowCount += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(writer, batch).ConfigureAwait(false);
            rowCount += batch.Count;
        }

        return new ExportResults { RecordsWritten = rowCount, OutputPath = destination };
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

    private async Task WriteBatchAsync(StreamWriter writer, List<string> batch)
    {
        foreach (var line in batch)
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}