# Remaining-Work Planner Spec

> **Purpose:** Plan the remaining work to take the **mechanical
> harvester** to its Phase 4 finish line. Audit the current state
> of the harvester pipeline (parser, resolver, enrichers, IR,
> CLI, output schemas), identify the gaps between that state and
> the Phase 4 "Done" criteria in
> [`docs/planning/strategy.md`](../../planning/strategy.md), and
> produce a focused execution plan for closing them.
> **Created:** 2026-04-25
> **Revised:** 2026-05-17 — retargeted from "general gap audit
> bridging import + parser-plan + harvester" to "Phase 4
> mechanical harvester finish line" after the 2026-05-17
> cross-provider strategic review. The original spec is preserved
> in git history; the revised version is below. See the
> `docs/strategic-reviews/2026-05-17-platform-tooling-fit/`
> review for the rationale.
> **Session Set:** `docs/session-sets/remaining-work-planner/`
> **Prerequisite (informational):** The earlier hard gate on
> `accessexportertemp1-import/change-log.md` and
> `vba-parser-session-set-generator/change-log.md` is satisfied
> — both sets shipped well before 2026-05-17. No remaining hard
> gate; the planner can run when authorized by the human.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: This is a meta-planning set. It produces documents
> (gap audit, stub specs, roadmap, change log). No UI, no
> behavioral changes, no E2E surface.

---

## Project Overview

The mechanical-harvester finish line is defined in
[`docs/planning/strategy.md`](../../planning/strategy.md)
**Phase 4**:

> The mechanical harvester (parser, extractor, enrichers, output
> schemas, CLI) can produce stable, documented output for the
> canonical fixtures with parser/extraction tests green and
> explicit `UnresolvedItem` records for constructs it cannot
> process.

This session set is the meta-plan for getting there. The audit
covers the harvester pipeline only — explicitly NOT the Blazor
pilot host, the IR generator, the platform-integration work, or
any other downstream consumer of harvester output. Those are
either frozen, deferred to the future migration repo, or out of
scope per the 2026-05-17 strategy reset.

### What "shippable mechanical harvester" includes (in scope)

1. **Parser coverage finality.** Every documented VBA / Jet SQL /
   property-sheet construct in the canonical fixtures either
   parses correctly, or emits an explicit `UnresolvedItem` with
   provenance and reason. No silent fallbacks.
2. **Schema finality.** The `kb/` and `ir/` output schemas (and
   the VBA parser's `harvester-schema.v0.json`) are stable,
   documented, and validated against committed sample output.
3. **Sample-corpus validation.** Golden output is committed for
   each canonical fixture (`NorthwindDevEdition.accdb`,
   `HomeHealthCare.accdb`, `MiniComplex.accdb`), and the CI / test
   suite verifies the harvester reproduces that golden output.
4. **CLI surface.** Every harvester subcommand (`extract`,
   `translate`, `run`, `parse`, etc.) is documented, has
   end-to-end test coverage, and produces deterministic output.
5. **Unresolved-record reporting.** The harvester emits a
   per-run report listing every `UnresolvedItem` with a
   structured record (source location, construct kind, reason,
   suggested follow-up). This is the harvester's primary signal
   to a downstream consumer about what wasn't fully extracted.
6. **Documentation.** A user-facing `README.md` and `CLI.md` (or
   equivalent) describing what the harvester does, what its
   output means, how to consume it, and what the known
   `UnresolvedItem` categories signal.
7. **Packaging.** The harvester ships as either a `dotnet tool`
   global / local install, a published binary release, or
   whatever distribution shape the planner determines is right
   — but it has to be installable by a downstream consumer
   without a from-source build.

### What is explicitly OUT OF SCOPE for this planner

The 2026-05-17 strategic review (see
`docs/strategic-reviews/2026-05-17-platform-tooling-fit/`)
formalized the exclusion list:

- **New Blazor application slices, workflows, or pilots.**
  `output-tests/blazor-add-an-order/` is preserved as-is for the
  future migration repo to reference. No further code lands in
  it from this repo, including `blazor-update-workflows` (Phase
  3a, deferred).
- **Production IR generator** beyond the existing
  `tools/AccessHarvester.IrCodegen/` probe.
- **Platform-integration work** — refreshing
  `docs/cross-repo/` snapshots, evaluating
  `../dabbler-platform/` CRUD templates, etc. The existing
  snapshot is dated and sufficient until the future migration
  repo consumes the platform.
- **UAT / E2E coverage** of any UI surface — there is no UI
  surface in scope for this repo's finish line.
- **Cross-database library references, Ribbons, Switchboards**
  beyond what's needed for the canonical fixtures' golden
  output. If those don't appear in the three canonical fixtures'
  surface, they don't gate the finish line.
- **Re-opening any item on the "Frozen Unless Reauthorized"
  list** in `docs/planning/strategy.md`. The audit may surface
  candidates for unfreezing; the planner records them but does
  NOT re-authorize them — the human decides.

### What the planner explicitly DOES NOT do

This remains a meta-planning set. It does not author parser
code, schema files, test fixtures, CLI subcommands, or
documentation beyond its own four artifacts (gap audit,
stub specs, roadmap, change log). It does not run
`dotnet build` or `dotnet test` unless a specific gap-audit
question requires confirming a build-state claim.

---

## Feature 1: Gap Audit (Session 1)

### Scope

Read every artifact relevant to the harvester's current state:

- `docs/planning/strategy.md` — especially Phase 4 and the
  Frozen Unless Reauthorized list.
- `docs/planning/project-guidance.md` — Principles → Project
  Scope And Audience and Output Architecture.
- `docs/planning/lessons-learned.md` — anything tagged as
  harvester-specific.
- `docs/VBA-Parser-Migration-Harvester-Plan.md`.
- Every prior session set's `change-log.md` whose scope touched
  the parser, resolver, enrichers, or CLI. Skim the index;
  drill into the ones that materially shaped the current
  pipeline.
- The current C# solution state: `AccessHarvester.sln`, every
  `*.csproj` file, `Pipeline.cs`, `Program.cs`.
- The current test suite layout under `tests/`.
- Any golden output committed at `output-tests/` (excluding the
  blazor-add-an-order/ subtree — that's pilot scaffolding, not
  harvester output).
- Sample fixtures at `samples/` and their committed YAML
  mirrors.

Cross-reference against the Phase 4 "Done" definition above.

Produce `gap-audit.md` listing:

- **Phase 4 criterion COVERED.** The artifact / session set that
  closes this criterion.
- **Phase 4 criterion GAP.** Specific work missing. For each
  gap, capture: working name, brief scope, dependency on existing
  planned work, rough cost band (small / medium / large session
  set).
- **AMBIGUOUS.** Items the audit cannot classify without human
  input. Flag with the specific question the human must answer.
- **CANDIDATE FOR UNFREEZING.** Items currently on the Frozen
  list that the audit thinks should be revisited. Recommendation
  only — the planner does NOT re-authorize anything itself.

### Standards

- The audit is opinionated. "Maybe we should do X" is not
  enough; recommend in-scope or out-of-scope with a reason.
- Where the audit interprets strategy or guidance, it quotes the
  relevant text inline so the human can verify.
- Use `route(task_type="analysis")` for the gap synthesis. The
  orchestrator-matches-target convention applies — if the
  orchestrator IS Opus, prefer in-conversation authoring over
  routing to Opus.

---

## Feature 2: Plan The Remaining Work (Session 2)

### Scope

For each "Phase 4 criterion GAP" item, propose how to close it:

- **New session set.** Create a stub `spec.md` at
  `docs/session-sets/<slug>/spec.md`. Same constraints as the
  original VBA generator's stubs: each set fits in 2–5
  sessions; the stub clearly says it's a stub; configuration
  block defaults (`requiresUAT: false, requiresE2E: false`).
- **Amendment to an existing planned set.** If the gap fits
  naturally into a set that already has a spec (e.g., the gap is
  "the navigation-graph enricher needs to handle Ribbons" and
  there's an enrichment set that could absorb it), the planner
  documents the proposed amendment in `tree-amendments.md`. The
  amendment is a proposal — the human applies it via a follow-up
  commit on the affected set's `spec.md`.
- **Drop.** If the audit recommended "not worth doing for the
  finish line," the planner does not produce a stub. The
  decision is recorded in the gap-audit.

Write `remaining-work-tree.md` with one row per planned set:
name, scope summary, prerequisites, estimated session count,
cost band. This is additive to whatever earlier
`session-set-tree.md` artifacts exist; the planner does not
rewrite earlier trees.

If any new task categories surface that the AI router doesn't
already cover, the planner notes them in
`routing-playbook-delta.md` for the human to fold into
`ai_router/router-config.yaml`. The planner does NOT edit the
router config itself.

### Standards

- Operational concerns (CI, packaging, end-user docs) typically
  warrant smaller session sets than parser-feature work — 2–3
  sessions, not 5–8.
- The Open Validation #7 partial — report / macro / query
  workflows untested — applies here. If the audit concludes
  these are needed for the finish line, the planner stubs the
  relevant set. If not, the planner records the explicit
  rationale.
- Use `route(task_type="planning")` for new session-set
  decomposition; `route(task_type="analysis")` for amendment
  proposals.

---

## Feature 3: Final Roadmap + Change Log (Session 3)

### Scope

Synthesize `docs/session-sets/ROADMAP.md` — a top-level pointer
document listing every session set in the project in
recommended execution order:

- Already-complete sets (with change-log.md links).
- Currently-in-progress sets, if any. (At 2026-05-17 there are
  none, post-housekeeping.)
- Planned sets from this planner's `remaining-work-tree.md`.
- Sets deferred to the future migration repo, with a clear
  "DEFERRED — see strategy.md Phase 3 Revisions" marker.

The roadmap is the artifact a future orchestrator (or a fresh
human picking the project up) reads first to know what to do
next.

### Standards

- The roadmap is a **pointer** document. Every entry is a
  one-paragraph description plus a link to the set's `spec.md`
  and `change-log.md` (if present). No content is duplicated
  from the specs themselves.
- Estimated session counts use ranges ("3–5 sessions")
  conservatively.
- The roadmap is committed alongside the change-log.

Use `route(task_type="documentation")` for the roadmap
(structural pointer doc); `task_type="summarization"` for the
change-log itself. Both are eligible for in-conversation
authoring per the orchestrator-matches-target convention if the
orchestrator is Opus or Sonnet.

---

## Sessions

### Session 1 of 3: Phase 4 Gap Audit

**Steps:**

1. Read `docs/planning/strategy.md` end-to-end, especially Phase
   4 and the Frozen Unless Reauthorized list.
2. Read `project-guidance.md`, `lessons-learned.md`,
   `session-set-authoring-guide.md`, and
   `VBA-Parser-Migration-Harvester-Plan.md`.
3. Read the strategic-review reports at
   `docs/strategic-reviews/2026-05-17-platform-tooling-fit/`
   so the audit inherits the same constraints.
4. Inspect the current C# solution state and test suite layout.
5. Skim the index of `docs/session-sets/` change-logs; drill
   into the ones that materially shaped the parser / resolver /
   enrichers / CLI.
6. Author `gap-audit.md` — in-conversation if the orchestrator
   is Opus or Sonnet (per the orchestrator-matches-target
   convention); routed via `task_type="analysis"` if the
   orchestrator is a smaller model. Document each Phase 4
   criterion's status as COVERED / GAP / AMBIGUOUS / CANDIDATE
   FOR UNFREEZING.
7. End-of-session cross-provider verification per Step 6 of the
   workflow.

**Creates:**
- `docs/session-sets/remaining-work-planner/gap-audit.md`
- `docs/session-sets/remaining-work-planner/ai-assignment.md`
- `docs/session-sets/remaining-work-planner/session-reviews/session-001.md`

**Touches:** none

**Ends with:** gap-audit committed; pending human review of
ambiguous items and unfreeze candidates before Session 2 starts.

**Progress keys:** `session-1/read-strategy`,
`session-1/read-guidance`, `session-1/read-strategic-reviews`,
`session-1/inspect-solution`, `session-1/read-changelogs`,
`session-1/author-audit`, `session-1/verify`

---

### Session 2 of 3: Plan The Remaining Work

**Steps:**

1. Verify Session 1's `gap-audit.md` exists; verify the human
   has resolved AMBIGUOUS items and adjudicated UNFREEZE
   candidates (or explicitly waived the wait).
2. For each "Phase 4 criterion GAP" item, propose a closure
   plan (new set, amendment, or drop):
   - **New set:** route a stub spec via `task_type="planning"`
     (or author in-conversation if the orchestrator is
     Opus/Sonnet); create `docs/session-sets/<slug>/spec.md`
     marked as a stub.
   - **Amendment:** add the proposal to
     `tree-amendments.md` with the target spec file noted.
   - **Drop:** record the rationale in `gap-audit.md` under the
     gap item.
3. Author `remaining-work-tree.md` with one row per planned set.
4. If any new task categories surface, author
   `routing-playbook-delta.md` for the human.
5. End-of-session cross-provider verification.

**Creates:**
- `docs/session-sets/<new-slug>/spec.md` (one per planned new set)
- `docs/session-sets/remaining-work-planner/remaining-work-tree.md`
- `docs/session-sets/remaining-work-planner/tree-amendments.md`
  (only if any amendments are proposed)
- `docs/session-sets/remaining-work-planner/routing-playbook-delta.md`
  (only if any new task categories are needed)
- `docs/session-sets/remaining-work-planner/session-reviews/session-002.md`

**Touches:** none. The planner does NOT edit existing specs or
the router config — it proposes; the human applies.

**Ends with:** every gap-audit "in scope" item has a closure
plan (new stub, amendment, or recorded drop); tree committed.

**Progress keys:** `session-2/verify-audit-resolved`,
`session-2/iterate-gaps`, `session-2/author-stubs`,
`session-2/write-tree`, `session-2/write-amendments`,
`session-2/write-playbook-delta`, `session-2/verify`

---

### Session 3 of 3: Final Roadmap + Change Log

**Steps:**

1. Verify Session 2's `remaining-work-tree.md` exists.
2. Enumerate every session set under `docs/session-sets/`
   (existing complete + deferred + planned).
3. Build the dependency DAG and flatten to a recommended
   execution order. Mark deferred sets clearly.
4. Author `docs/session-sets/ROADMAP.md` — in-conversation per
   the orchestrator-matches-target convention if the
   orchestrator is Opus/Sonnet; routed via
   `task_type="documentation"` otherwise.
5. Author `disposition.json` per the Convention (Workflow
   Expectations → "Write `disposition.json` before the final
   session's closeout commit").
6. Author this set's `change-log.md` — in-conversation if
   possible; `task_type="summarization"` otherwise.
7. Update `docs/planning/strategy.md` with the planned-sequence
   actuals: Phase 4 now has a named, sequenced execution plan.
8. End-of-session cross-provider verification.
9. Cost report, commit + push, `mark_session_complete()`,
   notify.
10. Step 9 reorganization review of `project-guidance.md` and
    `lessons-learned.md` AFTER the notify.

**Creates:**
- `docs/session-sets/ROADMAP.md`
- `docs/session-sets/remaining-work-planner/change-log.md`
- `docs/session-sets/remaining-work-planner/disposition.json`
- `docs/session-sets/remaining-work-planner/session-reviews/session-003.md`

**Touches:**
- `docs/planning/strategy.md` (Phase 4 section gets the
  planned-sequence actuals)

**Ends with:** ROADMAP at the session-sets root; this set's
change-log committed; session set marked complete; the project
has a concrete written plan for the harvester finish line.

**Progress keys:** `session-3/verify-tree`,
`session-3/enumerate-sets`, `session-3/build-dag`,
`session-3/author-roadmap`, `session-3/author-changelog`,
`session-3/update-strategy`, `session-3/disposition`,
`session-3/verify`, `session-3/commit-push`,
`session-3/notify`, `session-3/reorganization-review`

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.20 per session
  (cross-provider session-verification only, plus optional
  routed planning calls for stub authoring if the orchestrator
  is not Opus/Sonnet). ~$0.20–$0.60 across the set.
- **In-conversation token spend:** Substantial. The audit,
  stub-spec authoring, roadmap, and change log are all in-IDE
  per the orchestrator-matches-target convention assuming an
  Opus or Sonnet orchestrator.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Gap audit surfaces a Phase-4 criterion that is unbounded** | An audit gap looks like it requires more than ~8 sessions to close, or its scope keeps expanding under scrutiny | Stop. Surface to the human. Either re-scope the Phase-4 criterion, or recognize that this is a structural strategic question that strategy.md needs to address, not a tactical gap. |
| **Audit recommends unfreezing something the Frozen list explicitly froze** | A "candidate for unfreezing" item turns out to be needed for the finish line, not just nice-to-have | Stop. Surface to the human. The Frozen list is the strategy's commitment; unfreezing requires explicit reauthorization, not a planner-side override. |
| **Sessions 2 or 3 reveal the audit was wrong** | A stub spec can't be authored because the audit's gap description doesn't match reality, OR the roadmap exposes a dependency the audit missed | Stop. Loop back to Session 1's audit and amend it rather than papering over the gap. |
| **A gap turns out to require Blazor / IR-generator / platform-integration work** | An audit item drifts into the OUT-OF-SCOPE list | Stop. Record as "out-of-scope per 2026-05-17 strategic review" in the audit. Do not extend Phase 4 to absorb it. |

If any stop rule fires, the orchestrator surfaces to the human
and waits for direction. Does not auto-pivot.

---

## Why this set is authored at bootstrap

(Preserved from the 2026-04-25 original spec; still applies.)

This set is meta-planning. Its value is being on disk early so a
future orchestrator can find it and the human has full
visibility into the planned end-to-end workflow without having
to wait. The 2026-05-17 revision retargets its scope but
preserves the pre-commitment property — when the harvester
finish line is ready to plan, this spec is already shaped to
plan it.
