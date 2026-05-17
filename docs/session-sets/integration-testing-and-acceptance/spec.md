# Integration Testing and Acceptance Spec

> **Purpose:** Consolidate unit + integration coverage, establish the regression suite from real databases, expand the `.accdb` corpus to the deferred Phase 0.1 target, hit performance acceptance criteria, and check off `docs/acceptance-criteria.md` so the harvester is production-ready.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/integration-testing-and-acceptance/`
> **Prerequisite:** docs/session-sets/schema-finalization-and-docs/
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

> **Status:** elaborated. Session 1 closed VERIFIED on 2026-04-28; Sessions 2–5 reframed mid-set (see *Reframing Note* below) to deliver an AI-comprehension test bench in place of the original corpus-expansion + perf-benchmark scope.

---

## Canonical tree row

> Copied verbatim from `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #18. This is the authoritative record; the prose below interprets these fields, never overrides them.

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

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: this set delivers internal test infrastructure (an AI-comprehension test bench) and the v1 acceptance synthesis. There is no UI surface in the repo, so `requiresUAT: false`. The bench itself **is** our user-visible-behavior coverage layer — the test bench *is* the E2E story for "downstream AI can interpret harvester output," so `requiresE2E: false` (no separate E2E suite to gate against).

---

## Reframing Note (2026-04-28, mid-set)

This spec was authored 2026-04-25 with Sessions 2–5 sized for corpus expansion + snapshot regression + perf benchmarks + acceptance synthesis. After Session 1 closed VERIFIED, the human reframed the testing approach:

- **Original test target:** snapshot equivalence between parser output and committed `*.expected.json` baselines.
- **Reframed test target:** *AI comprehension* of harvester output — does an AI engine, fed the IR, correctly answer questions about the source database's intended behavior? This is the harvester's stated purpose per `docs/planning/project-guidance.md` → Project Scope And Audience, so it deserves a direct test rather than a derivative one.

What changes:

- **Corpus expansion (former Session 2)** is descoped. `samples/NorthwindDevEdition.accdb` and `samples/HomeHealthCare.accdb` are sufficient gold-standard inputs; we do not add 5–10 new real-migration-scope DBs.
- **Performance benchmarks (former Session 3)** are descoped to a post-v1 follow-up set (`performance-benchmarks-and-baseline`, recommended at the end of this set).
- **Sessions 2–5** are rewritten below to deliver the AI-comprehension test bench (Layer C), curate the question banks, run a baseline, do the acceptance synthesis with Layer C as primary evidence, and ship the v1 change-log.
- **Existing snapshot regression (`SyntheticFixtureSnapshotTests` + `VbaCorpusParserTests`)** stays as Layer A — it catches structural regressions cheaply.

A Rubberduck-as-oracle layer (Layer B) was considered and explicitly skipped (YAGNI) — Layer C tests the user's actual goal directly, and a Rubberduck oracle would only be useful as a debug aid when Layer C fails. If that need ever materializes, build the oracle then.

The change-log for this set will document the descope rationale alongside the v1 acceptance summary.

---

## Project Overview

Implements **Phase 8.1–8.5** of `docs/VBA-Parser-Migration-Harvester-Plan.md`. This is the leaf of the dependency DAG — the tree's `#18`, prerequisite-chain depth 14.

**As-originally-scoped (2026-04-25):** consolidate everything #1–#17 produced into a CI-runnable acceptance package — regression-suite expansion, performance benchmarks, the deferred Phase 0.1 corpus expansion (5–10 additional real-migration-scope `.accdb` files), and the `docs/acceptance-criteria.md` checkoff.

**As-reframed (2026-04-28, see *Reframing Note* above):** Session 1's End-to-End Test Consolidation closed VERIFIED as planned. Sessions 2–5 are reshaped to deliver an **AI-comprehension test bench (Layer C)** as the headline acceptance evidence. Performance benchmarks are deferred to a post-v1 follow-up set (`performance-benchmarks-and-baseline`, recommended at end of this set). The corpus expansion is descoped — `samples/NorthwindDevEdition.accdb` and `samples/HomeHealthCare.accdb` are sufficient gold-standard inputs. The `docs/acceptance-criteria.md` checkoff still happens (Session 4) with Layer C results as primary evidence and perf criteria marked "waived — deferred."

## Authority

Slug, plan-phase coverage, prerequisites, session count, and output paths are fixed by `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md` row #18.

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

## Feature 2: Regression Suite (Existing Layer A — KEEP) + Corpus Expansion (DESCOPED)

### Scope (revised after reframe)

**Layer A — snapshot regression — KEEP unchanged.** Per Phase 8.3, the existing [SyntheticFixtureSnapshotTests](../../../tests/AccessHarvester.Tests/SyntheticFixtureSnapshotTests.cs) and [VbaCorpusParserTests](../../../tests/AccessHarvester.Vba.Grammar.Tests/Modules/VbaCorpusParserTests.cs) drive every `docs/test-cases/*.expected.json` against the paired `tests/AccessHarvester.Tests/SyntheticDbs/*.accdb` through the full Extract→Translate→Enrich pipeline. They stay as the cheap structural regression layer.

**Corpus expansion — DESCOPED.** The parent tree's OQ 1(a) deferred Phase 0.1 corpus-expansion target (5–10 additional real-migration-scope `.accdb` files) is dropped from this set. `samples/NorthwindDevEdition.accdb` and `samples/HomeHealthCare.accdb` provide enough gold-standard surface for the reframed Layer C bench, and the user is a former Access developer who can curate scenarios + selectively translate macros to VBA where the bench surfaces gaps. Corpus expansion can return as a discrete follow-up set if real-world Layer C coverage gaps justify it.

### Standards

- Regression diffs are the contract; every change in output requires either a snapshot update with rationale or a fix.
- Sample databases must be free of sensitive data; per `project-guidance.md` → Output Architecture's privacy posture, output schema excludes data values, but the source `.accdb` should also be safe to commit.

## Feature 3: AI-Comprehension Test Bench (NEW Layer C — REPLACES former Performance Benchmarks)

### Scope

The headline addition. Per the harvester's stated purpose (`project-guidance.md` → Project Scope And Audience: "AI interpretation … is what we test FOR, not what we BUILD"), Layer C measures whether an AI engine consuming the harvester's IR output can correctly answer questions about the source database's intended behavior.

Per gold-standard DB (`NorthwindDevEdition`, `HomeHealthCare`):

1. Run the harvester pipeline once into a cached `outRoot`. Pipeline-prep is gated on `AccessAvailability.SkipIfNoAccess()`.
2. A **question bank** lives at `docs/test-cases/comprehension/<dbname>/questions.yaml`. Each entry: `{id, question, accepted_answers[], supporting_ir_paths[], difficulty: low|medium|high}`.
3. **Runner** at `tools/comprehension-bench/run.py` (Python — `route()` is Python). For each entry: load only the listed `supporting_ir_paths[]`, call `route(task_type="analysis")` for the answer, then a second `route(task_type="analysis")` (the *judge*) to score `actual` vs. `accepted_answers[]` 0.0–1.0.
4. **Pass rubric (graduated by difficulty):** low ≥ 0.85, medium ≥ 0.65, high ≥ 0.40.
5. **Cost gate:** xUnit wrapper `tests/AccessHarvester.Comprehension.Tests/ComprehensionBenchTests.cs` shells out to `run.py` only when `HARVESTER_COMPREHENSION_BENCH=1`. Plain `dotnet test` skips by default.
6. **Question-bank provenance:** *user scenarios + routed draft + human curate.* The user supplies a list of canonical Access patterns at `docs/test-cases/comprehension/scenarios.md`. Layer C drafting (Session 3 per the mid-session adjustment) routes `task_type="analysis"` against each DB's IR with the scenario list as priority guidance to produce ~20 candidate questions per DB. The draft is committed clearly labeled as uncurated; the user curates (accept/edit/reject) and the curated `questions.yaml` lands later in Session 3.

**Performance benchmarks (former Feature 3) — DEFERRED.** Phase 8.4's perf criteria are descoped from this set to a follow-up `performance-benchmarks-and-baseline` set, recommended in this set's `next-session-set-recommendation.md`. Rationale: orthogonal to AI-comprehension correctness; perf regressions don't move the harvester's actual-purpose needle the way comprehension regressions do.

### Standards

- Bench inputs (the IR snippets) are deterministic re-runs of the harvester pipeline. The bench's stochasticity comes only from the routed model's output; this is acceptable and is what the graduated rubric tolerates.
- Routed-call records (answers, judge scores) land in `router-metrics.jsonl` via the existing instrumentation.
- The first green run's per-question score JSONL is archived as `comprehension-baseline.jsonl` in this session-set folder; future runs diff against it.

## Feature 4: Acceptance Criteria Checkoff (Synthesis)

### Scope

Per Phase 8.5, produce `docs/acceptance-criteria.md` enumerating each acceptance criterion with status (met / not-met / waived-with-rationale). With Layer C in place, the acceptance bar for "downstream AI consumes output successfully" becomes a numeric Layer C pass rate against the curated question banks rather than a pilot-database integration log. Other criteria (CI green, documentation complete, all v1-scope DBs produce valid output) are unchanged.

Single synthesis session for the set (per the tree's synthesis count = 1): the criterion-by-criterion judgement pass.

### Standards

- "Met" requires evidence — a CI badge, a Layer C bench pass, a regression-suite green run. Subjective sign-off without evidence is "waived-with-rationale", not "met".
- Performance criteria from the original Phase 8.4 are explicitly "waived — deferred to `performance-benchmarks-and-baseline`" rather than "not met"; the Layer C reframe makes them out-of-scope for v1, not unmet.

---

## Sessions
### Session 1 of 4-5: End-to-End Test Consolidation

- Audit unit + integration coverage from #1–#17. Identify gaps.
- Land end-to-end integration tests under `tests/AccessHarvester.Integration/`.
- Route `schema-validation` for coverage-vs-target correspondence.

### Session 2 of 5: Layer C Scaffolding (Scenarios + Drafted Banks Deferred to Session 3)

> Reframed 2026-04-28 (see Reframing Note). Replaces "Regression Suite + Corpus Expansion." The corpus expansion is descoped per the reframing rationale.
>
> **Mid-session adjustment (2026-04-28):** Session 2 scaffolds the Layer C infrastructure and verifies the harvester pipeline runs cleanly against both gold-standard DBs. The user-supplied scenarios list and routed question drafting are deferred to Session 3 — Session 2 closes with a "**scenarios input handoff**" rather than a "drafted question curation handoff." This keeps Session 2 honest about what got built without forcing scenarios to be authored under time pressure.

**Steps (executed in Session 2):**

1. Scaffold `tools/comprehension-bench/`:
   - `draft_questions.py` — routes `task_type="analysis"` against each DB's IR + scenarios.md priority guidance, emits drafted questions YAML. Persists routed output to disk before any console output (per lessons-learned `Persist Routed Output To Disk Before Any Display Or Logging Side Effects`). Errors with exit code 2 if `scenarios.md` is missing.
   - `run.py` — bench runner: loads curated questions, slices IR by `supporting_ir_paths[]`, calls `route(task_type="analysis")` for answer + judge, emits per-question score JSONL.
   - `scoring.py` — graduated-rubric pass/fail (low ≥ 0.85, medium ≥ 0.65, high ≥ 0.40) + baseline-diff helper.
2. Create `tests/AccessHarvester.Comprehension.Tests/` xUnit project with one `[SkippableFact]` that shells out to `run.py` only when `HARVESTER_COMPREHENSION_BENCH=1`. Reuses `CliInvoker`-style `Process.Start` pattern from [tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs](../../../tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs).
3. Add `AccessHarvester.Comprehension.Tests` to `AccessHarvester.sln`.
4. Run the harvester pipeline against `samples/NorthwindDevEdition.accdb` and `samples/HomeHealthCare.accdb` to verify the IR-generation step works end-to-end. (IR is generated to a temp `outRoot` and not committed.)
5. `dotnet build && dotnet test` (sequentially per CLAUDE.md). Bench skips by default; other tests pass.
6. Cross-provider session-verification per `docs/ai-led-session-workflow.md` Step 6, with the trailing JSON response block.
7. Stop with a **scenarios input handoff**: Session 3 needs the user's scenarios.md before drafting questions.

**Steps deferred to Session 3 (the new shape — see Session 3 below):**

- Capture user-supplied `docs/test-cases/comprehension/scenarios.md`.
- Run `draft_questions.py` for both DBs; commit drafted YAMLs as uncurated drafts.
- (Then proceed with curation + first green Layer C run + baseline as originally planned.)

**Creates (Session 2):**
- `tools/comprehension-bench/{README.md, draft_questions.py, run.py, scoring.py}`
- `tests/AccessHarvester.Comprehension.Tests/{AccessHarvester.Comprehension.Tests.csproj, ComprehensionBenchTests.cs}`

**Touches:**
- `AccessHarvester.sln`
- `docs/session-sets/integration-testing-and-acceptance/spec.md` (this reframe + this mid-session adjustment)
- `docs/session-sets/integration-testing-and-acceptance/ai-assignment.md`

**Ends with:** `dotnet build` + `dotnet test` green; pipeline confirmed working against both gold-standard DBs; verifier returned VERIFIED (or fixed-and-re-VERIFIED); scenarios-input handoff stop message issued.

**Progress keys:** `session-2/spec-amendment`, `session-2/bench-scaffolding`, `session-2/test-project`, `session-2/pipeline-smoke`, `session-2/build-test`, `session-2/verification`.

### Session 3 of 5: Scenarios Capture + Question Drafting + Curated Banks + First Green Baseline

> Expanded 2026-04-28 to absorb scenarios capture and question drafting from Session 2.

**Steps:**

1. Capture the user's canonical Access scenarios at `docs/test-cases/comprehension/scenarios.md`.
2. Run `tools/comprehension-bench/draft_questions.py` for both gold-standard DBs (against a freshly-built IR `outRoot`); commit the drafted YAMLs as `questions.draft.yaml` with the auto-emitted "AUTO-DRAFTED — NOT CURATED" header.
3. Pause for human curation (the user accepts/edits/rejects each drafted question against `scenarios.md`); promote curated `questions.draft.yaml` to `questions.yaml`.
4. Re-confirm the curated YAMLs validate against the question-bank schema (id uniqueness, difficulty ∈ {low, medium, high}, supporting_ir_paths exist on disk).
5. With `HARVESTER_COMPREHENSION_BENCH=1`, run `tools/comprehension-bench/run.py` end-to-end against both DBs.
6. Score per the graduated rubric. If thresholds need calibration (e.g., "easy" turns out harder than expected on real DBs), adjust the rubric in `scoring.py` *with rationale documented inline*; do not silently relax to make the bench pass.
7. Archive the per-question score JSONL as `comprehension-baseline.jsonl` in this session-set folder.
8. Optionally add a small synthetic fixture under `docs/test-cases/scenarios/<name>/` for any scenario the gold-standard DBs don't cover.
9. `dotnet build && dotnet test` with bench env-flag set; assert pass.
10. Cross-provider session-verification.

**Creates:**
- `docs/test-cases/comprehension/scenarios.md`
- `docs/test-cases/comprehension/NorthwindDevEdition/questions.yaml` (curated)
- `docs/test-cases/comprehension/HomeHealthCare/questions.yaml` (curated)
- `docs/session-sets/integration-testing-and-acceptance/comprehension-baseline.jsonl`
- (Optional) `docs/test-cases/scenarios/<name>/{spec, .accdb, .expected.json}`

**Touches:**
- `tools/comprehension-bench/scoring.py` (only if rubric adjusted)

**Ends with:** Layer C bench passes the rubric on both DBs; baseline JSONL committed.

**Progress keys:** `session-3/scenarios-capture`, `session-3/question-drafting`, `session-3/curation-confirm`, `session-3/bench-run`, `session-3/threshold-calibration`, `session-3/baseline-archive`, `session-3/build-test`, `session-3/verification`.

### Session 4 of 5: Acceptance Criteria Checkoff (Synthesis)

**Steps:**

1. Read every prior child set's change-log + this set's Sessions 1–3 deliverables (especially `comprehension-baseline.jsonl`).
2. Author `docs/acceptance-criteria.md` enumerating each criterion with status (met / waived-with-rationale / not-met). Use Layer C as primary evidence for "downstream AI consumes output successfully." Mark performance criteria as "waived — deferred to `performance-benchmarks-and-baseline`."
3. Per `project-guidance.md` → Conventions → Workflow Expectations ("Use Opus as the orchestrator … for synthesis-document work above ~3K input tokens"), the orchestrator authors the document in-conversation rather than `route(task_type="architecture")` to avoid the Opus-timeout pattern.
4. `dotnet build && dotnet test`.
5. Cross-provider session-verification — include `acceptance-criteria.md`, `comprehension-baseline.jsonl`, and a representative slice of curated `questions.yaml` in the verifier prompt (per the "send related files" lesson).

**Creates:**
- `docs/acceptance-criteria.md`

**Ends with:** `acceptance-criteria.md` committed; verifier returned VERIFIED.

**Progress keys:** `session-4/evidence-gather`, `session-4/criterion-checkoff`, `session-4/document-author`, `session-4/build-test`, `session-4/verification`.

### Session 5 of 5: Verifier-Surfaced Cleanup + Final v1 Change-Log

**Steps:**

1. Triage any deferred verifier findings from Sessions 2–4. Apply fixes.
2. `dotnet build && dotnet test` to confirm the post-fix state.
3. Generate the **v1 release change-log** for the harvester as a whole (not just this set's deltas) via `route(task_type="summarization", max_tier=1)` per the lessons-learned guidance against `documentation` for narrative prose.
4. Cross-provider session-verification.
5. End-of-set: route the next-session-set recommendation. Plan-driven repos: read the parent `session-set-tree.md` and propose `performance-benchmarks-and-baseline` as the next set, with the corpus-expansion descope and Layer C deferred-perf rationale stated explicitly.
6. Step 9 reorganization review per the workflow.

**Creates:**
- `docs/session-sets/integration-testing-and-acceptance/change-log.md` (v1 release)

**Ends with:** v1 change-log committed; set marked complete; reorganization review run.

**Progress keys:** `session-5/cleanup`, `session-5/build-test`, `session-5/change-log`, `session-5/verification`, `session-5/next-set-recommendation`, `session-5/reorganization-review`.

> Step lists above are bullet hints. The orchestrator running each session must elaborate them per `docs/ai-led-session-workflow.md`.

---

## Routing notes (revised after reframe)

- **Question drafting (Session 3 — moved from Session 2):** `route(task_type="analysis")` against each DB's IR + scenarios.md as priority guidance. **Not `documentation`** — per lessons-learned, `documentation` wraps content in a code-doc template that mis-fits "generate questions about this IR" prompts. Persist routed output to disk before any console output.
- **Bench answer + judge (Session 3+):** Both legs use `task_type="analysis"`. The judge prompt explicitly requests a numeric 0.0–1.0 score with one-sentence justification.
- **Acceptance criteria checkoff (Session 4):** Authored in-conversation by the Opus orchestrator per the lessons-learned guidance against `route(architecture, complexity_hint=80)` timing out on synthesis-document prompts above ~3K input tokens.
- **Session-verification (every session):** `route(task_type="session-verification", complexity_hint=70)`. Include related files per the "send related files" convention. Append the trailing required JSON response block to every prompt.
- **Change-log (Session 5):** `route(task_type="summarization", max_tier=1)`. This is the **v1 release change-log** for the harvester as a whole, not just this set's deltas — ensure scope reflects the full tree's accomplishment plus the explicit corpus-expansion + perf-benchmark descope rationale from this set's reframe.
- **AI-assignment authoring + next-orchestrator + next-session-set recommendations:** All `route(task_type="analysis")` per Rule #17 — never self-opine.

## Open questions for the human

None at this writing. Three human-blocking handoffs are baked into the plan:
1. End of Session 2 — **scenarios input handoff** (the user provides `docs/test-cases/comprehension/scenarios.md` before Session 3 routes question drafting).
2. Mid-Session 3 — drafted question banks ready for curation.
3. End of Session 5 — Step 9 reorganization review (the standard workflow gate).
