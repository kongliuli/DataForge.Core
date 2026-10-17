using System.Globalization;
using System.Text.RegularExpressions;

namespace DataForge.Sync.Execution;

public static partial class WhereExpression
{
    [GeneratedRegex(
        @"^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>>=|<=|==|!=|>|<)\s*(?<value>.+)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PredicatePattern();

    public static Func<JobRow, bool> Compile(string expression, IReadOnlyDictionary<string, string> variables)
    {
        var match = PredicatePattern().Match(expression);
        if (!match.Success)
            throw new InvalidOperationException($"Unsupported where expression: '{expression}'");

        var field = match.Groups["field"].Value;
        var op = match.Groups["op"].Value;
        var rawValue = match.Groups["value"].Value.Trim();

        var compareValue = ResolveCompareValue(rawValue, variables);

        return row =>
        {
            var left = row.Get(field);
            return Compare(left, compareValue, op);
        };
    }

    private static string ResolveCompareValue(string rawValue, IReadOnlyDictionary<string, string> variables)
    {
        if (rawValue.StartsWith('@'))
        {
            var key = rawValue[1..];
            if (variables.TryGetValue(key, out var value))
                return value;

            throw new InvalidOperationException($"Variable '@{key}' is not defined.");
        }

        if (rawValue.Length >= 2 &&
            ((rawValue.StartsWith('"') && rawValue.EndsWith('"')) ||
             (rawValue.StartsWith('\'') && rawValue.EndsWith('\''))))
        {
            return rawValue[1..^1];
        }

        return rawValue;
    }

    private static bool Compare(string? left, string right, string op)
    {
        if (TryParseDecimal(left, out var leftNum) && TryParseDecimal(right, out var rightNum))
            return CompareNumbers(leftNum, rightNum, op);

        var leftText = left ?? string.Empty;
        return op switch
        {
            "==" => string.Equals(leftText, right, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(leftText, right, StringComparison.OrdinalIgnoreCase),
            ">" => string.Compare(leftText, right, StringComparison.OrdinalIgnoreCase) > 0,
            ">=" => string.Compare(leftText, right, StringComparison.OrdinalIgnoreCase) >= 0,
            "<" => string.Compare(leftText, right, StringComparison.OrdinalIgnoreCase) < 0,
            "<=" => string.Compare(leftText, right, StringComparison.OrdinalIgnoreCase) <= 0,
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };
    }

    private static bool CompareNumbers(decimal left, decimal right, string op) =>
        op switch
        {
            "==" => left == right,
            "!=" => left != right,
            ">" => left > right,
            ">=" => left >= right,
            "<" => left < right,
            "<=" => left <= right,
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };

    private static bool TryParseDecimal(string? value, out decimal number)
    {
        number = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }
}
