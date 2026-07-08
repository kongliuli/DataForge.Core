# DataForge.Sync

Monorepo CLI tool for YAML-driven data sync jobs (**DEC-03**).

## Status

**v0.4** — full job lifecycle for file + SQL Server pipelines, YAML validation rules, and cron watch.

| Feature | Support |
|---------|---------|
| Source | `csv` · `json` · `sqlserver` |
| Transforms | `where` · `select` |
| Validate | `rules` (required/min/max/pattern) · `onError` · `badRowOutput` |
| Sink | `csv` · `json` · `sqlserver` (`insert` / `upsert`) |
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

With variables:

```bash
dataforge run job.yaml --var lastSync=2026-07-01
```

Scheduled watch (Ctrl+C to stop):

```bash
dataforge watch job.yaml
```

## YAML schema (v0.4)

```yaml
name: orders-sync
schedule: "0 2 * * *"          # optional; used by watch

source:
  type: csv                    # csv | json | sqlserver
  path: ./orders.csv
  # sqlserver:
  # connection: ${DB_CONN}
  # table: Orders

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
  type: sqlserver              # json | csv | sqlserver
  connection: ${DB_CONN}
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
