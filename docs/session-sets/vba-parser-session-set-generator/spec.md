# VBA Parser Session-Set Generator Spec

> **Purpose:** Read `docs/VBA-Parser-Migration-Harvester-Plan.md` and
> `docs/test-cases/` end-to-end, then produce (a) a tree of child
> session sets that implement the plan in conversation-sized chunks and
> (b) a routing playbook telling future orchestrators which `task_type`,
> model, and parameters to use for each kind of work in those child
> sets.
> **Created:** 2026-04-25
> **Session Set:** `docs/session-sets/vba-parser-session-set-generator/`
> **Prerequisite:** None to start; can run in parallel with the
> `accessexportertemp1-import` session set. The child session sets this
> generator *produces* should not begin executing until that import has
> stabilized (so the parser has a `AccessHarvester.Core` to plug into),
> but the planning work itself doesn't depend on the import.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Project Overview

The migration plan covers ~35–55 AI-led working days across 9 phases
(0 through 8). It cannot be executed as a single session set — it would
take dozens of conversations to land. This session set is the planning
layer between the high-level plan and per-feature execution: it
decomposes the plan into named, scoped child session sets, identifies
their dependencies, and writes a routing playbook so each child set's
sessions get the cheapest-capable model on every routed call.

The deliverables are:

1. **`harvester-import-retrospective.md`** — a structured retrospective
   of the completed `accessexportertemp1-import` session set, with
   one row per finding flagged adopt-now / defer-to-integration /
   no-op. Adopted findings inform the decomposition, stubs, and
   playbook produced by the later sessions of this set. This
   retrospective is the first artifact because a sloppy decomposition
   multiplies cost across every child session set that follows; the
   harvester-import set is the only directly comparable data we have
   on this repo.
2. **`session-set-tree.md`** — a hierarchical map: VBA-parser-plan →
   child session sets → rough session count per child → dependency
   graph between children. One row per child session set with
   purpose, scope, prerequisites, estimated session count, and
   pointers into the plan.
3. **One stub `spec.md` per child session set**, written with enough
   scope detail that a future orchestrator can execute it without
   re-deriving the plan. Stubs use the standard session-set spec
   format (Project Overview / Features / Session Plan), but session
   step lists are abbreviated when later refinement is expected — the
   stub records the *shape* of the work, not necessarily every step.
4. **`routing-playbook.md`** — for each kind of work the child sets
   contain (grammar authoring, parser-infrastructure code,
   AST-shape design, symbol-resolution algorithm design,
   schema validation, regression-corpus authoring, etc.), the
   recommended `task_type`, complexity hint, and any
   `task_type_params` or `task_type_overrides` overrides. Includes a
   small set of proposed `router-config.yaml` deltas (e.g., "add a
   `grammar-authoring` task type pinned to Opus high-effort with
   adaptive thinking; add a `schema-validation` task type pinned to
   Sonnet"). The router-config delta is proposed but not applied —
   applying it is itself a routed-task decision the human approves.

The generator does **not** execute any of the parser work. It only
plans.

---

## Feature 1: Harvester-Import Retrospective (Session 1)

### Scope

Read the completed `accessexportertemp1-import` session set's artifacts
end-to-end and produce `harvester-import-retrospective.md`: a structured
list of findings that should shape the VBA-parser plan's decomposition,
stubs, or routing playbook.

The retrospective is **not** a project history. It is a focused
working document whose only purpose is to make the next three
sessions of *this* set better calibrated.

Inputs to read directly (do not route the read):

- `docs/session-sets/accessexportertemp1-import/import-plan.md`
- `docs/session-sets/accessexportertemp1-import/change-log.md`
- `docs/session-sets/accessexportertemp1-import/ai-assignment.md`
  (every Actuals block — actuals vs. recommendation deviations are
  the highest-signal calibration data)
- `docs/session-sets/accessexportertemp1-import/session-reviews/session-*.md`
  (verifier output, especially any ISSUES_FOUND rounds)
- `docs/session-sets/accessexportertemp1-import/issue-logs/`
- `docs/planning/lessons-learned.md` (entries added or promoted as a
  result of the import set)
- `docs/planning/project-guidance.md` Conventions section, focused on
  any conventions promoted or expanded as a result of the import set
- The `docs/VBA-Parser-Migration-Harvester-Plan.md` plan itself, with
  an eye to where harvester-import discoveries shift the plan's
  session shapes, task-type choices, or risk areas

For each finding, capture:

- **Finding** — one-sentence statement of what the import set revealed.
- **Evidence pointer** — which artifact and section/round it came from.
- **Disposition** — exactly one of:
  - **adopt-now** — apply during this generator session set's later
    sessions (decomposition, stubs, or playbook). The row names
    *which* later session and *what* it should change.
  - **defer-to-integration** — applies later, when the parser
    actually plugs into `AccessHarvester.Core`. Recorded so the
    integration session set can act on it; not actioned now.
  - **no-op** — true of harvester import but does not generalize to
    parser work. Recorded so the next person doesn't re-derive it.
- **Rationale** — one sentence on why the disposition holds.

Use `route(task_type="analysis", complexity_hint=65)` for the
synthesis. The cross-provider verifier should challenge the
adopt-now / defer / no-op call on each row, not just the wording.

### Standards

- The retrospective is **opinionated**. "Possibly relevant"
  findings without a concrete adoption path are noise; either
  attach a disposition or drop the finding.
- Findings must point at evidence in the actual artifacts. A finding
  the orchestrator can't ground in a specific session-review file,
  ai-assignment Actuals block, lessons-learned entry, or
  issue-logs entry should be cut.
- Each adopt-now finding names the later session it informs (Session
  2 decomposition, Session 3 stubs, or Session 4 playbook) and the
  specific change it implies. "Be careful" is not a change.
- Cross-reference any conventions/principles promoted out of the
  import set so the decomposition doesn't re-derive them.

---

## Feature 2: Plan Decomposition (Session 2)

### Scope

Read the full plan and all 12 test-case fixtures. Build a candidate
list of child session sets covering the 9 plan phases at appropriate
granularity (a phase may be one session set if it's small, or split
into 2–3 session sets if it's large; e.g., Phase 3 *Symbol Resolution*
is large enough to warrant splitting Declaration Model + Scope Tree
into one set and Reference Resolution + With-Block + Default Member
+ Type Inference + Cross-Reference Index into another).

For each candidate child set, capture:

- Working name (slug-style, fits in a folder name).
- Plan-phase coverage (e.g., "Phase 1.1–1.4" or "Phase 3.1–3.2").
- Prerequisite child session sets (forms a DAG).
- Whether it depends on
  `accessexportertemp1-import` having committed (most parser work
  does, since it ships into `AccessHarvester.Core`; pure
  grammar-design or schema-design work does not).
- Rough session count (3–8 typically).
- Outputs (files created/touched).
- Open questions for the human.

The deliverable is `session-set-tree.md`. Use
`route(task_type="planning")` for the decomposition itself —
synthesis-heavy planning earns Opus.

### Standards

- Each child session set is sized to be completable in a normal
  effort window (3–8 sessions, each one-conversation in size). If a
  candidate set exceeds 10 sessions in the rough estimate, split
  it.
- Child sets respect the test-case fixtures: every parser feature
  the plan calls for has a corresponding test case (existing or new).
  When a feature has no test case yet, the plan's child session set
  must include a "write the test case first" session.
- The DAG must be acyclic. Prefer lots of small leaves over deep
  chains.

---

## Feature 3: Stub Specs (Session 3)

### Scope

For each child session set named in `session-set-tree.md`, write
`docs/session-sets/<child-slug>/spec.md`. Stubs are not full specs —
they document the *shape* of the work so a future orchestrator can
elaborate without re-deriving the plan.

A stub spec contains:

- Standard frontmatter (Purpose, Created, Session Set,
  Prerequisite, Workflow).
- Project Overview (1–2 paragraphs).
- Feature sections (1–N) with Scope + Standards.
- A Session Plan with **abbreviated** step lists — typically 3–5
  bullet-style hints per session rather than the full 8–10 numbered
  steps a leaf-execution spec would carry. The stub explicitly says
  *"Session N step list to be elaborated by the orchestrator
  executing it; this stub records the shape only."*
- The same routing notes that apply to that child (pulled from
  `routing-playbook.md`, see Feature 4) inlined in a "**Routing
  notes**" section so a future orchestrator does not have to
  cross-reference back.

Use `route(task_type="planning")` for the spec writing. Each child's
stub is a separate routed call so failures are localized.

### Standards

- Stubs go in their final destination
  (`docs/session-sets/<slug>/spec.md`) and are committed in this
  session set's commits, not in the future child's own first
  commit.
- Stubs are flagged in their Project Overview with the line
  *"This spec is a stub. The orchestrator executing Session 1 of
  this set must elaborate the step lists before running them."*
- Stubs cross-reference the plan section they implement
  (e.g., "Implements Phase 3.3 of
  `docs/VBA-Parser-Migration-Harvester-Plan.md`").
- Each stub names its prerequisite session sets explicitly with a
  one-line "**Prerequisite:**" entry that is the exact path the
  future orchestrator can grep for.

---

## Feature 4: Routing Playbook (Session 4)

### Scope

Write `routing-playbook.md`. For each kind of work the child session
sets will route, document:

- **Task category name** (e.g., "grammar authoring",
  "parser AST-shape design", "schema validation",
  "regression-corpus authoring", "symbol-resolution algorithm
  selection").
- **Recommended `task_type`** (existing, or new — the playbook may
  propose new task types).
- **Recommended `complexity_hint`** range and rationale.
- **Recommended model tier** (and an explicit `task_type_overrides`
  entry if the default tier is wrong).
- **Recommended `task_type_params`** overrides (effort,
  thinking_budget) with rationale.
- **Cross-provider verification posture** — should this task type
  be added to `verification.auto_verify_task_types`? When does a
  manual verification round make sense?
- **Estimated cost per call** band.

The playbook also includes a proposed *router-config.yaml delta*
section — concrete YAML snippets the human can review and the next
session set can apply. Applying the delta is **not** in scope here.

### Standards

- Playbook entries are grounded in the plan's actual content. If
  the plan does not mention a given kind of work, the playbook does
  not list it.
- Mechanical-correspondence work (schema validation, AST-vs-fixture
  diffing, parser regression checks) is calibrated to Sonnet, per
  `lessons-learned.md` → *Mechanical-Gate Task Types Calibrate To
  Sonnet-Cost*.
- Synthesis work (grammar authoring, AST-shape design, symbol-
  resolution algorithm selection, error-recovery strategy) is
  calibrated to Opus, per the same lesson.
- New task types must include their proposed entries in
  `complexity.task_type_scores`, `routing.task_type_overrides`,
  `task_type_params`, and prompt-template files (with a one-line
  description of what each template should contain — actual
  template text is left for the future session set that *applies*
  the playbook).
- The playbook must explicitly state which existing
  `task_type_params` defaults are wrong for this project's content
  and why.

Use `route(task_type="architecture")` for the playbook synthesis —
this is design work, not enumeration. Cross-provider verification is
mandatory.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
```

## Sessions
### Session 1 of 4: Harvester-Import Retrospective

**Inputs:** the completed `accessexportertemp1-import` session set's
artifacts (import-plan.md, change-log.md, ai-assignment.md
Actuals blocks, session-reviews/, issue-logs/) plus
`docs/planning/lessons-learned.md` and `docs/planning/project-guidance.md`
Conventions section.

**Steps:**
1. Read every input artifact directly (do not route).
2. Read `docs/VBA-Parser-Migration-Harvester-Plan.md` once with the
   retrospective lens — i.e., where in the plan would each
   harvester-import discovery shift session shape, task-type
   choice, or risk?
3. Draft a finding table (Finding / Evidence pointer / Disposition /
   Rationale) and route synthesis:
   `route(task_type="analysis", complexity_hint=65,
   content=<finding table draft + plan summary + relevant
   conventions/lessons>)`. The verifier should challenge each
   row's adopt-now / defer / no-op call, not just wording.
4. Iterate based on verifier output. Maximum 2 retries.
5. Write `docs/session-sets/vba-parser-session-set-generator/harvester-import-retrospective.md`
   with the final finding table.
6. End-of-session cross-provider verification (the mandatory
   `session-verification` task type).

**Creates:**
- `docs/session-sets/vba-parser-session-set-generator/harvester-import-retrospective.md`

**Touches:** none

**Ends with:** retrospective committed; adopt-now findings named for
the specific later session (2/3/4) they inform.

**Progress keys:** `session-1/read-import-artifacts`,
`session-1/read-plan-with-lens`, `session-1/draft-findings`,
`session-1/route-synthesis`, `session-1/write-retrospective`,
`session-1/verify`

---

### Session 2 of 4: Plan Decomposition

**Inputs:** Session 1's `harvester-import-retrospective.md` (apply
every adopt-now row whose target is "Session 2 decomposition").

**Steps:**
1. Verify Session 1's retrospective exists. Extract the adopt-now
   rows that target Session 2 decomposition.
2. Read `docs/VBA-Parser-Migration-Harvester-Plan.md` end-to-end
   directly (do not route the read).
3. Read every fixture under `docs/test-cases/` (input + expected
   output pairs) directly.
4. Tabulate: which plan sections does each test case exercise? Note
   plan sections with no test case yet.
5. Route the decomposition draft:
   `route(task_type="planning", complexity_hint=80,
   content=<plan summary + test-case table + retrospective
   adopt-now rows + initial cut at child session sets>)`. Verifier
   should challenge over- and under-decomposition, dependency cycles,
   missing test-case coverage, **and** whether each adopt-now row
   was actually applied.
6. Iterate based on verifier output. Maximum 2 retries.
7. Write `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`
   with the final decomposition + DAG.
8. End-of-session cross-provider verification.

**Creates:**
- `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`

**Touches:** none

**Ends with:** session-set-tree committed; child session-set names
finalized; retrospective adopt-now rows for decomposition resolved.

**Progress keys:** `session-2/read-retrospective`,
`session-2/read-plan`, `session-2/read-fixtures`,
`session-2/build-coverage-table`, `session-2/route-decomposition`,
`session-2/write-tree`, `session-2/verify`

---

### Session 3 of 4: Stub Specs

**Inputs:** Session 1's retrospective (adopt-now rows targeting
Session 3 stubs) + Session 2's `session-set-tree.md`.

**Steps:**
1. Verify Session 2's `session-set-tree.md` exists and lists every
   child session set. Extract retrospective adopt-now rows that
   target Session 3 stubs.
2. For each child session set named in the tree:
   a. Create `docs/session-sets/<child-slug>/` folder.
   b. Route a stub spec via
      `route(task_type="planning", complexity_hint=70, content=<the
      child's row from the tree + relevant plan section excerpt +
      any retrospective adopt-now rows that apply to this child>)`.
   c. Save as `docs/session-sets/<child-slug>/spec.md`.
3. Build + test: not applicable (no code touched).
4. End-of-session cross-provider verification — pass the verifier
   the tree, a sample of the generated stub specs, and the
   retrospective adopt-now rows; ask "are the stubs internally
   consistent, do they collectively cover the plan, and did they
   apply the adopt-now rows?"

**Creates:** one `spec.md` per child session set named in the tree.

**Touches:** none

**Ends with:** every child session set has a committed stub spec;
retrospective adopt-now rows for stubs resolved.

**Progress keys:** `session-3/read-retrospective`,
`session-3/iterate-children`,
`session-3/route-stub-<slug>` (one key per child),
`session-3/verify`

---

### Session 4 of 4: Routing Playbook + Change Log

**Inputs:** Session 1's retrospective (adopt-now rows targeting
Session 4 playbook) + Session 3's stub-spec inventory.

**Steps:**
1. Verify Session 3's stubs exist. Extract retrospective adopt-now
   rows that target Session 4 playbook.
2. Re-read the plan with an eye to *kinds of work* (grammar,
   AST design, schema, regression, etc.).
3. Route the playbook draft:
   `route(task_type="architecture", complexity_hint=80,
   content=<plan summary + stub-spec inventory + current
   router-config.yaml + retrospective adopt-now rows for
   playbook>)`. Auto-verified cross-provider.
4. Iterate on verifier feedback. Max 2 retries.
5. Write
   `docs/session-sets/vba-parser-session-set-generator/routing-playbook.md`.
6. **Last session — generate `change-log.md`** via
   `route(task_type="summarization", content=<the four sessions'
   deliverables summarized>)`. Save in this session set's folder.
7. Build + test: not applicable.
8. End-of-session cross-provider verification.
9. Cost report, commit + push, notification.
10. Step 9 reorganization review (post-notify): scan
    `project-guidance.md` and `lessons-learned.md` for promotion
    candidates surfaced during decomposition (e.g., if the playbook
    development surfaced a new convention for routing
    grammar-authoring tasks, propose it).

**Creates:**
- `docs/session-sets/vba-parser-session-set-generator/routing-playbook.md`
- `docs/session-sets/vba-parser-session-set-generator/change-log.md`

**Touches:** none

**Ends with:** routing playbook committed, change-log committed,
session set marked complete.

**Progress keys:** `session-4/read-retrospective`,
`session-4/reread-plan`,
`session-4/route-playbook`, `session-4/write-playbook`,
`session-4/verify`, `session-4/route-changelog`,
`session-4/commit-push`, `session-4/notify`,
`session-4/reorganization-review`

---

## Notes

- This session set is **planning**, not execution. No C# code is
  touched. No `dotnet build` / `dotnet test` runs.
- Cross-provider verification is still mandatory at end of each
  session — the deliverables are documents, but their quality
  matters as much as code quality. A sloppy decomposition
  multiplies cost across every child session set that follows.
- The session-set-tree this generates is the canonical roadmap for
  the parser work; it should be the first artifact the
  `remaining-work-planner` session set reads.
