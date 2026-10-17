using System.Text.RegularExpressions;

namespace DataForge.Core.Core.Infrastructure;

/// <summary>
/// Validates and quotes simple SQL identifiers (table / column / schema.table).
/// Rejects anything outside [A-Za-z_][A-Za-z0-9_]* per segment to reduce injection risk.
/// </summary>
public static partial class SqlIdentifiers
{
    private static readonly Regex SegmentPattern = CreateSegmentPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CreateSegmentPattern();

    public static string Validate(string identifier, string paramName = "identifier")
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("SQL identifier must not be empty.", paramName);
        }

        var trimmed = identifier.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length is < 1 or > 2)
        {
            throw new ArgumentException(
                $"SQL identifier '{identifier}' is invalid. Use name or schema.name with simple segments.",
                paramName);
        }

        foreach (var part in parts)
        {
            if (!SegmentPattern.IsMatch(part))
            {
                throw new ArgumentException(
                    $"SQL identifier segment '{part}' is invalid. Allowed: letters, digits, underscore; must not start with a digit.",
                    paramName);
            }
        }

        return trimmed;
    }

    public static string QuoteSqlServer(string identifier)
    {
        var validated = Validate(identifier);
        return string.Join('.', validated.Split('.').Select(static p => $"[{p.Replace("]", "]]", StringComparison.Ordinal)}]"));
    }

    public static string QuoteMySql(string identifier)
    {
        var validated = Validate(identifier);
        return string.Join('.', validated.Split('.').Select(static p => $"`{p.Replace("`", "``", StringComparison.Ordinal)}`"));
    }

    public static string QuoteSqlite(string identifier)
    {
        var validated = Validate(identifier);
        return string.Join('.', validated.Split('.').Select(static p => $"\"{p.Replace("\"", "\"\"", StringComparison.Ordinal)}\""));
    }
}
