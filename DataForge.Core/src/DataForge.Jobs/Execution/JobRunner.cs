using DataForge.Core.Core.Pipeline;
using DataForge.Core.Core.Models;
using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;
using DataForge.Jobs.Providers;
using DataForge.Jobs.Validation;

namespace DataForge.Jobs.Execution;

public sealed class JobRunner
{
    public async Task<JobRunResult> RunAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            if (!File.Exists(jobFilePath))
                return Fail(jobFilePath, startedAt, $"Job file not found: {jobFilePath}");

            var jobDirectory = Path.GetDirectoryName(Path.GetFullPath(jobFilePath)) ?? Directory.GetCurrentDirectory();
            var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
            var job = JobYamlParser.Parse(yaml);
            var vars = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            VariableResolver.Apply(job, vars);
            ResolvePaths(job, jobDirectory);

            var pipeline = BuildPipeline(job, vars);
            var results = await JobSinkWriter.WriteAsync(
                pipeline.AsAsyncEnumerable(cancellationToken),
                job.Sink,
                cancellationToken).ConfigureAwait(false);

            if (pipeline is DataPipeline<JobRow> dataPipeline &&
                !string.IsNullOrWhiteSpace(job.Validate?.BadRowOutput))
            {
                results = await dataPipeline
                    .FinalizeExportResultsAsync(results, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new JobRunResult
            {
                Success = true,
                JobName = job.Name ?? Path.GetFileNameWithoutExtension(jobFilePath),
                JobFilePath = Path.GetFullPath(jobFilePath),
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow,
                ExportResults = results
            };
        }
        catch (Exception ex)
        {
            return Fail(jobFilePath, startedAt, ex.Message);
        }
    }

    private static void ResolvePaths(JobDefinition job, string jobDirectory)
    {
        if (!string.IsNullOrWhiteSpace(job.Source.Path))
            job.Source.Path = VariableResolver.ResolvePath(job.Source.Path, jobDirectory);

        if (!string.IsNullOrWhiteSpace(job.Sink.Path))
            job.Sink.Path = VariableResolver.ResolvePath(job.Sink.Path, jobDirectory);

        if (job.Validate?.BadRowOutput != null)
            job.Validate.BadRowOutput = VariableResolver.ResolvePath(job.Validate.BadRowOutput, jobDirectory);
    }

    private static IDataPipeline<JobRow> BuildPipeline(
        JobDefinition job,
        IReadOnlyDictionary<string, string> variables)
    {
        var pipeline = CreateSource(job);

        foreach (var step in job.Transforms)
        {
            if (!string.IsNullOrWhiteSpace(step.Where))
            {
                var predicate = WhereExpression.Compile(step.Where, variables);
                pipeline = pipeline.Where(predicate);
            }

            if (step.Select is { Count: > 0 })
                pipeline = pipeline.Select(row => ProjectRow(row, step.Select));
        }

        if (job.Validate is { Rules.Count: > 0 })
        {
            pipeline = pipeline.ValidateWith(new JobRowValidator(job.Validate.Rules));

            pipeline = job.Validate.OnError.Equals("continue", StringComparison.OrdinalIgnoreCase)
                ? pipeline.ContinueOnValidationError()
                : pipeline.FailOnValidationError();

            if (!string.IsNullOrWhiteSpace(job.Validate.BadRowOutput))
                pipeline = pipeline.WithBadRowOutput(job.Validate.BadRowOutput);
        }

        return pipeline;
    }

    private static IDataPipeline<JobRow> CreateSource(JobDefinition job)
    {
        if (!JobProviderRegistry.TryGetSource(job.Source.Type, out var provider))
            throw new NotSupportedException(
                $"Source type '{job.Source.Type}' is not registered. Reference the matching DataForge.Jobs.* adapter and call its registration method.");

        return provider.Create(job);
    }

    private static JobRow ProjectRow(JobRow row, Dictionary<string, string> mapping)
    {
        var projected = new JobRow();
        foreach (var (targetField, sourceField) in mapping)
            projected.Values[targetField] = row.Get(sourceField);

        return projected;
    }

    private static JobRunResult Fail(string jobFilePath, DateTimeOffset startedAt, string message) =>
        new()
        {
            Success = false,
            JobFilePath = jobFilePath,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            ErrorMessage = message
        };
}
