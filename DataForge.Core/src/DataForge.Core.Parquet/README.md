# DataForge.Core.Parquet

Parquet read/write support for `DataForge.Core` (roadmap item A-01), built on Parquet.Net.

```csharp
using DataForge.Core;
using DataForge.Core.Parquet;

var rows = await DataForgePipeline
    .FromParquet<Order>("orders.parquet")
    .ToListAsync();

await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ToParquetAsync("orders.parquet");
```

## Notes

- `FromParquet<T>(filePath, ParquetSourceOptions?)` reads a whole file into memory and deserializes it;
  row-group streaming is not implemented yet.
- `ParquetSourceOptions` / `ParquetExportOptions` control column naming and compression.
- For YAML jobs use the `DataForge.Jobs.Parquet` adapter package instead.
