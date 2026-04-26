# Parser Foundations Spec

> **Purpose:** Stand up the verification harness, reconcile the parser test-case schema with the harvester's tier outputs, and wire ANTLR4 build-time generation into the new `AccessHarvester.Vba.Grammar` project so every later parser child set has a working test loop on first commit.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/parser-foundations/`
> **Prerequisite:** docs/session-sets/accessexportertemp1-import/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #1. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `parser-foundations` |
| **Plan-phase coverage (column 3)** | Phase 0.2-0.4 (verification harness, schema reconciliation, ANTLR build wiring; project skeleton already done in `accessexportertemp1-import`; Phase 0.1 deferred per OQ 1(a)) |
| **Prereqs (column 4)** | `accessexportertemp1-import` committed |
| **Sessions (rough) (column 5)** | 3-4 |
| **Synthesis count (column 6)** | 1 (verification harness design) |
| **Outputs (column 7)** | `tools/parser-harness/`, schema reconciliation pass on `docs/test-cases/harvester-schema.v0.json`, new `AccessHarvester.Vba.Grammar/AccessHarvester.Vba.Grammar.csproj` with ANTLR4 build-task wired |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

This is the first parser-side child set in the VBA-parser tree. Implements **Phase 0.2–0.4** of `docs/VBA-Parser-Migration-Harvester-Plan.md` (verification harness, output-schema reconciliation, project setup and tooling). Phase 0.1 (corpus assembly) is deferred per Open Question 1(a) in `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` — module-level fixtures plus Northwind are sufficient through Phase 3, and database-level fixtures are forced into `access-metadata-extraction` (#10) where they are unavoidable.

The deliverables are: (1) a `tools/parser-harness/` runner that takes a parser binary and the `docs/test-cases/` corpus and reports parse success rate plus parse-tree statistics in human + JSON forms; (2) a reconciliation pass on `docs/test-cases/harvester-schema.v0.json` so it is consistent with the tier outputs `accessexportertemp1-import` produced; and (3) a new `AccessHarvester.Vba.Grammar/AccessHarvester.Vba.Grammar.csproj` with ANTLR4.Build.Tasks wired so a `Vba.g4` placeholder regenerates a parser on `dotnet build`.

This set does **not** author grammar, parser, or resolver code. Those are downstream child sets (`vba-lexical-and-expression-grammar` onward).

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #1 and must be referenced verbatim by future sessions.

---

## Feature 1: Verification Harness

### Scope

Build the harness Phase 0.2 of the plan calls for: a CLI runner that accepts a parser binary plus a corpus path. Output formats, diff mode, and performance mode are enumerated in Phase 0.2; Session 1 of this set picks the concrete shape. Lives under `tools/parser-harness/`.

### Standards

- Harness output is the regression-detection signal for every later parser child set; output shape is committed and downstream child sets reference it verbatim.
- Sample-corpus mode targets `docs/test-cases/` (the 12 module-level fixtures); database-level corpus support is deferred to `access-metadata-extraction` (#10).

## Feature 2: Schema Reconciliation

### Scope

Reconcile `docs/test-cases/harvester-schema.v0.json` against the `raw/` / `kb/` / `ir/` schemas produced by `accessexportertemp1-import`. The retrospective records the v0 parser schema and the tier schemas as intentionally independent for now (`project-guidance.md` → Output Architecture); the reconciliation pass narrows the gap to "named differences with rationale" rather than collapses them into one schema. A future child set (`schema-finalization-and-docs`, #17) finalizes v1.

### Standards

- The pass produces a delta document under `docs/schema/v0-vs-tiers.md` enumerating every field shape that differs between the parser schema and the tier schemas, with rationale.
- The v0 schema itself is touched only to fix outright errors discovered in reconciliation; structural changes wait for #17.

## Feature 3: ANTLR4 Build Wiring

### Scope

Create `AccessHarvester.Vba.Grammar/AccessHarvester.Vba.Grammar.csproj` with ANTLR4.Build.Tasks NuGet referenced. Drop a placeholder `Vba.g4` (single trivial rule) that regenerates a parser on `dotnet build`. Add the project to `AccessHarvester.sln`. Generated files are gitignored but build-reproducible.

### Standards

- Namespaces follow `project-guidance.md` → Code Style → `AccessHarvester.*`. The grammar project is `AccessHarvester.Vba.Grammar`.
- Build wiring is the contract. The placeholder grammar is intentionally trivial; lexical and expression rules land in `vba-lexical-and-expression-grammar` (#3).
- COM is not involved in this project; no `[SupportedOSPlatform("windows")]` gating.

---

## Session Plan

### Session 1 of 3-4: Verification Harness Design + Skeleton

- Read Phase 0.2 of the plan and `docs/test-cases/README.md`.
- Route an `architecture` design call for harness output shape (JSON schema for the machine-readable mode, diff-mode semantics, perf-mode metric set) — this is the synthesis session for the set per the tree (1 of 1).
- Land `tools/parser-harness/` with the CLI surface, JSON output schema, and a smoke test against `docs/test-cases/01-*.bas` (no parser yet → 0/0).

### Session 2 of 3-4: Schema Reconciliation Pass

- Read both the v0 parser schema and the tier output schemas committed by `accessexportertemp1-import`.
- Route `schema-validation` (mechanical-correspondence; pinned to Sonnet per the playbook) producing the delta document.
- Land `docs/schema/v0-vs-tiers.md`. No changes to the v0 schema itself unless the pass surfaces a real error.

### Session 3 of 3-4: ANTLR4 Build Wiring + Solution Update

- Create `AccessHarvester.Vba.Grammar.csproj` with `Antlr4.Runtime.Standard` + `Antlr4.Build.Tasks` references.
- Drop placeholder `Vba.g4`, add `Tests/AccessHarvester.Vba.Grammar.Tests` skeleton, wire into `AccessHarvester.sln`.
- Confirm `dotnet build` regenerates a parser; commit the generated-file gitignore entries.

### Session 4 of 3-4 (optional buffer): Verifier-Surfaced Cleanup

- Reserved for resolving any cross-provider verifier findings the prior sessions deferred (e.g., harness-JSON shape revisions, missing schema delta entries).

> Step lists above are bullet hints. The orchestrator running each session must elaborate them — including the mandatory Step 6 (build + test) and Step 8 (verification) per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Verification harness output design (Session 1):** `route(task_type="architecture", complexity_hint=70)`. Synthesis session; auto-verified cross-provider. `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`; first child set to invoke it logs actuals carefully so the playbook revision can update the cost band.
- **Schema reconciliation (Session 2):** `route(task_type="schema-validation", complexity_hint=40)`. Mechanical-correspondence; Sonnet-pinned per retrospective adopt-now `#10`. (Task type to be added by the routing-playbook child set #4 of `vba-parser-session-set-generator`.)
- **ANTLR build wiring (Session 3):** Mechanical; no routed reasoning expected.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)` per `task_type_overrides` (`gpt-5-4` for Claude orchestrators).
- **Change-log (last session):** `route(task_type="summarization")` with `max_tier=1` fallback if Gemini Pro returns 400 (per `project-guidance.md` Convention).

## Open questions for the human

None — Phase 0.1 deferral is resolved by the parent set's tree (OQ 1(a)); namespace reconciliation is resolved by the parent set's tree (OQ 2, default applied). Both decisions are recorded as inputs, not forks for this stub.
