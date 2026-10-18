namespace DataForge.Jobs.Parsing;

/// <summary>Checks YAML configuration without opening connections or executing jobs.</summary>
public static class JobConfigurationValidator
{
    public static JobConfigurationReport Validate(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return new(false, 0, "yaml is required");
        try
        {
            return new(true, JobYamlParser.ParseFile(yaml).Jobs.Count, null);
        }
        catch (Exception)
        {
            // Parser messages can contain input snippets and credentials.
            return new(false, 0, "Invalid job YAML or required configuration. Check source, sink and batch fields.");
        }
    }
}

/// <summary>Configuration validity is not a guarantee that migration will succeed.</summary>
public sealed record JobConfigurationReport(bool Valid, int JobCount, string? Error)
{
    public string Scope => "configuration-only";
    public IReadOnlyList<string> ChecksNotPerformed =>
        ["connectivity", "permissions", "type-compatibility", "data-reconciliation", "variable-resolution", "provider-registration"];
}
