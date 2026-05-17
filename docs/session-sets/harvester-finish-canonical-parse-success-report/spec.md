# Canonical Parse-Success Report Spec — **STUB**

> **Status:** STUB authored 2026-05-17 by `remaining-work-planner` Session 2.
> Acceptance criteria are pinned; implementation shape (file layout,
> harness wiring, fixture filtering details) is intentionally left for
> the set's own Session 1 to elaborate per the Session-1-vs-Session-2
> boundary discipline. **Treat the lists below as the contract; do
> NOT widen scope without a strategy-level re-authorization.**
>
> **Purpose:** Produce a committed, diff-against-on-regression
> parse-success report per canonical fixture so Phase 4 Criterion #1
> ("every documented VBA / Jet SQL / property-sheet construct in the
> canonical fixtures parses correctly OR emits an explicit
> `UnresolvedItem` with provenance and reason") is verifiable rather
> than asserted.
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/harvester-finish-canonical-parse-success-report/`
> **Prerequisite:** `remaining-work-planner` (this stub) and the four
> existing parser/resolver session sets — all complete.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface. Deliverables are parse-success report
> JSON files and a regression test that diffs them. Automated build
> / test plus cross-provider AI verification is the complete quality
> bar.

---

## Origin and authority

- **Authority for scope:** the gap audit at
  [`docs/session-sets/remaining-work-planner/gap-audit.md`](../remaining-work-planner/gap-audit.md)
  → GAP #1.
- **Authority for "Done":**
  [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
  Criterion #1.
- **Authority for canonical-fixture list:** the planner's resolved
  Open Question 1 — **two fixtures: `NorthwindDevEdition.accdb` +
  `HomeHealthCare.accdb`.** MiniComplex is OUT.
- **Authority for construct scope:** the planner's resolved Open
  Question 2 — **HYBRID, inventory-driven:** (a) VBA + (b) Jet SQL +
  (c) macros are definitely in; (d) property-sheet expressions get an
  inventory pass first (see Session 1 below).

---

## Project Overview

The harvester has shipped a complete VBA parser
(`AccessHarvester.Vba.Parser` + `Vba.g4`), Jet SQL parser
(`AccessHarvester.Sql.Parser` + `JetSql.g4`), macro action enricher
(`MacroActionsEnricher`), and `UnresolvedItem` /
`UnresolvedItemAggregator` for explicit non-silent fallbacks. What is
missing is a per-fixture, committed artifact that says: *"for
fixture X, here is the count of constructs parsed and the count of
`UnresolvedItem` records emitted, broken down by source."* Without
that, "the parser handles the canonical fixtures" is a claim that
cannot regress visibly when a future grammar edit silently drops a
construct.

This set produces that artifact.

### In scope

1. A `parse-success-report.json` (or equivalent name picked at
   stub-expansion time) per canonical fixture, committed at a
   conventional location (Session 1 decides — leading candidates:
   `samples/parse-success-reports/<fixture>.json` or
   `tests/.../GoldenParseReports/<fixture>.json`).
2. The report enumerates by source:
   - **VBA modules:** total modules / total procedures / parse errors
     / `ERROR_TOKEN` lexer-gap counts.
   - **Jet SQL queries:** total queries / parse OK / parse failed.
   - **Macros:** total macro objects / actions parsed / actions
     unresolved.
   - **Property-sheet expressions** (conditional — see Session 1's
     inventory pass).
3. The report also enumerates `UnresolvedItem` records produced by
   `UnresolvedItemAggregator` grouped by `Source` and `Reason` with
   counts and exemplary locations.
4. A regression test (under `tests/AccessHarvester.Integration/` or
   `tests/AccessHarvester.Pipeline.Tests/` — Session 1 decides)
   re-generates the report and asserts byte-equality against the
   committed golden.
5. Each new `UnresolvedItem` reason that surfaces during report
   generation gets a one-paragraph note in the report (or a sibling
   doc) describing the construct and the recommended downstream-AI
   interpretation. This is the manual-review queue's authoritative
   per-reason narrative.

### Out of scope

- Adding new parser features. If the inventory pass surfaces
  constructs not currently handled, the report records them as
  `UnresolvedItem` entries (or as "lexer gap" / "no rule" counts)
  rather than triggering parser-extension work in this set.
  Parser-extension scope, if any, is a separate strategy-level
  decision.
- Property-sheet inventory beyond what's required to decide whether
  (d) folds into the set (see Session 1).
- Wiring the harvester's per-run `UnresolvedItems.json` output —
  that's `harvester-finish-canonical-golden-output`'s job.
  This set computes counts post-hoc against existing aggregator
  output rather than relying on a not-yet-wired CLI emission.
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**
  Specifically: Blazor application slices / workflows / pilots,
  production IR codegen, `dabbler-platform-discovery` refresh,
  additional Northwind workflows beyond the existing four pilot
  packages, CRUD-scaffolder expansion, and any item the 2026-05-17
  strategic review froze. If this set's work surfaces evidence that
  one of those should be reauthorized, the orchestrator surfaces
  to the human; the planner does NOT re-authorize on its own
  authority.

---

## Sessions

### Session 1 of 2: Inventory + Report Shape

**Steps:**

1. Read the gap-audit GAP #1 entry + this stub end-to-end.
2. Elaborate this stub into a full spec (add Session-1-vs-Session-2
   step lists, file paths, the exact report JSON shape). *(Session 1
   only — stub expansion.)*
3. Author `ai-assignment.md` via routed analysis (Rule #17).
4. **Property-sheet inventory pass.** Enumerate expression-bearing
   property-sheet fields (validation rules, default values, input
   masks, format strings) across `NorthwindDevEdition.accdb` +
   `HomeHealthCare.accdb`. Decide:
   - **If small/bounded** (rough threshold: < ~15 unique
     expressions per fixture, simple shapes routable through the
     existing VBA expression grammar or a thin new sub-rule): fold
     property-sheet expressions into the report.
   - **If long-tail** (many unique shapes, multiple expression
     dialects, dynamic-format embeds): exclude from gating;
     emit `UnresolvedItem` records for non-mechanically-decodable
     cases so the harvester ships with explicit gaps rather than
     silent ones.
5. Generate a first-pass `parse-success-report.json` per fixture
   from the current parser output (no parser changes — only
   counting + grouping + UnresolvedItem aggregation against the
   live pipeline).
6. Commit the per-fixture reports as the **interim** golden; flag
   any surprises (high `ERROR_TOKEN` counts, unexpected
   `UnresolvedItem` reason families) for human review BEFORE
   pinning the regression test.
7. End-of-session cross-provider verification.
8. Commit + push + notify per workflow Step 8.

**Creates:**
- `docs/session-sets/harvester-finish-canonical-parse-success-report/ai-assignment.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/property-sheet-inventory.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews/session-001.md`
- The first-pass `parse-success-report.json` per fixture (path
  decided at stub expansion).

**Touches:**
- `docs/session-sets/harvester-finish-canonical-parse-success-report/spec.md`
  (stub expansion).

**Ends with:** interim per-fixture parse-success reports committed;
property-sheet (d) verdict recorded; any human-attention surprises
surfaced.

---

### Session 2 of 2: Regression Gate + Documentation

**Steps:**

1. Author the regression test that re-generates the report and
   diffs against the committed golden. Failure mode is clear:
   `[diff between expected and actual report]`.
2. Document every `UnresolvedItem` reason that appears in the
   report — at minimum a one-paragraph "what the construct is and
   what a downstream AI should do with it." Lands in the report
   itself or a sibling doc (Session 1's stub-expansion decides
   which).
3. Run `dotnet build && dotnet test` sequentially (per
   project-guidance Convention).
4. End-of-session cross-provider verification.
5. Closeout: `disposition.json` (per Convention),
   `change-log.md` via `task_type="summarization"` (or
   in-conversation if orchestrator is Opus/Sonnet),
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
6. Step 9 reorganization review of `project-guidance.md` and
   `lessons-learned.md` AFTER notify.

**Creates:**
- The regression test class (path decided at stub expansion).
- `docs/session-sets/harvester-finish-canonical-parse-success-report/change-log.md`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/disposition.json`
- `docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews/session-002.md`

**Touches:**
- The committed per-fixture golden reports (final pin).

**Ends with:** Phase 4 Criterion #1 has a CI gate that fails on
parse-coverage regression; the manual-review queue has a per-reason
narrative the next downstream AI consumer can read.

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.20 per session
  (cross-provider session-verification only). ~$0.10–$0.40
  across the set.
- **In-conversation token spend:** Moderate. Inventory pass + report
  shape design are synthesis tasks; the regression-test wiring is
  largely mechanical.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Inventory reveals property-sheet expressions need a new grammar dialect** | Session 1's inventory uncovers a non-trivial sub-language (e.g., format-string mini-grammar, validation-rule dialect divergence) | Stop. Surface to the human. Either declare property-sheet expressions OUT (per Open Question 2's fallback path) or open a separate parser-extension session set; do not absorb into Phase 4. |
| **`UnresolvedItem` count explodes** | Report shows hundreds of unresolved items in a single reason family, signalling a parser regression | Stop. Surface to the human. The fix lives in the affected parser/resolver project as a tree-amendment, not in this set. |
| **Two reports cannot agree on counts across runs** | Repeated regeneration produces different counts; signals non-determinism in the aggregator | Stop. Surface to the human. Determinism is the project-guidance.md → VBA Parser Strategy non-negotiable; non-determinism is a strategy-level defect, not a Phase-4-gate adjustment. |

---

## Why this is a stub

The stub fixes the contract: per-fixture report, regression gate,
per-reason narrative. It deliberately leaves the file paths, exact
JSON shape, harness wiring, and inventory threshold for the set's own
Session 1 — the orchestrator running that session has full repo state
loaded and is better-positioned to choose paths that match existing
conventions.
