using ClosedXML.Excel;
using DataForge.Jobs.Catalog;

namespace DataForge.Jobs.Excel;

internal sealed class ExcelCatalogProvider : IJobCatalogProvider
{
    public IReadOnlyList<string> ProviderTypes { get; } = ["excel"];

    public Task<ConnectionTestResult> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString) || !File.Exists(connectionString))
            return Task.FromResult(ConnectionTestResult.Fail($"Excel file not found: {connectionString}"));

        try
        {
            using var _ = new XLWorkbook(connectionString);
            return Task.FromResult(ConnectionTestResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ConnectionTestResult.Fail(ex.Message));
        }
    }

    public Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook(connectionString);
        var names = workbook.Worksheets.Select(sheet => sheet.Name).ToArray();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public Task<IReadOnlyList<TableColumnInfo>> DescribeTableAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook(connectionString);
        var sheet = workbook.Worksheets.FirstOrDefault(item => item.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Worksheet '{tableName}' not found.");
        var range = sheet.RangeUsed();
        if (range == null)
            return Task.FromResult<IReadOnlyList<TableColumnInfo>>([]);

        var columns = new List<TableColumnInfo>();
        for (var col = 1; col <= range.ColumnCount(); col++)
        {
            var name = sheet.Cell(1, col).GetString();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Column{col}";
            columns.Add(new TableColumnInfo { Name = name, Type = "text" });
        }

        return Task.FromResult<IReadOnlyList<TableColumnInfo>>(columns);
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> PreviewAsync(
        string connectionString,
        string tableName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook(connectionString);
        var sheet = workbook.Worksheets.FirstOrDefault(item => item.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Worksheet '{tableName}' not found.");
        var range = sheet.RangeUsed();
        if (range == null)
            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>([]);

        var headers = new List<string>();
        for (var col = 1; col <= range.ColumnCount(); col++)
        {
            var name = sheet.Cell(1, col).GetString();
            headers.Add(string.IsNullOrWhiteSpace(name) ? $"Column{col}" : name);
        }

        var take = Math.Clamp(limit, 1, 500);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        var last = Math.Min(range.RowCount(), take + 1);
        for (var rowIndex = 2; rowIndex <= last; rowIndex++)
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < headers.Count; col++)
            {
                var text = sheet.Cell(rowIndex, col + 1).GetString();
                row[headers[col]] = string.IsNullOrEmpty(text) ? null : text;
            }

            rows.Add(row);
        }

        return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>(rows);
    }

    public Task<bool> TableExistsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(connectionString))
            return Task.FromResult(false);

        using var workbook = new XLWorkbook(connectionString);
        var exists = workbook.Worksheets.Any(item => item.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }
}
