using System.Text.Json;
using DataForge.Jobs.Catalog;

namespace DataForge.Jobs.Http;

internal sealed class HttpCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["http", "rest"];

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return ConnectionTestResult.Fail("HTTP catalog connection must be an absolute http(s) URL.");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? ConnectionTestResult.Ok()
                : ConnectionTestResult.Fail($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail(ex.Message);
        }
    }

    public Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("HTTP catalog connection must be a URL.");

        var name = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
            ? "payload"
            : uri.AbsolutePath.Trim('/');
        return Task.FromResult<IReadOnlyList<string>>([name]);
    }

    public async Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        var rows = await PreviewAsync(connectionString, tableName, 1, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
            return [];

        return rows[0].Keys
            .Select(name => new TableColumnInfo { Name = name, Type = "json" })
            .ToArray();
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> PreviewAsync(
        string connectionString,
        string tableName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var response = await client.GetAsync(connectionString, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var take = Math.Clamp(limit, 1, 500);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in item.EnumerateObject())
                row[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
            rows.Add(row);
            if (rows.Count >= take)
                break;
        }

        return rows;
    }

    public Task<bool> TableExistsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out _))
            return Task.FromResult(false);

        return Task.FromResult(!string.IsNullOrWhiteSpace(tableName));
    }
}
