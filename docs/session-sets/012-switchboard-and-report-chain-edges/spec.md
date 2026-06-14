# Switchboard And Report-Chain Edges Spec

> **Purpose:** Deliver the two Round-2 consumer requests the wiring arc
> deliberately deferred — **R2-1** (switchboard menu rows → structured
> navigation edges / entry points) and **R2-2** (dialog→query→report chain
> edges) — by materializing the switchboard rows the harvest does not yet read
> and by deriving the cross-object chain edges over the models the resolver /
> navigation arc already produces. This is **006's P4**, the natural successor
> to 011 (P3). It turns the consumer's navigation-shell lane and chain-shaped
> AI-handoff packs from AI-interpretation work into mechanical reads — for
> every Access database with a switchboard ("most of them," per the consumer).
> **Created:** 2026-06-14 (authored by 011 Session 3 per the
> last-session-authors-next-set pattern)
> **Session Set:** `docs/session-sets/012-switchboard-and-report-chain-edges/`
> **Prerequisite:** P0 (committed green build/test baseline). Sequence after
> **P3 (011, complete — `NavigationGraphBuilder` + `UnresolvedItemAggregator`
> are now wired into the manifest; `NavigationGraph.Edges`/`UnresolvedTargets`
> carry the VBA-side dispatches)** and **P2 (010, complete — query types are
> decoded correctly, hard for the chain edges' record-source typing)**.
> 011 being done is load-bearing: this set populates the
> `NavigationGraphInputs` facets 011 wired but always calls with `null`
> (DW-9), and joins the individual edges 011 surfaces into chains (DW-10).
> **This is 006's P4.** Source of truth: the 006 review's R2-1 / R2-2 design
> sketches (`006-solution-review-and-upstream-feedback-discovery/review-recommendations.md`
> §2) and the §3 P4 entry, plus deferred-work register entries **DW-9** and
> **DW-10**.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full
> tier; no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 3
prerequisites: []
verificationMode: out-of-band-or-none
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. This set **changes shipped output substantially** —
> `manifest.NavigationGraph` gains a populated `Switchboard` tree, ordered
> `EntryPoints`, and `Macros`; new chain-edge records join launchers,
> dialogs, queries, and reports; and a new `raw/Navigation/switchboard-items.json`
> Access-native row dump appears. It therefore **regenerates the
> HomeHealthCare golden**, once per session after that session's behavioral
> changes are complete, per the established regeneration discipline. This set
> **adds a new COM/data surface** (a narrow, shape-gated switchboard row-read)
> — the one deliberate exception 011's Hard Constraints forbade and pushed
> here. `verificationMode: out-of-band-or-none` matches the inline-verify
> Full-tier pattern (007–011): the cross-provider `session-verification` runs
> inside each work session; register with
> `--verification-mode out-of-band-or-none` per the verification-mode lesson.

---

## Project Overview

What this set delivers, in consumer terms. Both requests are **proven** in the
006 record (fresh-output probes: switchboard rows absent from all 266 files;
every chain hop individually observable but unconnected). This set implements
the recorded design sketches; it does not re-litigate the diagnoses. Read each
sketch's full Evidence/Design block in
`006-solution-review-and-upstream-feedback-discovery/review-recommendations.md`
§2 (R2-1, R2-2) before touching it, and **re-ground every claim against the
current tree** (the 006 probes are dated 2026-06-12; Sets 008–011 have since
landed the wiring the sketches sit on).

### R2-1 — Switchboard rows → structured navigation (DW-9)

`NavigationGraphBuilder` is wired in production (011 S2) but always called with
`inputs: null`, so `NavigationGraph.Switchboard` / `.Ribbon` are omitted,
`.Macros` and `.EntryPoints` are empty. The 52 `tblSwitchboardItems` rows that
hold the actual menu tree (captions, target objects, command codes) ship as
`(no data file)` — they are in **no** harvested artifact. Materializing them
needs a narrow, shape-gated row-read (a new COM/data surface), then population
of the **already-modeled** `NavigationGraphInputs.Switchboard`
(`SwitchboardStructure` / `SwitchboardPage` / `SwitchboardItem` exist today,
command codes documented in `NavigationGraph.cs`). Source: review §2 R2-1
design sketch; DW-9.

### R2-2 — Dialog→query→report chain edges (DW-10)

The navigation graph surfaces individual `DoCmd` dispatches and preserves
`NavigationEdge.WhereCondition` / `OpenArgs`; `IrQuery.Parameters` carries
declared / form-reference / report-reference provenance — but nothing **joins**
them into the consumer-requested chain (launcher → report → parameter dialog →
query → parameters). The report-menu embedded macros (`OnClickEmMacro`
`OpenReport` targets), the report Open-event `acDialog` dialog opens, the
`RecordSource` → named-query edges, and the `TempVars!` flows are each
observable individually but not linked. Deriving the chain is substantial new
logic over models that already exist. Source: review §2 R2-2 design sketch;
DW-10.

**Non-goals:** P5 CLI input robustness, P6 zero-date guard, the DW-6
encoding-architecture transcode option, the DW-1/DW-2/DW-4 grammar
completeness items, DW-8 dynamic-SQL / SQL-form-reference wiring (a
distinct Phase 5.4 capability — only pull it in if a chain edge genuinely
requires a dynamic-SQL target, and even then record the boundary), and
**DW-11** (`unresolved-items.json` procedure-body-scoping undercount) /
**DW-12** (resolve-failure degradation-contract asymmetry) — both off-theme
for navigation and owned by their own candidate sets. Anything discovered
beyond R2-1 / R2-2 that no set owns → a `DW-<n>` entry, not scope creep.

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

Per the 2026-06-12 lesson *constraints travel in routed prompts*, these are
restated here in full, not by reference (carried forward verbatim from 011;
the per-delta finding mapping is updated to this set's R2-1 / R2-2):

- **NEVER regenerate, recreate, or commit `examples/northwind-output.json`
  or any NorthwindDevEdition-derived artifact** — regeneration reintroduces
  the residue sets `003`–`005` removed. All fixtures and goldens derive from
  committed HomeHealthCare artifacts only. NorthwindDevEdition is
  proprietary and local-only.
- **Golden regeneration is HomeHealthCare-only** and follows the established
  regeneration path (`samples/GoldenOutput/HomeHealthCare/`); regenerate
  once per session, after that session's behavioral changes are complete,
  and re-run `CanonicalGoldenRegressionTests` to confirm. *(Each golden delta
  must be explained by a specific finding's wiring — an unexplained delta is a
  stop-and-investigate signal. This set adds a `NavigationGraph.Switchboard`
  tree + `EntryPoints` + `Macros` (R2-1), the new chain-edge records (R2-2),
  and a new `raw/Navigation/switchboard-items.json` row dump; every such delta
  must map to R2-1 or R2-2.)*
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on,
  TRX `<Counters>` as the completion signal; never background a `dotnet`
  invocation; sweep spurious `samples/*.accdb` diffs
  (`git checkout -- samples/HomeHealthCare.accdb`) before staging any commit.
- **The quarantined sanitize test stays quarantined.**
  `SanitizeCommandTests.HomeHealthCare_sanitize_…` is skipped per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`;
  its re-enable belongs to the proposed sanitize-testhost-wedge
  investigation, not to this set.
- **Do not weaken assertions or tests to force green.** Intent changes
  require explicit disclosure in the verification prompt. Any hermetic test
  that exercised the unwired switchboard/chain models must be reconciled with
  the now-wired production path, not deleted.

### Set-specific constraint — the new row-read is narrow and shape-gated

This set is allowed the **one** new COM/data surface 011 forbade: a switchboard
row-read. It must stay narrow:

- **Detection is by column-signature, not by table name.** Flag a table as a
  switchboard only when its fields contain the Switchboard Manager canonical
  set (`SwitchboardID, ItemNumber, ItemText, Command, Argument`; tolerate
  extras like `ParentID`). Locale/name independent; zero false positives on
  shape-mismatched tables. Do **not** key off `tblSwitchboardItems` by name.
- **Acquisition is a bounded, ordered SELECT** of only the canonical columns,
  through the existing `OleDbValueSampler` escaping/degradation discipline,
  gated to shape-matched tables only. This is **not** a general row-data
  export surface — switchboard rows are navigation chrome, not business data.
  The ordered SELECT keeps output deterministic.
- **Unknown command codes or missing targets emit explicit unresolved
  entries** (`UnresolvedNavigationTarget` already models this) — never
  silently dropped, never defaulted.

---

## Sessions

### Session 1 of 3: Switchboard Row Materialization + Entry Points (R2-1, DW-9)

Materialize the switchboard rows and populate the non-VBA navigation facets the
builder already models.

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md`, the 006 R2-1 sketch, DW-9) and **re-ground**: confirm
   `tblSwitchboardItems` still ships `(no data file)`; confirm
   `NavigationGraphInputs.Switchboard` / `SwitchboardStructure` / `…Page` /
   `…Item` shapes and command-code docs are unchanged; confirm the
   `NavigationGraphBuilder.Build(inputs: null)` call site in `Pipeline.cs`.
2. **Detection** — during extract, after table schema export, flag tables
   whose field set contains the Switchboard Manager canonical signature
   (column-signature, not name). Hermetic pin on the detector.
3. **Acquisition** — a bounded, ordered `SELECT` of the canonical columns via
   the `OleDbValueSampler` seam, gated to shape-matched tables only; emit
   `raw/Navigation/switchboard-items.json` (Access-native row dump) → kb
   passthrough.
4. **Population** — build `NavigationGraphInputs.Switchboard` from the rows
   (decode the closed Switchboard Manager command set; carry per-row
   provenance `{ source: "switchboard-row", table, switchboardId, itemNumber }`);
   build `.Macros` (AutoExec + named macros from the macro IR) and pass the
   startup `DefaultFormName`; thread the populated `NavigationGraphInputs` into
   the production `NavigationGraphBuilder.Build` call (retire the `inputs:
   null`). `NavigationGraph` now emits the `Switchboard` tree, ordered
   `EntryPoints` (AutoExec → default form → switchboard roots), and `Macros`.
5. **Ribbon (corpus-dependent).** If `USysRibbons` is present in the fixture,
   populate `NavigationGraphInputs.Ribbon`; if HomeHealthCare has no custom
   ribbon (likely — it is switchboard-driven), record that the `Ribbon` facet
   stays null on this corpus and leave its population to a real-ribbon corpus
   case (a `DW-<n>` entry, not a synthesized fixture). Decide explicitly and
   record it.
6. **Explicit unresolved** for unknown command codes / missing targets via
   `UnresolvedNavigationTarget`. Hermetic pins for the switchboard tree, the
   entry-point ordering, and the unresolved path.
7. **Single coordinated HomeHealthCare golden regen** for this session's
   deltas + the COM `CanonicalGoldenRegressionTests` + `ManifestSchemaConformanceTests`
   confirmation; every delta maps to R2-1. Interpretation-guide §5.7 update
   (the `Switchboard` / `EntryPoints` / `Macros` facets are now populated).
8. Cross-provider verification; closeout.

**Progress keys:** `s1-switchboard-detect`, `s1-row-read`,
`s1-navinputs-populated`, `s1-entrypoints`, `s1-ribbon-decided`,
`s1-golden-regen`, `s1-verified`

### Session 2 of 3: Chain-Edge Model + Mechanical Producers (R2-2 part 1, DW-10)

Introduce the chain-edge model and the producers that derive edges from the
structured surfaces (record sources, embedded macros, declared parameters)
without needing the resolver's code-behind layer.

**Steps:**

1. Pre-session reading; re-ground the R2-2 mechanical-surface claims against
   the tree (embedded-macro block text on `frmReportMenu` controls; named-query
   `RecordSource` on reports; `IrQuery.Parameters` provenance).
2. **Edge model** — extend the existing `NavigationEdge` family with a closed,
   documented chain-edge kind set (`OpensDialog`, `OpensReport`, `OpensForm`,
   `FeedsRecordSource`, `SuppliesParameter`) carrying
   `{ From, To, Kind, Provenance }` with the closed provenance source set
   (`vba-call | embedded-macro | macro-action | record-source |
   query-form-reference | query-declared-parameter | tempvar-flow`). Append to
   the closed set; do not rename existing values.
3. **Record-source producer** — named-query `RecordSource` →
   `FeedsRecordSource(query → report/form)`; inline-SQL record sources go
   through the jet-sql parser (it already parses these) and emit edges for
   FROM/JOIN targets plus `SuppliesParameter` provenance for `TempVars!x` /
   `[Forms]!…` tokens in the parsed tree.
4. **Embedded-macro producer** — parse the `OnClickEmMacro` / `OnOpenEmMacro`
   block text (the block grammar the SaveAsText translator already handles for
   named macros) → `OpensReport` / `OpensForm` edges from the owning
   form/control. Closes the launcher→report hop.
5. **Declared-parameter producer** — project `IrQuery.Parameters`
   (declared / form-reference / report-reference provenance) as
   `SuppliesParameter` edges, or explicit unresolved entries when the supplier
   is unknowable (prompt-style runtime parameters).
6. Hermetic pins per producer; **single coordinated golden regen** + COM
   confirmation; every delta maps to R2-2.
7. Cross-provider verification; closeout.

**Progress keys:** `s2-edge-model`, `s2-record-source-edges`,
`s2-embedded-macro-edges`, `s2-declared-param-edges`, `s2-golden-regen`,
`s2-verified`

### Session 3 of 3: Code-Behind Chain Edges + Coordinated Closeout (R2-2 part 2, DW-10)

Derive the chain edges that need the resolver's Access-integration layer, then
close the set.

**Steps:**

1. Pre-session reading; ground the code-behind hops against a fresh tool run
   (per the verify-consumer-reports-against-fresh-output lesson) — the report
   Open-event `acDialog` opens, the `Forms!dlg!control` reads, the `TempVars`
   flows.
2. **Code-behind VBA producer** — `NavigationGraphBuilder` already extracts
   `DoCmd.OpenForm` / `OpenReport` literal-target calls; use the `acDialog`
   positional arg to distinguish `OpensDialog` from `OpensForm`.
   `Forms!dlg!control` member reads inside a report's module become
   `SuppliesParameter(dialog → report)` via the resolver's now-wired
   Access-integration layer. `TempVars` assignments in report code paired with
   `TempVars!` references in record sources become `tempvar-flow` edges.
3. **Explicit unresolved** for variable targets (`DoCmd.OpenReport strReport`),
   unmatched `TempVars`, and prompt-only parameters — the closed reason set;
   the consumer sees "a chain exists here but its target is dynamic" rather
   than silence.
4. Any residual chain gap → fix or defer with a `DW-<n>` entry.
5. **Interpretation-guide** updates for the full chain surface (the chain-edge
   kinds, the provenance set, how a consumer walks a launcher→…→query chain);
   extend §5.7.
6. Final hermetic validation; **single coordinated golden regen** (if step
   2/3/5 changed output) + COM confirmation; every delta explained.
7. Cross-provider verification (closeout-grade: prose-first lead + trailing
   JSON block; embed earlier-session evidence per the cross-session-evidence
   lesson).
8. **Author the next session set** (last-session-authors-next-set): per the 006
   ranking, the remaining unowned sets are P5 `cli-input-robustness` and P6
   `cleanup-zero-date-guard` (both independent, P0-only prereqs) — author
   against actual post-012 completion state, carrying the Hard Constraints
   block verbatim.
9. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit,
   push, notify; Step 9 reorganization review post-notify.

**Progress keys:** `s3-codebehind-edges`, `s3-tempvar-flows`,
`s3-unresolved-chains`, `s3-navgraph-documented`, `s3-next-set-authored`,
`s3-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is R2-1 / R2-2 only.** If a fix appears to require P5/P6 CLI / cleanup
  work, the DW-6 encoding-architecture transcode, DW-8 dynamic-SQL wiring, or a
  grammar-completeness item (DW-1/2/4), stop at the boundary, record a
  `DW-<n>` entry, and leave it to the owning set.
- **The new row-read stays narrow and shape-gated** (see the set-specific
  constraint). If the work tempts a general row-data export surface, stop —
  that is a separate, explicitly-scoped decision, not this set's scope.
- **Every golden delta must be explained by R2-1 or R2-2.** An unexplained
  delta in the regenerated HomeHealthCare golden is a stop-and-investigate
  signal, not a silent regen.
- **Do not weaken assertions or tests to force green.** Reconcile any hermetic
  test that exercised the unwired switchboard/chain models with the wired
  path; do not delete coverage.

## End-Of-Set Deliverables

- R2-1 (DW-9): shape-gated switchboard detection + narrow OleDb row-read;
  `raw/Navigation/switchboard-items.json`; populated
  `NavigationGraph.Switchboard` tree, ordered `EntryPoints`, and `Macros`;
  ribbon decision recorded; explicit unresolved for unknown commands.
  DW-9 moved to **Closed** with the landing commit.
- R2-2 (DW-10): closed chain-edge kind set on the `NavigationEdge` family;
  record-source / embedded-macro / declared-parameter / code-behind /
  tempvar-flow producers; explicit unresolved for dynamic targets. DW-10
  moved to **Closed** with the landing commit.
- Single coordinated HomeHealthCare golden regeneration per session; every
  delta explained; `CanonicalGoldenRegressionTests` +
  `ManifestSchemaConformanceTests` green.
- Interpretation-guide §5.7 updated for the populated facets and the chain
  surface.
- Next session set authored (spec + not-started v4 state stub).
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json`, `change-log.md`,
  `next-session-set-recommendation.md`.
