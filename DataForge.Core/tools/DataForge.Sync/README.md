# DataForge.Sync

Thin CLI host (`dataforge`) for YAML-driven jobs. Engine lives in **`DataForge.Jobs`** (model / parser / runner / providers).

## Status

**v0.5** — file + SQL Server / SQLite / PostgreSQL / Parquet / DuckDB pipelines, YAML validation, cron watch.

| Feature | Support |
|---------|---------|
| Source | `csv` · `json` · `sqlserver` · `sqlite` · `postgresql`/`postgres` · `parquet` · `duckdb` |
| Transforms | `where` · `select` |
| Validate | `rules` (required/min/max/pattern) · `onError` · `badRowOutput` |
| Sink | `csv` · `json` · `sqlserver` · `sqlite` · `postgresql`/`postgres` (`insert` / `upsert`) · `parquet` · `duckdb` |
| Schedule | 5-field cron via `dataforge watch` |

## Build

```bash
dotnet build tools/DataForge.Sync/DataForge.Sync.csproj
dotnet run --project tools/DataForge.Sync -- help
```

## Run an example job

```bash
cd tools/DataForge.Sync/examples
dotnet run --project .. -- run example-job.yaml
cat orders-clean.json
```

Cross-database example (SQLite → PostgreSQL):

```bash
# docker start dataforge-pg   # port 5433, user/db/password dataforge
dotnet run --project .. -- run sqlite-to-postgresql.yaml
```

With variables:

```bash
dataforge run job.yaml --var lastSync=2026-07-01
```

Scheduled watch (Ctrl+C to stop):

```bash
dataforge watch job.yaml
```

## YAML schema (v0.5)

```yaml
name: orders-sync
schedule: "0 2 * * *"          # optional; used by watch

source:
  type: sqlite                 # csv | json | sqlserver | sqlite | postgresql | parquet | duckdb
  connection: Data Source=./orders.db
  table: Orders
  # path: ./orders.csv         # for file sources
  # query: SELECT ...          # for duckdb

transforms:
  - where: "Amount > 0"
  - where: "OrderDate >= @lastSync"
  - select:
      OrderId: OrderId
      Amount: Amount

validate:
  onError: continue            # continue | fail
  badRowOutput: ./errors.ndjson
  rules:
    - field: OrderId
      required: true
    - field: Amount
      min: 0

sink:
  type: postgresql             # json | csv | sqlserver | sqlite | postgresql | parquet | duckdb
  connection: Host=127.0.0.1;Port=5433;Username=dataforge;Password=dataforge;Database=dataforge
  table: Fact_Orders
  mode: upsert                 # insert | upsert
  keys: [OrderId]
  # file sink:
  # path: ./out.json
```

Variables:

- `--var key=value`
- `${VAR}` in paths/connections — CLI vars, then environment
- `@var` in `where` — CLI vars only

## Install as global tool

```bash
dotnet pack tools/DataForge.Sync/DataForge.Sync.csproj -c Release -o ./nupkg
dotnet tool install --add-source ./nupkg DataForge.Sync
dataforge run examples/example-job.yaml
```
