using DataForge.Core.Core.Pipeline;
using DataForge.Core.Core.Models;
using DataForge.Jobs.Models;
using DataForge.Jobs.Parsing;
using DataForge.Jobs.Providers;
using DataForge.Jobs.Validation;
using System.Runtime.CompilerServices;

namespace DataForge.Jobs.Execution;

public sealed class JobRunner
{
    [ThreadStatic]
    private static string? _pendingWatermark;

    public async Task<JobRunResult> RunAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        IProgress<JobProgress>? progress = null)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            if (!File.Exists(jobFilePath))
                return Fail(jobFilePath, startedAt, $"Job file not found: {jobFilePath}");

            var yaml = await File.ReadAllTextAsync(jobFilePath, cancellationToken).ConfigureAwait(false);
            var job = JobYamlParser.Parse(yaml);
            return await RunAsync(job, jobFilePath, variables, cancellationToken, progress).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Fail(jobFilePath, startedAt, ex.Message);
        }
    }

    public async Task<JobRunResult> RunAsync(
        JobDefinition job,
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        IProgress<JobProgress>? progress = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var jobName = job.Name ?? Path.GetFileNameWithoutExtension(jobFilePath);
        try
        {
            progress?.Report(JobProgress.Started(jobName));

            var jobDirectory = Path.GetDirectoryName(Path.GetFullPath(jobFilePath)) ?? Directory.GetCurrentDirectory();
            var vars = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            VariableResolver.Apply(job, vars);
            ResolvePaths(job, jobDirectory);
            ApplyExecutionHints(job);

            var watermarkValue = job.Watermark is null
                ? null
                : await JobWatermark.ReadAsync(job.Watermark, cancellationToken).ConfigureAwait(false);
            string? nextWatermark = watermarkValue;
            _pendingWatermark = watermarkValue;

            ExportResults results;
            if (job.Sources.Count > 0)
            {
                var joined = ApplyTransforms(
                    WithProgress(JobJoin.ReadJoinedAsync(job, cancellationToken), jobName, job.Execution?.ProgressEvery ?? 1000, progress, cancellationToken),
                    job,
                    vars);
                joined = ApplyWatermark(joined, job.Watermark, watermarkValue, value => nextWatermark = value, cancellationToken);
                results = await JobSinkWriter.WriteAsync(joined, job.Sink, cancellationToken).ConfigureAwait(false);
                progress?.Report(JobProgress.Writing(jobName, results.RecordsWritten));
            }
            else
            {
                var pipeline = BuildPipeline(job, vars);
                var rows = WithProgress(
                    pipeline.AsAsyncEnumerable(cancellationToken),
                    jobName,
                    job.Execution?.ProgressEvery ?? 1000,
                    progress,
                    cancellationToken);
                rows = ApplyWatermark(rows, job.Watermark, watermarkValue, value => nextWatermark = value, cancellationToken);

                results = await JobSinkWriter.WriteAsync(rows, job.Sink, cancellationToken).ConfigureAwait(false);

                if (pipeline is DataPipeline<JobRow> dataPipeline &&
                    !string.IsNullOrWhiteSpace(job.Validate?.BadRowOutput))
                {
                    results = await dataPipeline
                        .FinalizeExportResultsAsync(results, cancellationToken)
                        .ConfigureAwait(false);
                }

                progress?.Report(JobProgress.Writing(jobName, results.RecordsWritten));
            }

            if (job.Watermark is not null &&
                !string.IsNullOrEmpty(nextWatermark) &&
                !string.Equals(nextWatermark, watermarkValue, StringComparison.Ordinal))
            {
                await JobWatermark.WriteAsync(job.Watermark, nextWatermark, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(JobProgress.Completed(jobName, results.RecordsWritten));

            return new JobRunResult
            {
                Success = true,
                JobName = jobName,
                JobFilePath = Path.GetFullPath(jobFilePath),
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.UtcNow,
                ExportResults = results
            };
        }
        catch (Exception ex)
        {
            progress?.Report(JobProgress.Failed(jobName, ex.Message));
            return Fail(jobFilePath, startedAt, ex.Message);
        }
    }

    private static void ApplyExecutionHints(JobDefinition job)
    {
        if (job.Execution?.BatchSize is int batchSize && batchSize > 0)
        {
            job.Sink.Options ??= new SinkOptionsDefinition();
            job.Sink.Options.BatchSize ??= batchSize;
        }
    }

    private static async IAsyncEnumerable<JobRow> WithProgress(
        IAsyncEnumerable<JobRow> rows,
        string? jobName,
        int every,
        IProgress<JobProgress>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long count = 0;
        var cadence = every <= 0 ? 0 : every;
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
            if (progress is not null && cadence > 0 && count % cadence == 0)
                progress.Report(JobProgress.Reading(jobName, count));
            yield return row;
        }

        if (progress is not null && count > 0)
            progress.Report(JobProgress.Reading(jobName, count));
    }

    private static void ResolvePaths(JobDefinition job, string jobDirectory)
    {
        if (!string.IsNullOrWhiteSpace(job.Source.Path))
            job.Source.Path = VariableResolver.ResolvePath(job.Source.Path, jobDirectory);

        foreach (var source in job.Sources)
        {
            if (!string.IsNullOrWhiteSpace(source.Path))
                source.Path = VariableResolver.ResolvePath(source.Path, jobDirectory);
        }

        if (!string.IsNullOrWhiteSpace(job.Sink.Path))
            job.Sink.Path = VariableResolver.ResolvePath(job.Sink.Path, jobDirectory);

        if (job.Validate?.BadRowOutput != null)
            job.Validate.BadRowOutput = VariableResolver.ResolvePath(job.Validate.BadRowOutput, jobDirectory);

        if (!string.IsNullOrWhiteSpace(job.Watermark?.StatePath))
            job.Watermark.StatePath = VariableResolver.ResolvePath(job.Watermark.StatePath, jobDirectory);
    }

    private static async IAsyncEnumerable<JobRow> ApplyWatermark(
        IAsyncEnumerable<JobRow> rows,
        WatermarkDefinition? watermark,
        string? current,
        Action<string?> remember,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var max = current;
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (watermark is null)
            {
                yield return row;
                continue;
            }

            var value = row.Get(watermark.Column);
            if (!JobWatermark.IsAfter(value, current))
                continue;

            max = JobWatermark.Max(max, value);
            remember(max);
            yield return row;
        }
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

    private static async IAsyncEnumerable<JobRow> ApplyTransforms(
        IAsyncEnumerable<JobRow> rows,
        JobDefinition job,
        IReadOnlyDictionary<string, string> variables)
    {
        var list = new List<JobRow>();
        await foreach (var row in rows.ConfigureAwait(false))
            list.Add(row);

        IEnumerable<JobRow> current = list;
        foreach (var step in job.Transforms)
        {
            if (!string.IsNullOrWhiteSpace(step.Where))
            {
                var predicate = WhereExpression.Compile(step.Where, variables);
                current = current.Where(predicate);
            }

            if (step.Select is { Count: > 0 })
            {
                var mapping = step.Select;
                current = current.Select(row => ProjectRow(row, mapping));
            }
        }

        foreach (var row in current)
            yield return row;
    }

    private static IDataPipeline<JobRow> CreateSource(JobDefinition job)
    {
        if (!JobProviderRegistry.TryGetSource(job.Source.Type, out var provider))
            throw new NotSupportedException(
                $"Source type '{job.Source.Type}' is not registered. Reference the matching DataForge.Jobs.* adapter and call its registration method.");

        return provider.Create(job, _pendingWatermark);
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
