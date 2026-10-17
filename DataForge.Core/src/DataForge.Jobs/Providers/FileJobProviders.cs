using DataForge.Core.Core.Models;
using DataForge.Core.Core.Pipeline;
using DataForge.Core.Core.Sources.Options;
using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using DataForge.Jobs.Sources;

namespace DataForge.Jobs.Providers;

/// <summary>Built-in csv/json source and sink (no extra connector package).</summary>
public sealed class FileJobProviders : IJobSourceProvider, IJobSinkProvider
{
    public IReadOnlyList<string> SourceTypes { get; } = ["csv", "json"];

    public IReadOnlyList<string> SinkTypes { get; } = ["csv", "json"];

    public IDataPipeline<JobRow> Create(JobDefinition job)
    {
        return job.Source.Type.ToLowerInvariant() switch
        {
            "csv" => CreateCsv(job),
            "json" => CreateJson(job),
            _ => throw new NotSupportedException($"Source type '{job.Source.Type}' is not a file provider.")
        };
    }

    public Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default) =>
        FileSinkWriter.WriteAsync(rows, sink, cancellationToken);

    private static IDataPipeline<JobRow> CreateCsv(JobDefinition job)
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

    private static IDataPipeline<JobRow> CreateJson(JobDefinition job)
    {
        var source = new JobRowJsonSource(job.Source.Path);
        return new DataPipeline<JobRow>(source.ReadAsync());
    }
}
