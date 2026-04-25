# Dabbler Access Harvester

Dabbler Access Harvester is a C# / .NET 10 toolchain that harvests metadata
from Microsoft Access databases (`.accdb`, `.mdb`). It captures the
structural details of forms, reports, queries, macros, ribbons, tables,
and modules into structured JSON at sufficient fidelity that a downstream
AI consumer can interpret and document the database. Output is produced
across three processing tiers: `raw/` (Access-native artifacts), `kb/`
(clean parsed JSON), and `ir/` (`kb/` plus mechanical enrichments such as
geometry, label resolution, and property decoding).

There is no UI surface in this repo. The end-of-session quality gate is
automated build/test plus mandatory cross-provider AI verification.

## Repository Layout

- `AccessHarvester.Cli/` — Thin CLI exe. Subcommands: `extract`, `translate`, `run`.
- `AccessHarvester.Core/` — Extractors, translators, enrichers, COM session, model.
- `tests/AccessHarvester.Tests/` — xUnit hermetic unit tests + `[SkippableFact]` integration tests.
- `tools/` — Python developer utilities (`generate_property_defaults.py`, `validate_against_yaml.py`).
- `test-builder/` — Python synthetic-DB builder (pywin32 + Access COM).
- `samples/` — Committed sample Access inputs: `HomeHealthCare.accdb`, `MiniComplex.accdb`, `NorthwindDevEdition.accdb`, `ProjectBoard.accdb`, `FormAndReportDefaults.accdb`, `MasterDbPropertyExtractor.accdb`. Plus `FormsAndReportsYaml/` ground-truth dumps consumed by the Python tools.
- `docs/` — Workflow, planning, parser test cases, session sets.
- `ai-router/` — Router code, prompt templates, workflow utilities.

## Build & Test

```bash
dotnet build
dotnet test
```

Run **sequentially**, not in parallel, against the same worktree on
Windows — file-lock contention obscures real failures.

The hermetic unit-test suite (parser, enrichers, schema validators) runs
on any platform. Integration tests against committed `.accdb` fixtures
use `[SkippableFact]` and skip cleanly on non-Windows or where Access is
unavailable.

## CLI Usage

The CLI exposes three subcommands:

### `extract` — produces the `raw/` tier

```bash
AccessHarvester extract --db <path>.accdb --out <output-dir>/
```

Harvests Access-native artifacts (SaveAsText output, DAO JSON dumps) into
`<output-dir>/raw/`. Requires Windows + Microsoft Access (COM-based).

### `translate` — produces the `kb/` tier

```bash
AccessHarvester translate --in <raw-dir>/ --out <output-dir>/
```

Parses the `raw/` artifacts into clean JSON (AccessNode tree for
forms/reports, pass-through for queries/tables/relationships). Pure
file-to-file; runs on any platform.

### `run` — full pipeline

```bash
AccessHarvester run --db <path>.accdb --out <output-dir>/
```

Runs `extract`, then `translate`, then enrichment, producing `raw/`,
`kb/`, and `ir/` tiers in `<output-dir>/`. Requires Windows + Access.

## Constraints

- **Windows + Microsoft Access required** for the COM-based extractor
  (`extract` and `run` subcommands). The `AccessHarvester.Core` assembly
  is gated `[SupportedOSPlatform("windows")]`.
- **Hermetic tests run anywhere**; integration tests against committed
  `.accdb` fixtures are gated `[SkippableFact]` and skip on non-Windows
  or when Access is absent.
- **Interpretive slots remain null at the `ir/` tier.** Fields like
  `purpose`, `notes`, and `intent` are reserved for the AI consumer to
  fill downstream. The harvester does not guess at semantics.

## Planning & Documentation

For project goals, conventions, the session-set workflow, and history:

- [Project Status](docs/planning/project-status.md) — current status snapshot
- [Project Guidance](docs/planning/project-guidance.md) — durable principles and conventions
- [Lessons Learned](docs/planning/lessons-learned.md) — failure patterns and tactics
- [AI-Led Session Workflow](docs/ai-led-session-workflow.md) — single source of truth for the session-set procedure
