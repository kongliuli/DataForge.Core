using DataForge.Jobs.Catalog;

namespace DataForge.Jobs.Parquet;

internal sealed class ParquetCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["parquet"];

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString) || !File.Exists(connectionString))
            return ConnectionTestResult.Fail($"Parquet file not found: {connectionString}");

        try
        {
            _ = await DescribeAsync(connectionString, cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail(ex.Message);
        }
    }

    public Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(connectionString))
            throw new FileNotFoundException("Parquet file not found.", connectionString);

        return Task.FromResult<IReadOnlyList<string>>([Path.GetFileName(connectionString)]);
    }

    public Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default) =>
        DescribeAsync(connectionString, cancellationToken);

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> PreviewAsync(
        string connectionString,
        string tableName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 500);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        var source = new JobRowParquetSource(connectionString);
        await foreach (var row in source.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
            if (rows.Count >= take)
                break;
        }

        return rows;
    }

    public Task<bool> TableExistsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = File.Exists(connectionString) &&
                     Path.GetFileName(connectionString).Equals(tableName, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(exists);
    }

    private static async Task<IReadOnlyList<TableColumnInfo>> DescribeAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        await using var reader = await global::Parquet.ParquetReader.CreateAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return reader.Schema.Fields
            .Select(field => new TableColumnInfo { Name = field.Name, Type = field.GetType().Name })
            .ToArray();
    }
}
