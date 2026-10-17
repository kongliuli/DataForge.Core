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
}
