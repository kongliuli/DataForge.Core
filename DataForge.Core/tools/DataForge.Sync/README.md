# DataForge.Sync

Monorepo CLI tool for YAML-driven data sync jobs (**DEC-03**).

## Status

**v0.3** — `dataforge run job.yaml` executes CSV/JSON source → `where` / `select` transforms → CSV/JSON sink.

Planned for later: SQL sinks, validators, cron scheduling (see [roadmap-and-iteration.md](../../docs/roadmap-and-iteration.md) §8.5).

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

- `${VAR}` in paths resolves `--var` values, then environment variables.
- `@var` in `where` expressions resolves `--var` values only.

## YAML schema (v0.3 subset)

```yaml
name: orders-sync
source:
  type: csv          # csv | json
  path: ./orders.csv
  options:
    hasHeader: true
transforms:
  - where: "Amount > 0"
  - where: "OrderDate >= @lastSync"
  - select:
      OrderId: OrderId
      Amount: Amount
sink:
  type: json         # json | csv
  path: ./out.json
  options:
    indented: true
```

## Install as global tool

```bash
dotnet pack tools/DataForge.Sync/DataForge.Sync.csproj -c Release -o ./nupkg
dotnet tool install --add-source ./nupkg DataForge.Sync
dataforge run examples/example-job.yaml
```
