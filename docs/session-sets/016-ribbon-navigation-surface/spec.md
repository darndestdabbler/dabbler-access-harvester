# Ribbon Navigation Surface Spec

> **Purpose:** Close **DW-13** — `NavigationGraph.Ribbon` ships null on every
> corpus because no producer reads the `USysRibbons` custom-ribbon XML into the
> existing `RibbonStructure` model. Produce the ribbon navigation surface
> (tabs → groups → controls, with each control's `onAction` VBA callback) and its
> entry points, validated against **NorthwindDevEdition's real custom ribbon
> locally** and pinned with **synthetic committed fixtures**.
> **Created:** 2026-06-15 (authored after 015 closed DW-11; the human flagged that
> verification had run HomeHealthCare-only, and a local Northwind harvest proved
> the custom-ribbon corpus DW-13 was waiting for already exists — see
> `feedback_use_northwind_as_local_test_corpus` and the corrected DW-13 entry).
> **Session Set:** `docs/session-sets/016-ribbon-navigation-surface/`
> **Prerequisite:** **P0 only** (a committed green build/test baseline). Independent
> of any other open set; slots anywhere.
> **Source of truth:** `docs/planning/deferred-work.md` → DW-13 (corrected
> 2026-06-15), the existing `RibbonStructure` / `RibbonTab` / `RibbonGroup` /
> `RibbonControl` models + the `NavigationGraph.Ribbon` / `NavigationGraphInputs.Ribbon`
> wiring point (`AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs` ~93, ~396-452;
> `NavigationGraphBuilder.Build` already threads `Ribbon = inputs?.Ribbon` at ~94),
> the `SwitchboardExporter` DAO row-read pattern
> (`AccessHarvester.Core/Extraction/SwitchboardExporter.cs`, the DW-9/012 S1
> template), and the **local** Northwind harvest (`run` → `C:/temp`, never
> committed). Re-confirm every pointer before editing; 015/016 may have shifted lines.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification. Full tier;
> no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 2
prerequisites: []
verificationMode: out-of-band-or-none
```

> Rationale: no UI surface; the quality gate is build + tests + cross-provider
> verification. **Session 1 (RibbonStructure facet)** adds a new COM/DAO row-read
> of the `USysRibbons.RibbonXML` value plus a pure customUI-XML parser and the
> `NavigationGraphInputs.Ribbon` wiring — golden-neutral on HHC (no custom ribbon),
> validated against Northwind locally + synthetic pins. **Session 2 (ribbon entry
> points + closeout)** derives the `onAction` → callback navigation entry points and
> does the closeout — split out per the "split sessions whose risk/scope differ"
> lesson (S1 = new data surface + parser; S2 = navigation derivation + closeout).
> `verificationMode: out-of-band-or-none` matches the inline-verify Full-tier
> pattern (007-015); register with `--verification-mode out-of-band-or-none`.

---

## Project Overview

`NavigationGraph.Ribbon` (type `RibbonStructure`) and `NavigationGraphInputs.Ribbon`
already exist; `NavigationGraphBuilder.Build` already copies `inputs?.Ribbon` onto
the graph. **No producer populates the input**, so the facet ships null everywhere.
DW-13 is the missing producer.

Re-grounding (confirmed 2026-06-15 on the current tree + a local Northwind harvest):

1. **The `RibbonXML` value is NOT harvested yet.** `USysRibbons` is currently
   extracted as **table schema only** (DDL + `Fields`, `RecordCount: 1`, **no row
   data**). The custom-ribbon markup lives in the single row's `RibbonXML` LONGTEXT
   field, which nothing reads. So a producer needs a **row-data read** of
   `USysRibbons` (one or more `(RibbonName, RibbonXML)` rows) — new data surface,
   mirroring how `SwitchboardExporter` reads `tblSwitchboardItems` rows.
2. **The XML is Office Fluent customUI.** Shape:
   `<customUI xmlns="…/2009/07/customui"><ribbon><tabs><tab id label>`
   `<group id label><button id label onAction="cmdFoo_OnAction"/>…`. The
   `RibbonControl.OnAction` field is already modelled for the callback name.
3. **The callbacks are real VBA.** NorthwindDevEdition has `modRibbonCallback`
   (`cmdAbout_OnAction(control)`, `ActivateTab`, …) — the procedures `onAction`
   names point at. These are navigation **entry points** (user clicks a ribbon
   button → its callback runs).
4. **HomeHealthCare has no custom ribbon** (`USysRibbons` absent), so the committed
   HHC golden keeps `Ribbon` null/omitted — the producer is **golden-neutral on
   HHC**. The ground-truth corpus is **NorthwindDevEdition, local-only**.

### Design constraints carried from DW-13

- **Validate against Northwind locally; commit only synthetic fixtures.**
  NorthwindDevEdition is proprietary — its `.accdb` and every derived artifact are
  gitignored and must **never** be committed. The producer is validated against
  Northwind's real `USysRibbons` by a **local** run; the committed regression is a
  **synthetic** hand-authored customUI fixture + hermetic parser/producer pins.
  `NorthwindLocalInvariantTests` (added 2026-06-15) is the local gate — extend it,
  do not commit a Northwind golden.
- **Mirror the switchboard precedent, do not re-invent.** The
  `SwitchboardExporter` (shape-gated DAO row-read, `Category Navigation`,
  `EscapeIdentifier` discipline) + `NavigationGraphInputs` threading via
  `Pipeline.BuildNavigationInputs` is the established pattern for a navigation
  data surface read from a system/shape-gated table. Reuse its structure.
- **Parser is pure + faithful-mechanical.** Parse the customUI structure
  (tabs/groups/controls + `onAction`) into `RibbonStructure`; **preserve raw**
  where the schema has no slot (do not interpret `imageMso`, sizes, dynamic
  `getVisible`/`getEnabled` callbacks beyond recording the static `onAction`).
  Unknown/unsupported control elements are surfaced, not silently dropped.

---

## Hard Constraints (carry verbatim into every routed prompt and the next-set recommendation)

- **NEVER regenerate, recreate, or commit any NorthwindDevEdition-derived
  artifact** (`samples/GoldenOutput/NorthwindDevEdition/`, `DatabaseYaml/`,
  `FormsAndReportsYaml/`, parse-success-reports, `examples/northwind-output.json`,
  or a ribbon dump). NorthwindDevEdition is proprietary and local-only; regeneration
  reintroduces the residue sets 003-005 removed. Northwind is for **local**
  validation only.
- **Use NorthwindDevEdition locally to validate the ribbon producer.** HHC has no
  custom ribbon, so HHC alone cannot exercise this work — both sessions must run
  Northwind locally (`run --db samples/NorthwindDevEdition.accdb --out C:/temp/…`,
  foreground COM) and confirm the parsed `NavigationGraph.Ribbon` matches the real
  `USysRibbons`. Commit only synthetic fixtures + hermetic pins.
- **Golden regeneration is HomeHealthCare-only**, via `GoldenCorpusRegenerator`
  (`AH_REGEN_GOLDEN=1`) + re-run `CanonicalGoldenRegressionTests`. **Both sessions
  are expected golden-neutral on HHC** (no `USysRibbons` → `Ribbon` stays null) —
  prove it **byte-level** (a fresh run reproduces the committed golden), never by
  assertion.
- **COM-tier runs are deliberate, sequential, foreground**, TRX logger on, TRX
  `<Counters>` as the completion signal; never background a `dotnet` invocation;
  sweep spurious `samples/*.accdb` diffs (`git checkout -- samples/HomeHealthCare.accdb`)
  before staging. (The new `USysRibbons` row-read is a COM/DAO data surface; the
  switchboard/golden/integration suites touch the COM tier. NorthwindDevEdition.accdb
  is gitignored, so Access mutating its timestamps needs no sweep.)
- **The quarantined sanitize test stays quarantined**
  (`SanitizeCommandTests.HomeHealthCare_sanitize_…`, per
  `docs/session-sets/007-integration-test-rebase/s2-flake-disposition.md`).
- **Do not weaken assertions or tests to force green.** New behaviour (a populated
  `NavigationGraph.Ribbon`, ribbon entry points) must be pinned by synthetic
  hermetic tests asserting the new behaviour, not silently accepted.

### Set-specific constraint — faithful-mechanical, no interpretation

- **Surface the ribbon structure, do not interpret it.** Capture
  tabs/groups/controls + `onAction` (and ids/labels). Do not decode `imageMso`,
  evaluate dynamic `getVisible`/`getEnabled` callbacks, or infer semantics. If a
  consumer needs dynamic callback evaluation, that is a `DW-<n>`, not scope creep.
- **An `onAction` that names a procedure not found in the harvested modules is an
  explicit unresolved entry point**, never a silent drop (the project's
  unresolvable-constructs-are-explicit principle; mirror the navigation
  unresolved-target precedent).

**Non-goals:** DW-18 (resolver intrinsic-constant registry), the other open DW
items, dynamic ribbon callbacks beyond static `onAction`, and any non-ribbon
navigation work. Anything discovered beyond the ribbon surface that no set owns →
a `DW-<n>` entry.

---

## Sessions

### Session 1 of 2: RibbonStructure facet — parse `USysRibbons` into `NavigationGraph.Ribbon`

**Steps:**

1. Pre-session reading (`project-guidance.md`, `lessons-learned.md`,
   `deferred-work.md` → DW-13) and **re-ground**: confirm the `RibbonStructure`
   models + `NavigationGraphInputs.Ribbon` wiring point are unchanged; confirm
   `USysRibbons` is still extracted as schema-only (no row data); read
   `SwitchboardExporter` as the row-read template. Do a **local** Northwind harvest
   and dump the real `RibbonXML` to scope the customUI shape (never commit it).
2. **Extract the `RibbonXML` value:** a shape-gated exporter (mirror
   `SwitchboardExporter`: detect a table with `RibbonName` + `RibbonXML` columns;
   DAO/OleDb row-read; `Category Navigation`) emits the `(RibbonName, RibbonXML)`
   row(s) to the raw/kb Navigation tier. Shape-gated so a database without
   `USysRibbons` produces nothing (HHC → null).
3. **Parse the customUI XML:** a pure `RibbonXmlParser` (namespace-tolerant) →
   `RibbonStructure` (Tabs → Groups → Controls, each with `Id`/`Label`, control
   `OnAction`). Cover the common control elements (`button`, `toggleButton`,
   `splitButton`, `menu`, `dropDown`, `gallery`, separators/labels with null
   `OnAction`). Deterministic, source-order.
4. **Wire it:** populate `NavigationGraphInputs.Ribbon` (thread via
   `Pipeline.BuildNavigationInputs`, exactly as the switchboard input is threaded)
   so `NavigationGraphBuilder.Build` emits `NavigationGraph.Ribbon`.
5. **Pin (synthetic + local):** hermetic `RibbonXmlParser` tests over a
   **hand-authored** customUI fixture (committed) covering nested tabs/groups,
   `onAction` and declarative controls, namespace prefix variants, and a
   malformed/empty XML degradation case; a builder test that a shape-gated table
   produces a populated `Ribbon` and a non-ribbon table produces null. Extend
   `NorthwindLocalInvariantTests` to assert `NavigationGraph.Ribbon` is populated
   and its tab/control ids match the **local** `USysRibbons` (skip-gated; never
   committed as a golden).
6. **Golden:** prove HHC byte-level neutral (no `USysRibbons` → `Ribbon` null);
   run the **local** Northwind harvest and confirm the real ribbon parses.
7. Cross-provider verification (prose-first + trailing JSON; in-IDE
   devil's-advocate pair as primary on the parser + the shape-gate seam).
8. Log every step; commit + push.

**Progress keys:** `s1-reground-and-local-northwind`, `s1-usysribbons-exporter`,
`s1-ribbon-xml-parser`, `s1-navgraph-wiring`, `s1-synthetic-and-local-pins`,
`s1-golden-neutral`, `s1-verified`

---

### Session 2 of 2: Ribbon entry points (`onAction` → callback) + closeout

**Steps:**

1. Pre-session reading + **re-ground** Session 1's output (local Northwind run);
   confirm `RibbonControl.OnAction` carries the callback names.
2. **Derive ribbon entry points:** for each `RibbonControl.OnAction`, resolve the
   named VBA procedure in the harvested modules and emit a navigation **entry
   point** (kind: ribbon) — the canonical "user clicks a ribbon button → runs this
   callback" surface, mirroring the switchboard `EntryPoints` precedent. An
   `onAction` whose procedure is not found → an **explicit unresolved** entry point
   (never a silent drop).
3. **Pin (synthetic + local):** hermetic tests that a control's `onAction` resolves
   to the matching procedure entry point and that an unknown callback surfaces as
   unresolved; extend `NorthwindLocalInvariantTests` to assert the real Northwind
   `onAction` callbacks (e.g. `cmdAbout_OnAction` in `modRibbonCallback`) resolve
   to entry points locally.
4. **Golden:** HHC byte-level neutral (no ribbon); local Northwind confirmation.
5. Cross-provider verification (closeout-grade: prose-first + trailing JSON;
   **embed the Session-1 parser + producer code/test snippet** per the
   cross-session-evidence convention since this is the final session; in-IDE
   devil's-advocate pair as primary).
6. **Author the next session set** (last-session-authors-next-set) via routed
   recommendation against actual post-016 state, carrying the Hard Constraints
   block verbatim — or a recorded consumer-driven-only handoff (the standing
   bundling candidates from 015's recommendation — DW-1..8, DW-14 — plus the new
   DW-18 remain; flag DW-18 as golden-affecting).
7. Closeout: `disposition.json` **before** the closeout commit, `change-log.md`,
   `next-session-set-recommendation.md` (routed), `close_session`, commit, push,
   notify; Step 9 reorganization review post-notify. **Discharge DW-13** in
   `deferred-work.md` (move to Closed with this set/commit, retain the entry).

**Progress keys:** `s2-reground`, `s2-ribbon-entry-points`,
`s2-synthetic-and-local-pins`, `s2-golden-neutral`, `s2-next-set-authored`,
`s2-verified`, `set-complete`

---

## Stop Rules

- The **Hard Constraints** block above applies in full to every session.
- **Scope is DW-13 only** (the ribbon structure surface + ribbon entry points). If
  the work appears to need dynamic-callback evaluation, a new COM surface beyond the
  `USysRibbons` row read, or a resolver change (DW-18), stop at the boundary, record
  a `DW-<n>` entry, and leave it to the owning set.
- **Never commit a NorthwindDevEdition artifact.** Validate the ribbon against
  Northwind **locally**; commit only synthetic fixtures + hermetic pins.
- **Every golden delta must be explained by the fix.** Both sessions are expected
  HHC golden-neutral; a "no regen" claim must be proven byte-level, and any
  unexpected HHC delta is a stop-and-investigate signal.
- **Do not weaken assertions or tests to force green.**
- **Unresolvable constructs are explicit** — an `onAction` with no matching
  procedure is an unresolved entry point, not a dropped one.

## End-Of-Set Deliverables

- A shape-gated `USysRibbons` row-read exporter + a pure customUI `RibbonXmlParser`
  + `NavigationGraphInputs.Ribbon` wiring, so `NavigationGraph.Ribbon` is populated
  on a custom-ribbon database; hermetically pinned with **synthetic** fixtures.
- Ribbon entry points (`onAction` → callback procedure, with explicit unresolved
  for misses); hermetically pinned.
- `NorthwindLocalInvariantTests` extended to assert the ribbon surface + entry
  points against the **local** Northwind `USysRibbons` (skip-gated; no committed
  golden).
- HHC golden proven byte-level neutral (no custom ribbon).
- DW-13 discharged in `deferred-work.md` (moved to Closed, entry retained, cites
  this set + commit).
- Next session set authored (spec + not-started v4 state stub) or a recorded
  consumer-driven-only handoff.
- Standard set artifacts per session: `session-state.json`, `activity-log.json`,
  `ai-assignment.md` (with actuals), `sN-verification.md`, `sN-issues.json` for
  findings-bearing rounds, `disposition.json` (final session), `change-log.md`
  (final session), `next-session-set-recommendation.md` (final session).
