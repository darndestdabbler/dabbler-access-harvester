# Lowdefy-as-IR Foundation Spec

> **Purpose:** Implement the cross-provider consensus from the lowdefy
> evaluation (Claude, Gemini Pro, Codex) on IR architecture for
> Access-to-Blazor codegen. Deliver a proof-of-concept IR schema
> (ModalDetailForm), a hand-authored sample, a minimal slice generator,
> and validation against the existing Blazor pilot.
> **Created:** 2026-05-16
> **Session Set:** `docs/session-sets/lowdefy-ir-foundation/`
> **Prerequisite:** Completion of `docs/lowdefy-evaluation/synthesis.md`
> cross-provider review (completed 2026-05-15).
> **Workflow:** Orchestrator → build locally → cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> **Rationale:** This set is pure schema + tooling design (JSON Schema, T4 template, code-gen infrastructure). No UI surfaces are modified, no Blazor runtime behavior is affected; the generator output is compared against existing hand-written Blazor for design validation only, not shipped as the production generator. UAT and E2E are deferred until a production-ready generator exists.

---

## Project Overview

The lowdefy evaluation (May 2026) concluded that the IR for Access-to-Blazor
codegen should adopt a **flat, high-level domain DSL** (Option A) with
first-class types per Access pattern (`MasterDetailListPage`,
`ModalDetailForm`, `ContinuousForm`, etc.), borrowing Lowdefy's conceptual
discipline internally (Requests separate from Blocks, Operators as expression
trees, Events as ordered Actions).

This session set implements the agreed-upon first step: define a working
schema for `ModalDetailForm`, hand-author a realistic sample, build a
minimal code generator for that one type, and compare its output against
the hand-written Blazor pilot in `output-tests/blazor-add-an-order/`.

**Success definition:** a reversible, vertical-slice experiment that
validates the Option A approach concretely before committing to broader
schema design or a full generator.

---

## Features

### Feature 1: ModalDetailForm JSON Schema

Define a strict JSON Schema for `ModalDetailForm` IR files, incorporating
the consensus borrowings from Lowdefy:

- **Requests** section (decoupled from layout): named, parameterized queries
  and commands (load, save).
- **Layout** section (display): Blocks (TextBox, ComboBox, Button, etc.) with
  explicit binding to controls.
- **Operators** section (optional, for cross-file use): named, reusable
  expression trees with C#-shaped operator catalog (`Nz`, `IIf`, `DLookup`,
  `DCount`, `DSum`, standard arithmetic/comparison/logical).
- **Events** section: named event triggers (onClick, onInit, onSubmit, etc.)
  mapped to ordered action lists (Validate, Request, SetState, CallMethod,
  CloseModal, etc.) with `try`/`catch` semantics.
- **Metadata**: `pageType`, `title`, `modalType` (page vs modal vs drawer),
  etc.

**Output:** `AccessHarvester.Core/Ir/ModalDetailFormSchema.json` —
strict JSON Schema. Reject unknown properties. Validate every field type.

**Reference:** Synthesis recommends starting with ModalDetailForm because
it exercises Requests, Blocks, Events, and Actions without the complexity
of master/detail or subreports.

### Feature 2: Hand-Authored Sample IR

Manually translate `frmBudgetServicePlan` (the "edit budget service plan
modal" form from `samples/HomeHealthCare.accdb`) from its Access-shaped IR
into the new ModalDetailForm schema.

**Scope:** The detail-modal pattern only — capture:
- Load request (fetch the plan record by ID from OpenArgs)
- Layout: TextBox controls for plan fields, ComboBox for need selection
- Events: onInit (load data), onChange handlers for validation, onSave
  (persist), onCancel (close).
- Operators: any computed visibilities or validation rules (e.g., IIf,
  DLookup for need-availability checks).

**Output:** `AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml` —
hand-authored IR file conforming to the ModalDetailFormSchema. (Originally
specced as `tmp/ir-samples/...`, but `tmp/` is gitignored; samples must live
next to the schema to be committed.)

**Anti-scope:** Do not hand-author the full Add-an-Order workflow. One
modal is enough for validation; leave other forms for later.

### Feature 3: Minimal Slice Generator

Build a code generator that reads `ModalDetailForm` IR files and emits a
Blazor component pair (`.razor` and `.razor.cs` code-behind).

**Approach:** T4 template (`.tt` file), C# Source Generator, **or a
standalone .NET console executable that walks the IR and writes the
output files.** Recommend the standalone console exe for this slice
(easier .NET 10 + System.Text.Json plumbing, closer to a future Source
Generator port than a `.tt` template, and avoids the build-tool
dependency on `dotnet-t4`).

> **Spec amendment (Session 2 of 2, 2026-05-17):** the original spec
> listed only T4 or Source Generator. The console-exe option was added
> after the orchestrator's pre-Session-2 decision prompt; the actual
> deliverable lives at [`tools/AccessHarvester.IrCodegen/`](../../../tools/AccessHarvester.IrCodegen/),
> which is a console exe. T4 / Source Generator remain valid follow-up
> targets when a production codegen path is built.

**Generator input:** one IR file (ModalDetailForm schema).
**Generator output:** one `.razor` + one `.razor.cs` pair that:
- Declares the Blazor component and its parameters (bound request, initial
  data).
- Emits MudBlazor form controls for each Block in the layout.
- Wires up event handlers (onClick, onInit, onSave, etc.) to C# methods
  that execute the action sequences from the IR.
- Generates placeholders for Actions that require hand-written logic
  (Request/SaveData calls, custom validation).

**Complexity:** Keep it under 200 lines of template. The goal is not a
production generator — it's to surface friction in the schema before
expanding.

**Output:** `tools/ModalDetailFormGenerator.tt` — T4 template (or equivalent
Source Generator).

### Feature 4: Comparison Against Pilot

Run the generator on the hand-authored `frmBudgetServicePlan.ir.yaml`.
Compile and compare the generated `.razor` / `.razor.cs` pair against:

1. The hand-written modal in `output-tests/blazor-add-an-order/` (the
   budget service plan detail modal from Session 3, if it exists).
2. The manually-reviewed expected output (if hand-written example does not
   exist, code-review the template output for reasonableness).

**Validation checklist:**
- Does the generated layout match the intended UI? (control names, field
  bindings, visibility rules)
- Does the event-action sequence make sense? (are action order and
  sequencing preserved?)
- Is the generated code readable and review-able, or is it tangled?
- What gaps did the generator miss? (e.g., cross-field validation, computed
  totals, custom formatting)

**Output:** `docs/session-sets/lowdefy-ir-foundation/generator-validation-notes.md`
— findings, friction points, gaps.

### Feature 5: Schema Refinements Log

Capture all friction surfaced during hand-authoring and generation. For
each issue, note:
- **What failed:** schema gap, generator limitation, semantic mismatch, etc.
- **Why:** missing field? operator not C#-idiomatic? missing action type?
- **Recommended fix:** minimal change to the schema, or skip (out of scope).

**Output:** `docs/session-sets/lowdefy-ir-foundation/schema-refinements.md`
— prioritized backlog of schema changes for follow-up sessions.

---

## Session Plan

### Session 1 of 2: Schema + Hand-Authored Sample

- Define ModalDetailFormSchema.json (Feature 1) by hand, incorporating
  the consensus from the lowdefy evaluation.
- Hand-author `frmBudgetServicePlan.ir.yaml` (Feature 2) using the schema.
  Surface friction: what feels awkward? What's missing?
- Route an `analysis` call (cross-provider review) on the schema for
  completeness and C# idiomaticity.
- Land both schema and sample file with commit.

**Success criteria:** schema is strict, validates, rejects unknown
properties; sample IR is readable and mirrors the form's structure.

### Session 2 of 2: Generator + Validation

- Build the minimal T4 template (Feature 3) that reads ModalDetailForm IR
  and emits `.razor` + `.razor.cs`.
- Run the generator on the hand-authored sample.
- Compare generated output against the Blazor pilot (Feature 4).
- Capture all friction and schema gaps (Feature 5).
- Route an `analysis` call (cross-provider review) on the generated Blazor
  and the schema refinements.
- Land generator, validation notes, and refinements log with commit.

**Success criteria:** generator produces compilable Blazor that is readable
on review, and the refinements log is specific enough to guide the next
schema iteration.

---

## Boundaries

- **No full IR implementation.** This set covers ModalDetailForm only. Other
  types (MasterDetailListPage, ContinuousForm, etc.) are out of scope.
- **No full generator.** T4 template or Source Generator for one type only.
  Full codegen library is a follow-up.
- **No report IR.** The consensus separated report IR from form IR. Reports
  are deferred to a separate session set.
- **No `_ref` preprocessing.** Cross-file includes are documented in the
  schema but not implemented in this generator.
- **Blazor pilot is reference only.** Do not modify the pilot during this
  set. If generator output reveals pilot gaps, flag them; don't patch.

---

## Routing Notes

- **Schema review (Session 1):** `route(task_type="analysis",
  complexity_hint=40)` — shape a JSON Schema for a bounded type, validate
  against the lowdefy consensus.
- **Generator design (Session 2):** `route(task_type="code-review",
  complexity_hint=45)` — review the T4 template and generated Blazor for
  readability and correctness.
- **Session verification:** `route(task_type="session-verification",
  complexity_hint=60)` — verify schema completeness, generator correctness,
  and refinements log relevance. Disclose which Access form was hand-authored
  and what controls it exercises (modal, textbox, combobox, buttons).

**Recommended orchestrator:** Sonnet 4.6 across both sessions. Schema
design and generator templates are well-scoped mechanical work, not
architectural synthesis.

---

## Artifacts & Deliverables

### Session 1
- `AccessHarvester.Core/Ir/ModalDetailFormSchema.json` — JSON Schema
- `AccessHarvester.Core/Ir/samples/frmBudgetServicePlan.ir.yaml` — hand-authored IR sample
- Git commit

### Session 2
- `tools/AccessHarvester.IrCodegen/` — standalone .NET console exe (per
  the Session 2 spec amendment; replaces the originally-specced
  `tools/ModalDetailFormGenerator.tt` T4 template)
- Generated sample output (`.razor` + `.razor.cs` files in `tmp/`)
- `docs/session-sets/lowdefy-ir-foundation/generator-validation-notes.md`
- `docs/session-sets/lowdefy-ir-foundation/schema-refinements.md`
- Git commit

### Session State
- `session-state.json` updated with session numbers and completion status

---

## Next Steps (After This Set)

1. **Report IR foundation.** Use the same approach (schema + sample + slice
   generator) for the `Report` type (includes GroupLevel, LinkMasterFields,
   page headers, etc.). Consider a separate report-generation library
   (QuestPDF / IronPDF target) rather than Blazor.
2. **Expand domain types.** Once ModalDetailForm is validated,
   MasterDetailListPage, ContinuousForm, Switchboard, etc. follow the same
   pattern.
3. **`_ref` preprocessing.** Implement cross-file include resolution for
   the full IR.
4. **Full generator.** Merge all slice generators into a single, production
   code-gen pipeline.

---

## Open Questions

1. **T4 vs Source Generator.** Should the template use T4 (simpler,
   instant feedback) or C# Source Generator (better integration with
   `dotnet build`)? Recommend T4 for this validation set; Source Generator
   for production.
2. **Generator error handling.** How chatty should generator errors be?
   Recommend: fail loudly on schema violations (missing required fields,
   unknown operators), warn on soft issues (unused requests, no event
   handlers).
3. **Placeholder action types.** Some actions (Request/SaveData, cross-field
   validation) will need hand-written C# in the code-behind. Should the
   generator emit method stubs, comments, or exceptions? Recommend: emit
   `// TODO: implement <action name>` comments with a method signature
   placeholder.
