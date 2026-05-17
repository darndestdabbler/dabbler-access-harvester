# Change Log — `lowdefy-ir-foundation`

> **Status:** Closed 2026-05-17. Two sessions, both VERIFIED on
> round 2 by cross-provider session-verification (GPT-5.4 per the
> active `task_type_overrides` for the Claude orchestrator). One
> Step 7 disagreement (Session 2 F1) resolved via context-gap
> reverify-reshaped after the orchestrator amended the spec to
> record the console-exe deliverable.
>
> **Authoring note:** This change-log is authored 2026-05-17 as a
> housekeeping artifact during the Phase 3 reset (see
> `docs/planning/strategy.md` → Revisions → 2026-05-17). The
> session-state.json notes field already carried the closeout
> summary; this file extracts that summary into the canonical
> `change-log.md` location so downstream readers and the
> session-set listing tooling find it where expected.

---

## Purpose this set served

Probe whether a Lowdefy-style JSON Schema is a workable
intermediate representation (IR) for the Blazor migration, and
prove that a minimal slice generator can compile a generated
component into the existing pilot host (`output-tests/blazor-
add-an-order/`). The probe was scoped to a single concrete
form — Northwind's `frmBudgetServicePlan` — as the proving
ground.

Companion to the harvester's broader "what does an AI agent
actually need to interpret this?" probe arc. Lowdefy was
selected for evaluation because it has a published JSON Schema
shape that an AI could in principle target without a custom
language.

The probe is explicitly distinct from a production IR generator
— see "Probe verdict and production gate" below.

---

## What shipped

### Schema and sample (Session 1)

- **`AccessHarvester.Core/Ir/ModalDetailFormSchema.json`** — JSON
  Schema describing a modal-detail-form IR shape. Defines a
  block tree (container, field, label, button, datagrid, ...),
  per-block `properties`, `state` declarations, `events` with
  per-event-name handler bodies, `validate` rules with
  conditional gates, `requests` with cardinality discrimination,
  and `operators` for reusable expression fragments.
- **`AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml`**
  — Hand-authored IR sample for the `frmBudgetServicePlan`
  Northwind form. Exercises the schema across all major shapes
  (validators, requests, state, events).

### Generator and generated artifacts (Session 2)

- **`tools/AccessHarvester.IrCodegen/`** — standalone .NET console
  exe slice generator. Reads a `.ir.yaml` file, emits a Razor
  component + a partial class. Generation is mechanical (no AI
  interpretation in the codegen path).
- **`output-tests/blazor-add-an-order/Ui/Pages/Generated/`** —
  the generated `FrmBudgetServicePlan.razor` + `.razor.cs`
  pair, compiled into the pilot host's csproj clean. Compile-
  success was the Session-2 success gate; runtime usefulness was
  NOT proved (see "Probe verdict and production gate").
- **`generator-validation-notes.md`** — Session 2's record of
  what the generator did and didn't surface from the IR; the
  source of the post-set refinements log.
- **`schema-refinements.md`** — refinements backlog logged from
  both Session 1's verifier deferrals and Session 2's
  generator findings. The post-set cleanup pass (2026-05-17)
  landed 24 platform-independent operator additions, optional
  `field` on Validate rules, explicit `cardinality` on requests,
  state-field initializers, schema-anchored event naming,
  `visible`/`enabled` block-level conditionals, and the
  `_op` operator method emission pattern. Platform-shape-
  dependent items (`SR-2`, `SR-4`, `SR-5`, `SR-E`, `SR-N`)
  remain deferred pending consumption by the future migration
  repo.

### Verification trail (`session-reviews/`)

Each session's raw verifier output preserved per the workflow
discipline. Both sessions were ISSUES_FOUND on round 1 with
findings applied or adjudicated before round 2 returned VERIFIED.

---

## Probe verdict and production gate

The probe answered "yes, the IR shape can carry a non-trivial
real Northwind form, and a mechanical generator can compile it
into the pilot host." That is the entirety of the verdict.

**The probe did NOT prove:**
- That the generated Razor + partial-class pair is *useful at
  runtime* (no smoke test of the generated component running in
  the pilot host's request flow).
- That the schema scales beyond ModalDetailForm to the other
  shapes the migration will need (ReportRoot, MasterDetailList,
  Dashboard, etc.).
- That typed DTOs and a request-runner contract pull their
  weight relative to hand-authored services.

**Production gate (per `docs/planning/strategy.md` → Frozen
Unless Reauthorized → 2026-05-17):** the IR generator stays a
probe artifact at `tools/AccessHarvester.IrCodegen/`. No
production generator work is authorized until a consuming
workflow (e.g., the future migration repo's first real form)
demonstrates the IR + generator path is preferable to hand-
authoring. The schema-refinements backlog stays open and the
deferred platform-shape items wait for that consumer.

---

## Per-session summary

| Session | Title | What landed | Verified by |
|---|---|---|---|
| 1 | ModalDetailForm Schema + Hand-Authored Sample | `ModalDetailFormSchema.json`, `frmBudgetServicePlan.ir.yaml`, ai-assignment.md. Round 1 ISSUES_FOUND; findings applied; round 2 VERIFIED at $0.64 routed. | GPT-5.4 — VERIFIED round 2. |
| 2 | Slice Generator + Validation | `tools/AccessHarvester.IrCodegen/` console-exe, generated component pair at `output-tests/blazor-add-an-order/Ui/Pages/Generated/`, `generator-validation-notes.md`, `schema-refinements.md`. Round 1 ISSUES_FOUND with one Step 7 disagreement (F1 — context-gap, resolved by reshaping the verifier prompt to include the spec amendment recording the console-exe deliverable); round 2 VERIFIED at $0.76 routed. | GPT-5.4 — VERIFIED round 2 after one context-gap reshape. |

**Set total routed cost:** ~$1.40 across two sessions.

---

## Notable adjudication

**Session 2 — F1 (Step 7 disagreement, resolved as context-gap).**
The Round-1 verifier flagged the absence of a console-exe
deliverable. The orchestrator's position was that the deliverable
WAS present but the spec hadn't been amended to record it
explicitly. Per the Step 7 disagreement procedure, the human
adjudicated: the resolution was `reverify-reshaped` — amend the
spec to record the console-exe deliverable, then re-route with
the amended spec. Round 2 returned VERIFIED. The
`record_adjudication` call captured cause `context-gap`,
resolution `reverify-reshaped`.

---

## Output artifact paths (canonical)

| Artifact | Path |
|---|---|
| Schema | `AccessHarvester.Core/Ir/ModalDetailFormSchema.json` |
| Sample IR | `AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml` |
| Generator | `tools/AccessHarvester.IrCodegen/` |
| Generated component | `output-tests/blazor-add-an-order/Ui/Pages/Generated/FrmBudgetServicePlan.razor` (+ `.razor.cs`) |
| Generator notes | `docs/session-sets/lowdefy-ir-foundation/generator-validation-notes.md` |
| Refinements backlog | `docs/session-sets/lowdefy-ir-foundation/schema-refinements.md` |
| Per-session reviews | `docs/session-sets/lowdefy-ir-foundation/session-reviews/` |
| AI assignment ledger | `docs/session-sets/lowdefy-ir-foundation/ai-assignment.md` |

---

## Lessons surfaced

Step 9 reorganization review for this set committed two lessons
in the 2026-05-17 commit window (see git history — commits
`36fdbc3` "address platform-independent SR refinements" and
`7cb7fb6` "Session set lowdefy-ir-foundation: Step 9
reorganization"). The Step 9 work landed in `project-guidance.md`
and `lessons-learned.md` rather than as a separate artifact in
this set.

---

## Where this set fits in the strategy

Pre-2026-05-17 framing: an early probe of whether a published IR
schema could substitute for hand-authoring Blazor slices. Post-
2026-05-17 framing (after the Phase 3 reset; see
`docs/planning/strategy.md` → Revisions → 2026-05-17): the probe
verdict is preserved; production generator work is frozen until
the future migration repo demonstrates real-world demand.

The `schema-refinements.md` backlog is the executable forward
list — that file, plus this change-log, are what the future
migration repo (or any later IR-resumption session set) should
read before reopening this work-stream.
