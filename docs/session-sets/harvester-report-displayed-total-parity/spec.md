# Harvester Report Displayed-Total Parity Spec

> **Purpose:** Capture, for each canonical report whose displayed total is a
> footer-control expression (e.g. `rptInvoice`), a **computed expected
> total derived from the RecordSource result set** — the number a human
> would actually read on the report — so downstream report-migration work
> has a deterministic, number-level regression guard without depending on
> a rendered PDF. Separately, wire the existing PDF render path as an
> **optional, non-blocking human visual spot-check** (layout/format/
> grouping), captured opportunistically and never part of the automated
> gate.
> **Created:** 2026-06-01
> **Session Set:** `docs/session-sets/harvester-report-displayed-total-parity/`
> **Prerequisite:** `harvester-report-baseline-golden-corpus` complete.
> That set committed the aggregates-only `reports-baseline/` corpus and
> wired it into `CanonicalGoldenRegressionTests`. This set extends the
> per-report `aggregates.json` with footer-expression display totals.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification.
> Full tier; no UI, so no UAT/E2E.
> **Status:** Draft (authored 2026-06-01 in response to a consumer-repo
> request; pending human go-ahead to start Session 1).

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites:
  - slug: harvester-report-baseline-golden-corpus
    condition: complete
totalSessions: 2
```

> Rationale: a CLI/data-artifact follow-up set. Quality bar is focused
> Core code + tests + mandatory cross-provider verification plus COM
> corpus regeneration; no human UAT or browser E2E applies.

---

## Why This Set, Why Now

The committed `reports-baseline/<report>/aggregates.json` artifacts carry
an explicit contract note:

> "Aggregates reflect the report's RecordSource query result set (executed
> via ACE OLE DB), not the rendered report's footer-control expressions.
> Parity comparisons must compare like-for-like."

That is correct and deliberate — but it leaves a gap for reports whose
*displayed* total is a footer expression rather than a raw column sum. On
`rptInvoice`, for example, the meaningful number is the invoice total a
footer text box computes (e.g. an extended-price sum over
`UnitPrice * Quantity * (1 - Discount)`), not the bare `GrandTotals` of
the individual `UnitPrice` / `Quantity` / `Discount` columns the result
set already exposes.

A downstream migration needs to assert that the re-implemented report
shows the *same total a user sees*. The agreed approach (former-Access-dev
guidance, 2026-06-01) is:

- Gate report parity on **aggregates** as the primary, automated signal.
- For reports whose displayed total is a footer expression, add a
  **computed expected-total check derived from the RecordSource** — do
  not depend on a PDF for the number.
- Keep the rendered **PDF as an optional human visual spot-check**
  (layout/format/grouping), captured opportunistically once the PDF path
  is solid — **never blocking**.

The capability is feasible with the code already in place. The
report-baseline path opens each report in Design view
(`AccessReportSpecReader.ReadSpec`) and executes the RecordSource SQL via
ACE OLE DB (`ReportAggregateComputer`, which already computes
`GrandTotals` over the in-memory result set). The remaining work is to
(a) read the footer/group-footer aggregate controls while the report is
already open, and (b) evaluate those expressions over the same result set.

## Scope

1. **Read footer-expression total controls.** Extend
   `AccessReportSpecReader.ReadSpec` to enumerate the report's
   footer-section text boxes (report footer + group footers) whose
   `ControlSource` is an aggregate expression (begins with `=`, calls a
   supported aggregate function) **and** whose `RunningSum` property is
   off. Capture control name, owning section, and the raw expression on
   the `ReportLiveSpec`. Reading happens in the same already-open
   Design-view COM session — no new lock window.
2. **Evaluate supported footer aggregates over the result set.** In
   `ReportAggregateComputer`, evaluate each captured footer expression
   against the already-materialized RecordSource result set, reusing the
   repo's existing Access-expression parser for the arithmetic inside the
   aggregate call. Support exactly the **closed set of aggregate
   functions the canonical report footers actually use** (survey in
   Session 1; `Sum` at minimum). Emit results as a new
   `DisplayTotals` collection on `ReportAggregates` /
   `aggregates.json`: `{ control, section, expression, computedValue }`.
3. **Record the un-derivable totals honestly.** Footer controls whose
   value cannot be a pure function of the flat result set —
   `RunningSum` totals, page-level aggregates, anything referencing
   report-runtime-only context — are added to the existing `Skipped`
   list with a specific reason, never silently dropped. Update the
   `aggregates.json` `ContractNote` to state that supported footer
   display totals are now captured.
4. **Regenerate + regression.** Regenerate the committed
   `reports-baseline/` aggregates for the affected canonical reports
   (Windows + Access; COM). `aggregates.json` is already byte-compared by
   `CanonicalGoldenRegressionTests` (only `Provenance.HarvesterVersion`
   normalized), so the new `DisplayTotals` field participates in the gate
   automatically — confirm parity rather than adding new normalization.
5. **Optional non-blocking PDF spot-check (Session 2).** Wire the
   existing post-aggregate PDF render path so a rendered PDF can be
   captured opportunistically into a **non-gated** location (outside the
   byte-diffed corpus, or an explicitly-ignored `spot-check/` sibling),
   behind an opt-in flag, for human visual inspection only. It must never
   block capture and never enter the deterministic regression gate.
   Document it in both fixture `READ-ME-FIRST.md` files and the
   interpretation guide.

## Out of Scope

- A general Access/VBA expression evaluator. Support only the closed set
  of aggregate functions the canonical report footers actually use.
- PDF byte-diffing, or any PDF artifact inside the automated regression
  gate. PDFs are not byte-deterministic; they stay an optional human
  spot-check.
- `RunningSum`, page-level, or report-runtime-context totals — recorded
  as `Skipped`, not computed.
- Simulating `Forms!`, `Reports!`, `TempVars`, or `OpenArgs` runtime
  context (already a standing non-goal from
  `harvester-report-baseline-golden-corpus`).

---

## Sessions

### Session 1 of 2: Footer-Expression Display Totals (Core + capture proof)

**Steps:**
1. Survey the canonical report subset
   (`rptInvoice`, `rptSalesByEmployee`, `rptSalesByProduct`,
   `srptClientBudgets`, `srptClientCurrentNeeds`) and record, per report,
   each footer/group-footer aggregate control, its expression, its
   `RunningSum` state, and whether the value is derivable from the
   RecordSource result set. This survey fixes the exact closed set of
   aggregate functions to support.
2. Extend `AccessReportSpecReader.ReadSpec` to capture the derivable
   footer aggregate controls onto `ReportLiveSpec`.
3. Extend `ReportAggregateComputer` to evaluate the supported footer
   expressions over the result set and emit `DisplayTotals`; route
   un-derivable controls to `Skipped` with reasons.
4. Add focused hermetic tests (expression evaluation against a synthetic
   result set; `RunningSum` / unsupported-function routed to `Skipped`)
   and a narrow live capture slice proving `rptInvoice`'s displayed total
   is computed.

**Creates:** `docs/session-sets/harvester-report-displayed-total-parity/ai-assignment.md`

**Touches:**
`AccessHarvester.Core/Com/AccessReportSpecReader.cs`
`AccessHarvester.Core/ReportBaseline/ReportLiveSpec.cs`
`AccessHarvester.Core/ReportBaseline/ReportAggregateComputer.cs`
`AccessHarvester.Core/ReportBaseline/ReportAggregates.cs`
`tests/AccessHarvester.Tests/ReportBaseline/ReportAggregateComputerTests.cs`

**Ends with:** The shipped capture path computes a displayed-total value
for every canonical footer-expression report that is derivable from its
RecordSource, and every non-derivable footer control is named in
`Skipped` with a grounded reason.

**Progress keys:** `session-1/survey-footer-aggregates`,
`session-1/read-footer-controls`,
`session-1/evaluate-display-totals`

---

### Session 2 of 2: Corpus Regen + Regression + Optional PDF Spot-Check + Closeout

**Steps:**
1. Regenerate and commit the updated `reports-baseline/` aggregates (now
   carrying `DisplayTotals`) for the affected canonical reports; confirm
   `CanonicalGoldenRegressionTests` passes with the new field under the
   existing byte-diff (no new normalization expected).
2. Wire the optional, non-blocking PDF visual spot-check (opt-in flag;
   non-gated output location).
3. Update both fixture `READ-ME-FIRST.md` files and the interpretation
   guide to explain the new `DisplayTotals` parity artifact and the
   optional PDF spot-check (visual-only, never a regression signal).
4. Run focused validation, perform closeout, update ROADMAP/change-log,
   and run the Step 9 reorganization review.

**Creates:** (updated corpus artifacts under existing `reports-baseline/`)

**Touches:**
`tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs`
`tests/AccessHarvester.Integration/Pipeline/GoldenNormalizer.cs`
`AccessHarvester.Cli/Commands/ReportBaselineCommand.cs`
`samples/GoldenOutput/HomeHealthCare/READ-ME-FIRST.md`
`samples/GoldenOutput/NorthwindDevEdition/READ-ME-FIRST.md`
`docs/harvester/interpretation-guide.md`
`docs/session-sets/ROADMAP.md`

**Ends with:** The committed corpus carries deterministic displayed-total
parity for the supported footer-expression reports, the optional PDF
spot-check is available but never blocking, and the discoverability docs
describe both.

**Progress keys:** `session-2/regen-corpus-display-totals`,
`session-2/confirm-regression-parity`,
`session-2/optional-pdf-spot-check`,
`session-2/closeout`

---

## Deliverables

- `DisplayTotals` on `aggregates.json` for every canonical
  footer-expression report whose total is derivable from its RecordSource
  result set, with un-derivable controls recorded in `Skipped`.
- Updated committed `reports-baseline/` corpus under both fixtures,
  byte-diffed by the existing regression gate.
- An optional, opt-in, non-blocking PDF visual spot-check path.
- Updated `READ-ME-FIRST.md` guides + interpretation guide describing the
  new parity artifact and the spot-check's visual-only role.

## Provenance

Requested 2026-06-01 by the repo owner after a consumer repo flagged that
report parity should not depend on a PDF for the displayed total. Builds
directly on the aggregates-only corpus committed by
`harvester-report-baseline-golden-corpus`. Ships in a later release
(target v1.2.0); the existing aggregates-only report-baseline ships first
as v1.1.0.
