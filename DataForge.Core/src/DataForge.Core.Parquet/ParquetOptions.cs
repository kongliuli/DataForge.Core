namespace DataForge.Core.Parquet;

public sealed class ParquetSourceOptions
{
    public int? MaxRows { get; set; }
}

public sealed class ParquetExportOptions
{
    public int RowGroupSize { get; set; } = 10_000;

    public bool Append { get; set; }
}
