using Parquet.Schema;
using Parquet.Serialization;

namespace DataForge.Sync.Sources;

internal sealed class JobRowParquetSource
{
    public string FilePath { get; }

    public JobRowParquetSource(string filePath) => FilePath = filePath;

    public async IAsyncEnumerable<JobRow> ReadAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(FilePath);
        var result = await ParquetSerializer
            .DeserializeUntypedAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in result.Data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jobRow = new JobRow();
            foreach (var (key, value) in row)
                jobRow.Values[key] = value?.ToString();
            yield return jobRow;
        }
    }
}
