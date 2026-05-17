# Lowdefy-as-IR Evaluation — Synthesis

> **Cross-provider perspectives:** Claude Opus 4.7 (orchestrator, initial
> position), Gemini Pro (routed via `query()`), Codex / GPT (in-IDE via
> Copilot subscription after `gpt-5-4` routed calls timed out twice).
> **Date:** 2026-05-15
> **Inputs:** [`evaluate-lowdefy-as-ir.md`](../evaluate-lowdefy-as-ir.md)
> brief, harvester `ir/` schema doc, paraphrased project-guidance
> principles, the `add-an-order.md` workflow package as the
> de-facto AI-readable layer example.

---

## Converged answer

**Option A — flat high-level domain DSL is the consensus.** Both peer
models explicitly rejected my Option B (two-stage IR with desugaring) as
over-engineered for a bounded Access→Blazor migration. The maintenance
cost of two schemas + a desugaring transformation is not justified by
flexibility a well-defined escape hatch (drop to hand-written Blazor)
already provides.

Lowdefy's primitive decomposition has value as a **set of conceptual
splits to enforce inside the high-level types**, not as the IR shape
itself. The IR's first-class types (`MasterDetailListPage`,
`ModalDetailForm`, `ContinuousForm`, `ReportWithSubreports`,
`Switchboard`, `ReportParameterForm`, etc.) are the right surface for AI
authoring; each type's schema borrows Lowdefy's discipline internally
(Requests separate from Blocks, Operators as expression tree, Events as
ordered Actions).

---

## Borrow from Lowdefy (in priority order, all three views agreed)

1. **Block / Request separation as a conceptual split inside every
   domain type.** Access conflates these (`Form.RecordSource` is a
   property of the form). Decoupling them at the IR level lets the
   same query feed a list, an export, and a chart without duplication
   and is the single most valuable idea to import. Both peers ranked
   this #1.
2. **Operator catalog as composable expression tree** with **C#-shaped
   operators** — `Nz`, `IIf`, `DLookup`, `DCount`, `DSum`, `Format`,
   plus the standard arithmetic / comparison / logical operators.
   Avoids string-template parsing in the generator.
3. **Events as ordered Actions** with `try` / `catch` / debounce
   semantics. Directly mirrors Access's `[Event Procedure]` model
   while adding hygiene primitives the user otherwise has to think
   about.
4. **`_ref`-style cross-file reuse.** Access has no equivalent and
   suffers for it. Worth importing as a pre-processing step in the
   codegen pipeline.
5. **Schema validation on every property; reject unknown properties.**
   Table stakes for any AI-authored layer. Codegen pipeline must
   reject invalid IR at the boundary.
6. **Explicit `page` vs `modal` vs `drawer` distinction** at the page
   type level. Access does it (`acDialog`, `PopUp`, `Modal`); Blazor
   needs to.

## Reject from Lowdefy (in priority order, all three views agreed)

1. **Generic Block-composition as the AI-authored surface.** A
   high-level domain DSL with first-class types per Access pattern
   shrinks the AI's surface area dramatically and pushes complexity
   into the schema instead of the emitter. The bounded scope of the
   migration is what makes this trade right; outside the migration
   scope it would be wrong.
2. **`_js` operator.** Wrong target language. Escape hatch is a
   **named method on a partial class** (type-safe, idiomatic Blazor).
   Concrete shape: an IR action like
   `{ action: CallMethod, method: "CalculateTotals" }` resolves to
   a method in `<Page>.Custom.cs`.
3. **Generic connector abstraction.** EF Core against SQL Server /
   Postgres is fixed. A thin "named query" / "named command" layer
   suffices.
4. **Reports as UI Blocks.** Reports are print-layout documents with
   `LinkMasterFields` / `LinkChildFields` / grouping / page headers
   — first-class `Report` / `Subreport` / `GroupLevel` IR types,
   likely a separate IR schema and a separate generator targeting
   a PDF library (QuestPDF / IronPDF / similar).

---

## Three additional resolved questions

1. **Workflow packages — retain, supplement, or replace?**
   **Replace as codegen input; retain as human-centric documentation.**
   Both peers converged on this. The packages excel at capturing
   business intent, invariants, evidence, and ambiguity — information
   that resists formal encoding. Gemini Pro's elegant addition:
   eventually the prose sections of a workflow package could be
   *generated from* the validated IR + its metadata, ensuring the two
   stay in sync. The IR is the deterministic source of truth for
   codegen; the package is the human-readable accompanying narrative.

2. **Reports — first-class in the IR or a separate domain?**
   **Separate IR + separate generator.** Interactive Blazor and
   paginated print are different concerns with different rendering
   targets. Forcing report grouping / page-header / `LinkMasterFields`
   into the UI IR pollutes it. Two schemas, two generators, two
   target libraries.

3. **Architectural fork A vs B vs C?**
   **Option A.** Both peers explicitly rejected my Option B
   (two-stage) as over-engineered. Option C (flat Lowdefy primitives)
   was dismissed by all three views.

---

## Where my initial position was wrong

- **Option B (two-stage).** I tentatively recommended it as
  best-of-both-worlds; both peers said the maintenance cost of two
  schemas outweighs the flexibility benefit when the escape hatch
  (hand-written Blazor for one-offs) already exists.
- **Reviewability.** I tentatively said Lowdefy wins on this dimension
  because flat YAML is denser; both peers said domain DSL is **more**
  reviewable because intent is declared (`type: ModalDetailForm`)
  rather than inferred from a generic block tree.
- **Mild understatement on the "Separation of concerns" dimension.**
  Codex called Block / Request / Operator / Action separation "the
  single most important architectural improvement"; I had filed it
  as one of several adopt items. It deserves to be ranked as the #1
  conceptual borrow.

## Where my initial position held up

- All four borrow items (Block/Request, Operator catalog with C#
  operators, Events-as-Actions, `_ref`) survived peer review unchanged.
- All four reject items (`_js`, generic connector, generic Block
  composition for reports, JS-shaped operator names) survived peer
  review unchanged.
- The "first-class types for Access patterns" framing held up — both
  peers endorsed it explicitly.

---

## Recommended next steps (consolidated, both peers proposed nearly
identical sequences)

1. **Define a JSON Schema for one high-value type.** `ModalDetailForm`
   is the recommended starting point (Gemini and Codex both named it;
   it exercises Requests, Blocks, Events, and Actions in one type
   without the complexity of master/detail or subreports).
2. **Hand-author one sample IR file.** `frmBudgetServicePlan` is the
   recommended source — Codex named it directly; it's the brief's
   listed "hard pattern" target. Manually translate the Access YAML
   into the new schema.
3. **Build a slice generator.** Minimal T4 template or C# Source
   Generator that reads only `ModalDetailForm` IR files and emits a
   `.razor` + `.razor.cs` pair. Don't try to build the full generator
   yet.
4. **Compare the generated output against the hand-authored Blazor
   pilot.** This is the cheap, reversible experiment that validates
   the Option A approach concretely before committing to broader
   schema design.

A larger commitment (full IR schema, codegen architecture, library
catalog) should wait for the answers this slice surfaces.

---

## Cost summary

| Source | Cost | Notes |
|---|---|---|
| Gemini Pro (`query()`) | $0.0528 | 24,150 input / 2,261 output tokens |
| `gpt-5-4` (`query()`, both attempts) | $0 paid; ~10 minutes wall-clock wasted | Timed out 3× per attempt at provider boundary; matches the "large architecture prompt timeout" pattern in `lessons-learned.md`. |
| Codex (in-IDE Copilot subscription) | $0 marginal | The "subscription in-IDE orchestrator pair" pattern from `project-guidance.md` → Token-Window Budget Overrides — works as designed when routed calls hit timeout walls. |
| **Total routed spend** | **$0.0528** | Cross-provider check delivered for ~5¢. |

## Workflow signal (consider at next Step 9)

The Codex-via-Copilot fallback when `gpt-5-4` timed out is a clean
real-world instance of the "subscription in-IDE orchestrator pair as
cross-provider rigor" Convention in `project-guidance.md`. This is the
**second context** (first was `vba-symbol-db-path-b-probe`) where the
pattern materially mattered — the first established the pattern after a
calibration-driven pivot; this one used it as a routed-timeout fallback.
Worth flagging at a future Step 9 review.
