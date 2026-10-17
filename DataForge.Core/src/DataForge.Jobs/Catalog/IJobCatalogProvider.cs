namespace DataForge.Jobs.Catalog;

/// <summary>
/// LocalDts-style connection/catalog APIs exposed via NuGet adapters.
/// </summary>
public interface IJobCatalogProvider
{
    IReadOnlyList<string> ProviderTypes { get; }

    Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> PreviewAsync(
        string connectionString,
        string tableName,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<bool> TableExistsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default);
}
