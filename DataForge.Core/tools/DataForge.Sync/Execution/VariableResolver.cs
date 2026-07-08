using System.Text.RegularExpressions;
using DataForge.Sync.Models;

namespace DataForge.Sync.Execution;

public static partial class VariableResolver
{
    [GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex EnvPattern();

    public static IReadOnlyDictionary<string, string> MergeVariables(IEnumerable<KeyValuePair<string, string>> cliVariables)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in cliVariables)
            merged[pair.Key] = pair.Value;
        return merged;
    }

    public static JobDefinition Apply(JobDefinition job, IReadOnlyDictionary<string, string> variables)
    {
        job.Source.Path = ResolveText(job.Source.Path, variables);
        job.Sink.Path = ResolveText(job.Sink.Path, variables);

        foreach (var step in job.Transforms)
        {
            if (step.Where != null)
                step.Where = ResolveText(step.Where, variables);
        }

        if (job.Validate?.BadRowOutput != null)
            job.Validate.BadRowOutput = ResolveText(job.Validate.BadRowOutput, variables);

        return job;
    }

    public static string ResolveText(string text, IReadOnlyDictionary<string, string> variables)
    {
        return EnvPattern().Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            if (variables.TryGetValue(key, out var cliValue))
                return cliValue;

            var env = Environment.GetEnvironmentVariable(key);
            return env ?? match.Value;
        });
    }

    public static string ResolvePath(string path, string jobDirectory)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(jobDirectory, path));
    }
}
