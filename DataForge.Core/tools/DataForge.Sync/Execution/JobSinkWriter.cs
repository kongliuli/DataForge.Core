using DataForge.Core.Core.Models;
using DataForge.Sync.Models;
using System.Text;
using System.Text.Json;

namespace DataForge.Sync.Execution;

internal static class JobSinkWriter
{
    public static async Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        return sink.Type.ToLowerInvariant() switch
        {
            "json" => await WriteJsonAsync(rows, sink, cancellationToken).ConfigureAwait(false),
            "csv" => await WriteCsvAsync(rows, sink, cancellationToken).ConfigureAwait(false),
            "sqlserver" => await JobRowSqlServerSink.WriteAsync(rows, sink, cancellationToken).ConfigureAwait(false),
            "parquet" => await JobRowParquetSink.WriteAsync(rows, sink, cancellationToken).ConfigureAwait(false),
            "duckdb" => await JobRowDuckDbSink.WriteAsync(rows, sink, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Sink type '{sink.Type}' is not supported.")
        };
    }

    private static async Task<ExportResults> WriteJsonAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken)
    {
        var items = new List<Dictionary<string, string?>>();
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            items.Add(new Dictionary<string, string?>(row.Values, StringComparer.OrdinalIgnoreCase));

        var options = new JsonSerializerOptions
        {
            WriteIndented = sink.Options?.Indented ?? true
        };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sink.Path))!);
        await File.WriteAllTextAsync(sink.Path, JsonSerializer.Serialize(items, options), cancellationToken)
            .ConfigureAwait(false);

        return new ExportResults
        {
            RecordsWritten = items.Count,
            OutputPath = sink.Path,
            OutputSize = new FileInfo(sink.Path).Length
        };
    }

    private static async Task<ExportResults> WriteCsvAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken)
    {
        var delimiter = ',';
        var includeHeader = sink.Options?.IncludeHeader ?? true;
        var count = 0;
        string[]? headers = null;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sink.Path))!);
        await using var writer = new StreamWriter(sink.Path, false, Encoding.UTF8);

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            headers ??= row.Values.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase).ToArray();

            if (includeHeader && count == 0)
                await writer.WriteLineAsync(string.Join(delimiter, headers)).ConfigureAwait(false);

            var line = string.Join(
                delimiter,
                headers.Select(h => EscapeCsv(row.Get(h) ?? string.Empty, delimiter)));
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            count++;
        }

        if (includeHeader && count == 0 && headers != null)
            await writer.WriteLineAsync(string.Join(delimiter, headers)).ConfigureAwait(false);

        return new ExportResults
        {
            RecordsWritten = count,
            OutputPath = sink.Path
        };
    }

    private static string EscapeCsv(string value, char delimiter)
    {
        if (value.Contains('"') || value.Contains(delimiter) || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
