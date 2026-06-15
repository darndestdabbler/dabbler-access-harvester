# Deferred Work Register

> **Purpose:** a durable, single backlog of work **deliberately deferred during
> session execution** that is *not already owned by a planned/authored session
> set*. It exists so nothing deferred gets silently forgotten — the
> "half-implemented code / unwired seams" risk the 006 review documented across
> three layers.
>
> **This file is required reading** (see `project-guidance.md` → Conventions →
> Workflow Expectations). Every session reads it at start; every closeout checks
> whether any open item's trigger has arrived and, if so, must address it or carry
> it into the next-session-set recommendation. Removing an item is allowed **only**
> when the work has landed (cite the commit/set) or the human explicitly cancels
> it — never a silent drop.
>
> **Relationship to other records:** items already ranked/owned by the 006 review
> arc (F-3-4 resolver wiring → P3, F-2-1 query decode → P2, etc.) live in
> `docs/session-sets/006-.../next-session-set-recommendation.md` and are NOT
> duplicated here. This register is for gaps surfaced *mid-execution* that no spec
> yet owns. Cross-provider review findings that are deferred are also flagged in
> `docs/planning/fable-review-queue.md`; this register is the authoritative
> "do not forget" list.

## How to use

- **Adding:** when you defer something mid-session, add an entry with: what, the
  origin (session/finding), why it was deferred, the concrete trigger that should
  pull it back in, and a candidate owning set. Give it a stable `DW-<n>` id.
- **Discharging:** when the work lands, move the entry to **Closed** with the
  commit/set that did it and the date. Do not delete the line.
- **Mechanical backstop:** where practical, pin the deferred behavior with a
  `Skip`-ped or known-limitation test that names the `DW-<n>` id, so the gap also
  shows up on every test run, not just in this doc.

---

## Open

### DW-1 — Contextual keywords accepted as identifiers only in variable-class slots
- **What:** `STEP, COMPARE, EXPLICIT, PRESERVE, LIB, ALIAS` are accepted as
  ordinary identifiers in variable-class positions (declaration names, parameters,
  **named arguments**, loop variables, bare expressions). They are still rejected
  as: **procedure names** (`Sub Step()`, `Function Compare()`), **UDT type
  members**, **enum members**, **statement labels**, and **`Declare` names**.
- **Origin:** Set 008 Session 2 (F-3-2). Scope confirmed by the devil's-advocate
  interim review (Gemini + GPT, finding H3). The named-argument slot **was fixed**
  in S2 (it was an internal inconsistency the change itself created); the remaining
  slots were deferred.
- **Why deferred:** unproven need — **zero occurrences** in the HomeHealthCare
  fixture (32 modules) *or* the NorthwindDevEdition reference DB (66
  Microsoft-authored modules). Widening every slot risks unneeded over-acceptance;
  GPT advised "do not widen every slot blindly."
- **Trigger to pull back in:** a real customer DB (or a new corpus case) exercises
  one of these slots, OR a parser-grammar-completeness set is scoped.
- **Owning set candidate:** a future `parser-grammar-completeness` set.
- **Mechanical backstop:** known-limitation grammar test (added Set 008 S2) naming DW-1.

### DW-2 — `Report.Line` / `Report.Circle` graphics-method coordinate syntax
- **What:** the legacy graphics-drawing syntax `Me.Line (x1,y1)-(x2,y2)[, color][,
  B[F]]` and `Me.Circle (x,y), radius[, ...]` is unparsed (the parser emits
  `VBA0003 missing ')' at ','`).
- **Origin:** Set 008 Session 2 — `rptClientProfile` in the HomeHealthCare corpus
  uses `Me.Line (…)-(…)` (it is the one remaining parse failure on the fixture; the
  module comment tags it a "migration stress test"). Absent from Northwind.
- **Why deferred:** out of scope for Set 008 (manifest VBA ingestion + F-3-2/F-3-3
  hardening); the `(coord)-(coord)` step syntax is a distinct grammar feature.
- **Trigger to pull back in:** a parser-grammar-completeness set, or a consumer that
  needs report graphics methods parsed.
- **Owning set candidate:** `parser-grammar-completeness`.
- **Mechanical backstop:** the committed HomeHealthCare parse-success report records
  this as a standing `parseFailed` (1 of 32); the regression test surfaces it.

### DW-3 — Type-declaration suffix → type for VARIABLE/const/param declarations
- **What:** `Dim s$` (and suffixed module variables, constants, and parameters with
  no explicit `As`) keep `Type = null` plus a `TypeSuffix`, so a consumer reads the
  language default (Variant) rather than the suffix-implied type (`$`→String,
  `%`→Integer, `&`→Long, `!`→Single, `#`→Double, `@`→Currency). Set 008 S2 fixed
  this for **function / property-get return types** (finding H4); the **variable**
  parallel was left.
- **Origin:** Set 008 Session 2 synthesis (Gemini + GPT H4, variable parallel).
- **Why deferred:** pre-existing (not introduced by S2); type-*emission* is the
  F-3-5 class the Set 008 spec Non-Goals explicitly defer to P2. Golden-neutral
  (no suffixed variable declarations in HHC or Northwind).
- **Trigger to pull back in:** the output-contract-fidelity batch (P2) that owns
  F-3-5 (ByVal emission), which is the natural home for suffix→type emission too.
- **Owning set candidate:** `output-contract-fidelity-batch` (P2).
- **010 Session 1 decision (2026-06-13): evaluated and explicitly DEFERRED** (per
  the 010 spec's "decide explicitly whether to pull DW-3 in here" instruction).
  Rationale: DW-3 is golden-neutral with **zero** occurrences in either corpus
  (no real-data fixture exercises it), and it spans the variable/const/module
  declaration projection paths — separate from F-3-5's parameter `BuildParameter`
  site. Pulling it into a session already landing two Majors (F-2-1 query-type
  decode + F-3-5 ByVal) plus the F-3-7 reconcile would broaden the verification
  surface for no corpus-observable benefit, against the set's scope-discipline
  Stop Rule. The natural home is now a focused parser/type-emission set that can
  do params + variables + constants uniformly. Remains **open**.

### DW-4 — `!`-suffixed function / property-get NAME does not parse (BANG ambiguity)
- **What:** `Function Calc!()` / `Property Get P!()` fails to parse —
  `extraneous input '!' expecting '('`. In the procedure-name + `(` position the
  lexer reads `!` as the BANG operator, not a type-declaration suffix, so the
  Single-return form via `!` is rejected. The other five suffixes
  (`$ % & # @`) parse and type correctly in that position (008 S2 H4 fix).
- **Origin:** Set 008 Session 2 — surfaced by a Session-2 pin
  (`S2ParserHardeningTests.FunctionSuffix_MapsToBuiltInReturnType`) while
  validating the H4 return-type-from-suffix fix.
- **Why deferred:** narrow (the `!`/Single return form on a function *name* is
  rare) and absent from both the HHC and Northwind corpora; resolving the
  BANG-vs-suffix lexer ambiguity in the name+`(` position is a grammar change
  beyond the F-3-2 scope. The `!`→Single *mapping* is already correct for
  variable/parameter contexts; only the function-name lexing is gapped.
- **Trigger to pull back in:** a real DB (or corpus case) declares a `!`-suffixed
  function/property name, OR a parser-grammar-completeness set.
- **Owning set candidate:** `parser-grammar-completeness` (with DW-1, DW-2).
- **Mechanical backstop:** the `!` case is deliberately excluded from the H4
  suffix-mapping theory with a comment pointing here.

### DW-5 — `RunParse` honours a narrower BOM set than the translator / manifest paths
- **What:** the CLI `parse` path (`RunParse`, `Program.cs:~590-600`) detects only
  a UTF-8 BOM (`EF BB BF`) and a UTF-16 **LE** BOM (`FF FE`) via a manual byte
  check, then falls back to cp1252. The translator (`SaveAsTextTranslator`, Set
  009) and the manifest path (`Pipeline`) read via
  `File.ReadAllText(path, GetEncoding(1252))`, whose StreamReader auto-detects a
  **broader** BOM set (UTF-8, UTF-16 LE **and BE**, UTF-32). So a UTF-16-BE-BOM
  file decodes correctly through `translate`/manifest but is mis-read as cp1252
  through `parse`.
- **Origin:** Set 009 Session 1 devil's-advocate interim review (Gemini + GPT,
  finding DA-1). Both reviewers ran the same probe and confirmed the divergence;
  they split only on severity (GPT Minor, Gemini Major).
- **Why deferred:** **latent / unproven need.** There is no evidence Access
  SaveAsText ever emits UTF-16-BE output (the kb/ir flow this set fixed does not
  go through `RunParse` at all), so the divergence is on synthetic-but-valid
  input, not demonstrated real Access output. Out of scope for 009 (P1b =
  translator decode only).
- **Trigger to pull back in:** a real `.txt`/VBA input with a UTF-16-BE BOM
  reaches `parse`, OR a session unifies the three read sites behind one helper
  (see DW-6).
- **Owning set candidate:** the DW-6 decode-centralization set, or a CLI-robustness set.
- **010 Session 2 update (2026-06-13):** the two **run-pipeline** sites (translate
  + manifest read) now share `SaveAsTextDecoder` (BOM → strict UTF-8 → cp1252), so
  this divergence is **narrowed to `RunParse` only** — and `RunParse` now also
  lags on the no-BOM-UTF-8 case (the regression 010 fixed for the run pipeline:
  a UTF-8-encoded `§` mis-read as cp1252 → `Â§`). Discharging DW-5 is now a
  one-line change: point `RunParse` at `SaveAsTextDecoder.ReadText` too. Left
  deferred per the 010 scope boundary (CLI `parse` path is not golden-exercised).

### DW-6 — Centralize SaveAsText decode (consider transcoding raw→UTF-8 at the exporter boundary)
- **What:** three disjoint code paths each manage their own cp1252 provider
  registration and (differing) BOM logic to read SaveAsText output:
  `SaveAsTextTranslator` (Set 009), `Pipeline.EmitManifestArtifacts`, and
  `RunParse`. A cleaner architecture would normalize the encoding **once** — e.g.
  transcode `raw/*.txt` to UTF-8 inside `SaveAsTextExporter` immediately after
  `Application.SaveAsText` (the cp1252-ness is a property of the COM exporter's
  output, not of every downstream reader), so consumers read plain UTF-8 with no
  code-page registration or per-site BOM logic — or, more conservatively, a
  single shared `OpenAccessText(path)` helper with BOM-detect-then-cp1252.
- **Origin:** Set 009 Session 1 devil's-advocate interim review (Gemini DA-4,
  UPHELD Major; GPT DA-4 OVERTURNED — GPT confirmed the current per-site
  registration is *correct* and that no sibling read site was left on the UTF-8
  default, and advised factoring a helper only on a second real maintenance pain,
  not pre-emptively). Opus reconciliation: the static-ctor registration is **not**
  dead code (no must-fix for 009); the centralization is a legitimate but
  **out-of-scope architecture** change with its own risk (transcoding alters the
  `raw/` tier's on-disk encoding contract and would touch the golden raw flow).
- **Why deferred:** out of scope for P1b (009 spec stop-rule: anything broader
  than the translator decode → DW entry); larger change than the bug warranted;
  the lean-by-default principle says factor the shared helper when a second
  concrete maintenance need appears, not speculatively.
- **Trigger to pull back in:** a third read site needs the same decode, OR DW-5
  is actioned (unifying BOM behavior is the natural moment to centralize), OR a
  consumer needs the `raw/` tier in a canonical (UTF-8) encoding.
- **Owning set candidate:** a future `encoding-architecture` / CLI-robustness set
  (would also absorb DW-5).
- **010 Session 2 update (2026-06-13): the shared-helper half is partly done.**
  The 010 golden regen surfaced a real correctness bug — Set 009's fixed-cp1252
  decode mojibakes UTF-8-without-BOM SaveAsText output (a UTF-8 `§` → `Â§`) — so
  010 introduced `AccessHarvester.Core.Translation.SaveAsTextDecoder` (BOM →
  strict UTF-8 → cp1252) and adopted it at the **two run-pipeline read sites**
  (`SaveAsTextTranslator`, `Pipeline.cs` module read). What remains of DW-6:
  (a) point the third site, `RunParse` (`Program.cs`), at the same helper
  (also discharges DW-5); and (b) the larger **transcode-`raw/`-to-UTF-8-at-the-
  exporter-boundary** option, which alters the `raw/` on-disk encoding contract
  and is the genuinely out-of-scope architecture change. The helper itself is no
  longer speculative — it is the live decode for the run pipeline.

### DW-7 — Crosstab (TRANSFORM) abstraction drops the embedded SELECT
- **What:** `QueryAbstractionBuilder.cs:~398` maps `TransformStatement =>
  (QueryShape.Transform, null, null, …)` — the inner SELECT's select-list / FROM /
  JOIN / WHERE, which the parsed AST already carries, is discarded. So a crosstab
  query ships with `Sql.Parsed.QueryType = "Crosstab"` (now correctly derived from
  the AST, 010 S1 / F-3-7) but no structured `Select`/`From`/`Joins`/`Where`
  abstraction — the one query family with no structured SQL body.
- **Origin:** 006 review F-3-7 recommendation part (b); 010 Session 1 implemented
  part (a) (derive `Sql.Parsed.QueryType` from the AST shape) and deferred part (b).
- **Why deferred:** out of scope for 010 Session 1, whose F-3-7 ask was narrowly
  "reconcile the query type so the parser result is no longer discarded/re-stamped"
  (done). Projecting the embedded SELECT of a TRANSFORM is a distinct
  abstraction-builder feature with its own golden churn (the 4 HHC crosstabs would
  gain a populated `Sql.Parsed` body), larger than the type reconcile warranted.
- **Trigger to pull back in:** a consumer needs the structured body of crosstab
  queries (e.g. to migrate the inner aggregate/grouping), OR a query-abstraction
  completeness set is scoped.
- **Owning set candidate:** a future query-abstraction / migration-fidelity set.
- **Mechanical backstop:** the committed HHC golden records the 4 crosstabs with a
  `Sql.Parsed` carrying only `Raw` + `QueryType`; the absence of a structured body
  is visible there and in `QueryAbstractionBuilderTests`.

### DW-8 — DynamicSqlAnalyzer / FormReferenceResolver output is not wired into ParserOutput
- **What:** `ParserOutput` `Body.SqlStrings` is now **omitted** (011 S1, F-3-4)
  rather than emitted as the forbidden always-empty array. The
  `DynamicSqlAnalyzer` (`AccessHarvester.Sql.Parser/DynamicSql/`) and
  `FormReferenceResolver` (`AccessHarvester.Sql.Parser/FormReferences/`) are
  built and hermetically tested but have **no production call site**, so
  VBA-constructed dynamic SQL (string-built `SELECT … & x & …`) and SQL form
  references (`Forms!frmFoo!ctl` inside query/validation SQL) are not surfaced
  in the manifest.
- **Origin:** 006 review F-3-4 (the dynamic-SQL / SQL-form-reference half); 011
  Session 1 chose the spec-sanctioned "omit the key until wired" option for
  `SqlStrings` and recorded it here.
- **Why deferred:** wiring `DynamicSqlAnalyzer` is a distinct Phase 5.4
  capability — per-procedure symbolic string-build tracking with its own golden
  churn and analysis surface — materially larger than 011 S1's resolver-wiring
  scope. Omitting the empty array is the minimal correct change (the schema
  marks `Body.SqlStrings` optional, so omission is valid and reads as "not
  analyzed" rather than the synthesized default "builds no SQL").
- **Trigger to pull back in:** a consumer needs dynamic-SQL or SQL-form-reference
  surfaces, OR a dynamic-sql-wiring / query-fidelity set is scoped.
- **Owning set candidate:** a future `dynamic-sql-and-form-reference-wiring` set
  (the P4/P5/P6 remainder 011's next-set recommendation will rank).
- **Mechanical backstop:** `ParserOutputProjectorTests.SqlStrings_OmittedNotEmittedAsEmptyArray`
  pins the omission; the unwired analyzers' tests remain green in isolation.

### DW-9 — Switchboard / Macro / EntryPoint navigation inputs are not produced (NavigationGraphInputs unwired) — **CLOSED 2026-06-14 (012 S1)**
> **CLOSED** by `012-switchboard-and-report-chain-edges` Session 1 (R2-1).
> A shape-gated `SwitchboardExporter` (DAO row-read, Category `Navigation`)
> now materialises the switchboard rows to `raw|kb/Navigation/switchboard-items.json`,
> a `StartUpForm` DAO read yields the default form, and `MacroChainBuilder`
> projects the macro IR; `Pipeline.BuildNavigationInputs` threads the populated
> `NavigationGraphInputs` into `NavigationGraphBuilder.Build` (the `inputs: null`
> is retired). The HHC golden's `NavigationGraph` now carries ordered
> `EntryPoints`, the generic `Switchboard` page tree, and `Macros`. The
> **`Ribbon`** facet was the one sub-part NOT landed (HHC has no custom
> `USysRibbons`; no producer exists) — carved out as **DW-13**.
- **What:** `NavigationGraphBuilder.Build` is now wired in production (011 S2,
  F-2-2) but is always called with `inputs: null`, so the navigation graph
  carries only the **VBA-side** edges (`DoCmd.OpenForm/OpenReport/Close/BrowseTo`
  in code-behind) and **no top-level surfaces**: `NavigationGraph.Switchboard`,
  `.Ribbon`, `.Macros`, and the AutoExec/default-form/switchboard-root
  `EntryPoints` are all empty. The R2-1 consumer request (switchboard menu tree
  → structured edges) needs `NavigationGraphInputs.Switchboard` populated from
  the switchboard **item rows**.
- **Origin:** 011 Session 2 (F-2-2 wiring) + re-grounding against the tree.
- **Why deferred (premise disproven):** the 011 spec assumed "rows are in the
  harvested table data; no new COM surface." **They are not.**
  `tblSwitchboardItems` ships `(no data file)` in the migration output; its 52
  rows (ItemText / Command / Argument — the actual menu tree) are absent from
  **every** harvested artifact, and the 006 review's own probe says verbatim
  "no artifact in the fresh output carries row content." Reading them requires a
  **new** OleDb/DAO row-read — new COM/data surface, which the 011 Hard
  Constraints forbid and the Stop Rule pushes to the owning set. The HHC
  switchboard is *also* driven by a harvested macro (`mcrSwitchboardActions`:
  named sub-macros → `OpenForm`), but turning the macro IR into
  `NavigationGraphInputs.Macros` is itself new production code and still omits
  the menu captions (which live only in the unharvested rows). Scope decision
  recorded by cross-provider consensus (gpt-5-4 + gemini-pro both Option A) +
  human sign-off; see `docs/session-sets/011-.../s2-scope-consensus-*.md`.
- **Trigger to pull back in:** the P4 set is scoped, OR a consumer needs the
  switchboard/ribbon/macro entry-point surfaces.
- **Owning set candidate:** **`012-switchboard-and-report-chain-edges`**
  (006's P4; authored 2026-06-14 by 011 Session 3 — Session 1 owns this).
  Its scope lists the shape-gated detection + narrow OleDb row-read +
  `NavigationGraphInputs.Switchboard` population.
- **Mechanical backstop:** the committed HHC golden records a `NavigationGraph`
  with populated `Edges`/`UnresolvedTargets` but empty `EntryPoints` /
  `Macros` and absent (omitted) `Switchboard` / `Ribbon`; the absence is
  visible there and pinned by the production-wiring hermetic test added in
  011 S2.

### DW-10 — Dialog→query→report chain edges (R2-2) are not derived — **CLOSED 2026-06-14 (012 S2+S3)**
> **CLOSED** by `012-switchboard-and-report-chain-edges` Sessions 2 (mechanical
> producers) and 3 (code-behind producer). `NavigationGraph.ChainEdges` /
> `.UnresolvedChainTargets` now carry the closed chain-edge family across the
> embedded-macro, record-source, query-parameter (S2) and code-behind `vba-call`
> (S3: `acDialog`→`OpensDialog`, `Forms!dlg!ctl` reads, `TempVars!x=` setters)
> producers; HHC golden 63 ChainEdges + 3 UnresolvedChainTargets. Residual
> **coverage** gaps (named-argument opens, indexer/`With` reads, non-bang setter
> supplier edges) carved out as **DW-14** — they are unsurfaced hops, not wrong
> data.
- **What:** the navigation graph surfaces individual `DoCmd` dispatches and
  preserves `NavigationEdge.WhereCondition` / `OpenArgs`, and `IrQuery.Parameters`
  carries declared/form-reference/report-reference parameter provenance — but
  nothing **joins** them into the consumer-requested chain
  (launcher → report → parameter dialog → query → parameters). The report-menu
  embedded macros (`OnClickEmMacro` `OpenReport` targets), the report Open-event
  `DoCmd.OpenForm "frmReportParam_*", , , , , acDialog` dialog opens, the
  `RecordSource` → named-query edges, and the `TempVars!` flows are each
  observable individually but not linked.
- **Origin:** 006 review R2-2 (Confirmed); 011 Session 2 (deferred with DW-9).
- **Why deferred:** the 006 review explicitly scopes "embedded-macro parsing,
  record-source edges, dialog/parameter edges, TempVars flows" to the P4 set,
  and the derivation is substantial new logic (not a wiring task). 011 is P3
  (resolver + navigation **wiring**), scoped by its Stop Rule to stop at the
  P4 boundary and record deferred work.
- **Trigger to pull back in:** the P4 set is scoped, OR a consumer needs the
  chain-shaped handoff packs.
- **Owning set candidate:** **`012-switchboard-and-report-chain-edges`**
  (006's P4; authored 2026-06-14 by 011 Session 3 — Sessions 2–3 own this,
  with DW-9).
- **012 Session 2 progress (2026-06-14, R2-2 part 1):** the **mechanical**
  chain edges are landed. A new `NavigationChainBuilder` derives, over the IR
  surfaces, the closed chain-edge family
  (`NavigationGraph.ChainEdges` / `.UnresolvedChainTargets`; kinds
  `OpensDialog|OpensReport|OpensForm|FeedsRecordSource|SuppliesParameter`;
  closed kebab-case provenance set): the embedded-macro launcher→report hop
  (`embedded-macro`), the record-source → query/table edges incl. inline-SQL
  FROM/JOIN (`record-source`), the query `[Forms]!`/`[Reports]!` references
  (`query-form-reference`), the `TempVars!` record-source references
  (`tempvar-flow`), and prompt-style declared parameters as explicit
  `UnresolvedChainTarget`s (`query-declared-parameter`). HHC golden: 37
  edges + 3 unresolved. **Still open for S3:** the `vba-call` code-behind hops
  (`acDialog` → `OpensDialog`, `Forms!dlg!ctl` reads, the `TempVars!x =`
  assignments that pair a supplier to a `tempvar-flow` reference). DW-10 closes
  with the S3 landing commit per the spec's end-of-set deliverables.
- **Mechanical backstop:** `NavigationChainBuilderTests` pins each producer; the
  committed HHC golden's `NavigationGraph.ChainEdges` now records the derived
  chains. Before S2 the golden had only `Edges` and no chain records.

### DW-11 — `unresolved-items.json` is procedure-body-scoped and undercounts header / module-level unresolved references — **CLOSED 2026-06-15 (015 S1 Layer A + S2 Layer B)**
- **What:** the unresolved surfaces undercount in **two distinct layers**, both
  surfaced by the 011 S3 devil's-advocate review (R1 + R2):
  - **Layer A — aggregator drops procedure-header refs (index-only gap).** The VBA
    half of `unresolved-items.json` is built from
    `FunctionClassification.UnresolvedReferences` —
    `UnresolvedItemAggregator.Build(navigationGraph, functionClassifications)` at
    `AccessHarvester.Core/Pipeline.cs:612-614`. `FunctionClassificationBuilder`
    walks only procedure **bodies** (`procedureAst.Body`), so an unresolved
    reference in a procedure **header** (an optional parameter's default
    expression) is **absent from `unresolved-items.json`** — but it **is** present
    inline on `ParserOutput.Body.References[]` (the projector groups by procedure
    *scope*, header included). So the index is not a structural superset of
    `Body.References[ResolutionStatus="Unresolved"]`.
  - **Layer B — projection drops module-level refs (no manifest signal at all).**
    `ParserOutputProjector.GroupReferencesByProcedure` keeps only references with
    an enclosing `ProcedureScope` and explicitly drops module-level references
    ("the v1.0 schema has no module-level item type",
    `ParserOutputProjector.cs:65-67`). So a module-level unresolved reference
    (`Const`/`Dim` initializer, `Implements <unresolved>`, module
    `Private x As New <unresolved>`) appears in **neither**
    `ParserOutput.Body.References[]` **nor** `unresolved-items.json` — there is no
    complete manifest signal for it. Fixing Layer B is the larger change (it needs
    a manifest home for module-level references, i.e. a schema addition).
- **Proven reproducers (GPT-5.4 devil's-advocate probes, 011 S3):**
  - Layer A: `Public Sub Demo(Optional ByVal x As Variant = MissingThing)`
    (trivial body) projects **one** unresolved reference (`MissingThing`,
    `not-in-scope`) into `Body.References`, while
    `FunctionClassification.UnresolvedReferences` is **0** and
    `UnresolvedItemAggregator.Build(functionClassifications: …)` returns **0**.
  - Layer B: `Public Const DemoConst = MissingThing` (plus a trivial procedure)
    yields one unresolved reference on the raw `ResolvedModule`, but
    `ParserOutput.Body.References[]` is **0** and the aggregator is **0** — the
    module-level reference is dropped at projection.
- **Origin:** 011 Session 3 closeout devil's-advocate review (Gemini + GPT, set-wide
  addendum, concern C9). Both providers independently flagged it (GPT Major, Gemini
  Critical); GPT proved it with the probe above. The defect was introduced by 011
  Session 2's `UnresolvedItemAggregator` wiring (F-2-2).
- **Why deferred:** both layers carry real design decisions. **Layer A** — the
  aggregator was *deliberately* fed only `functionClassifications` (not
  `resolverDiagnostics`) to avoid **double-emitting** the references both sources
  carry; the fix must aggregate the *full* procedure-scoped reference set the
  `ParserOutputProjector` already uses **without** re-introducing that
  double-count. **Layer B** is larger — module-level references have no manifest
  home today (the v1.0 schema has no module-level item type), so surfacing them
  needs a schema addition (a module-level reference slot) plus the projector change
  to stop dropping them — an envelope-level change with its own golden churn, not a
  wiring tweak. Both are golden-neutral on HomeHealthCare (zero
  such cases), so they cannot be validated by the standard golden-regen gate and
  need synthetic pins; doing it as a closeout afterthought (S3 is docs/validation
  scope) would ship an under-verified change to a subtle aggregation path and (for
  Layer B) a schema bump. Consumer impact is bounded **for Layer A** — the inline
  per-reference `ResolutionReason` / `ResolutionStatus` remain a complete signal for
  procedure-scoped references (body and header). **Layer B** module-level
  references have **no** manifest signal until the schema gains a home for them; a
  consumer that must account for them reads the source directly. 011 S3 disclosed
  both layers in `interpretation-guide.md` §6.
- **Trigger to pull back in:** a focused unresolved-items-completeness session is
  scoped, OR a consumer relies on `unresolved-items.json` as a complete index, OR a
  real DB exercises header / module-level unresolved references.
- **Owning set candidate:** a focused `unresolved-items-completeness` set (small;
  P0-only prereq). NOT folded into `012-switchboard-and-report-chain-edges` (that
  set's theme is navigation, not unresolved-item aggregation).
- **Mechanical backstop (owning session's first task):** encode both proven
  reproducers as hermetic pins — Layer A
  (`Public Sub Demo(Optional ByVal x As Variant = MissingThing)`) asserting the
  reference appears in **both** `Body.References` and the aggregated
  `UnresolvedItems`; Layer B (`Public Const DemoConst = MissingThing`) asserting
  the module-level reference reaches whatever manifest home the fix gives it. 011 S3
  captured the reproducers here rather than adding `Skip`-ped pins in the docs-only
  closeout.

### DW-12 — Latent: a `"Resolved"` reference can point to a `VBA_RESOLVE_FAILURE` stub (degradation-contract asymmetry)
- **What:** under the shared `ProjectScope` (011 S1, F-3-4), a module's public
  declarations are attached to the shared project in pass 1 (scope build); pass 2
  resolves each module. A module that **throws during pass-2 resolution** is caught
  with a `VBA_RESOLVE_FAILURE` diagnostic and **excluded from `resolvedModules`**
  (`AccessHarvester.Core/Pipeline.cs:480-499` — the `parsed.Add(...)` only runs on
  success), so it ships as a degraded stub (`ParserOutput=null` for standard
  modules). **But its pass-1 scope remains in the shared project**, so a sibling
  resolved in pass 2 can bind a cross-module reference to its public members —
  producing a `ResolutionStatus:"Resolved"` reference whose **target module shipped
  as a `ParserOutput=null` stub**.
- **Origin:** 011 Session 3 closeout devil's-advocate review (concern C7(b)).
  Gemini flagged it as a consumer-deref hazard; GPT could **not** find a live
  `VbaReferenceResolver.Resolve()` throw path (only null-argument guards), so the
  state is **reachable by contract but not currently triggerable** on any known
  input. (The original 011 S1 R1→R2 review had reasoned the resolve-failure case
  "benign" on the grounds the symbols are genuine — true, but it did not consider
  the target shipping as a `null`-`ParserOutput` stub.)
- **Why deferred:** latent — no input is known to make `Resolve()` throw, so it
  affects no current output; and the consumer-facing hazard is closed by the 011 S3
  documentation note (`interpretation-guide.md` §4 `VBA_RESOLVE_FAILURE` bullet:
  "null-check a referenced module's `ParserOutput` regardless of the reference's
  resolved status"). The code-side hardening (prune resolve-failed scopes from the
  shared project before sibling resolution, or annotate the resolved reference as
  pointing at a degraded target) is a real but low-priority change with its own
  re-resolve cost.
- **Trigger to pull back in:** a real DB (or a parser change) produces a
  `VBA_RESOLVE_FAILURE`, OR a consumer reports a deref crash on a resolved target,
  OR the shared-`ProjectScope` degradation contract is revisited.
- **Owning set candidate:** a future resolver-hardening / degradation-contract set.
- **Mechanical backstop:** none added (not currently triggerable, so no input
  reproduces it); the `interpretation-guide.md` §4 note is the standing consumer
  guard.

### DW-13 — Ribbon (`USysRibbons`) navigation surface is not produced — **CLOSED 2026-06-15 (016 S1 + S2)**
> **CLOSED** by `016-ribbon-navigation-surface`: Session 1 added the shape-gated
> `USysRibbons (RibbonName, RibbonXML)` row-read (`RibbonExporter` +
> `RibbonSignatureDetector`), the pure namespace-tolerant `RibbonXmlParser` →
> recursive `RibbonStructure`, the `RibbonStructureBuilder`, and the
> `Pipeline.BuildNavigationInputs` wiring so `NavigationGraph.Ribbon` is populated on
> a custom-ribbon DB; Session 2 added the ribbon **entry-point** surface
> (`NavigationGraph.RibbonEntryPoints`): every control `onAction` + the root `onLoad`
> resolved against the harvested modules, with explicit `Resolved`/`Unresolved`/
> `Ambiguous` (no guessed module). Validated against NorthwindDevEdition's real custom
> ribbon **locally** (never committed — proprietary) via `NorthwindLocalInvariantTests`;
> pinned with synthetic hermetic tests; HHC golden proven **byte-level neutral** by a
> fresh COM run (no `USysRibbons` → `Ribbon`/`RibbonEntryPoints` omitted). Both sessions
> cross-provider VERIFIED (gpt-5-4). Remainders carved out: **DW-20** (dynamic `get*`
> callbacks → entry points) and **DW-21** (non-`<tabs>` customUI sections in the
> structured view). Full entry retained below.
- **What:** `NavigationGraph.Ribbon` / `NavigationGraphInputs.Ribbon` and the
  `RibbonStructure` model exist, but no producer reads `USysRibbons` custom-ribbon
  XML into structure, so the facet ships null on every corpus. (Carved out of DW-9,
  whose switchboard / macro / entry-point parts landed in 012 S1.)
- **Origin:** 012 Session 1 (R2-1). HomeHealthCare is switchboard-driven with no
  custom ribbon, so there is no fixture to drive or pin a ribbon producer against;
  per the spec's Step 5 ribbon decision, populating it from a **real** custom-ribbon
  corpus is left here rather than synthesising a fixture.
- **Why deferred (premise now CORRECTED 2026-06-15):** the original deferral said
  "no in-corpus signal to build against." That was an **HHC-only** view.
  **NorthwindDevEdition HAS a custom ribbon** — a local harvest (`run` →
  `C:/temp`, gitignored, never committed) confirmed `USysRibbons` (one record,
  `RibbonName` + `RibbonXML LONGTEXT`) is extracted to `raw|kb/ir/Tables/USysRibbons`
  with a companion `modRibbonCallback` VBA module, while `NavigationGraph.Ribbon`
  still ships null (no producer). So the ground-truth corpus the deferral was waiting
  for **already exists locally**; the only reason this is still open is that nobody
  scoped the producer. (Surfaced 2026-06-15 when the human flagged that verification
  had run HHC-only — Northwind is the standing local test corpus.)
- **Trigger to pull back in:** **AVAILABLE NOW** (Northwind's custom `USysRibbons`),
  OR a consumer needs the ribbon entry-point surface. Validate the producer against
  Northwind's real ribbon **locally** (never commit a Northwind golden — proprietary);
  pin the committed regression with a **synthetic** USysRibbons-shaped fixture + a
  hermetic producer test.
- **Owning set:** **`016-ribbon-navigation-surface`** (authored 2026-06-15: spec +
  not-started v4 stub; 2 sessions — S1 USysRibbons row-read + customUI parser →
  `RibbonStructure`, S2 `onAction` → callback entry points + closeout; Northwind-local
  validation + synthetic committed pins). The natural next set now that DW-11 is closed.
- **Mechanical backstop:** the HHC golden's `NavigationGraph` omits `Ribbon` (null);
  the §5.7 interpretation-guide note records the deliberate null. The committed
  corpus has no custom ribbon, so the producer's regression pin must be synthetic.

### DW-14 — Navigation-target resolution misses named-argument and indexer/`With` reference forms
- **What:** the navigation layer resolves a `DoCmd.OpenForm/OpenReport` target only
  from the **first positional** argument, and the S3 chain code-behind producer
  recognises dialog-control references only as the bare two-bang
  `Forms!obj!ctl` / `Reports!obj!ctl` shape. Three realistic Access VBA forms are
  therefore not surfaced: (a) a **named-argument** target
  (`DoCmd.OpenForm FormName:="frmX"`, `DoCmd.OpenReport ReportName:="rptX"`) —
  the existing **VBA-edge** layer (`NavigationGraphBuilder`, 011) records it as a
  `missing-target` unresolved and the S3 chain producer emits nothing; (b) an
  **indexer** control reference (`Forms("frmX")!ctl`); (c) a `With Forms!frmX …`
  block with implicit `!ctl` members (`ImplicitWithBangExpression`). And (d) a
  **non-bang TempVars setter in form/report code-behind**
  (`TempVars("x") = …`, `TempVars.Add "x", …`) does not emit the
  `Owner → TempVar` `SuppliesParameter` chain edge that the canonical
  `TempVars!x = …` form emits — the non-bang forms are recognised only in the
  **supplied universe** (so they correctly suppress a false `tempvar-no-supplier`),
  not in owner-side edge production, so the walkable chain edge is missing for
  them.
- **Origin:** 012 Session 3 routed cross-provider verification (gpt-5-4, R1
  concerns 1 + 2(a); R2 confirmed item (d)). The write-direction false-positive
  (R1 concern 2b), the TempVars non-bang **setter** false-positive in the
  supplied universe (R1 concern 4), and the `.Add` name-slot scan-forward bug
  (R2 Major 1) were **fixed in S3**; only these **read/target/edge coverage**
  gaps (no false data, just unsurfaced hops) are deferred.
- **Why deferred:** golden-neutral on HomeHealthCare (every HHC `DoCmd` open uses
  a positional string-literal target; every dialog read is a bare two-bang), so
  no fixture exercises them. The named-argument gap (a) is **shared with the
  already-shipped VBA-edge layer** — fixing it only in the chain layer would make
  the two layers disagree on the same call (chain emits an edge, `Edges` shows a
  missing-target unresolved); a correct fix resolves named-arg targets in
  `NavigationGraphBuilder` **and** the chain producer together, which is 011's
  navigation-extraction surface, beyond this set's R2-2 (DW-10) scope. Pulling it
  into the S3 closeout would ship an under-verified multi-pattern change to the
  navigation extractor against the scope-discipline Stop Rule.
- **Trigger to pull back in:** a real DB (or a corpus case) uses a
  named-argument `DoCmd` open, an indexer/`With` dialog-control reference, OR a
  navigation-extraction-completeness set is scoped.
- **Owning set candidate:** a future `navigation-target-completeness` set (would
  span the VBA-edge layer + the chain code-behind producer; small, P0-only
  prereq).
- **Mechanical backstop:** none added (golden-neutral, no fixture reproduces it);
  the §5.7 interpretation-guide note records that chain opens/reads resolve only
  positional-literal targets and bare two-bang control references.

### DW-15 — `identify-cleanup` returns exit 2 (not the contract's 1) for usage errors — **CLOSED 2026-06-15 (013 devil's-advocate follow-up)**
> **CLOSED** by the Set 013 whole-set devil's-advocate review (GPT-5.4 + Gemini
> 3.1 Pro both CHALLENGED the deferral as defeating the set's exit-code-honesty
> mandate). With human approval, `IdentifyCleanupCommand.Run` was normalised onto
> the CLI-wide contract: usage errors (unknown switch, missing/mutually-exclusive
> `--in`/`--manifest`, bad `--threshold-days`/`--harvest-date`) now return **1**;
> the unexpected-exception catch returns **2**; directory-not-found stays 1
> (usage-class, matching `validate`). Pinned: `identify-cleanup` joined the
> `CliUnknownSwitchTests` uniform exit-1 theory + two added usage-error pins
> (`IdentifyCleanup_MissingRequiredOption_IsUsageErrorExit1`,
> `IdentifyCleanup_BadThresholdDays_IsUsageErrorExit1`).
- **What:** every CLI subcommand reports a usage error as exit **1** and an
  unexpected error as exit **2** (the usage text from F-1-3 states
  `1 = usage error`), **except `identify-cleanup`**, whose `Run` summary documents
  and implements the inverted convention "0 on success, **2 on usage error**, 1 on
  other failure." So a downstream consumer keying off exit codes reads an
  `identify-cleanup` usage mistake (missing/mutually-exclusive `--in`/`--manifest`,
  bad `--threshold-days`, now also an unknown `--switch`) as the harvester's
  "unexpected error" (2) signal rather than "usage error" (1).
- **Origin:** Set 013 Session 2 (006 P5), surfaced by the O-1-4 uniform
  unknown-switch rejection — the new `CliUnknownSwitchTests` theory expected exit 1
  and `identify-cleanup` returned 2.
- **Why deferred:** **out of P5 scope.** O-1-4's deliverable is "unknown-switch
  rejection on all seven subcommands, each command's accepted-option set
  preserved" — it adds rejection, it does not renumber any command's exit codes.
  The exit-2 value is `identify-cleanup`'s own pre-existing contract (before this
  set, `identify-cleanup --bogus x` already exited 2 via the "--in required"
  path), so the O-1-4 change is exit-code-preserving for this command — only the
  message improved. Normalising 2→1 is a command-specific exit-code contract
  change that could break a consumer already coded to `identify-cleanup`'s 2; per
  the set's Stop Rule ("a fix that would make a previously-failing valid run pass
  differently is a stop-and-disclose signal") it is recorded here, not done inline.
- **Trigger to pull back in:** a CLI-exit-code-consistency set is scoped, OR a
  consumer reports mis-reading an `identify-cleanup` usage error as an unexpected
  error.
- **Owning set candidate:** a future `cli-exit-code-consistency` set (small,
  P0-only prereq), or folded into the next CLI-robustness set.
- **Mechanical backstop:** `CliUnknownSwitchTests.IdentifyCleanup_UnknownSwitch_IsRejectedExit2`
  pins the current exit-2 behaviour and names DW-15; flipping it to 1 is the
  owning session's first signal.

### DW-16 — Sampler-side time-only-column skip (zero-date root cause) is deferred; analyser guard is the shipped fix
- **What:** the headline zero-date misfire (a **time-only** `Date/Time` column's
  `MAX()` presenting as `1899-12-30 HH:MM:SS`) is fixed at the **analyser** by the
  Set 014 zero-date guard (`CleanupAnalyser` treats a sampled maximum whose date
  component is *exactly* `1899-12-30` as no date signal). The **root-cause**
  alternative — skip/special-case time-only columns at the COM sampler
  (`AccessDateMaximaSampler`) so a time-of-day value never presents as a date
  maximum at all — was **evaluated and deferred**.
- **Origin:** Set 014 Session 1 (P6; the misfire + O-1-5 + O-2-6 set). The spec's
  Step 3 instructed an explicit ship-or-defer decision on the sampler root cause.
- **Why deferred:** **No reliable time-only signal exists at the sampler/IR.**
  `IrTable.DateTimeColumnNames` is derived from the column `TypeName` containing
  "Date"/"Time"; Access exposes a single `Date/Time` column type with no type-level
  date-only / time-only / datetime distinction. Time-only-ness is a *Format*
  property or a property of the stored data, not the column type — identifying it
  would need a DAO `Format`-property read or a value probe, neither currently
  surfaced. The analyser guard fully fixes the **observed** misfire (and is the
  robust safety net regardless of *why* a maximum is the zero-date), but it is
  **not** a complete correctness proof (the 014 R1/R2 cross-provider review
  overturned an initial "provably complete" framing — see the accepted-false-
  negatives bullet below). The sampler-side root cause would additionally let the
  pipeline (a) avoid a wasted COM `MAX()` on a time-only column, (b) emit a
  "time-only column skipped" diagnostic, and (c) supply the per-column provenance
  needed to resolve the accepted false-negatives.
- **Accepted false-negatives this DW also owns (014 R2 cross-provider review):**
  because the sampler returns a single `MAX` across all of a table's DateTime
  columns with **no per-column provenance**, the analyser guard cannot decide two
  edge cases and accepts suppressing them: (a) a genuine record dated *exactly*
  `1899-12-30` (in Access this value is the zero/default, so a meaningful real max
  of exactly the epoch is effectively never observed); and (b) a real pre-epoch
  date in one column masked by a sibling time-only column whose `1899-12-30` wins
  the cross-column `MAX`. Both are undecidable without the per-column sampler
  output this DW would add; neither occurs on the HHC fixture. Documented in-code
  at `CleanupAnalyser`'s guard and pinned (as accepted behaviour) by
  `Run_Suppresses_Exact_OleEpoch_Maximum_As_NoSignal`.
- **Trigger to pull back in:** a `Format`/type signal that reliably identifies
  time-only columns becomes available in the IR or via a DAO property read, OR a
  consumer needs the sampler to positively distinguish "time-only column" from
  "column genuinely empty / failed read", OR a consumer reports the exact-epoch /
  masked-pre-epoch false-negative above, OR a sampler-fidelity set is scoped.
- **Owning set candidate:** a future `cleanup-sampler-fidelity` set (small, P0-only
  prereq), or folded into a set that adds column `Format`/type metadata to the IR.
- **Mechanical backstop:** the analyser guard's hermetic pins
  (`CleanupAnalyserTests.Run_Suppresses_TimeOnly_ZeroDate_Maximum_As_NoSignal` and
  the exact-epoch / name-survives / real-date-survives siblings) pin the shipped
  behaviour; the absence of a sampler-side skip is recorded here.

### DW-17 — Module-level unresolved refs reach the index but NOT per-module `ParserOutput` (completeness asymmetry)
- **What:** after DW-11 Layer B (015 S2), a **module-level** unresolved
  reference (a `Const`/`Dim` initializer, an `Implements <unresolved>`, etc.)
  is surfaced in the run-wide `unresolved-items.json` index under
  `UnresolvedSource.ModuleLevelReference` (`ProcedureName=null`) — but it is
  **still absent from the per-module `ParserOutput.Body.References`** inline
  surface, because a module-level reference has no procedure `Body` to live in
  and `ParserOutput.Items` has no module-level item type. So the two
  granularities are **asymmetric**: a **procedure-scoped** unresolved ref has
  TWO signals (inline `ParserOutput.Body.References` + the index), while a
  **module-level** unresolved ref has ONE (the index only). A consumer
  processing a single module's `ParserOutput` in isolation sees no signal for
  its module-level unresolved refs and must join against the run-wide index.
- **Origin:** 015 Session 2 routed pre-implementation architecture critique
  (opus, caveat #2) — flagged the asymmetry as a real known limitation that
  should be explicit rather than buried in a rejected-alternative. DW-11 asks
  for "a manifest signal," which the index satisfies; per-module granularity
  was out of DW-11's scope.
- **Why deferred:** adding a module-level slot to `ParserOutput` is a
  **structural envelope change** (a new top-level references array on every
  `ParserOutput`, or a new module-level item type) — a full envelope version
  bump with broad golden churn — for no completeness the index does not
  already provide. The index is the complete run-wide signal DW-11 required;
  the inline per-module surface is a convenience a consumer can synthesise by
  joining on `ModuleName`.
- **Trigger to pull back in:** a consumer needs per-module completeness
  without joining against the run-wide index, OR a `ParserOutput` envelope
  bump is being made for another reason and the module-level slot can ride it.
- **Owning set candidate:** a future `parser-output-module-level-surface` set,
  or fold into the next set that bumps the `ParserOutput`/manifest envelope.
- **Mechanical backstop:** none needed (no incorrect output today — the index
  is complete); the asymmetry is documented here and in
  `015/s2-design-note.md`. The interpretation guide's unresolved-items section
  should note module-level refs appear in the index only.

### DW-18 — Resolver has no intrinsic-constant registry, so VBA built-in constants surface as `not-in-scope` (false-positive unresolved refs)
- **What:** the resolver recognises built-in **functions** (a
  `BuiltInFunctionRegistry`) but has **no registry for VBA intrinsic
  *constants*** (`vbCrLf`, `vbTab`, `vbNullString`, the colour constants
  `vbWhite`/`vbYellow`/…, the comparison/date/keycode/message-box constant
  families, `vbObjectError`, etc.). Every use of one resolves to
  `not-in-scope` and surfaces as an unresolved reference (inline
  `ResolutionReason: "not-in-scope"` and, after 015, in `unresolved-items.json`
  under `FunctionClassification` / `ModuleLevelReference`). These are
  **false positives**: the identifiers are well-known language intrinsics the
  resolver should bind as built-ins, not real "could not resolve" misses. They
  dilute the unresolved index and can mislead a consumer triaging it.
- **Origin:** 2026-06-15 NorthwindDevEdition **local** validation (the
  HHC-only-verification gap the human flagged — see
  `feedback_use_northwind_as_local_test_corpus`). A local Northwind harvest
  surfaced `vbWhite`/`vbYellow` as module-level `not-in-scope`, **`vbCrLf` 17
  times**, and `vbNullString`; HHC's golden likewise carries `vbObjectError`.
  The breadth (a common string constant like `vbCrLf` is unrecognised) confirms
  the gap is the *absence of a constant registry*, not a few missing entries —
  but the exact intrinsic-constant set + values is the owning set's scope to
  enumerate (hypothesis-not-verified until that set audits the registry).
- **Why deferred:** out of scope for DW-11 (which faithfully *surfaces* what the
  resolver could not bind — it does not decide what *should* bind) and for DW-13
  (ribbon). Adding an intrinsic-constant registry is a **resolver-completeness**
  change: it needs the canonical VBA/VBA-Access constant set with correct
  values/types, and it is **golden-affecting** (every such reference flips from
  `Unresolved`/`not-in-scope` to `Resolved` → `unresolved-items.json` shrinks and
  the inline `ResolutionStatus`/`ResolutionReason` change), so it must land as a
  deliberate set with a coordinated HHC golden regen, not folded into unrelated
  work.
- **Trigger to pull back in:** a consumer reports intrinsic constants polluting
  the unresolved index, OR a resolver-completeness / grammar-completeness set is
  scoped (natural bundling with DW-1/DW-2/DW-4), OR a migration consumer needs
  intrinsic constants resolved to their values.
- **Owning set candidate:** a future `resolver-builtin-constants` set (small,
  P0-only prereq), or folded into the parser/resolver-completeness bundle
  (DW-1..DW-4). **Golden-affecting — plan the HHC regen.**
- **Mechanical backstop:** none committed (the false positives are only visible
  on real corpora — HHC carries `vbObjectError` in the committed golden, which is
  the one in-repo signal; the broader set shows only on the local Northwind run
  pinned by `NorthwindLocalInvariantTests`). The owning set should add a hermetic
  pin that a representative intrinsic constant resolves as built-in.

### DW-19 — `UnresolvedItemAggregator.Build` attributes SQL / dynamic-SQL feeds with SCALAR module/procedure names, but is called ONCE run-wide
- **What:** `Build(...)` takes **scalar** `sqlFormReferenceModuleName` /
  `sqlFormReferenceProcedureName` / `dynamicSqlModuleName` /
  `dynamicSqlProcedureName` parameters to attribute the SQL form-reference and
  dynamic-SQL feeds, but `Pipeline.cs` calls `Build(...)` **exactly once for the
  whole run**. So those feeds are structurally incapable of carrying annotations
  from **more than one module** in a single run-wide call — the scalar name would
  mislabel every site with one module's name. The VBA feeds (resolver diagnostics,
  navigation graph, function classifications, and the 015 procedure-scoped /
  module-level feeds) do NOT have this problem: they carry per-item module/
  procedure on each record. The limitation is currently **latent, not live**:
  `Pipeline.cs` does not feed `formReferenceAnnotations` / `dynamicSqlResults` into
  the run-wide `Build(...)` at all, so no SQL site is mis-attributed today.
- **Origin:** 2026-06-15 Set 015 **set-wide adversarial review** (Gemini 3.1 Pro,
  hypothesis 9 / highest-risk finding; GPT-5.4's pass did not flag it). Pre-existing
  since the aggregator's introduction (Set 011 F-2-2); Set 015 neither introduced
  nor worsened it — it only made the VBA half of the index complete, which threw the
  SQL-half asymmetry into relief.
- **Why deferred:** out of scope for DW-11 (VBA unresolved-reference completeness).
  The fix belongs with the work that actually wires SQL form-references / dynamic-SQL
  into the run-wide index — **DW-8** (DynamicSqlAnalyzer / FormReferenceResolver
  output is not wired into `ParserOutput`). When DW-8 lands, `Build` must take the
  SQL/dynamic feeds as **per-item-attributed collections** (each annotation carrying
  its own module/procedure, like the VBA feeds) rather than a scalar name beside a
  flat list — otherwise the "run-wide index" guarantee breaks for non-VBA inputs.
- **Trigger to pull back in:** DW-8 (or any set) wires SQL form-references or
  dynamic-SQL sites into the run-wide `unresolved-items.json`.
- **Owning set candidate:** **DW-8**'s owning set (dynamic-SQL / form-reference
  wiring) — fix the signature there, do not do it standalone.
- **Mechanical backstop:** none (latent — no live mis-attribution today because the
  SQL feeds are not passed to the run-wide `Build`). Recorded here so the signature
  is fixed at the same time the feeds are wired.

### DW-20 — Dynamic ribbon control callbacks (`get*`) are surfaced verbatim but not resolved to navigation entry points
- **What:** the ribbon producer surfaces the dynamic control callback family
  (`getItemCount`, `getItemLabel`, `getSelectedItemIndex`, `getItemID`,
  `getVisible`, `getEnabled`, …) verbatim in each node's `Attributes` bag — but only
  the **static `onAction`** (every control) and the **root `onLoad`** are promoted to
  typed ribbon entry points and resolved against the harvested modules (016 S2,
  `NavigationGraph.RibbonEntryPoints`). The dynamic `get*` callbacks are real
  ribbon→VBA edges (a `getVisible` names a VBA function) that are **not** surfaced as
  entry points, so a consumer building the full ribbon→VBA call graph must read them
  out of the `Attributes` bag itself.
- **Origin:** 016 Session 1 — cross-provider model consensus (gpt-5-4 flagged the
  non-`onAction` callbacks as real edges) + confirmed on the local NorthwindDevEdition
  ribbon (`ddMRU_GetItemCount`/`GetItemLabel`/…). **Narrowed 2026-06-15 (016 S2):** the
  `onLoad` part of the original deferral is now CLOSED — S2 resolves the root `onLoad`
  to a `RibbonEntryPoint` (`Kind=OnLoad`) alongside the `onAction` callbacks; only the
  per-control `get*` dynamic-callback family remains deferred here.
- **Why deferred:** the 016 spec explicitly scopes the producer to the **static
  `onAction`** callback ("do not interpret … dynamic `getVisible`/`getEnabled`
  callbacks beyond recording the static `onAction`. If a consumer needs dynamic
  callback evaluation, that is a `DW-<n>`, not scope creep"). The callbacks are
  faithfully surfaced (in `Attributes`), so nothing is lost; only the convenience
  of a typed entry-point projection is deferred.
- **Trigger to pull back in:** a consumer needs the full ribbon→VBA callback graph
  (not just `onAction` + `onLoad`), OR a ribbon-callback-completeness set is scoped.
- **Owning set candidate:** a future `ribbon-dynamic-callbacks` set (small, P0-only),
  or fold into a navigation-completeness set.
- **Mechanical backstop:** the local `NorthwindLocalInvariantTests` exercises a ribbon
  whose dropDown carries `get*` callbacks; they appear in the node `Attributes` bag
  (verbatim), not as entry points. No committed pin (HHC has no ribbon).

### DW-21 — Ribbon structured view models `<tabs>`/`<contextualTabs>` only; other customUI sections are raw-only
- **What:** `RibbonXmlParser` projects the ribbon's `<tabs>` and `<contextualTabs>`
  into the structured `RibbonStructure` (the navigation surface). Other customUI
  sections — `<qat>`, `<officeMenu>`, `<backstage>` — and root-level `<customUI>`
  attributes beyond `onLoad` (e.g. `loadImage`) are **not** projected into the
  structured tree. A `<backstage>`/`<qat>` control that carries an `onAction` would
  therefore not surface as a ribbon navigation entry point in the structured view.
- **Origin:** 016 Session 1 routed cross-provider verification (gpt-5-4, R1 — the
  "structured tree drops authored content outside tabs" finding). Disposition:
  documented deliberate boundary, not a code expansion.
- **Why deferred:** the raw, byte-verbatim customUI XML is preserved losslessly in
  `raw/Navigation/ribbon-xml.json`, so **nothing is dropped from the harvest** — the
  omission is only from the navigation-focused *structured view*, and it is now
  explicitly documented (in `RibbonXmlParser`), not silent. `<qat>`/`<officeMenu>`/
  `<backstage>` are navigation-irrelevant Office chrome, absent from both the HHC and
  NorthwindDevEdition corpora, and modeling them is untested scope creep against the
  lean-by-default principle. The structured view faithfully models every *control
  element* within the tabs it does project (no control is dropped there).
- **Trigger to pull back in:** a real DB customises `<backstage>`/`<qat>` with
  `onAction` callbacks a consumer needs as entry points, OR a ribbon-completeness set
  is scoped (natural bundling with DW-20).
- **Owning set candidate:** a future `ribbon-completeness` set (with DW-20).
- **Mechanical backstop:** none committed (no corpus exercises these sections); the
  `RibbonXmlParser` doc records the boundary and the raw-tier lossless fallback.

### DW-22 — Ribbon entry-point contract: `ResolvedProcedure` is not a non-null type, and the bound callback's signature is not captured
- **What:** two consumer-contract refinements on `RibbonEntryPoint` (016 S2),
  surfaced by the set-wide adversarial review (in-IDE GPT + Gemini): (a) a
  `Resolved` ribbon entry point always populates
  `ResolvedProcedure.ModuleName`, but the shared `ProcedureReference` type makes
  `ModuleName` nullable (macro `RunCode` entries on
  `NavigationEntryPoint.CallsProcedure` leave it null) — so a consumer cannot
  rely on the type to guarantee a module, only on the `Resolution=Resolved`
  invariant. A distinct non-null `ResolvedProcedureReference` would encode the
  guarantee in the type. (b) The entry point records *which* procedure binds but
  not its **signature** (parameter count / shape), so a downstream code-generator
  cannot mechanically flag a callback bound to a procedure with the wrong arity
  (Access ribbon `onAction` requires `(control As IRibbonControl)`; `onLoad`
  requires `(ribbon As IRibbonUI)`).
- **Origin:** 016 S2 set-wide evaluation (`s2-set-wide-evaluation-*.md`): A8
  (Major, type-split) + A9 (in-IDE rated Critical, signature capture). The
  routed closeout verifier (gpt-5-4) had judged `ProcedureReference` reuse
  "appropriate", so this is a refinement, not a defect — no `Resolved` entry
  ever ships a null module today.
- **Why deferred:** purely additive consumer-ergonomics; the resolved procedure's
  full signature is already recoverable from the manifest's
  `FunctionClassification` / `ModuleExport` for that module + procedure, so no
  information is lost — only the convenience of a co-located signature and a
  type-level non-null guarantee. Changing `ResolvedProcedure`'s type or adding a
  signature field touches the schema + every consumer; out of scope for the
  closed 016 set.
- **Trigger to pull back in:** a downstream code-generator needs to validate
  ribbon-callback signatures inline, OR a ribbon-completeness set is scoped.
- **Owning set candidate:** the ribbon-completeness set (natural bundle with
  DW-20 / DW-21).
- **Mechanical backstop:** none; the `Resolution=Resolved` invariant (module
  always present) is asserted by the hermetic builder tests and the
  `NorthwindLocalInvariantTests` ribbon-entry-point assertions.

### DW-23 — Ribbon-table detection is signature-only; a non-canonical table with `RibbonName`+`RibbonXML` columns could surface as the ribbon
- **What:** `RibbonSignatureDetector` flags any non-system table carrying both a
  `RibbonName` and a `RibbonXML` column as a ribbon source (mirroring the
  switchboard signature precedent), without preferring the canonical
  `USysRibbons` table name. An unrelated user table with those two column names
  would be harvested as the ribbon and could become the published
  `NavigationGraph.Ribbon` + `RibbonEntryPoints`.
- **Origin:** 016 S1 routed verification raised this as a Minor (disposed:
  signature-based detection is the repo precedent, consistent with switchboard);
  016 S2 set-wide review (gpt-5-4) **escalated to Major** because a false-positive
  now propagates past the raw tier into the resolved entry-point surface, not
  just `raw/Navigation/ribbon-xml.json`.
- **Why deferred:** practical risk is low — a table needs **both** the
  `RibbonName` and `RibbonXML` column names (and to pass the malformed-XML
  degradation guard) to be mistaken for a ribbon, which is essentially always the
  real `USysRibbons`; no corpus (HHC, Northwind) exercises a false positive, and
  the raw tier preserves the truth. A safe hardening is cheap (prefer the literal
  `USysRibbons` table when present; demote non-canonical signature matches to a
  diagnostic) but is a behaviour change to a closed, golden-validated set.
- **Trigger to pull back in:** a real DB surfaces a false-positive ribbon table,
  OR the ribbon-completeness set is scoped (fold the canonical-name preference in).
- **Owning set candidate:** the ribbon-completeness set (with DW-20 / DW-21 / DW-22).
- **Mechanical backstop:** the malformed-XML degradation path
  (`RIBBON_XML_PARSE_FAILURE`) catches a non-customUI payload, so a non-ribbon
  table with garbage XML degrades rather than fabricating a ribbon; only a table
  with both columns AND valid customUI would mislead.

## Closed

- **DW-9** — Switchboard / Macro / EntryPoint navigation inputs — **CLOSED
  2026-06-14** by `012-switchboard-and-report-chain-edges` Session 1 (R2-1). Full
  entry retained above with its CLOSED banner. Ribbon remainder → **DW-13**.
- **DW-10** — Dialog→query→report chain edges (R2-2) — **CLOSED 2026-06-14** by
  `012-switchboard-and-report-chain-edges` Sessions 2 (mechanical producers) +
  3 (code-behind producer). Full entry retained above with its CLOSED banner.
- **DW-13** — Ribbon (`USysRibbons`) navigation surface not produced — **CLOSED
  2026-06-15** by `016-ribbon-navigation-surface`: Session 1 (USysRibbons row-read +
  `RibbonXmlParser` → `NavigationGraph.Ribbon`) + Session 2 (`NavigationGraph.RibbonEntryPoints`:
  `onAction` + root `onLoad` → resolved VBA entry points, explicit Unresolved/Ambiguous).
  Northwind-local validation; synthetic committed pins; HHC golden byte-neutral. Full
  entry retained above with its CLOSED banner. Remainders → **DW-20** (dynamic `get*`
  callbacks) and **DW-21** (non-`<tabs>` customUI sections).
  Residual coverage gaps → **DW-14**.
- **DW-15** — `identify-cleanup` exit 2 (not 1) for usage errors — **CLOSED
  2026-06-15** by the Set 013 whole-set devil's-advocate follow-up (both providers
  CHALLENGED; normalised onto the CLI-wide exit-code contract with human
  approval). Full entry retained above with its CLOSED banner.
- **DW-11** — `unresolved-items.json` undercounts header / module-level unresolved
  references — **CLOSED 2026-06-15** by `015-unresolved-items-completeness`: Session 1
  (Layer A) surfaced procedure-HEADER refs via a dedup-additive procedure-scoped feed;
  Session 2 (Layer B) gave MODULE-LEVEL refs a manifest home via
  `EnumerateModuleLevelReferences` + the new `UnresolvedSource.ModuleLevelReference`
  (which surfaced 2 real HHC cases — `vbObjectError` in `basDataOperations` Const
  decls — disproving the original "zero cases" assumption). Full entry retained above
  with its CLOSED banner. Per-module-`ParserOutput` completeness asymmetry carved out
  to **DW-17**.
