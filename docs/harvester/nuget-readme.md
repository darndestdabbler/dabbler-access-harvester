# Dabbler Access Harvester (`access-harvester`)

A .NET 10 CLI that harvests Microsoft Access (`.accdb` / `.mdb`) metadata —
forms, reports, queries, macros, ribbons, tables, and modules — into
structured JSON that a downstream AI can interpret and document. It also
parses VBA modules and Access expressions (validation rules, query SQL,
calc expressions) into structured trees.

This package installs the harvester as a **`dotnet tool`** exposing the
command **`access-harvester`**.

## Install

```bash
# install (requires the .NET 10 SDK — `dotnet tool` is an SDK command):
dotnet tool install --global Dabbler.AccessHarvester.Tool

# update / pin:
dotnet tool update  --global Dabbler.AccessHarvester.Tool
dotnet tool install --global Dabbler.AccessHarvester.Tool --version 1.2.0
```

Running the installed tool needs only the .NET 10 **runtime**.

## Use

```bash
access-harvester --version
access-harvester --help

# file-to-file — run anywhere the .NET 10 runtime does:
access-harvester parse <source-file>                 # parse a VBA module / expression
access-harvester validate --manifest <manifest.json> # validate against the embedded schema
access-harvester translate --in <raw-dir>  --out <kb-dir>
access-harvester generate-docs --in <manifest.json> --out <output-dir>
access-harvester generate-migration --in <harvested-root> --out <output-dir>

# COM-based — Windows + Microsoft Access required:
access-harvester extract --db <file.accdb> --out <output-root-dir>
access-harvester run     --db <file.accdb> --out <output-root-dir>
```

## Host requirements

| Subcommands | Requirement |
|---|---|
| `extract` / `run` / `report-baseline` | **Windows + Microsoft Access** (COM/DAO interop) |
| `parse` / `translate` / `generate-docs` / `validate` / `generate-migration` | Any host with the .NET 10 runtime |

The COM-based extraction path is Windows + Access regardless of how the
tool is acquired; the distribution channel does not change that.

## Alternative channel

A self-contained, single-file Windows binary (carries its own .NET
runtime — no SDK/runtime install required) ships on the GitHub Releases
page for hosts without .NET installed.

## Links

- Source & releases: <https://github.com/darndestdabbler/dabbler-access-harvester>
- Full install + verification guide (`docs/harvester/install.md`) and the
  downstream-consumer interpretation guide live in the source repository.
