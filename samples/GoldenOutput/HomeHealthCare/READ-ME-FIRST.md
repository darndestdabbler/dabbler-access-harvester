# Dabbler Access Harvester output

This golden corpus has two entry points:

- `manifest.json` for the structural harvester output under `kb/` and `ir/`
- `reports-baseline/baseline-manifest.json` for the committed report aggregate baseline subset

This output conforms to `harvester-schema` version 1.1. The
manifest's `Documentation.Url` field points at the interpretation
guide so a binary-only consumer can read the schema without the
source repo. The manifest's `FileMap` field lists the on-disk
globs for every per-object file under `ir/`.

## AI consumers: read the workflow first

If you are an AI agent consuming this output, do NOT hand-derive an
object inventory. Read section 3.1 "The canonical consumer workflow"
in the interpretation guide first: after `run`, the `identify-usage`
and `identify-cleanup` subcommands already classify every object (with
confidence scores and clarifying questions) and flag stale ones,
writing `object-use.json` / `object-cleanup.json` plus
`*-checklist.json` review files for a developer to confirm. Point those
tools' `--in` at this per-database directory (the one that contains
`ir/`) — not the output root.

The `reports-baseline/` tier captures only the clean unattended
HomeHealthCare subset currently locked by regression:

- `srptClientBudgets`
- `srptClientCurrentNeeds`

Other HomeHealthCare reports may require unresolved runtime parameters and
are intentionally excluded from the committed golden baseline subset.
Use `reports-baseline/baseline-manifest.json` to discover the included
reports and each report's `aggregates.json` path.

The committed `reports-baseline/` sibling captures deterministic
report `RecordSource` result-set aggregates for the canonical
unattended report subset in this fixture. Use
`reports-baseline/baseline-manifest.json` as the index and each
`<report>/aggregates.json` as the parity artifact for the report's
underlying query results, not for rendered footer expressions or PDF
layout. For HomeHealthCare, the committed subset is
`srptClientBudgets` and `srptClientCurrentNeeds`.

Interpretation guide:
https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md
