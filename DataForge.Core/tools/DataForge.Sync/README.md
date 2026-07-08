# DataForge.Sync

Monorepo CLI tool for YAML-driven data sync jobs (**DEC-03**).

## Status

Scaffold only. Job execution engine is planned for v1.0 — see [roadmap-and-iteration.md](../../docs/roadmap-and-iteration.md).

## Build

```bash
dotnet build tools/DataForge.Sync/DataForge.Sync.csproj
dotnet run --project tools/DataForge.Sync -- help
```

## Planned usage

```bash
dotnet tool install --add-source ./nupkg DataForge.Sync
dataforge run examples/example-job.yaml
```
