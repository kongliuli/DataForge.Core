using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length < 2 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return args.Length == 0 ? 1 : 0;
}

try
{
    var type = args[0].ToLowerInvariant();
    var path = args[1];
    var maxRows = ParseMaxRows(args);

    var rules = type switch
    {
        "csv" => SchemaInferEngine.InferFromCsv(path, maxRows),
        "json" => SchemaInferEngine.InferFromJson(path, maxRows),
        _ => throw new InvalidOperationException($"Unsupported type '{type}'. Use csv or json.")
    };

    Console.WriteLine("# Generated validate.rules — paste into job YAML");
    Console.WriteLine("validate:");
    Console.WriteLine("  onError: continue");
    Console.WriteLine("  rules:");
    foreach (var rule in rules)
        Console.WriteLine($"    - field: {rule.Field}{rule.ToYamlSuffix()}");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int ParseMaxRows(string[] args)
{
    for (var i = 2; i < args.Length; i++)
    {
        if (args[i] == "--rows" && i + 1 < args.Length &&
            int.TryParse(args[++i], out var rows))
            return rows;
    }

    return 100;
}

static void PrintHelp()
{
    Console.WriteLine("""
        DataForge.SchemaInfer (A-03)

        Usage:
          schema-infer csv <file.csv> [--rows 100]
          schema-infer json <file.json> [--rows 100]

        Outputs YAML validate.rules block for DataForge.Sync jobs.
        """);
}

public static class SchemaInferEngine
{
    public static List<InferredRule> InferFromCsv(string path, int maxRows)
    {
        using var reader = new StreamReader(path);
        var headerLine = reader.ReadLine()
            ?? throw new InvalidOperationException("CSV file is empty.");

        var headers = headerLine.Split(',');
        var samples = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headers)
            samples[h.Trim()] = [];

        var rowCount = 0;
        string? line;
        while ((line = reader.ReadLine()) != null && rowCount < maxRows)
        {
            var values = line.Split(',');
            for (var i = 0; i < headers.Length && i < values.Length; i++)
                samples[headers[i].Trim()].Add(values[i].Trim());
            rowCount++;
        }

        return InferRules(samples);
    }

    public static List<InferredRule> InferFromJson(string path, int maxRows)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var samples = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var count = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (count++ >= maxRows)
                    break;
                CollectObject(element, samples);
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            CollectObject(doc.RootElement, samples);
        }

        return InferRules(samples);
    }

    private static void CollectObject(JsonElement element, Dictionary<string, List<string>> samples)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (!samples.ContainsKey(prop.Name))
                samples[prop.Name] = [];
            samples[prop.Name].Add(prop.Value.ValueKind switch
            {
                JsonValueKind.Null => "",
                JsonValueKind.String => prop.Value.GetString() ?? "",
                _ => prop.Value.GetRawText()
            });
        }
    }

    private static List<InferredRule> InferRules(Dictionary<string, List<string>> samples)
    {
        var rules = new List<InferredRule>();
        foreach (var (field, values) in samples.OrderBy(static k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var nonEmpty = values.Where(static v => !string.IsNullOrWhiteSpace(v)).ToList();
            var rule = new InferredRule { Field = field };

            if (nonEmpty.Count == values.Count && values.Count > 0)
                rule.Required = true;

            if (nonEmpty.All(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
            {
                var nums = nonEmpty
                    .Select(v => decimal.Parse(v, CultureInfo.InvariantCulture))
                    .ToList();
                rule.Min = nums.Min();
                rule.Max = nums.Max();
            }
            else if (nonEmpty.All(v => Regex.IsMatch(v, @"^\d{4}-\d{2}-\d{2}")))
            {
                rule.Pattern = @"^\d{4}-\d{2}-\d{2}$";
            }

            rules.Add(rule);
        }

        return rules;
    }
}

public sealed class InferredRule
{
    public string Field { get; set; } = "";
    public bool Required { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public string? Pattern { get; set; }

    public string ToYamlSuffix()
    {
        var parts = new List<string>();
        if (Required)
            parts.Add("\n      required: true");
        if (Min is { } min)
            parts.Add($"\n      min: {min.ToString(CultureInfo.InvariantCulture)}");
        if (Max is { } max && Max != Min)
            parts.Add($"\n      max: {max.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(Pattern))
            parts.Add($"\n      pattern: \"{Pattern}\"");
        return string.Concat(parts);
    }
}
