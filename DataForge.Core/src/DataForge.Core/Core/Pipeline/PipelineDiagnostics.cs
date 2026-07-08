using DataForge.Core.Core.Models;

namespace DataForge.Core.Core.Pipeline;

internal sealed class PipelineDiagnostics
{
    private readonly List<RowError> _rowErrors = [];

    public IReadOnlyList<RowError> RowErrors => _rowErrors;

    public void Add(RowError error) => _rowErrors.Add(error);
}
