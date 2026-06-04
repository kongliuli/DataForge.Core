using DataForge.Core.Core.Infrastructure;

namespace DataForge.Core.Sqlite;

public class SqliteExportOptions
{
    public int BatchSize { get; set; } = 1000;
    public InsertMode InsertMode { get; set; } = InsertMode.Insert;
    public string[]? UpsertKeyColumns { get; set; }
    public bool UseTransaction { get; set; } = true;
}
