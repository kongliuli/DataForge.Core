using DataForge.Core.Core.Pipeline;
using DataForge.Jobs.Models;

namespace DataForge.Jobs.Providers;

/// <summary>
/// Creates a source pipeline for one or more YAML <c>source.type</c> values.
/// </summary>
public interface IJobSourceProvider
{
    IReadOnlyList<string> SourceTypes { get; }

    IDataPipeline<JobRow> Create(JobDefinition job);

    /// <summary>
    /// Optional source-side filter. Relational adapters push <c>column &gt; watermark</c> into SQL.
    /// Default keeps the pipeline unchanged so file sources stay client-filtered.
    /// </summary>
    IDataPipeline<JobRow> Create(JobDefinition job, string? watermark) => Create(job);
}
