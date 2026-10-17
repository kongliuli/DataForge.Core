using DataForge.Jobs.Execution;
using DataForge.Jobs.Parsing;

namespace DataForge.Web;

public static class JobWebRunner
{
    public static async Task<IReadOnlyList<JobRunResult>> RunYamlAsync(
        string yaml,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var jobs = JobYamlParser.ParseMany(yaml);
        var runner = new JobRunner();
        var results = new List<JobRunResult>(jobs.Count);
        const string virtualPath = "web.yaml";

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await runner.RunAsync(job, virtualPath, variables, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!result.Success)
                break;
        }

        return results;
    }
}
