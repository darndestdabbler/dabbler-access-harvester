# Spec Coverage Artifacts

This directory contains coverage artifacts for the VBA parser planning work.

## ms-vbal-coverage.csv

Coverage matrix produced by Session 2 of `vba-spec-study`. Maps each MS-VBAL grammar production to its scope and priority disposition.

**Columns:** Section, Construct, In_Corpus, Corpus_Fixtures, In_Scope, Priority, Difficulty, Notes

**Corpus scope (Session 2):** `In_Corpus` and `Corpus_Fixtures` reflect the **12 hand-curated text fixtures** under `docs/test-cases/` only. `NorthwindDevEdition.accdb` (committed by `accessexportertemp1-import`) was **not inspected** — it is a binary `.accdb` file whose VBA content requires Access COM to extract. Access COM inspection is Windows-only and out of scope for this documents-only session set. Northwind corpus cross-references are deferred to child set `#10 access-metadata-extraction`, which runs Access COM as part of its mandate.

**Priority levels:**
- `must` — parser will fail on real Access code without this construct
- `should` — important for completeness; present in some corpus or valid spec construct
- `can-defer` — rare, low risk; no corpus examples and not needed for migration-scope databases
- `wont` — explicitly out of scope (none assigned in v1)

**In_Corpus values:** Y = clearly present, N = not found in 12 fixtures, Partial = limited presence

## hazard-list.md

Produced by Session 3 of `vba-spec-study`. Concrete parsing hazards discovered from the 12 fixtures plus spec study. Each hazard points at specific syntax, MS-VBAL section, and parsing difficulty estimate. Drives edge-case fixture authoring in child set `#5 vba-grammar-validation`.
