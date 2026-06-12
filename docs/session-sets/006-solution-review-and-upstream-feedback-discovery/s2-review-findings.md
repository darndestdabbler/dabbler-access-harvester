# Session 2 Review Findings — Pipeline: Enrichment, Usage, Classification, Migration

> **Set:** `006-solution-review-and-upstream-feedback-discovery`
> **Session:** 2 of 4 (2026-06-12)
> **Orchestrator:** Claude Code / Claude Fable 5 (review authored in-conversation
> per the spec's Orchestrator Assignment And Delegation Deviation section)
> **Scope reviewed:** `AccessHarvester.Pipeline` — `Enrichment/NavigationGraph*`
> (line-level, as the Round 2 substrate), `UsageIdentification/*` (classifier,
> signal index, collectors, question gating), `Cleanup/*` (analyser + samplers),
> `Migration/*` (DDL emitter, data-load generator, value formatter, type mapper,
> default translator, CSV reader), `Classification/HeuristicsEngine` (skim) —
> plus the `AccessHarvester.Core/Enrichment/` enrichers Session 1 deliberately
> deferred here (GeometryEnricher, LabelResolver, PropertyDecoder full;
> EnrichmentPipelineRunner full).
> **Validation:** solution build green (0 errors); `AccessHarvester.Pipeline.Tests`
> 794/794 passed (hermetic, sequential, foreground). All probes below are
> hermetic file/golden probes — no COM-driven runs (per spec stop rule).

## Summary verdict

The Pipeline project's individual components are well-engineered — the
migration generators in particular are exemplary on the untrusted-input and
explicit-degradation dimensions, and the determinism discipline (sorted
collections, fixed LF, closed reason sets) is consistently applied. The two
Major findings are not about component quality but about **what reaches the
consumer**: (1) the DAO query-type decode table is shifted across nearly the
whole enum, so every crosstab in shipped output is labeled `MakeTable` and
classified as a possibly-unused action query — proven end-to-end in the
committed HomeHealthCare golden; and (2) the entire Phase 6.3–6.8 enrichment
substrate (navigation graph, unresolved-item aggregation, event-handler /
validation-rule classification, row-source specs) is built and hermetically
tested but has **zero production call sites** — which is the proven root cause
of the Round 2 consumer feedback items R2-1/R2-2 and makes the interpretation
guide's §6 (`unresolved-items.json`) describe a surface that is always empty.

Every finding carries an explicit **Status** label per the spec's
Evidence-Over-Inference rule: `proven` (a named probe/source check establishes
the claim), `partially proven` (mechanism proven from source; a stated impact
premise remains inferred), or `inferred — not verified`. Where a claim rests on
a full in-session read of a file, that is stated.

## Findings

### F-2-1: DAO query-type decode table is shifted — crosstabs ship as "MakeTable" and classify as possibly-unused action queries
- **Severity:** Major
- **Status:** proven (golden-output probe + source pointers below; the DAO
  ground truth is the documented `QueryDefTypeEnum` in Microsoft's DAO
  reference, corroborated empirically by the TRANSFORM probe)
- **Area:** AccessHarvester.Core — `Extraction/QueryExporter.cs:62-81`
  (`QueryTypeName`); AccessHarvester.Pipeline —
  `UsageIdentification/UsageClassifier.cs:14-23` (type constants)
- **Evidence:** `QueryExporter` reads `qd.Type` raw from DAO
  (`QueryExporter.cs:37`) and decodes it with a switch claiming to be the "DAO
  dbQueryType enumeration": `16 => "MakeTable", 32 => "Append", 48 => "Update",
  64 => "Delete", 80 => "Union", 96 => "Xbase", 112 => "CrossTab",
  128 => "DataDefinition", 144 => "SqlPassthrough", …`. The documented DAO
  `QueryDefTypeEnum` is `16 = dbQCrosstab, 32 = dbQDelete, 48 = dbQUpdate,
  64 = dbQAppend, 80 = dbQMakeTable, 96 = dbQDDL, 112 = dbQSQLPassThrough,
  128 = dbQSetOperation (union), 144 = dbQSPTBulk, 160 = dbQCompound,
  224 = dbQProcedure` — only Select (0) and Update (48) agree, and the
  `176/192/208/224 => "Sp*"` arms decode values that do not exist in the DAO
  enum. **Proven by golden probe (2026-06-12):** the four committed
  HomeHealthCare crosstab queries (`qxtClientServiceCosts`,
  `qxtClientServiceHours`, `qxtProviderServiceCosts`,
  `qxtProviderServiceHours`) all have `Sql` beginning with `TRANSFORM` — the
  Jet crosstab syntax — and all carry `QueryTypeCode: 16` labeled
  `QueryType: "MakeTable"` in `ir/Queries/*.ir.json`. **End-to-end:** in
  `samples/GoldenOutput/HomeHealthCare/usage/object-use.json` all four classify
  as `PossiblyUnused` 0.65 (the Q9 action-query branch — `UsageClassifier`'s
  `TypeMakeTable = 16` puts them in the action-query set) instead of the
  strategy's Q1 `Reporting` 0.70 with the `CrosstabQuery` question.
- **Problem:** Every customer database's query-type surface is mislabeled in
  raw/kb/ir, and the usage classifier routes most non-Select types through the
  wrong rule family: real crosstabs (16) → action-query branch (Q8/Q9 —
  `StandaloneUtility`/`PossiblyUnused`); real make-tables (80) → the
  Select/Union path (can classify `Reporting`); real pass-throughs (112) → Q1
  `Reporting` plus a bogus `CrosstabQuery` question; real unions (128) → Q11
  `StandaloneUtility` "InfraQuery"; real DDL (96) → the Select/Union path.
  Delete (32) and Append (64) land in the action branch by luck but with
  swapped labels. A downstream migration consumer triaging "possibly unused"
  objects will be told reporting crosstabs are cleanup candidates. The hermetic
  suite cannot catch this: `UsageClassifierTests.Query_Q1_Crosstab_Is_Reporting`
  feeds `typeCode: 112` (`UsageClassifierTests.cs:196`) — the classifier's own
  wrong constant — so the tests pin internal consistency, not DAO reality.
  The strategy doc §4 lists a third, also-incorrect set
  (`1 = Crosstab, 2 = Delete, …`), so no written artifact currently records
  the true enum.
- **Recommendation:** Correct `QueryTypeName` to the documented DAO values;
  align `UsageClassifier`'s constants (or better, share one decode table);
  update the strategy doc §4 table; regenerate the HomeHealthCare goldens; add
  a hermetic pin test asserting the decode for each documented DAO value (the
  test that would have caught this). One session, mechanical; the golden churn
  is the bulk of the diff.

### F-2-2: The Phase 6.3–6.8 enrichment substrate has zero production call sites — navigation graph, unresolved items, and handler/validation classifications never reach output
- **Severity:** Major
- **Status:** proven (call-site greps + golden/manifest probes below; all
  cited files read in-session)
- **Area:** AccessHarvester.Pipeline — `Enrichment/NavigationGraphBuilder.cs`,
  `Enrichment/UnresolvedItemAggregator.cs`,
  `Enrichment/EnrichmentPipelineRunner.cs` (+ `EventHandlerClassifier`,
  `ValidationRuleExtractor`, `RowSourceSpecBuilder`); AccessHarvester.Core —
  `Pipeline.cs:399,429-439`
- **Evidence:** Repo-wide grep (2026-06-12): `NavigationGraphBuilder`,
  `UnresolvedItemAggregator`, and `EnrichmentPipelineRunner` are referenced
  only by their own definitions and by `tests/AccessHarvester.Pipeline.Tests`
  (`EndToEndFixtureTests.cs:74` exercises the full wiring — in a test).
  `EventHandlerClassifier` / `ValidationRuleExtractor` / `RowSourceSpecBuilder`
  appear nowhere in `AccessHarvester.Core` or `AccessHarvester.Cli`.
  `Pipeline.cs:399` hardcodes
  `IReadOnlyList<UnresolvedItem> unresolvedItems = Array.Empty<UnresolvedItem>()`
  and the `DatabaseManifestAggregator.Build` call (`Pipeline.cs:429-439`)
  never passes `navigationGraph`. **Golden probes:** `manifest.json` has no
  `NavigationGraph` key (null omitted); `unresolved-items.json` is `[]`;
  every manifest `Forms[].Spec.EventHandlers[]` record lacks a
  `Classification` field and `ValidationRules[]` records carry references but
  no rule content. Meanwhile `docs/harvester/interpretation-guide.md` §6
  documents `unresolved-items.json` in detail — including a
  `NavigationTarget` producer source — and teaches the consumer to consult it.
- **Problem:** This is the proven root cause of the Round 2 consumer feedback:
  R2-1 (no switchboard navigation edges) and R2-2 (no dialog→query→report
  chain edges) ask for exactly the surfaces this substrate produces, and the
  models even anticipate the inputs (`NavigationGraphInputs.Switchboard` /
  `SwitchboardStructure` map `Switchboard Items` rows; `RibbonStructure`,
  `MacroChain` likewise) — but nothing produces those inputs and nothing calls
  the builder. Three consequences: (a) consumer-visible value that was built
  and tested across multiple session sets is not delivered; (b) the
  interpretation guide documents a permanently-empty contract surface
  (extends Session 1's O-1-1 with the root cause); (c) the hermetic
  end-to-end test gives false confidence — it validates wiring the product
  does not have.
- **Recommendation:** A dedicated wiring session set (Session 4 should rank
  it; it is the natural vehicle for R2-1/R2-2). The seam already exists:
  `Pipeline.cs:348` builds `resolvedModules` + `crossRefs` for
  `FunctionClassificationBuilder` — the same inputs
  `NavigationGraphBuilder.Build` takes; `UnresolvedItemAggregator.Build`
  accepts the resolver diagnostics, navigation graph, and function
  classifications already in scope there. R2-1 additionally needs a
  `Switchboard Items` table reader to populate
  `NavigationGraphInputs.Switchboard` (rows are in the harvested table data;
  no new COM surface). Until wired, the lean interim fix is one paragraph in
  the interpretation guide §6 stating the array is reserved and currently
  always empty.

### F-2-3: Usage and cleanup JSON outputs are written with a UTF-8 BOM, unlike every other output surface
- **Severity:** Minor
- **Status:** proven (byte probe + source pointers; consumer impact
  demonstrated in-session)
- **Area:** AccessHarvester.Pipeline —
  `UsageIdentification/UsageChecklistWriter.cs:53,94`,
  `Cleanup/CleanupChecklistWriter.cs:48,119`
- **Evidence:** Byte probe over `samples/GoldenOutput/HomeHealthCare/`
  (2026-06-12): exactly 4 of 182 output JSON files begin with `EF BB BF` —
  `object-use.json`, `object-use-checklist.json`, `object-cleanup.json`,
  `object-cleanup-checklist.json`. The four cited write calls pass
  `System.Text.Encoding.UTF8` (BOM-emitting) to `File.WriteAllText`; the
  sanitization writers in the same project already use the correct
  `new UTF8Encoding(false)` (`SanitizationRunner.cs:40`,
  `MaskingEngine.cs:157`), and Core's `Pipeline.cs` uses the default
  (BOM-less) overload. Demonstrated impact: Python's strict `json.load`
  rejects the files (`Unexpected UTF-8 BOM`) — hit live during this session's
  probing.
- **Problem:** RFC 8259 §8.1 forbids adding a BOM to JSON. The downstream
  consumer is AI tooling that commonly shells out to strict parsers; two of
  the repo's primary consumer-facing artifacts (the checklists) and both
  intermediate findings files require BOM-tolerant decoding while the other
  178 files do not. Cross-surface inconsistency is dimension-7 drift.
- **Recommendation:** Pass `new UTF8Encoding(false)` (or omit the encoding
  argument) at the four call sites; regenerate the four goldens. A read-back
  test using a BOM-rejecting parse (e.g. assert first byte is `{`/`[`) pins it.

### F-2-4: Strategy rule Q6 ("query references no tables") is dead code — the helper always returns false
- **Severity:** Minor
- **Status:** proven (source-only: the cited method body unconditionally
  returns false on every reachable path; downstream gating verified by grep)
- **Area:** AccessHarvester.Pipeline —
  `UsageIdentification/UsageClassifier.cs:911-920` (`QueryRefsNoTables`),
  `:354-359` (the Q6 branch), `QuestionGenerator.cs:95-106`
- **Evidence:** `QueryRefsNoTables` returns false when SQL targets are absent,
  false when the target set is empty, and — per its own comment ("We can't
  distinguish here without a full object catalog; for now fire when empty SQL
  refs") — false in the remaining case. The Q6 branch
  (`category = Unknown; evidence.Add("references-no-tables")`) is therefore
  unreachable, and `QuestionGenerator` gates the `QueryReferencesNoTables`
  question on that same evidence string (`QuestionGenerator.cs:100`), so the
  question is unreachable too. No test asserts Q6 fires (794 tests green with
  the rule dead).
- **Problem:** The strategy doc §4 declares Q6 as a shipped rule and §6
  defines its clarifying question; the implementation silently ships neither.
  A query layered purely over other queries — the strategy's "intermediate
  computation step" case — classifies as ordinary `Reporting`/`PossiblyUnused`
  with no flag. Silent divergence from the authoritative strategy doc is the
  exact failure class the doc's authority note forbids ("divergences require
  human approval").
- **Recommendation:** The classifier already has what it needs: a FROM/JOIN
  target set per query (`QuerySqlTargetsByQuery`) and the known-query-name set
  (`KnownQueryNames`) — Q6 is `targets.Count > 0 && targets.All(KnownQueryNames.Contains)`.
  Implement plus one rule test; or, if the rule is deliberately abandoned,
  strike it from the strategy doc with a note. Either closes the drift.

### F-2-5: Tables referenced only via an inline-SQL RecordSource earn no referenced-ness credit and can classify PossiblyUnused
- **Severity:** Minor
- **Status:** partially proven — the no-credit mechanism is proven from
  source (pointers below; the code path is unambiguous); the end-of-chain
  `PossiblyUnused` misclassification is **inferred — not verified**
  (mechanically derived from the proven mechanism plus table rule T6, but no
  end-to-end repro was run: this read-only set cannot add the hermetic
  fixture that would demonstrate it)
- **Area:** AccessHarvester.Pipeline —
  `UsageIdentification/RecordSourceCollector.cs:28-34`,
  `UsageIdentification/SignalIndex.cs:113-126,206-230`
- **Evidence:** `RecordSourceCollector.AddIfNamedSource` discards any
  RecordSource that `SqlDetection.IsInlineSql` matches, so inline-SQL bindings
  contribute nothing to `RecordSourceByObject` (and hence
  `UsedAsRecordSource`). `SignalIndex.Build`'s phase-2 inline-SQL scan
  (`:113-126`) extracts FROM/JOIN names but filters them through
  `knownQueryNames` (`ExtractQueryNamesFromSql:220` — deliberately, per its
  comment, to avoid table-name false positives), so only *queries* named in
  inline SQL are credited. A table bound only as
  `SELECT * FROM tblFoo WHERE …` on a form is absent from every referenced-ness
  set; if it also has no FK edges, no saved-query references, and no VBA
  references, `IsCompletelyUnreferenced` (`SignalIndex.cs:318-325`) returns
  true and table rule T6 classifies it `PossiblyUnused` 0.70.
- **Problem:** Inline-SQL RecordSources naming bare tables are routine in
  Access apps. The inferred consequence (per the Status split above): the
  consumer checklist asks "is this genuinely obsolete?" about a table that is
  the record source of a live form. (Cleanup output is unaffected —
  cross-reference absence was retired as a cleanup signal on 2026-06-02.)
  HomeHealthCare's unbound-form/ADODB style means the committed golden does
  not exhibit the pattern, which is why it has not been seen.
- **Recommendation:** In the phase-2 scan, credit *all* extracted FROM/JOIN
  names into `UsedAsRecordSource` (and the form/report-specific sets when the
  name is a known query), restricting to names that exist in the IR object
  catalog (`db.Tables` ∪ `db.Queries`) instead of queries only — the catalog
  is already in scope in `Build`. One hermetic test with an inline-SQL-bound
  table pins it.

### F-2-6: Label provenance casing in shipped output contradicts the documented convention
- **Severity:** Minor
- **Status:** proven (golden grep + source pointers)
- **Area:** AccessHarvester.Core — `Enrichment/LabelResolver.cs:132-157,366-371`;
  `docs/planning/project-guidance.md` → Conventions → Output JSON Conventions
- **Evidence:** The convention documents
  `Label: { source: "attached", text: "First Name" }` and
  `Label: { source: "unresolved" }` (lowercase). The resolver assigns
  `"Attached"`, `"Proximity"`, `"FromName"`, `"FromBinding"`, `"Unresolved"`
  (PascalCase — `LabelResolver.cs:132,143,151,157`), and the committed goldens
  carry the PascalCase forms (grep 2026-06-12: 34 × `"source": "Attached"`,
  14 × `"FromName"`, 3 × `"Proximity"` across `ir/Forms/*.ir.json`).
- **Problem:** Dimension-7 drift between the documented contract and the
  emitter. A consumer coding to the convention doc string-matches the wrong
  casing; provenance `source` values are exactly the kind of closed-set token
  a downstream AI will be prompted with verbatim.
- **Recommendation:** Align the cheaper side: update the convention text (and
  any guide examples) to the shipped PascalCase tokens, since changing the
  emitter churns every golden and any consumer already coded to reality. State
  the closed set explicitly wherever the `Label` shape is documented.

### F-2-7: data.sql interpolates numeric/bit CSV tokens unvalidated and unquoted
- **Severity:** Minor
- **Status:** partially proven — the bare emission path is proven from source
  (pointers below, files read in full); the hostile/stale-CSV premise that
  turns it into malformed or injected SQL is **inferred — not verified** (no
  probe constructed; bounded by Access's own column typing in the normal
  workflow)
- **Area:** AccessHarvester.Pipeline — `Migration/SqlValueFormatter.cs:38-52`,
  `Migration/SqlServerDataLoadGenerator.cs:134-140`
- **Evidence:** `SqlValueFormatter.Format` quotes-and-doubles text/date/GUID
  values, but the numeric-family default arm emits `raw` verbatim and
  unquoted, and `FormatBit`'s fallback passes any non-0/1 token through
  unquoted. `SqlServerDataLoadGenerator.EmitTable` performs no token
  validation between `CsvTableReader` and `Format` (`:134-140`).
- **Problem:** The generated `data.sql` is executed by the consumer. In the
  documented workflow the CSV comes from this repo's own `sanitize` step and a
  numeric Access column can only hold numeric values — but `generate-migration
  --data` consumes whatever CSV is on disk: a stale CSV from a pre-rename
  schema, a hand-edited cell, or a column-type drift between classification
  and CSV puts arbitrary text bare into an INSERT. Today that ships with exit
  0/3 and no degradation. The repo's own standard elsewhere (the
  `OleDbValueSampler` identifier-escaping confirmation in Session 1, and this
  project's `DefaultValueTranslator`) is validate-or-degrade, never pass-through.
- **Recommendation:** Validate numeric tokens against an invariant pattern
  (`^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?$`) and bit tokens against the known set;
  on mismatch emit `NULL` plus a `MigrationDegradation` (the established
  pattern) rather than the raw token. A few lines plus one test.

## Observations (recorded, below the finding bar)

> Evidence basis: source inspection of the named files, read in-session;
> file:line pointers are the verification trail. No probes unless stated.

- **O-2-1 — BrowseTo dispatches are systematically unresolvable (known,
  pinned).** `NavigationGraphBuilder` reads positional[0] as the target name
  for `BrowseTo`, but the real signature's first argument is the object-type
  enum — so every well-formed `DoCmd.BrowseTo acBrowseToForm, "frmFoo"` lands
  in `UnresolvedTargets` with the semantically wrong reason
  `non-literal-target`. The limitation is explicitly pinned
  (`NavigationGraphBuilderTests.cs:307-323`), i.e. explicit-not-silent —
  recorded because it becomes consumer-visible the moment F-2-2's wiring
  lands, and the fix (read positional[1]; map `acBrowseTo*` to TargetKind) is
  small enough to ride along.
- **O-2-2 — Geometry/Label parse-culture inconsistency.**
  `GeometryEnricher.TryGetInches` uses culture-sensitive `long.TryParse`
  (`GeometryEnricher.cs:82`) while `LabelResolver.TryGetTwips` correctly uses
  `InvariantCulture` (`LabelResolver.cs:306`). Same folder, same input data.
  Align to invariant when next touched (CA1305 class; practical risk low for
  ASCII integer twips).
- **O-2-3 — Confidence "corroboration" bonus counts the deciding signal.**
  `AdjustConfidence` (`UsageClassifier.cs:589-607`) adds +0.05 per
  RecordSource/nav-edge/VBA signal including the one that decided the rule
  (strategy says "*additional* corroborating signal"), and queries always pass
  `false,false,false` so they can never earn corroboration. Deterministic and
  bounded (+0.15 max); a calibration nit to fold into any classifier rework.
- **O-2-4 — Table/module/macro rule drift vs the strategy doc.** T5 in code
  lacks the strategy's "No FK relationships" guard (a FK-child table >4
  columns with VBA references classifies `StandaloneUtility` via T5);
  T4b (`used-as-RecordSource` + FK-child → MainDataEntry 0.60) and the
  module/macro classifiers exist in code but not in the strategy's rule
  tables; the strategy has no fallback rule while code falls to
  `Unknown 0.30`. Same authority-drift class as F-2-4 but behaviour-plausible
  in each case — reconcile the doc in one pass.
- **O-2-5 — Data-load silently NULL-pads short CSV rows and accepts duplicate
  headers last-wins.** `SqlServerDataLoadGenerator.cs:136-137` (`CsvIndex <
  row.Count ? … : null`) and `:93-95` (comment "last-wins on dup header").
  Both are degenerate-producer cases; a `MigrationDegradation` on first
  occurrence would make them honest.
- **O-2-6 — Cleanup analyser has no zero-date guard (context for the known
  `identify-cleanup` zero-date misfire).** `CleanupAnalyser.cs:86-87` computes
  `days` from whatever maximum the sampler returns; the known COM misread of
  time-only DateTime columns as OLE zero date `1899-12-30` therefore flows
  straight into a ~46176-day Medium/High finding. Session 1's O-1-5 adds two
  adjacent inputs in the COM sampler (`Core/Com/AccessDateMaximaSampler.cs`):
  local wall-clock stamped as UTC, and a silent per-column `catch → null`
  with no degradation record. Lean fix options for Session 4's ranking:
  (a) skip time-only columns at the sampler (root cause), and/or (b) treat
  `MaxValue <= 1899-12-30` as no-signal in the analyser (cheap guard), plus a
  degradation note for swallowed per-column failures.
- **O-2-7 — Inline-SQL FROM-scan limitations.** `ExtractQueryNamesFromSql`
  (`SignalIndex.cs:208-230`) takes only the first name after FROM (comma lists
  lose the second name), cannot see bracketed names containing spaces (token
  split on space), and contains a dead `IndexOf(' ')` branch (split already
  removed spaces). Bounded by F-2-5's broader fix.
- **O-2-8 — `generatedAt` non-determinism is by design.** `CleanupAnalyser`
  and the usage writer stamp `DateTime.UtcNow` per their schemas; recorded for
  the determinism dimension as documented exceptions, no action.

## Test posture (spec step 3)

`AccessHarvester.Pipeline.Tests`: 794/794 green hermetically (run this
session, sequential, no skips). Breadth is genuinely good: every classifier
rule family has direct tests (including the launcher/parameter-dialog
threading discipline), the migration generators are pinned by golden-script
tests plus a real-writer CSV round-trip, the navigation builder's
missing-argument hole semantics and determinism sorts are pinned, and the
sanitization FK-consistency invariant has its own suite. Three structural
gaps, each tied to a finding above:

1. **The query-type decode is pinned nowhere against ground truth.** The
   classifier tests feed the classifier's own constants (`typeCode: 112` for
   crosstab — `UsageClassifierTests.cs:196`), so the F-2-1 shift is invisible
   by construction. A table-driven decode test against the documented DAO
   values is the missing pin.
2. **Substrate tests validate wiring the product does not have.**
   `EndToEndFixtureTests.cs:74` runs `NavigationGraphBuilder` +
   `UnresolvedItemAggregator` + `DatabaseManifestAggregator` end-to-end — in
   the test project only (F-2-2). Green here reads as "the manifest carries a
   navigation graph," which is false for every shipped run. Once wiring lands,
   an integration-tier assertion that `manifest.NavigationGraph` is non-null
   for a fixture with macros closes the gap.
3. **No output read-back through a strict parser.** The four BOM-carrying
   files (F-2-3) ship through writer tests that re-read with BOM-tolerant
   .NET APIs; a first-byte or strict-parse assertion on the writer outputs
   would have caught the BOM at introduction.

## Positive confirmations (claims checked and found sound)

> Evidence basis: full in-session reads of the named files; probes as stated.

- **Migration untrusted-input discipline (dimension 6) is largely exemplary:**
  `SqlServerDdlEmitter.Quote` bracket-quotes every identifier and doubles
  embedded `]` (`:126`); `DefaultValueTranslator` is a strict allowlist that
  refuses locale-ambiguous `#1/2/2020#` literals and arbitrary expressions
  with an `UntranslatedDefault` degradation rather than guessing;
  `SqlServerTypeMapper` flags assumed decimal precision and refuses
  multi-value/Attachment columns honestly; binary columns are omitted from
  INSERT with a degradation, never fabricated. F-2-7 is the one seam in an
  otherwise consistent validate-or-degrade posture.
- **Determinism discipline:** `NavigationGraphBuilder` sorts edges and
  unresolved targets on a five-key comparator with a documented closed reason
  set; `SqlServerDdlEmitter`/`DataLoadGenerator` emit fixed-LF, timestamp-free
  scripts; `UnresolvedItemAggregator` documents and enforces a global sort.
- **Explicit-unresolved contract in the navigation substrate:** non-literal
  and missing targets produce provenance-bearing `UnresolvedNavigationTarget`
  records (never silently dropped); `DoCmd.Close` with no name args is
  deliberately not an edge, with the rationale in-source.
- **Threaded-flag emission discipline (the set-001 lesson) held:**
  `UsageClassifier.BuildSignals`/`BuildSummary` key launcher and
  parameter-dialog output off the classifier's threaded flags
  (`launcherDataTable`, `parameterDialogForm`, `paramEvidence`), not name
  recomputation; the bound-form guard in the `LauncherSurface` signal mirrors
  F5b exactly (`UsageClassifier.cs:799-830`).
- **PropertyDecoder leaves unknown `"NotDefault"` markers in place** as a
  load-bearing unknown (with `NonDefaultProperties` preserving the
  deviated-from-default signal) — explicit-over-silence done right.
- **LabelResolver's proximity round is conservative by design:** the 2×
  uniqueness rule vetoes on *all* eligible controls (including already-labeled
  ones), labels are consumed at most once, and parsing is invariant-culture.
- **CsvTableReader's NULL/empty-string collapse is documented and pinned** by
  a real-writer round-trip test (the "producer caveat" remark), so the honest
  NULL choice cannot regress silently.

## Not reviewed this session (stop-rule remainder)

Recorded per the spec's "do not silently thin the review" rule:

- `Sanitization/*` beyond the CSV writer/reader contract and a masking skim
  (the FK-remap consistency engine has its own invariant test suite).
- `Classification/*` beyond a `HeuristicsEngine` structural skim — the PII
  heuristics, `ValueFormatDetector`, and `ScanConfig` deserve a focused pass
  if Session 4 ranks classification work.
- `Manifest/DatabaseManifestAggregator` internals, `ParserOutputProjector`,
  `QuerySqlProjector`; `DocGen/SchemaDocGenerator`; `Validation/SchemaValidator`
  (Session 1 reviewed its caching convention at the guidance level).
- `QuestionGenerator` beyond the Q6 gating check; `UsageChecklistWriter`
  beyond encoding; `IrLoader` (read for parameter extraction context only).
- Core VBA/macro enrichers: `MacroActionsEnricher`, `MacroXmlParser`,
  `VbaSignatureEnricher`/`Parser`, `TextCapacityEnricher`,
  `FormComponentSpecBuilder`/`QueryAbstractionBuilder`/`RowSourceSpecBuilder`
  internals, `EventHandlerClassifier` internals.
- `Cleanup/ComDateMaximaProbe` (COM-gated; Session 4's COM run is the place
  to exercise it).

Session 4 should fold the Classification deep pass and the
ReportBaseline remainder from Session 1 into the same scoping decision.

## Known issues cited as context (not re-discovered)

- Integration suite red on clean checkout (7 tests depend on local-only
  `examples/northwind-output.json`) — owned by the standing
  `integration-test-rebase` recommendation.
- `identify-cleanup` zero-date misfire — folded into O-2-6 above with the
  analyser-side evidence and lean fix options, per the spec's instruction to
  rank rather than re-derive.

## Seam notes for Session 4 (R2-1 / R2-2 design sketches)

- The navigation-graph model is ready for R2-1: `SwitchboardStructure` /
  `SwitchboardPage` / `SwitchboardItem` already model `Switchboard Items` rows
  (command codes documented in-source), and `NavigationGraph.EntryPoints`
  orders AutoExec → default form → switchboard roots. What is missing is
  (a) a producer that reads the switchboard table rows out of harvested table
  data into `NavigationGraphInputs`, and (b) the F-2-2 wiring itself.
- For R2-2 (dialog → query → report chains): `NavigationEdge.WhereCondition` /
  `OpenArgs` already preserve raw dispatch payloads; `IrQuery.Parameters`
  carries declared/form-reference/report-reference parameter provenance
  (`IrLoader.ExtractQueryParameters` reuses the shared Access-side scan); and
  `UsageClassifier.BuildQueryParameterEvidence` shows the join pattern. The
  chain edges are derivable from existing surfaces; F-2-1's type-code fix
  should land first so report-feeding queries are typed correctly.
- `NavigationGraphBuilder.Build` accepts `CrossReferenceIndex` for
  reverse-edge enrichment but ignores it today (documented in-source) — the
  R2-2 sketch can use it without an API change.
