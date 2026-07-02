# LocalDts migration

Source repository: `kongliuli/LocalDts`
Source commit: `88570d479db728f60788db757f59f788474df7ce`
Target branch: `DataForge.Core/apps/local-dts`

## What moved

- `DataMigration.Contracts`, `DataMigration.Core`, console, WPF, Blazor, tests, plugin source projects, scripts, configs, and Markdown reference docs.
- Plugin source code for Redis, Mongo, Kafka, Azure Blob, Elasticsearch, REST API, DWG, geometry, filtering, aggregation, encryption, standardization, and type conversion.
- Test input files such as `test_data.csv`, `test_data.xlsx`, and `test_config.json`.

## What did not move

- Compiled plugin/runtime artifacts: `*.dll`, `*.pdb`, `*.deps.json`.
- Build outputs: `bin/`, `obj/`.
- Local sample databases: `data/*.db`.
- Generated automated-test output CSV files.
- Word reports (`*.docx`) where Markdown/reference copies already exist.

## Intended shape

`DataForge.Core` remains the core pipeline library. `apps/LocalDts` is a legacy application branch payload for continuing the LocalDts UI/plugin work and gradually moving reusable adapters into `src/DataForge.Core.*` packages.
