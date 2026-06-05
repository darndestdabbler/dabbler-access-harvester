# Harvester Report-Baseline Capture Spec

> **Purpose:** Add a harvester capability that captures **Access report
> baselines automatically** — the rendered PDF plus the key aggregate numbers —
> so downstream migration projects verify ported-report parity against Access
> ground truth without a human exporting PDFs by hand. Eliminates the one
> remaining manual step in the harvest → migrate pipeline.
> **Created:** 2026-05-29
> **Finalized:** 2026-05-29 — maintainer applied the four open decisions via
> cross-provider consensus (Gemini Pro + Opus, GPT verifier). See
> *Resolved decisions* below.
> **Session Set:** `docs/session-sets/harvester-report-baseline-capture/`
> **Prerequisite:** `harvester-manifest-index-and-batch-readiness` (reports are
> enumerated in the manifest with their RecordSource). Windows + Microsoft
> Access required (COM), like `extract`/`run`.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.
> **Status:** Finalized — authored as a draft by a downstream consumer
> (`Dabbler.NorthwindToBlazor`, Set 001 Session 3), finalized 2026-05-29.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites: [harvester-manifest-index-and-batch-readiness]
totalSessions: 3   # provisional — see Finalization checklist
```

---

## Why This Set, Why Now

`Dabbler.NorthwindToBlazor` Set 004 must verify each ported report against the
*original Access report* — both the rendered PDF and the aggregate numbers
(group subtotals, grand totals). Today that baseline can only be produced by a
human opening Access and exporting PDFs by hand (Set 001 left it as a PENDING
manual step). That does not scale to the stated target of **hundreds** of Access
databases, and it is the only non-automated step left in the pipeline.

Crucially, **none of it actually needs a human**:
- **PDF render:** `DoCmd.OutputTo acOutputReport, "<report>", acFormatPDF,
  <path>` over the COM session the harvester already drives — no GUI.
- **Aggregates:** run each report's `RecordSource` query through **ACE OLEDB**
  (which the harvester already uses) and compute the group/grand totals from the
  result set. That is *Access's own query engine* producing the numbers — it is
  legitimate ground truth, not a reimplementation of the report's math.

## Design sketch

A new standalone subcommand `report-baseline` (resolved — see *Resolved
decisions*):

```
AccessHarvester report-baseline --db <file.accdb> --out <output-root-dir>
    [--reports <name,name>] [--params <params.json>] [--include-pdf] [--json-status]
```

Per report, under `<out>/AccessMetaData/<dbName>/reports-baseline/<report>/`:
- `aggregates.json` — the report's RecordSource query result aggregates
  (row count, group subtotals, grand total), plus the parameters used and the
  harvester version (deterministic provenance). Aggregates reflect the
  RecordSource **result set**, not rendered footer-control expressions.
  **Always written** (the deterministic parity ground truth).
- `output.pdf` — `DoCmd.OutputTo` render. **Opt-in** via `--include-pdf`
  (Session-3 revision — see *Resolved decisions — Session 3 addendum*).
  Default capture is aggregates-only; the PDF is a non-deterministic binary
  for human visual review.

Plus a top-level index at
`<out>/AccessMetaData/<dbName>/reports-baseline/baseline-manifest.json`
listing every report processed — `{name, status, paths, skipped_reason?}` —
so downstream agents do not have to walk the directory tree.

**Scope:** `--reports` is optional; default is **all reports enumerated in
the manifest** (consistent with `extract`/`run` do-all defaults). Pass a
comma-separated allowlist to scope down.

**Parameters:** runtime default is **unfiltered (no parameters applied)**
when the report runs that way. The manifest (extended in the prerequisite
set) records each report's parameter dependencies and binding source —
form control, `TempVars`, query `PARAMETERS`, `OpenArgs`. Reports that
hard-fail without a parameter surface as a clarifying question through the
existing `identify-usage` mechanism rather than blocking. `--params
<params.json>` overrides per-report values with shape
`{"ReportName": {"ParamName": value}}`; values used are recorded verbatim
in `aggregates.json` provenance.

---

## Sessions

### Session 1 of 3: Aggregates from the report's query (no COM render yet)
- Resolve each report's `RecordSource` (saved query or inline SQL) from the IR;
  run it via ACE OLEDB; compute aggregates. Decide the aggregate contract:
  result-set group/grand totals vs. evaluating the report's footer
  `=Sum(...)` controls. **Tradeoff:** query-result aggregates are simpler and
  hermetically testable; footer-expression evaluation is truer to the *rendered*
  report but requires expression evaluation. Recommend query-result aggregates
  as the baseline, footer expressions as a documented future option.
- Emit `aggregates.json` with provenance (params, version). Hermetic tests on a
  fixture query result.

### Session 2 of 3: COM PDF render + CLI wiring + determinism
- Add `DoCmd.OutputTo` PDF export over the COM session; wire the subcommand,
  options (`--reports`, `--params`), exit codes (mirror `run`: 0/1/2/3), and
  `--json-status`. `[SkippableFact]` integration test against a committed
  fixture. Keep the determinism gate green (exclude the PDF binary / timestamps
  from byte-comparison the way `ExportedAt` is handled).

### Session 3 of 3: Docs, parameter elicitation, golden + closeout
- Document the subcommand (install.md / interpretation-guide), including how
  parameters are chosen and how unresolved parameter decisions route through the
  feedback-question mechanism.
- Capture goldens for the two canonical fixtures (`NorthwindDevEdition` —
  `rptInvoice`, `rptTotalsByOwner`, `rptTotalsByCategory`; `HomeHealthCare` —
  its in-scope reports). Closeout + ROADMAP/state + Step 9.

---

## Resolved decisions

Finalized 2026-05-29 via cross-provider consensus (route → Gemini Pro tier 2;
route → Opus tier 3; GPT-5-4 verifier on each). Both verifier passes returned
`ISSUES_FOUND`; the issues are reflected in the resolutions below, not
deferred. The original DRAFT phrasing is preserved here only as the open
question each resolution closes.

1. **Subcommand name** — was `report-baseline` vs `export-reports` vs folding
   into `run`. **Resolved:** standalone `report-baseline` subcommand. It is
   intent-named (the artifact's *purpose* — establishing a parity baseline —
   not its mechanism), which matters because hundreds of downstream AI agents
   will need to self-discover what the artifact is for. A `run`-integration
   flag is **deferred** (not blocking) and would land as a follow-up if
   demand emerges.
2. **Aggregate contract** — was query-result totals vs. report-footer
   expression evaluation. **Resolved:** query-result totals via ACE OLEDB.
   Driving tradeoff: hermetic testability with fixture data and zero
   expression-parsing code, vs. an unbounded expression interpreter
   (`=Sum`, `=IIf`, nested `DLookup`). Query-result totals *are* Access
   ground truth — same engine, same data — so the fidelity gap is negligible
   for parity checks; any real divergence shows up in the PDF visual diff.
   `aggregates.json` must state explicitly that values reflect the
   RecordSource result set, not the rendered footer controls.
3. **Parameter strategy** — was deterministic defaults + `--params`. **Resolved:**
   hybrid — the manifest (extended in the prerequisite set) analyzes and
   exposes each report's parameter dependencies *and binding source*
   (form control / `TempVars` / query `PARAMETERS` / `OpenArgs`). Runtime
   default is unfiltered when feasible; required-param reports surface as a
   clarifying question through the existing `identify-usage` mechanism
   rather than blocking. `--params` JSON schema is
   `{"ReportName": {"ParamName": value}}`; the values used (including
   defaults) are recorded verbatim in `aggregates.json` provenance so two
   runs reproduce exactly. (Resolves the verifier-flagged gap that Gemini's
   "front-load in manifest" answer omitted the runtime contract and Opus's
   "always unfiltered" answer broke reports with required parameters.)
4. **Scope** — was all reports vs. caller-supplied list. **Resolved:** default
   to all reports enumerated in the manifest; `--reports name,name` scopes
   down. Driving tradeoff: consistency with `extract`/`run` (every existing
   subcommand does all objects by default) wins over saving COM-render time
   on the first harvest, since the COM cost is paid once per harvest and
   amortizes across downstream consumers. The Opus-rationale that
   "discoverability requires running everything" was verifier-flagged as
   shaky (the prerequisite manifest already enumerates reports); the
   resolution stands on the consistency argument, not on that one. A
   top-level `baseline-manifest.json` index lists every report processed —
   `{name, status, paths, skipped_reason?}` — so failed/skipped reports
   are explicit, not silently omitted.

## Resolved decisions — Session 3 addendum (2026-05-30)

Three open questions that Sessions 1–2 explicitly escalated "for S3
maintainer consideration" were resolved at the start of Session 3 (the
maintainer chose #1 directly via `AskUserQuestion`; #2 and #3 by
accepting the orchestrator's recommendation). They refine — not
overturn — the four original Resolved decisions above.

5. **PDF capture is opt-in (`--include-pdf`), default aggregates-only.**
   Was: PDF rendered for every report (S2 shipped it always-on). The
   aggregate contract is `RecordSourceResultSet` (Resolved decision #2),
   so `aggregates.json` — not the PDF — is the parity ground truth. The
   PDF adds modal-prompt exposure for required-parameter reports (it is
   `DoCmd.OutputTo` that pops the modal) and is a non-deterministic
   binary that cannot participate in a byte-comparison golden gate.
   **Resolved:** `aggregates.json` is always written; `output.pdf` is
   rendered only when `--include-pdf` is passed. This makes the default
   path deterministic and modal-free (an unsupplied parameter now
   surfaces as a `Failed`/`AggregateFailed` manifest entry from the OLE
   DB layer — no UI — rather than a blocking modal), which is exactly
   what lets the golden corpus + regression gate stay clean. The PDF
   remains available on demand for visual-fidelity / footer-expression
   review.

6. **Parameter elicitation is surface-only this session.** Resolved
   decision #3 routes required-parameter reports through the
   `identify-usage` clarifying-question mechanism "rather than blocking"
   — which is itself a surfacing, not an auto-resolution, contract.
   **Resolved:** the `baseline-manifest.json` records required-parameter
   reports as explicit `Failed` (`AggregateFailed`) entries (non-blocking;
   the batch continues), and the docs spell out the resolution path
   (supply values via `--params`, whose JSON shape and positional-binding
   order are documented). The in-Access `acSaveNo` RecordSource-
   substitution auto-render (validated in S2) and a dedicated
   report-parameter question type in `QuestionGenerator` are a **named
   follow-up set**, not S3 scope — `QuestionGenerator` today emits no
   report-parameter question, and building the COM auto-resolver in the
   closeout session would violate lean-by-default and risk the final
   session.

7. **Goldens are wired into the regression gate.** Because the default
   capture is deterministic (aggregates-only), the committed
   `reports-baseline/` corpus participates in the same byte-comparison
   regression pattern as `kb/ir/cleanup/usage`. **Resolved:** golden
   `reports-baseline/` artifacts are committed under
   `samples/GoldenOutput/<fixture>/` and a regression test reproduces +
   diffs them, with the volatile `HarvesterVersion` / database-path
   provenance normalized (mirroring `GoldenNormalizer`'s existing
   manifest handling). The golden is scoped to reports that capture
   cleanly aggregates-only so the corpus carries deterministic aggregate
   numbers (from the fixed committed fixture data) and no
   locale/version-volatile OLE DB error text.

### Deferred to a follow-up set (out of S3 scope)

- **`acSaveNo` in-memory RecordSource substitution** so required-
  parameter reports render/aggregate unattended (the universal-fallback
  COM technique S2 validated).
- **A report-parameter clarifying-question type in `QuestionGenerator`**
  so `identify-usage` actively elicits the parameter values, closing the
  loop with `--params`.

## Finalization checklist (maintainer)

- [x] Confirm slug, `totalSessions`, Session Set Configuration block — slug
      and `totalSessions: 3` confirmed; configuration block matches `full`
      tier, no UAT/E2E.
- [x] Add a ROADMAP row (maintenance / consumption-triggered) — added under
      *Currently in-progress* in `docs/session-sets/ROADMAP.md`.
- [ ] `ai-assignment.md` — authored at Step 3.5 of Session 1 by the
      orchestrator (per workflow), not before session start.
- [x] `session-state.json` stub (v4, omit-null) — present.

---

> **Provenance:** `Dabbler.NorthwindToBlazor` Set 001 Session 3 (2026-05-29),
> where report baselines (`harvest/reports-baseline/<report>/{output.pdf,
> aggregates.json}`) were left as a manual Access step pending a tool to
> automate them.
