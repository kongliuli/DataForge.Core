namespace DataForge.Jobs.Catalog;

public sealed class TableColumnInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsNullable { get; init; } = true;
    public bool IsPrimaryKey { get; init; }
}
