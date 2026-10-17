using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;

namespace DataForge.Jobs.Execution;

public static class JobBatch
{
    public static async Task<IReadOnlyList<JobRunResult>> RunFileAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jobFilePath))
            return [Fail(jobFilePath, $"Job file not found: {jobFilePath}")];

        var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
        var jobs = JobYamlParser.ParseMany(yaml);
        var runner = new JobRunner();
        var results = new List<JobRunResult>(jobs.Count);

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await runner.RunAsync(job, jobFilePath, variables, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!result.Success)
                break;
        }

        return results;
    }

    private static JobRunResult Fail(string path, string message) => new()
    {
        Success = false,
        JobFilePath = path,
        ErrorMessage = message,
        StartedAt = DateTimeOffset.UtcNow,
        FinishedAt = DateTimeOffset.UtcNow
    };
}
