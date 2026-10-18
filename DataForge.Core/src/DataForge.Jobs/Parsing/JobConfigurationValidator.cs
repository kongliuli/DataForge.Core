using DataForge.Jobs.Providers;

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
            var jobs = JobYamlParser.ParseFile(yaml).Jobs;
            for (var index = 0; index < jobs.Count; index++)
            {
                var job = jobs[index];
                var sources = job.Sources.Count > 0
                    ? job.Sources.Select(source => source.ToSource())
                    : [job.Source];
                foreach (var source in sources)
                    if (!JobProviderRegistry.TryGetSource(source.Type, out _))
                        return new(false, jobs.Count, $"Job {index + 1}: source adapter is not registered. Install and register the required Jobs adapter.");
                if (!JobProviderRegistry.TryGetSink(job.Sink.Type, out _))
                    return new(false, jobs.Count, $"Job {index + 1}: sink adapter is not registered. Install and register the required Jobs adapter.");
            }
            return new(true, jobs.Count, null);
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
        ["connectivity", "permissions", "type-compatibility", "data-reconciliation", "variable-resolution"];
}
