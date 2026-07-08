using DataForge.Core.Core.Pipeline;
using DataForge.Core.Core.Sources.Options;
using DataForge.Core.Core.Models;
using DataForge.Sync.Models;
using DataForge.Sync.Parsing;
using DataForge.Sync.Sources;
using DataForge.Sync.Validation;

namespace DataForge.Sync.Execution;

public sealed class JobRunResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public ExportResults? ExportResults { get; init; }
}

public sealed class JobRunner
{
    public async Task<JobRunResult> RunAsync(
        string jobFilePath,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(jobFilePath))
                return Fail($"Job file not found: {jobFilePath}");

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

            return new JobRunResult { Success = true, ExportResults = results };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
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
        return job.Source.Type.ToLowerInvariant() switch
        {
            "csv" => CreateCsvSource(job),
            "json" => CreateJsonSource(job),
            "sqlserver" => CreateSqlServerSource(job),
            "parquet" => CreateParquetSource(job),
            "duckdb" => CreateDuckDbSource(job),
            _ => throw new NotSupportedException($"Source type '{job.Source.Type}' is not supported.")
        };
    }

    private static IDataPipeline<JobRow> CreateCsvSource(JobDefinition job)
    {
        var options = new CsvSourceOptions
        {
            HasHeaderRow = job.Source.Options?.HasHeader ?? true
        };

        if (job.Source.Options?.Delimiter is { } delimiter)
            options.Delimiter = delimiter;

        var source = new JobRowCsvSource(job.Source.Path, options);
        return new DataPipeline<JobRow>(source.ReadAsync());
    }

    private static IDataPipeline<JobRow> CreateJsonSource(JobDefinition job)
    {
        var source = new JobRowJsonSource(job.Source.Path);
        return new DataPipeline<JobRow>(source.ReadAsync());
    }

    private static IDataPipeline<JobRow> CreateParquetSource(JobDefinition job)
    {
        var source = new JobRowParquetSource(job.Source.Path);
        return new DataPipeline<JobRow>(source.ReadAsync());
    }

    private static IDataPipeline<JobRow> CreateDuckDbSource(JobDefinition job)
    {
        var source = new JobRowDuckDbSource(job.Source.Path, job.Source.Query!);
        return new DataPipeline<JobRow>(source.ReadAsync());
    }

    private static IDataPipeline<JobRow> CreateSqlServerSource(JobDefinition job)
    {
        var source = new JobRowSqlServerSource(job.Source.Connection!, job.Source.Table!);
        return new DataPipeline<JobRow>(source.ReadAsync());
    }

    private static JobRow ProjectRow(JobRow row, Dictionary<string, string> mapping)
    {
        var projected = new JobRow();
        foreach (var (targetField, sourceField) in mapping)
            projected.Values[targetField] = row.Get(sourceField);

        return projected;
    }

    private static JobRunResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
