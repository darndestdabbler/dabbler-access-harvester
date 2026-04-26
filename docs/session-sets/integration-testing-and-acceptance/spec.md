# Integration Testing and Acceptance Spec

> **Purpose:** Consolidate unit + integration coverage, establish the regression suite from real databases, expand the `.accdb` corpus to the deferred Phase 0.1 target, hit performance acceptance criteria, and check off `docs/acceptance-criteria.md` so the harvester is production-ready.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/integration-testing-and-acceptance/`
> **Prerequisite:** docs/session-sets/schema-finalization-and-docs/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **This spec is a stub.** The orchestrator executing Session 1 of this set must elaborate the step lists before running them.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #18. This is the authoritative record; the prose below interprets these fields, never overrides them.

| Field | Value |
|---|---|
| **Slug (column 2)** | `integration-testing-and-acceptance` |
| **Plan-phase coverage (column 3)** | Phase 8.1-8.5 (unit consolidation, integration tests, regression suite, performance, acceptance criteria); expands `.accdb` corpus |
| **Prereqs (column 4)** | #17 |
| **Sessions (rough) (column 5)** | 4-5 |
| **Synthesis count (column 6)** | 1 (acceptance-criteria validation) |
| **Outputs (column 7)** | Regression-suite expansion under `tests/AccessHarvester.Integration/`, perf benchmarks, 5-10 additional `.accdb` files under `samples/` for the regression suite, `docs/acceptance-criteria.md` checked off |

Slug, phase coverage, prereqs, session count, and output paths above are the strings later sessions reference. Future orchestrators that need to grep for the prerequisite session sets follow the `Prerequisite:` frontmatter line at the top of this stub.

---

## Project Overview

Implements **Phase 8.1–8.5** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. This is the leaf of the dependency DAG — the tree's `#18`, prerequisite-chain depth 14. The work consolidates everything #1–#17 produced into a CI-runnable acceptance package: regression suite expansion, performance benchmarks, the deferred Phase 0.1 corpus expansion (5–10 additional real-migration-scope `.accdb` files added under `samples/` per the parent tree's OQ 1(a) decision; below the plan's 10–20 target but adequate for acceptance, with the full target deferred to post-v1 follow-up), and the `docs/acceptance-criteria.md` checkoff.

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md` row #18.

---

## Feature 1: Unit and Integration Test Consolidation

### Scope

Per Phase 8.1–8.2, comprehensive coverage:
- Each grammar production (positive + negative) — already largely covered by #3–#5
- Each resolver scenario (control reference, With block, default member, scope shadowing) — already covered by #7–#9
- Each metadata extraction case — already covered by #10
- Each enrichment classification — already covered by #14–#16

This set adds **end-to-end** integration tests under `tests/AccessHarvester.Integration/` that run the full pipeline against fixtures and assert structural properties of output. Unit tests reach 80%+ line coverage on core libraries.

### Standards

- Integration tests use `[SkippableFact]` per `project-guidance.md` → Platform And Runtime, so non-Windows CI stages skip cleanly.
- Coverage gaps surfaced here drive back-edits to earlier child sets, not workarounds.

## Feature 2: Regression Suite + Corpus Expansion

### Scope

Per Phase 8.3, capture every successfully processed database's output as a regression fixture. Periodic CI runs re-process all snapshot databases and diff outputs; unexpected diff fails CI.

Per the parent tree's OQ 1(a) deferred Phase 0.1 work: add **5–10** additional real-migration-scope `.accdb` files under `samples/` for the regression suite. Below the plan's 10–20 target; full target is post-v1 follow-up. Each new sample is paired with `expected.json` snapshot output.

### Standards

- Regression diffs are the contract; every change in output requires either a snapshot update with rationale or a fix.
- Sample databases must be free of sensitive data; per `project-guidance.md` → Output Architecture's privacy posture, output schema excludes data values, but the source `.accdb` should also be safe to commit.

## Feature 3: Performance Benchmarks

### Scope

Performance criteria per Phase 8.4 of the plan; the specific median-database parameters and elapsed-time/memory thresholds are Session 3 elaboration. Output: benchmark project under `tests/AccessHarvester.Integration/` with a CI failure mode tied to a recorded baseline.

### Standards

- Benchmarks are reproducible — fixed seed, fixed corpus, recorded baseline.
- "Acceptable regression" is a numeric threshold in CI config, not a judgement call per run.

## Feature 4: Acceptance Criteria Checkoff (Synthesis)

### Scope

Per Phase 8.5, produce `docs/acceptance-criteria.md` enumerating each acceptance criterion with status (met / not-met / waived-with-rationale): all v1-scope databases produce valid output; manual review queue size < 5% threshold; downstream Blazor migration tooling consumes output successfully on a pilot database; documentation complete; CI green; performance criteria met.

Single synthesis session for the set (per the tree's synthesis count = 1): the criterion-by-criterion judgement pass.

### Standards

- "Met" requires evidence — a CI badge, a regression-suite green run, a pilot-database integration log. Subjective sign-off without evidence is "waived-with-rationale", not "met".

---

## Session Plan

### Session 1 of 4-5: End-to-End Test Consolidation

- Audit unit + integration coverage from #1–#17. Identify gaps.
- Land end-to-end integration tests under `tests/AccessHarvester.Integration/`.
- Route `schema-validation` for coverage-vs-target correspondence.

### Session 2 of 4-5: Regression Suite + Corpus Expansion

- Add 5–10 real-migration-scope `.accdb` files under `samples/` (with sensitive-data scrub).
- Run pipeline against each; capture `expected.json` snapshots.
- Wire into CI as the regression suite.

### Session 3 of 4-5: Performance Benchmarks

- Land benchmark project + baseline measurements.
- Wire CI failure on regression > N% over baseline.

### Session 4 of 4-5: Acceptance Criteria Checkoff (Synthesis)

- Read every prior child set's change-log + this set's regression/perf results.
- Route an `architecture` synthesis call producing the criterion-by-criterion checkoff (1 of 1 synthesis session).
- Land `docs/acceptance-criteria.md`.

### Session 5 of 4-5: Verifier-Surfaced Cleanup + Final Change-Log

- Resolve any cross-provider verifier findings.
- Generate the change-log marking the v1 release.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes

- **Acceptance criteria checkoff (Session 4):** `route(task_type="architecture", complexity_hint=75)`. The single synthesis session — the criterion-by-criterion judgement pass. `architecture` is **uncalibrated against this repo's actuals** per retrospective adopt-now `#13`.
- **Test consolidation (Session 1) + regression + perf (Sessions 2–3):** Mechanical work; coverage audit may route `schema-validation` (mechanical-correspondence, Sonnet-pinned per retrospective adopt-now `#10`). Sample-DB scrub is mechanical.
- **Session-verification:** `route(task_type="session-verification", complexity_hint=70)`. Include the regression suite, perf baseline, and a sample of `acceptance-criteria.md`.
- **Change-log (Session 5):** `route(task_type="summarization")` with `max_tier=1` fallback. This is the **v1 release change-log** for the harvester as a whole, not just this set's deltas — ensure scope reflects the full tree's accomplishment.

## Open questions for the human

None.

## Default sample-DB sourcing assumption

The parent tree's OQ 1(a) defers Phase 0.1 corpus expansion to this set, targeting 5–10 real-migration-scope `.accdb` files. Default execution guidance for Session 2: any database in the migration scope whose business owner approves committing to the public repo qualifies; sensitive data is replaced with synthetic equivalents via `test-builder/`. If repo-policy approval is required and not yet obtained when Session 2 starts, that becomes a Session-1 elaboration concern, not a stub-level fork — the canonical tree should be amended before this stub introduces a new Open Question.
