namespace DataForge.Core.DuckDB;

public sealed class DuckDbSourceOptions
{
    public int? MaxRows { get; set; }
}

public sealed class DuckDbExportOptions
{
    public int BatchSize { get; set; } = 1000;

    public bool CreateTableIfNotExists { get; set; } = true;

    public DuckDbInsertMode InsertMode { get; set; } = DuckDbInsertMode.Insert;

    public string[]? UpsertKeyColumns { get; set; }
}

public enum DuckDbInsertMode
{
    Insert,
    Upsert
}
