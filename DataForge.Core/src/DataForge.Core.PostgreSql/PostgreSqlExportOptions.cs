using DataForge.Core.Core.Infrastructure;

namespace DataForge.Core.PostgreSql;

public class PostgreSqlExportOptions
{
    public int BatchSize { get; set; } = 1000;
    public InsertMode InsertMode { get; set; } = InsertMode.Insert;
    public string[]? UpsertKeyColumns { get; set; }
    public bool UseTransaction { get; set; } = true;
}
