using DataForge.Core.Core.Infrastructure;

namespace DataForge.Core.SqlServer;

public class SqlServerExportOptions
{
    public int BatchSize { get; set; } = 1000;
    public InsertMode InsertMode { get; set; } = InsertMode.Insert;
    public string[]? UpsertKeyColumns { get; set; }
    public bool UseTransaction { get; set; } = true;
}
