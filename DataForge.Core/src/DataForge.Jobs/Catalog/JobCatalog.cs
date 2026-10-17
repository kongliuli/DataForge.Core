namespace DataForge.Jobs.Catalog;

public static class JobCatalog
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, IJobCatalogProvider> Providers = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(IJobCatalogProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (Gate)
        {
            foreach (var type in provider.ProviderTypes)
                Providers[type] = provider;
        }
    }

    public static bool TryGet(string type, out IJobCatalogProvider provider)
    {
        lock (Gate)
            return Providers.TryGetValue(type, out provider!);
    }

    public static IJobCatalogProvider Get(string type)
    {
        if (!TryGet(type, out var provider))
            throw new NotSupportedException($"No catalog provider registered for '{type}'.");
        return provider;
    }

    public static IReadOnlyList<string> RegisteredTypes
    {
        get
        {
            lock (Gate)
                return Providers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public static Task<ConnectionTestResult> TestConnectionAsync(string type, string connectionString, CancellationToken cancellationToken = default) =>
        Get(type).TestConnectionAsync(connectionString, cancellationToken);

    public static Task<IReadOnlyList<string>> ListTablesAsync(string type, string connectionString, CancellationToken cancellationToken = default) =>
        Get(type).ListTablesAsync(connectionString, cancellationToken);

    public static Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string type, string connectionString, string tableName, CancellationToken cancellationToken = default) =>
        Get(type).DescribeTableAsync(connectionString, tableName, cancellationToken);

    public static Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> PreviewAsync(
        string type,
        string connectionString,
        string tableName,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        Get(type).PreviewAsync(connectionString, tableName, limit, cancellationToken);

    public static Task<bool> TableExistsAsync(string type, string connectionString, string tableName, CancellationToken cancellationToken = default) =>
        Get(type).TableExistsAsync(connectionString, tableName, cancellationToken);
}
