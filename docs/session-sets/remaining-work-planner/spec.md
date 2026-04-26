# Remaining-Work Planner Spec

> **Purpose:** Once the AccessExporterTemp1 import and the VBA-parser
> session-set generation are complete, take stock of what remains
> between "imported parser/extractor scaffolding + planned parser child
> session sets" and "shippable harvester," and produce a plan for that
> remaining work.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/remaining-work-planner/`
> **Prerequisite (HARD GATE):** This session set must NOT begin until
> both of the following session sets have committed `change-log.md`:
>   - `docs/session-sets/✅accessexportertemp1-import/change-log.md`
>   - `docs/session-sets/✅vba-parser-session-set-generator/change-log.md`
> An orchestrator picking this set up via `find_active_session_set()`
> must check both prerequisites in Session 1, Step 1, and stop and
> notify the human if either is missing.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Project Overview

After the import and the VBA-parser planning are done, three categories
of work likely remain to make this repo shippable:

1. **Glue / cross-cutting concerns** the import session set didn't
   land because they sit between the imported extraction code and the
   parser that doesn't exist yet — shared model types, output-tier
   reconciliation between the extractor's `kb/`/`ir/` schemas and the
   parser's `harvester-schema.v0.json`, CLI subcommand consolidation
   (parser invocation, combined-run subcommand), Pipeline integration
   between extraction and parsing.
2. **Categories not covered by the imported codebase or the parser
   plan** — Ribbons (mentioned in the project mission but not
   currently tagged as in-scope by either the import plan or the VBA
   parser plan); Switchboards (mentioned in the parser plan's Phase 4
   but not in the extraction codebase); Property-Sheet-only behavior
   captured in `validation rules` and `default values` that are
   neither pure VBA nor SaveAsText control properties; Cross-database
   library references; etc.
3. **Operational concerns** — CI / build automation, sample-corpus
   curation, performance benchmarks, output-stability invariants,
   release packaging, end-user documentation for the State customer.

The session set's first task is to *discover* the actual gap (rather
than assume it from this overview), then plan it.

This is a **meta** session set, like
`vba-parser-session-set-generator`. It plans work; it does not execute
it. No C# code is touched. No `dotnet build` / `dotnet test` runs
unless they are needed to characterize a discovered gap.

---

## Feature 1: Gap Audit (Session 1)

### Scope

Read every artifact produced by the two prerequisite session sets:

- `docs/session-sets/✅accessexportertemp1-import/import-plan.md`
- `docs/session-sets/✅accessexportertemp1-import/change-log.md`
- `docs/session-sets/✅vba-parser-session-set-generator/session-set-tree.md`
- `docs/session-sets/✅vba-parser-session-set-generator/routing-playbook.md`
- All stub `spec.md` files under `docs/session-sets/<vba-child>/`
  that the generator created.
- The current state of the C# solution (top-level `*.csproj` files,
  `Pipeline.cs`, `Program.cs`).

Cross-reference against:

- `docs/planning/project-guidance.md` (especially Principles → Project
  Scope And Audience and Output Architecture).
- `docs/VBA-Parser-Migration-Harvester-Plan.md` (in case sections
  weren't fully decomposed by the generator).

Produce `gap-audit.md` listing:

- **Confirmed in scope and planned** — covered by an existing child
  session set or shipped by the import.
- **In scope and unplanned** — covered by the project mission or
  guidance but not yet in any session set. This is the heart of the
  audit.
- **Ambiguous** — flag for the human to resolve.
- **Explicitly out of scope** — items the audit considered and
  rejected, with rationale (so they don't get re-considered later).

For each "in scope and unplanned" entry, the audit captures:
working name, brief scope, dependency on existing planned work,
rough cost band (small / medium / large session set).

### Standards

- The audit is opinionated. "Maybe we should plan for X" is not
  enough — the audit recommends in-scope or out-of-scope, even if
  the recommendation is "tentatively in scope, awaiting human
  confirmation."
- Where the audit has to interpret guidance, it quotes the relevant
  guidance text in-line so the human can verify the interpretation.
- Use `route(task_type="analysis")` for the gap synthesis. This is
  the right calibration: not full architecture (we're not designing
  the missing pieces yet), not pure planning (we're reasoning about
  what's missing).

---

## Feature 2: Plan The Remaining Work (Session 2)

### Scope

For each "in scope and unplanned" item from the gap audit, create a
session-set entry — analogous to what
`vba-parser-session-set-generator` did for the parser:

- A row in `remaining-work-tree.md` with name, scope summary,
  prerequisites, estimated session count, dependency on existing
  parser/import sets.
- A stub `spec.md` at `docs/session-sets/<slug>/spec.md` if the
  human approves it as in-scope.

The session also extends `routing-playbook.md` with any new task
categories the remaining-work items introduce (e.g., if a "build CI
pipeline" session set adds a `ci-config-authoring` task category, the
playbook gets a new entry). The extension is a **delta** to the
existing playbook, written as an appended section, not a rewrite.

The tree this session writes is added to, not replaces, the
`vba-parser-session-set-generator/session-set-tree.md`. Together
they form the project's full session-set roadmap.

### Standards

- Same constraints as the VBA generator's stubs: each child session
  set fits in 3–8 sessions; stubs flagged as stubs.
- Operational concerns (CI, packaging, end-user docs) typically
  warrant smaller session sets than parser-feature work — 2–4
  sessions, not 6–8.
- If the gap audit surfaced an item that should *retroactively* be
  added to an existing planned session set (e.g., "the
  `phase-3-symbol-resolution` set also needs to handle library
  references") rather than warrant its own new set, the planner
  documents the proposed amendment in `tree-amendments.md` and
  asks the human to apply it via a follow-up commit on the affected
  set's `spec.md`.

Use `route(task_type="planning")` for new session-set decomposition,
`route(task_type="architecture")` for any playbook extension that
introduces a new task category.

---

## Feature 3: Final Roadmap + Change Log (Session 3)

### Scope

Synthesize a top-level `docs/session-sets/ROADMAP.md` that lists, in
recommended execution order, every session set in the project:

- Already-complete sets (with their change-log.md links).
- Currently-in-progress sets (if any).
- Planned sets from the VBA parser tree.
- Planned sets from the remaining-work tree.

The roadmap shows the dependency DAG flattened to a recommended
linear order, with explicit notes where work can run in parallel.

This is the artifact a future orchestrator (or a fresh human picking
the project up) reads first to know what to do next.

### Standards

- The roadmap is a **pointer** document. It does not duplicate
  spec content — every entry is a one-paragraph description plus a
  link to the set's `spec.md`.
- The roadmap uses absolute estimates conservatively. If a set's
  stub estimates "3–5 sessions," the roadmap says "3–5 sessions
  (range; depends on edge cases)."
- The roadmap is committed alongside the change-log.

Use `route(task_type="documentation")` for the roadmap — it is
structural code-doc-like content, not narrative prose. The change-log
itself uses `task_type="summarization"`.

---

## Session Plan

### Session 1 of 3: Gap Audit

**Steps:**
1. **HARD-GATE prerequisite check** — verify both
   `accessexportertemp1-import/change-log.md` and
   `vba-parser-session-set-generator/change-log.md` exist on disk.
   If either is missing, stop and tell the human; do not proceed.
2. Read all artifacts from the two prerequisite session sets (see
   Feature 1 scope).
3. Read `project-guidance.md`, `lessons-learned.md`, and
   `VBA-Parser-Migration-Harvester-Plan.md`.
4. Inspect the current C# solution state (`AccessHarvester.sln`,
   csproj files, `Pipeline.cs`).
5. Route the gap synthesis:
   `route(task_type="analysis", complexity_hint=70,
   content=<summary of read artifacts + initial gap candidates>)`.
   Verifier should challenge missed items and false positives.
6. Iterate. Max 2 retries.
7. Write `docs/session-sets/remaining-work-planner/gap-audit.md`.
8. End-of-session cross-provider verification.

**Creates:**
- `docs/session-sets/remaining-work-planner/gap-audit.md`

**Touches:** none

**Ends with:** gap-audit committed; pending human review of
ambiguous items before Session 2 starts.

**Progress keys:** `session-1/gate-check`, `session-1/read-prereqs`,
`session-1/read-guidance`, `session-1/inspect-solution`,
`session-1/route-synthesis`, `session-1/write-audit`,
`session-1/verify`

---

### Session 2 of 3: Plan The Remaining Work

**Steps:**
1. Verify Session 1's `gap-audit.md` exists and that the human has
   resolved ambiguous items (or the human explicitly waives this
   check).
2. For each "in scope and unplanned" item:
   a. Route a stub spec via
      `route(task_type="planning", complexity_hint=70,
      content=<the audit row + relevant guidance/plan excerpts>)`.
   b. Create `docs/session-sets/<slug>/` and save the stub.
3. Write `docs/session-sets/remaining-work-planner/remaining-work-tree.md`
   with the same row-per-set format the VBA generator used.
4. If any new task categories surface, route a playbook extension:
   `route(task_type="architecture", complexity_hint=80, content=<the
   new categories + existing routing-playbook.md content>)`.
5. Save extensions as appended sections in
   `docs/session-sets/✅vba-parser-session-set-generator/routing-playbook.md`
   under a clearly-labeled "Extensions from remaining-work-planner"
   header, OR as
   `docs/session-sets/remaining-work-planner/routing-playbook-delta.md`
   if the human prefers physical separation. Default to the inline
   appended-section approach unless the human has indicated a
   preference.
6. If any audit items warrant amending an existing planned spec
   rather than getting their own set, write
   `tree-amendments.md` listing the proposed amendments and their
   target spec files.
7. Build + test: not applicable.
8. End-of-session cross-provider verification.

**Creates:**
- `docs/session-sets/<new-slug>/spec.md` (one per new set)
- `docs/session-sets/remaining-work-planner/remaining-work-tree.md`
- `docs/session-sets/remaining-work-planner/tree-amendments.md` (if
  any amendments)

**Touches:**
- Possibly `docs/session-sets/✅vba-parser-session-set-generator/routing-playbook.md`

**Ends with:** every gap-audit "in scope and unplanned" item has a
stub spec; tree committed.

**Progress keys:** `session-2/iterate-items`,
`session-2/route-stub-<slug>` (one per item),
`session-2/write-tree`, `session-2/extend-playbook`,
`session-2/write-amendments`, `session-2/verify`

---

### Session 3 of 3: Final Roadmap + Change Log

**Steps:**
1. Verify Session 2 prerequisites exist.
2. Enumerate every session set in `docs/session-sets/` (this set
   plus its prerequisites plus the VBA child sets plus the
   remaining-work child sets).
3. Build the dependency DAG and flatten to a recommended execution
   order.
4. Route the roadmap:
   `route(task_type="documentation", complexity_hint=60,
   content=<the enumerated sets + their stub purposes + the
   DAG>)`.
5. Save `docs/session-sets/ROADMAP.md` at the session-sets root.
6. **Last session — generate `change-log.md`** for this session
   set via `route(task_type="summarization")`.
7. Build + test: not applicable.
8. End-of-session cross-provider verification.
9. Cost report, commit + push, notification.
10. Step 9 reorganization review (post-notify): scan
    `project-guidance.md` and `lessons-learned.md` for promotion
    candidates the gap-audit + planning surfaced.

**Creates:**
- `docs/session-sets/ROADMAP.md`
- `docs/session-sets/remaining-work-planner/change-log.md`

**Touches:** none

**Ends with:** ROADMAP at the session-sets root; this set's
change-log committed; session set marked complete; the project has
a complete written plan for the remaining work.

**Progress keys:** `session-3/enumerate-sets`,
`session-3/build-dag`, `session-3/route-roadmap`,
`session-3/write-roadmap`, `session-3/verify`,
`session-3/route-changelog`, `session-3/commit-push`,
`session-3/notify`, `session-3/reorganization-review`

---

## Why this set is authored at bootstrap

This set is gated on two other sets completing — so a reasonable
question is "why write its spec now instead of waiting?" The answer:

1. **Pre-commitment removes a decision later.** If this set is not
   on disk, a future orchestrator might forget to plan the
   remaining work and the project drifts off the planned-execution
   discipline.
2. **The hard-gate check at Session 1 Step 1 is self-protective.**
   The set cannot run early because the prerequisite check stops
   it. So writing it now costs nothing.
3. **The human has full visibility into the planned end-to-end
   workflow** — bootstrap → import → parser planning → parser
   execution → remaining-work planning → remaining-work execution
   — without having to wait.

If, after #1 and #2 complete, the gap audit reveals that no
remaining work is needed, Session 1 produces a `gap-audit.md` that
says exactly that and Sessions 2 + 3 produce minimal artifacts (the
roadmap is still useful even if it lists only the previously-planned
sets). The set never produces less than a final roadmap.
