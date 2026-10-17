using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;

namespace DataForge.Jobs.Execution;

public static class JobBatch
{
    public static async Task<IReadOnlyList<JobRunResult>> RunFileAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        IProgress<JobProgress>? progress = null)
    {
        if (!File.Exists(jobFilePath))
            return [Fail(jobFilePath, $"Job file not found: {jobFilePath}")];

        var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
        return await RunYamlAsync(yaml, jobFilePath, variables, cancellationToken, progress).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<JobRunResult>> RunYamlAsync(
        string yaml,
        string jobFilePath = "web.yaml",
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        IProgress<JobProgress>? progress = null)
    {
        var file = JobYamlParser.ParseFile(yaml);
        var jobs = file.Jobs;
        var options = file.Execution ?? jobs.FirstOrDefault()?.Execution;
        var stopOnError = options?.StopOnError ?? true;
        var parallelism = Math.Max(1, options?.Parallelism ?? 1);
        var runner = new JobRunner();

        if (parallelism <= 1 || jobs.Count <= 1)
        {
            var sequential = new List<JobRunResult>(jobs.Count);
            for (var i = 0; i < jobs.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var job = jobs[i];
                progress?.Report(JobProgress.Batch(job.Name, i + 1, jobs.Count, "batch-started"));
                var result = await runner.RunAsync(job, jobFilePath, variables, cancellationToken, progress).ConfigureAwait(false);
                sequential.Add(result);
                progress?.Report(JobProgress.Batch(job.Name, i + 1, jobs.Count, result.Success ? "batch-completed" : "batch-failed"));
                if (!result.Success && stopOnError)
                    break;
            }

            return sequential;
        }

        var gate = new SemaphoreSlim(parallelism);
        var tasks = new List<Task<(int Index, JobRunResult Result)>>(jobs.Count);
        for (var i = 0; i < jobs.Count; i++)
        {
            var index = i;
            var job = jobs[i];
            tasks.Add(RunOneAsync(runner, job, index, jobs.Count, jobFilePath, variables, gate, progress, cancellationToken));
        }

        var finished = await Task.WhenAll(tasks).ConfigureAwait(false);
        return finished.OrderBy(item => item.Index).Select(item => item.Result).ToArray();
    }

    private static async Task<(int Index, JobRunResult Result)> RunOneAsync(
        JobRunner runner,
        JobDefinition job,
        int index,
        int count,
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables,
        SemaphoreSlim gate,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progress?.Report(JobProgress.Batch(job.Name, index + 1, count, "batch-started"));
            var result = await runner.RunAsync(job, jobFilePath, variables, cancellationToken, progress).ConfigureAwait(false);
            progress?.Report(JobProgress.Batch(job.Name, index + 1, count, result.Success ? "batch-completed" : "batch-failed"));
            return (index, result);
        }
        finally
        {
            gate.Release();
        }
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
