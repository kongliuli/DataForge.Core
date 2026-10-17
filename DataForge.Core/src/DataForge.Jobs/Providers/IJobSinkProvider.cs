using DataForge.Core.Core.Models;
using DataForge.Jobs.Models;

namespace DataForge.Jobs.Providers;

/// <summary>
/// Writes rows for one or more YAML <c>sink.type</c> values.
/// </summary>
public interface IJobSinkProvider
{
    IReadOnlyList<string> SinkTypes { get; }

    Task<ExportResults> WriteAsync(
        IAsyncEnumerable<JobRow> rows,
        SinkDefinition sink,
        CancellationToken cancellationToken = default);
}
