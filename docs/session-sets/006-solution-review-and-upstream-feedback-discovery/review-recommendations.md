# Review Recommendations — Solution Review And Upstream Feedback Discovery

> **Set:** `006-solution-review-and-upstream-feedback-discovery`
> **Session:** 4 of 4 (2026-06-12)
> **Orchestrator:** Claude Code / Claude Fable 5 (synthesis authored in-conversation
> per the spec's Orchestrator Assignment And Delegation Deviation section)
> **Inputs:** `s1-review-findings.md`, `s2-review-findings.md`,
> `s3-review-findings.md` (all cross-provider VERIFIED), the Round 2
> consumer feedback in `docs/planning/harvester-upstream-feedback.md`, and a
> fresh COM-grounded HomeHealthCare run executed this session (details below).
> **Read-only attestation:** no shipping `.cs`, `.csproj`, schema, golden,
> sample, or test file was changed by this set. The fresh run wrote only under
> gitignored `tmp/`; the spurious `samples/HomeHealthCare.accdb` COM-touch
> diff was swept (`git checkout -- samples/*.accdb`) before staging.

This is the set's primary deliverable: one document the maintainer can read
to see what the review found, what the Round 2 consumer feedback is worth,
and which follow-up sets to run in what order.

## Status-label legend

Claims carry the set's three canonical labels: `proven` (a named
probe/command/source check establishes the claim), `partially proven`
(mechanism proven; a named impact premise remains inferred), or
`inferred — not verified`.

---

## 1. Fresh-run grounding (Session 4, 2026-06-12)

Per the *ground consumer reports against fresh output* lesson, all Round 2
assessments below were grounded against a fresh harvester run on this
Access-equipped host — deliberate, sequential, COM-isolated:

| Command | Result |
|---|---|
| `run --db samples/HomeHealthCare.accdb --out tmp/s4-fresh` | exit 0; 86/86 objects ok (extract, translate, enrich); harvester 1.2.0; tree at `tmp/s4-fresh/AccessMetaData/HomeHealthCare/` |
| `identify-usage --in tmp/s4-fresh/AccessMetaData/HomeHealthCare` | exit 0; 74 objects classified; `object-use.json` + checklist written into the harvest tree |
| `classify --in samples/HomeHealthCare.accdb --out tmp/s4-fresh` | exit 0; 13 tables; wrote `tmp/s4-fresh/homehealthcare/classification.json` |

Fresh output also re-confirmed, value-for-value on the probed surfaces the
S1–S3 evidence cites, the findings that the assessments below depend on: the
crosstab `QueryTypeName: "MakeTable"` mislabel and `PossiblyUnused 0.65`
classification (F-2-1), the UTF-8 BOM on the four usage/cleanup files
(F-2-3), zero `ParserOutput` items for all 19 forms and 12 reports with
exactly one manifest diagnostic (F-3-1/F-3-2), and the always-empty
substrate surfaces — no `NavigationGraph` key, `unresolved-items.json` = `[]`,
`EventHandlerClassifications` / `FunctionClassifications` / `RowSourceSpecs`
/ `ValidationRules` / `UnresolvedItems` all present-but-empty (F-2-2/F-3-4).

### Session 1 premise probes (resolved this session)

Two S1 findings carried named escalation conditions pending a Session 4 COM
probe. Both premises were probed via DAO (`DAO.DBEngine.120`) against a
throwaway copy of HomeHealthCare under `tmp/` (deleted after):

- **F-1-2 premise — proven, escalation condition met.** Access object names
  *do* accept path separators: `CreateQueryDef("probe/slash")` and
  `CreateQueryDef("probe\bs")` both succeeded and stored the names verbatim.
  Names containing `.` are rejected (`'..\probeup' is not a valid name`), so
  **traversal above the output root is impossible** — the original
  worst-case branch (planting files outside the root) is refuted. The
  remaining proven impact: a legal separator-bearing object name makes
  `SaveAsTextExporter`'s unsanitized `Path.Combine` target a non-existent
  subdirectory, failing that object's export (recorded failure, exit 3) on a
  perfectly valid database. Per the condition stated in F-1-2, severity
  escalates Minor → **Major** with the narrowed impact statement above; the
  recommended fix (apply the siblings' `SanitizeFileName`) is unchanged.
- **F-1-6 premise — proven.** `PARAMETERS [Enter start date, please]
  DateTime; …` and `PARAMETERS [Pick one; carefully] Text(50); …` are both
  accepted; DAO reports exactly one parameter each, name intact including
  the `,`/`;`. `ParseParametersClause`'s split-on-`,` / terminate-at-`;`
  therefore corrupts legal declared-parameter names. Severity stays
  **Minor** (the verbatim `Sql` is preserved and the jet-sql parser is the
  contract-grade re-extraction path); the finding is now fully proven.

---

## 2. Round 2 feedback assessment

### R2-1 — Switchboard navigation rows → structured edges. **Confirmed.**

Fresh-output evidence (all proven):

- The usage pass fires the signal the consumer described:
  `tblSwitchboardItems` → `LauncherSurface` with evidence
  `switchboard-navigation-data`, summary "Navigation/switchboard
  configuration table … backing the application's launcher menu"
  (classification `StandaloneUtility 0.65`).
- The signal is where it stops. `tblSwitchboardItems.ir.json` is
  schema-only — fields `SwitchboardID, ItemNumber, ItemText, Command,
  Argument, ParentID`, `RecordCount: 52` — and **no artifact in the fresh
  output carries row content**: a whole-tree search (266 files) finds
  `ItemText` only as a schema/column name (the raw/kb/ir/manifest
  table-schema artifacts plus classify's column metadata) and finds
  `NavigationGraph` in zero files. The manifest has no edge records of any
  kind; `unresolved-items.json` is `[]`.
- The 52 rows holding the actual menu tree (captions, target objects,
  command codes) are therefore invisible to the downstream consumer,
  exactly as reported.

One correction to the S2 seam note: S2 suggested the rows were "in the
harvested table data; no new COM surface" — the fresh probe disproves the
first half. Harvest output carries table *schemas* only; materializing
switchboard rows needs a narrow row-read (sketch below).

**Design sketch (input to the proposed `switchboard-and-report-chain-edges`
set; depends on the wiring arc):**

1. **Detection — column-signature, not name.** During extract, after table
   schema export, flag tables whose fields contain the Switchboard Manager
   canonical set (`SwitchboardID, ItemNumber, ItemText, Command, Argument`;
   tolerate extras such as `ParentID`). Deterministic, locale/name
   independent, zero false positives on shape-mismatched tables.
2. **Acquisition — narrow row-read through the existing OleDb seam.** A
   bounded `SELECT [SwitchboardID],[ItemNumber],[ItemText],[Command],[Argument]
   FROM [<table>] ORDER BY [SwitchboardID],[ItemNumber]` using the
   `OleDbValueSampler` escaping/degradation discipline (S1 confirmed it
   sound). Gated to shape-matched tables only — this does not open a general
   row-data export surface; switchboard rows are navigation chrome, not
   business data. Ordered SELECT keeps output deterministic.
3. **Output placement.** `raw/Navigation/switchboard-items.json` (Access-native
   row dump) → kb passthrough → enrichment populates the **already-modeled**
   `NavigationGraphInputs.Switchboard` (`SwitchboardStructure` /
   `SwitchboardPage` / `SwitchboardItem` exist today, with the command codes
   documented in-source) → `NavigationGraphBuilder` emits the menu tree into
   `manifest.NavigationGraph` once F-2-2's wiring lands.
4. **Contract shape.** Each materialized item carries provenance per the
   output conventions, e.g.
   `{ source: "switchboard-row", table: "tblSwitchboardItems", switchboardId: 1, itemNumber: 3 }`
   with decoded `command` (the closed Switchboard Manager code set) and
   `target`. Unknown command codes or missing targets emit explicit
   unresolved entries (`UnresolvedNavigationTarget` already models this) —
   never silently dropped, never defaulted.
5. **Consumer reading.** The downstream mechanical navigation lane reads
   `NavigationGraph.EntryPoints` (AutoExec → default form → switchboard
   roots, already ordered by the builder) and walks ordered items with
   caption/command/target — platform navigation config becomes emittable
   without AI interpretation, which is precisely the consumer's stated value.

### R2-2 — Report-chain linkage edges (dialog → query → report). **Confirmed.**

Fresh-output evidence (proven): every hop of the chain is individually
observable in ir/, and no artifact records any connection:

| Hop | Where it is observable today (fresh run) | Form |
|---|---|---|
| Launcher → report | `frmReportMenu` button controls carry embedded macros: `OnClickEmMacro` text with `Action ="OpenReport"`, `Argument ="rptClientProfile"` etc. | raw embedded-macro text on the control |
| Report → param dialog | report Open-event code-behind: `DoCmd.OpenForm "frmReportParam_FY", , , , , acDialog` then `m_FiscalYear = Forms!frmReportParam_FY!cboFiscalYear` — probed across all 12 ir/Reports `VbaCode`: 8 of 12 contain the pattern, each naming its dialog | raw `VbaCode` text in ir/Reports |
| Query → report | `rptProviderCosts` `RecordSource: qryProviderServiceCosts_ByFiscalYear` (named query), others inline SQL | structured `RecordSource` field |
| Dialog/params → query | `qryProviderPlansByClient_Param` fires `ParameterizedInput` with `declared-parameter:Enter Provider ID:` / `Enter Fiscal Year:`; `rptServicePlanAndShortfall` code-behind writes `TempVars!repCID` / `repFY`, which exactly two subreport record sources read (`srptServicePlanEntries`, `srptServiceShortfall` — probed) | usage signal + raw text |

The five `frmReportParam_*` forms classify with
`ParameterizedInput: param-dialog-name-pattern, dialog-chrome,
opened-as-dialog` and `frmReportMenu` classifies `NavigationShell 0.85` —
the 1.2.0 pieces the consumer credited. But: no `NavigationGraph` key, no
edge records, the string `Edge` absent from the 1MB manifest. The consumer's
framing is exactly right: 1.2.0 made the pieces observable; the connections
are not.

Two grounding caveats, labeled per the spec:

- The `[Forms]![frmX]![ctl]` *query-criteria* variant of the dialog→query
  hop does **not** occur in HomeHealthCare (zero hits across all 22 query
  SQLs — the fixture wires dialogs through code-behind reads and TempVars
  instead). That variant's prevalence in real databases is
  `inferred — not verified`; the design below handles it via the
  already-built `FormReferenceResolver` regardless.
- Today the code-behind hops are observable only as **raw text** at ir/
  tier: the manifest's parsed layer is empty for all forms/reports (F-3-1)
  and the resolver wiring is absent (F-3-4). Edge derivation is therefore
  sequenced **after** the wiring arc — building it first would re-derive
  ad hoc what the parser stack already knows how to produce.

**Design sketch:**

1. **Edge model.** Extend the existing `NavigationEdge` family (it already
   carries `WhereCondition`/`OpenArgs`) with a chain-edge kind set, closed
   and documented: `OpensDialog`, `OpensReport`, `OpensForm`,
   `FeedsRecordSource`, `SuppliesParameter`. Each edge:
   `{ From: {kind,name}, To: {kind,name}, Kind, Provenance: { source: "vba-call" | "embedded-macro" | "macro-action" | "record-source" | "query-form-reference" | "query-declared-parameter" | "tempvar-flow", ... } }`.
2. **Producers, one per observable surface:**
   - *Record sources* (mechanical now): named-query `RecordSource` →
     `FeedsRecordSource(query → report/form)`; inline-SQL record sources go
     through the jet-sql parser (it already parses these — S3) and emit
     edges for FROM/JOIN targets plus `SuppliesParameter` provenance for
     `TempVars!x` / `[Forms]!…` tokens found in the parsed tree.
   - *Embedded macros*: parse the `OnClickEmMacro`/`OnOpenEmMacro` block
     text (same block grammar the SaveAsText translator already handles for
     named macros) → `OpensReport`/`OpensForm` edges from the owning
     form/control. This closes the launcher→report hop that HomeHealthCare
     demonstrates.
   - *Code-behind VBA* (after F-3-1 + F-3-4): `NavigationGraphBuilder`
     already extracts `DoCmd.OpenForm`/`OpenReport` literal-target calls;
     the `acDialog` positional arg distinguishes `OpensDialog` from
     `OpensForm`. `Forms!dlg!control` member reads inside a report's module
     become `SuppliesParameter(dialog → report)` once the resolver's
     Access-integration layer (built, unwired) resolves them. `TempVars`
     assignments in report code paired with `TempVars!` references in record
     sources become `tempvar-flow` edges.
   - *Declared parameters*: `IrQuery.Parameters` already carries
     declared/form-reference/report-reference provenance — project it as
     `SuppliesParameter` edges (or explicit unresolved entries when the
     supplier is unknowable, e.g. prompt-style runtime parameters).
3. **Explicit unresolved.** Variable targets (`DoCmd.OpenReport strReport`),
   unmatched `TempVars`, and prompt-only parameters emit
   `UnresolvedNavigationTarget`-style records with the closed reason set —
   the consumer sees "a chain exists here but its target is dynamic" rather
   than silence.
4. **Sequencing.** F-2-1's decode fix first (report-feeding queries must be
   typed correctly), then F-3-1 → F-3-4 → F-2-2 wiring, then this
   derivation — which at that point is mostly joins over models that
   already exist. S3's cross-cutting thread 1 said the same: R2-1/R2-2 sit
   directly on top of the wiring arc.

### R2-3 — Single flat reviewed-input export. **Confirmed mechanically; recommend downstream ownership.**

The two-root split reproduces exactly as described — and is slightly worse
than reported: with the **same** `--out tmp/s4-fresh`, the harvest tree went
to `AccessMetaData/HomeHealthCare/` while classify wrote
`homehealthcare/classification.json` — disjoint roots **and** a casing drift
(classify lowercases its database slug; the harvest tree preserves
`HomeHealthCare`; `AccessComSession.DatabaseName` strips the extension —
S1's O-1-2). A consumer joining the two by path or name must special-case
both.

**Ownership recommendation: orchestrator-side intake (downstream F9), no
upstream `export-reviewed-input` command now.** Reasoning:

- The downstream consumer's own default recommendation is orchestrator-side
  intake, and `project-guidance.md`'s lean principle points the same way:
  one known consumer, one concrete workflow — an upstream command modeled on
  a hypothetical population of consumers is speculative surface area.
- The two authorities (consumer's recommendation, repo guidance) agree, so
  the spec's decision-time-consensus trigger ("if contested") does not fire;
  no consensus round was run, and nothing here needs `AskUserQuestion`.
- What upstream *should* own is the friction that makes the copy error-prone
  rather than the copy itself: normalize or document the classify slug
  casing and the database name forms (folded into the contract-fidelity set
  below as a ride-along). Revisit the export command only if a second
  consumer materializes — that event, not ergonomic preference, is the
  trigger.

---

## 3. Consolidated recommendations — proposed follow-up session sets

Ranking criteria: consumer-visible value first (the downstream AI consumer's
ability to trust and use the output), cost second. Set numbers below are
indicative; per convention each set takes the next free `NNN-` prefix when
actually created. Sizing units are sessions.

### P0 — `integration-test-rebase` (standing; create first)

- **Source:** set `005`'s standing recommendation (unchanged by this review;
  re-confirmed as the right first move).
- **Scope:** re-base the 7 clean-checkout-red Integration tests onto the
  committed HomeHealthCare fixture; investigate the recorded sanitize-test
  timeout.
- **Why first:** every correctness fix below regenerates goldens and needs a
  green integration baseline to validate against.
- **Size:** 1–2 sessions. **Prereqs:** none.

### P1a — `manifest-vba-ingestion-and-parser-hardening`

- **Findings:** F-3-1 (Critical), F-3-2, F-3-3 (Major), F-3-6, plus the
  F-3-9 corpus additions and the F-3-10 AstBuilder items that the same
  files' edits naturally absorb.
- **Consumer value:** the manifest currently asserts that every form and
  report in every customer database has no VBA semantics — silently. This
  set makes the entire UI-surface code-behind visible (kb-tier `VbaCode`
  ingestion via the in-repo `KbVbaSourceLoader` pattern), stops legal legacy
  VBA (`Function F$()`, `Dim step …`) from killing whole modules, fixes the
  type-suffix declaration/reference asymmetry, and surfaces parse
  diagnostics instead of discarding them.
- **Size:** 3 sessions (ingestion + golden regen; AstBuilder/grammar
  hardening + corpus pins; diagnostics surfacing + F-3-9
  conformance-contract decision).
- **Prereqs:** P0.

### P1b — `output-encoding-correctness`

- **Findings:** F-1-1 (Major), F-1-8.
- **Consumer value:** any real-world database with non-ASCII captions,
  labels, or validation text is silently and irreversibly corrupted
  (U+FFFD) in the kb/ and ir/ tiers consumers read — and the same caption
  can differ between `manifest.json` and ir/. Invisible on the all-ASCII
  committed fixtures, certain on production databases.
- **Size:** 1 session (two-line translator fix + hermetic cp1252 translator
  pins per routing branch).
- **Prereqs:** P0. Independent of P1a; can run in parallel with it
  (different files; no shared golden churn — kb/ir goldens are ASCII-stable
  under the fix).

### P2 — `output-contract-fidelity-batch`

- **Findings:** F-2-1 (Major; with F-3-7's parser-side cross-check), F-3-5
  (Major), F-2-3, F-2-6, F-2-4 (implement-or-strike decision), F-1-4,
  F-1-7, F-2-5, F-2-7, plus the R2-3 ride-along (classify slug casing /
  database name-form documentation, O-1-2) and O-1-1's interim
  interpretation-guide note if the wiring arc (P3) has not landed yet.
- **Consumer value:** what ships is mislabeled or non-conformant in ways
  that directly mislead triage: every crosstab in every database is labeled
  `MakeTable` and classified a possibly-unused action query; explicit
  `ByVal` is indistinguishable from default ByRef under the repo's own
  omission contract; four consumer-facing JSON files carry a BOM strict
  parsers reject; the documented label-provenance casing contradicts the
  emitter. One coordinated set, one golden regeneration at the end.
- **Size:** 2 sessions. **Prereqs:** P0 (sequence after P1a to share regen
  discipline, but not blocked by it).

### P3 — `resolver-and-navigation-wiring`

- **Findings:** F-3-4 (Major), F-2-2 (Major), O-2-1, F-3-8.
- **Consumer value:** the differentiating capabilities the plan calls "where
  the harvester's value is created" — cross-module resolution, Access
  object-model binding (`Me`, controls), default members, dynamic SQL,
  navigation graph, unresolved-item aggregation — are built and tested but
  affect zero bytes of output. This set wires them: shared `ProjectScope`
  (the SymbolDb pattern), `AccessObjectModelRegistry` from already-extracted
  form/report metadata, `DefaultMemberRegistry` consultation,
  `NavigationGraphBuilder` + `UnresolvedItemAggregator` into the manifest,
  with the BrowseTo arg fix and event-binding gating riding along.
- **Size:** 3 sessions. **Prereqs:** P1a (ships together or the second
  masks the first — S3's sequencing note), P2 recommended (typed queries).

### P4 — `switchboard-and-report-chain-edges` (the R2-1/R2-2 set)

- **Scope:** the two design sketches in §2 — switchboard row
  materialization (detection, narrow OleDb row-read, raw/kb placement,
  `NavigationGraphInputs.Switchboard` population) and chain-edge derivation
  (embedded-macro parsing, record-source edges, dialog/parameter edges,
  TempVars flows, explicit unresolved for dynamic targets), plus
  interpretation-guide updates for the new surfaces.
- **Consumer value:** turns the consumer's navigation-shell lane and
  chain-shaped AI-handoff packs from AI-interpretation work into mechanical
  reads — the stated goal of both Round 2 strong candidates, for every
  Access database with a switchboard ("most of them," per the consumer).
- **Size:** 3 sessions. **Prereqs:** P3 (hard), P2 (typed queries, hard for
  the chain edges).

### P5 — `cli-input-robustness`

- **Findings:** F-1-3 (Major), F-1-2 (Major as escalated in §1), F-1-5,
  F-1-6 (doc-or-tokenize; premise now proven), O-1-4 (shared `ParseOptions`
  with unknown-switch rejection everywhere), O-1-7.
- **Consumer value:** the CLI's consumer is automation keying off exit
  codes. Today a typo'd `--types` value turns a full harvest into a silent
  empty success (exit 0), and a legal separator-bearing object name fails
  its export. Both are cheap validation fixes; the ParseOptions
  consolidation retrofits unknown-switch rejection to all seven commands at
  once.
- **Size:** 2 sessions. **Prereqs:** P0 only; can interleave anywhere.

### P6 — `cleanup-zero-date-guard`

- **Findings:** the known `identify-cleanup` zero-date misfire (deferred
  since `002`), O-1-5, O-2-6.
- **Consumer value:** stops a time-only `DateTime` column from reading as
  "~46176 days stale" (root-cause skip at the sampler plus the cheap
  `MaxValue <= 1899-12-30` analyser guard), stamps sampler dates honestly,
  and records per-column sampling failures as degradations instead of
  silent nulls.
- **Size:** 1 session. **Prereqs:** P0 only.

**Suggested execution order:** P0 → P1a ∥ P1b → P2 → P3 → P4, with P5/P6
slotted into any gap (both are independent). If consumer pressure on
R2-1/R2-2 is high, the minimal path to P4 is P0 → P1a → P2(F-2-1 only) →
P3 → P4.

---

## 4. Recorded, not recommended (below the value bar standalone)

Per the lean rule, these are preserved with their evidence in the S1–S3
findings docs and ride along only when their file is already open:

- **O-1-1 / interpretation-guide §6 empty-surface note** — absorbed by P3
  (wiring) or P2 (interim one-paragraph honesty fix); not a set of its own.
- **O-1-3** (unordered `Enrich` outcome iteration — no file-content impact),
  **O-1-6** (Access-managed timestamp noise; one guide line), **O-1-8**
  (`MapType` 22/23 alignment one-liner).
- **O-2-2** (geometry parse culture), **O-2-3** (corroboration-bonus
  calibration nit), **O-2-5** (CSV NULL-pad/dup-header degradations),
  **O-2-7** (FROM-scan limitations — bounded by F-2-5's fix), **O-2-8**
  (documented `generatedAt` non-determinism — no action).
- **F-3-10 collected observations** — fold into P1a's AstBuilder work
  (whole-block collapse + silent defaults are naturally replaced by the
  recovered-stub guard); BodyHasher continuation guard and
  SymbolDb `"call"` label ride along if SymbolDb files are touched.
- **Not-reviewed remainder, accepted as-is:** `Core/ReportBaseline/*`
  (~1.5K lines, 9 hermetic test classes), the Com report-path readers,
  `TypeLibraryRegistry`, the five unreviewed CLI command bodies, and the
  `Classification/*` deep pass (PII heuristics) — no dedicated review set
  is recommended; the PII heuristics get a focused pass only if a future
  set touches classification behavior. `DynamicSqlAnalyzer` line-level
  review folds into P3, which gives it call sites.

---

## 5. Disposition summary for the maintainer

- The Opus-built solution is **structurally sound and often exemplary**
  (COM lifecycle, migration generators' validate-or-degrade discipline,
  determinism habits, closed unresolved-reason sets). The dominant defect
  pattern is not component quality but **delivery seams**: substrates built
  and hermetically tested across multiple sets that never got wired into
  production output (F-2-2, F-3-4), an ingestion seam that silently parses
  the wrong text (F-3-1), and decode/contract drift that ships wrong labels
  with full confidence (F-2-1, F-3-5).
- All three Round 2 items are **confirmed against fresh output**; R2-1 and
  R2-2 are worth building and have concrete design sketches sitting on
  models that already exist; R2-3 stays downstream by both parties'
  defaults.
- Nine follow-up items are organized into seven proposed sets (P0–P6) with
  sizes and prerequisites above; everything else is recorded with evidence
  but deliberately not scheduled.
