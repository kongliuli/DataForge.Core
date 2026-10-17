using DataForge.Core.Core.Infrastructure;
using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources;
using DataForge.Core.Core.Sources.Options;
using System.Text;

namespace DataForge.Sync.Sources;

internal sealed class JobRowCsvSource : IFileDataSource<JobRow>
{
    private readonly CsvSourceOptions _options;

    public string FilePath { get; }

    public JobRowCsvSource(string filePath, CsvSourceOptions? options = null)
    {
        FilePath = filePath;
        _options = options ?? new CsvSourceOptions();
    }

    public string Name => $"CSV: {FilePath}";

    public DataSourceType SourceType => DataSourceType.Csv;

    public async IAsyncEnumerable<JobRow> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(FilePath, _options.Encoding);
        string[]? headers = null;

        if (_options.HasHeaderRow)
        {
            var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            headers = headerLine?.Split(_options.Delimiter);
        }

        for (var i = 0; i < _options.SkipRows; i++)
            await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.CommentPrefix != null && line.StartsWith(_options.CommentPrefix, StringComparison.Ordinal))
                continue;

            var values = ParseCsvLine(line);
            yield return MapRow(values, headers);
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(FilePath);
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "CSV",
            Location = FilePath,
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(FilePath));

    public async Task<IReadOnlyList<JobRow>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<JobRow>();
        await foreach (var row in ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return rows;
    }

    private string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == _options.QuoteChar)
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == _options.Delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static JobRow MapRow(string[] values, string[]? headers)
    {
        var row = new JobRow();

        if (headers != null)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Trim();
                if (string.IsNullOrEmpty(header))
                    continue;

                row.Values[header] = i < values.Length ? NullIfEmpty(values[i]) : null;
            }

            return row;
        }

        for (var i = 0; i < values.Length; i++)
            row.Values[$"Column{i + 1}"] = NullIfEmpty(values[i]);

        return row;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : value.Trim();
}
