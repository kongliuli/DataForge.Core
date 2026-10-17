using DataForge.Core.Core.Models;
using DataForge.Sync.Models;
using Parquet;
using Parquet.Schema;
using Parquet.Serialization;

namespace DataForge.Sync.Execution;

internal static class JobRowParquetSink
{
    public static async Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default)
    {
        var batch = new List<IDictionary<string, object>>();
        ParquetSchema? schema = null;
        var count = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var dict = row.Values.ToDictionary(
                static k => k.Key,
                static k => (object)(k.Value ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            batch.Add(dict);
            schema ??= BuildSchema(dict.Keys);

            if (batch.Count >= (sink.Options?.BatchSize ?? 10_000))
            {
                count += await FlushAsync(batch, schema, sink.Path, append: count > 0, cancellationToken)
                    .ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0 && schema != null)
        {
            count += await FlushAsync(batch, schema, sink.Path, append: count > 0, cancellationToken)
                .ConfigureAwait(false);
        }
        else if (schema != null && count == 0)
        {
            await FlushAsync([], schema, sink.Path, append: false, cancellationToken).ConfigureAwait(false);
        }

        var fileInfo = new FileInfo(sink.Path);
        return new ExportResults
        {
            RecordsWritten = count,
            OutputPath = sink.Path,
            OutputSize = fileInfo.Exists ? fileInfo.Length : 0
        };
    }

    private static ParquetSchema BuildSchema(IEnumerable<string> columns)
    {
        var fields = columns
            .OrderBy(static c => c, StringComparer.OrdinalIgnoreCase)
            .Select(static c => (Field)new DataField<string>(c))
            .ToArray();
        return new ParquetSchema(fields);
    }

    private static async Task<int> FlushAsync(
        IReadOnlyList<IDictionary<string, object>> batch,
        ParquetSchema schema,
        string path,
        bool append,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = new FileStream(
            path,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await ParquetSerializer.SerializeUntypedAsync(
            batch,
            schema,
            stream,
            new ParquetOptions { Append = append },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return batch.Count;
    }
}
