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

1. **`session-set-tree.md`** — a hierarchical map: VBA-parser-plan →
   child session sets → rough session count per child → dependency
   graph between children. One row per child session set with
   purpose, scope, prerequisites, estimated session count, and
   pointers into the plan.
2. **One stub `spec.md` per child session set**, written with enough
   scope detail that a future orchestrator can execute it without
   re-deriving the plan. Stubs use the standard session-set spec
   format (Project Overview / Features / Session Plan), but session
   step lists are abbreviated when later refinement is expected — the
   stub records the *shape* of the work, not necessarily every step.
3. **`routing-playbook.md`** — for each kind of work the child sets
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

## Feature 1: Plan Decomposition (Session 1)

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

## Feature 2: Stub Specs (Session 2)

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
  `routing-playbook.md`, see Feature 3) inlined in a "**Routing
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

## Feature 3: Routing Playbook (Session 3)

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

## Session Plan

### Session 1 of 3: Plan Decomposition

**Steps:**
1. Read `docs/VBA-Parser-Migration-Harvester-Plan.md` end-to-end
   directly (do not route the read).
2. Read every fixture under `docs/test-cases/` (input + expected
   output pairs) directly.
3. Tabulate: which plan sections does each test case exercise? Note
   plan sections with no test case yet.
4. Route the decomposition draft:
   `route(task_type="planning", complexity_hint=80,
   content=<plan summary + test-case table + initial cut at child
   session sets>)`. Verifier should challenge over- and
   under-decomposition, dependency cycles, and missing test-case
   coverage.
5. Iterate based on verifier output. Maximum 2 retries.
6. Write `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`
   with the final decomposition + DAG.
7. End-of-session cross-provider verification (this is the
   `session-verification` task type, mandatory).

**Creates:**
- `docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`

**Touches:** none

**Ends with:** session-set-tree committed; child session-set names
finalized.

**Progress keys:** `session-1/read-plan`, `session-1/read-fixtures`,
`session-1/build-coverage-table`, `session-1/route-decomposition`,
`session-1/write-tree`, `session-1/verify`

---

### Session 2 of 3: Stub Specs

**Steps:**
1. Verify Session 1's `session-set-tree.md` exists and lists every
   child session set.
2. For each child session set named in the tree:
   a. Create `docs/session-sets/<child-slug>/` folder.
   b. Route a stub spec via
      `route(task_type="planning", complexity_hint=70, content=<the
      child's row from the tree + relevant plan section excerpt>)`.
   c. Save as `docs/session-sets/<child-slug>/spec.md`.
3. Build + test: not applicable (no code touched).
4. End-of-session cross-provider verification — pass the verifier
   the tree plus a sample of the generated stub specs and ask "are
   the stubs internally consistent and do they collectively cover
   the plan?"

**Creates:** one `spec.md` per child session set named in the tree.

**Touches:** none

**Ends with:** every child session set has a committed stub spec.

**Progress keys:** `session-2/iterate-children`,
`session-2/route-stub-<slug>` (one key per child),
`session-2/verify`

---

### Session 3 of 3: Routing Playbook + Change Log

**Steps:**
1. Verify Session 2's stubs exist.
2. Re-read the plan with an eye to *kinds of work* (grammar,
   AST design, schema, regression, etc.).
3. Route the playbook draft:
   `route(task_type="architecture", complexity_hint=80,
   content=<plan summary + stub-spec inventory + current
   router-config.yaml>)`. Auto-verified cross-provider.
4. Iterate on verifier feedback. Max 2 retries.
5. Write
   `docs/session-sets/vba-parser-session-set-generator/routing-playbook.md`.
6. **Last session — generate `change-log.md`** via
   `route(task_type="summarization", content=<the three sessions'
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

**Progress keys:** `session-3/reread-plan`,
`session-3/route-playbook`, `session-3/write-playbook`,
`session-3/verify`, `session-3/route-changelog`,
`session-3/commit-push`, `session-3/notify`,
`session-3/reorganization-review`

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
