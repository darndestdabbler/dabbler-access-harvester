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

### DW-9 — Switchboard / Ribbon / Macro navigation inputs are not produced (NavigationGraphInputs unwired)
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
- **Owning set candidate:** P4 `switchboard-and-report-chain-edges` (006 ranked,
  3 sessions; its scope already lists the "narrow OleDb row-read").
- **Mechanical backstop:** the committed HHC golden records a `NavigationGraph`
  with populated `Edges`/`UnresolvedTargets` but empty `EntryPoints` /
  `Switchboard` / `Ribbon` / `Macros`; the absence is visible there and pinned
  by the production-wiring hermetic test added in 011 S2.

### DW-10 — Dialog→query→report chain edges (R2-2) are not derived
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
- **Owning set candidate:** P4 `switchboard-and-report-chain-edges` (with DW-9).
- **Mechanical backstop:** the committed HHC golden's `NavigationGraph.Edges`
  records the report→dialog `OpenForm` edges (with `OpenArgs`/`WhereCondition`)
  but no derived cross-object chain records; the absence is visible there.

## Closed

_(none yet)_
