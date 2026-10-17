using DataForge.Jobs.Execution;

namespace DataForge.Web;

public static class JobWebRunner
{
    public static Task<IReadOnlyList<JobRunResult>> RunYamlAsync(
        string yaml,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        IProgress<JobProgress>? progress = null) =>
        JobBatch.RunYamlAsync(yaml, "web.yaml", variables, cancellationToken, progress);
}
