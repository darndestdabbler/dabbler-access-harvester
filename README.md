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

- `AccessHarvester.Cli/` — Thin CLI exe. Subcommands: `extract`, `translate`, `run`, `generate-docs`, `validate`, `parse`.
- `AccessHarvester.Core/` — Extractors, translators, enrichers, COM session, model.
- `tests/AccessHarvester.Tests/` — xUnit hermetic unit tests + `[SkippableFact]` integration tests.
- `tools/` — Python developer utilities (`generate_property_defaults.py`, `validate_against_yaml.py`) and the per-session-set runner scripts under `tools/session-runner/`. The VS Code "Session Set Explorer" extension previously vendored here is now distributed from the canonical [`dabbler-ai-orchestration`](https://github.com/darndestdabbler/dabbler-ai-orchestration) repo (`tools/dabbler-ai-orchestration/dabbler-ai-orchestration-0.12.0.vsix`).
- `test-builder/` — Python synthetic-DB builder (pywin32 + Access COM).
- `samples/` — Committed sample Access inputs: `HomeHealthCare.accdb`, `MiniComplex.accdb`, `NorthwindDevEdition.accdb`, `ProjectBoard.accdb`, `FormAndReportDefaults.accdb`, `MasterDbPropertyExtractor.accdb`. Plus `FormsAndReportsYaml/` ground-truth dumps consumed by the Python tools.
- `docs/` — Workflow, planning, parser test cases, session sets.
- `ai_router/` — Operator-tuned `router-config.yaml` and the
  append-only `router-metrics.jsonl` cost telemetry. The router itself
  is `pip install dabbler-ai-router` (declared in `requirements.txt`);
  see CLAUDE.md → AI router import for the env-var setup.
- `requirements.txt` — Python dependency manifest. Currently lists
  `dabbler-ai-router>=0.1.0` only.

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

Downstream consumers install the CLI from the
[GitHub Releases page](https://github.com/darndestdabbler/dabbler-access-harvester/releases) —
each release ships a self-contained Windows binary
(`AccessHarvester.exe`, ~36 MB) inside a versioned zip bundle with a
matching `.sha256` sidecar. See
[the install guide](docs/harvester/install.md) for the download +
checksum-verification procedure and the smoke check. Maintainers can
also produce the same artifact locally with
`tools/publish/publish.ps1`. Run `AccessHarvester --help` for the
full usage text.

The pipeline-driving subcommands:

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

### `validate` — schema conformance check

```bash
AccessHarvester validate --in <output-dir>/
AccessHarvester validate --manifest <path-to-manifest.json>
```

Validates a `DatabaseManifest` JSON produced by `run` against the
harvester-schema embedded in the binary (both `v1.0` and `v1.1` are
embedded; `validate` dispatches on the manifest's declared
`schemaVersion` — current output is `v1.1`). `--in` locates
`manifest.json` recursively under the `run` output root. `--manifest`
validates a single specified file. Exit code 0 = conforms; 3 = schema
errors found (details on stderr). Pure file-to-file; no Access required.

### `generate-docs` — markdown schema summary

```bash
AccessHarvester generate-docs --in <manifest.json> --out <output-dir>/
```

Reads a `DatabaseManifest` JSON and writes a markdown schema summary to
`<output-dir>/<database-slug>/schema-summary.md`. Pure file-to-file.

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

The full docs index — every document in `docs/` with a one-line "what it
is and who reads it" — lives at [docs/README.md](docs/README.md). For a
downstream AI consumer interpreting harvester output, the entry point is
[the Interpretation Guide](docs/harvester/interpretation-guide.md). For
project goals, conventions, the session-set workflow, and history:

- [Interpretation Guide](docs/harvester/interpretation-guide.md) — how a downstream AI consumer reads a harvester run output
- [Project Status](docs/planning/project-status.md) — current status snapshot
- [Project Guidance](docs/planning/project-guidance.md) — durable principles and conventions
- [Lessons Learned](docs/planning/lessons-learned.md) — failure patterns and tactics
- [AI-Led Session Workflow](docs/ai-led-session-workflow.md) — single source of truth for the session-set procedure
