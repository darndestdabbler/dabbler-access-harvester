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
`<report>/aggregates.json` as the parity artifact. For HomeHealthCare,
the committed subset is `srptClientBudgets` and
`srptClientCurrentNeeds`.

Each `aggregates.json` now carries two parity signals: `GrandTotals` /
`GroupLevels` (the `RecordSource` query result-set aggregates) and
`DisplayTotals` — the **number a human reads in the report footer**,
computed for the supported closed set of footer `Sum` expressions.
These two sub-reports have no derivable report-footer `Sum` control, so
their `DisplayTotals` array is empty; footer controls that are not a
pure function of the flat result set are recorded in `Skipped` with a
grounded reason, never silently dropped. See interpretation-guide §5.2
for the full contract.

The optional, non-blocking PDF visual spot-check (`--include-pdf`) is
written to the SEPARATE, non-gated `reports-baseline-spot-check/`
sibling — never inside this byte-diffed corpus. It is for human visual
review of layout/format/grouping only and is not committed here nor
part of any regression gate.

Interpretation guide:
https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/interpretation-guide.md

## Developer review: opening the checklists

The `*-checklist.json` files are the human-review surface. When you present
them, do NOT hand over raw JSON — tell the developer what each file is, what
to do, and which zero-install editor opens it (interpretation guide section
3.3 is the mandatory instruction):

- `object-use-checklist.json` and `object-cleanup-checklist.json` open in the
  uat-checklist-editor (hosted, zero-install):
  https://darndestdabbler.github.io/uat-checklist-editor/
- `classification.json` (from `classify` / `sanitize`) opens in the
  classification-editor — not hosted yet; the developer downloads and opens
  `tools/classification-editor.html` locally.

Plain-language developer guide for the review step:
https://github.com/darndestdabbler/dabbler-access-harvester/blob/main/docs/harvester/reviewing-the-feedback-artifacts.md
