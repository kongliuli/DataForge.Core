using System.Globalization;
using DataForge.Core.Core.Infrastructure;
using DataForge.Jobs.Models;

namespace DataForge.Jobs.Execution;

public static class JobWatermark
{
    public static async Task<string?> ReadAsync(WatermarkDefinition watermark, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(watermark.StatePath))
            return watermark.Initial;

        var text = (await File.ReadAllTextAsync(watermark.StatePath, cancellationToken).ConfigureAwait(false)).Trim();
        return string.IsNullOrEmpty(text) ? watermark.Initial : text;
    }

    public static Task WriteAsync(WatermarkDefinition watermark, string value, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(watermark.StatePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        return File.WriteAllTextAsync(watermark.StatePath, value, cancellationToken);
    }

    public static bool IsAfter(string? candidate, string? watermark)
    {
        if (string.IsNullOrEmpty(candidate))
            return false;
        if (string.IsNullOrEmpty(watermark))
            return true;

        if (decimal.TryParse(candidate, NumberStyles.Number, CultureInfo.InvariantCulture, out var left) &&
            decimal.TryParse(watermark, NumberStyles.Number, CultureInfo.InvariantCulture, out var right))
            return left > right;

        if (DateTimeOffset.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var leftTime) &&
            DateTimeOffset.TryParse(watermark, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rightTime))
            return leftTime > rightTime;

        return string.Compare(candidate, watermark, StringComparison.Ordinal) > 0;
    }

    public static string? Max(string? current, string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
            return current;
        if (string.IsNullOrEmpty(current))
            return candidate;
        return IsAfter(candidate, current) ? candidate : current;
    }

    /// <summary>
    /// Builds <c>SELECT * FROM table WHERE column &gt; @watermark</c> when a cursor exists.
    /// Column and table names are validated identifiers; the cursor value is a parameter.
    /// </summary>
    public static (string Sql, string ParameterName, string? Value) BuildSelect(
        string quotedTable,
        string column,
        string? watermark,
        Func<string, string> quote)
    {
        var quotedColumn = quote(SqlIdentifiers.Validate(column, nameof(column)));
        if (string.IsNullOrEmpty(watermark))
            return ($"SELECT * FROM {quotedTable}", "watermark", null);

        return ($"SELECT * FROM {quotedTable} WHERE {quotedColumn} > @watermark", "watermark", watermark);
    }
}
