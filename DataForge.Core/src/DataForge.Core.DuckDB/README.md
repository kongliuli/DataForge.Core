# DataForge.Core.DuckDB

DuckDB query/import support for `DataForge.Core` (roadmap item A-02).

```csharp
using DataForge.Core;
using DataForge.Core.DuckDB;

var rows = await DataForgePipeline
    .FromDuckDb<Order>("analytics.duckdb", "SELECT * FROM orders WHERE amount > 100")
    .ToListAsync();

await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .ToDuckDbAsync("analytics.duckdb", "orders");
```

## Notes

- `FromDuckDb<T>(databasePath, sql, DuckDbSourceOptions?)` opens the database for the query.
- A missing database file is never created by a read; write paths create it.
- For YAML jobs use the `DataForge.Jobs.DuckDB` adapter package instead.
