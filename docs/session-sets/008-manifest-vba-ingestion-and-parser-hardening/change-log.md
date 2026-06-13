# Change Log — 008-manifest-vba-ingestion-and-parser-hardening

**Status:** COMPLETE — 3 of 3 sessions, all VERIFIED.
**Arc:** 006's P1a — the first product-fix set of the review arc, unblocked by
007's green integration baseline. Makes the manifest's parsed-VBA layer real for
form/report code-behind, stops legal legacy VBA from silently costing modules,
and surfaces the diagnostics/reasons the parser stack already produces.

## What shipped, in consumer terms

1. **Form/report code-behind is now visible in `ParserOutput`** (F-3-1, the
   set's one Critical). Session 1 re-sourced form/report VBA from the kb tier's
   `VbaCode` (VBE-header-stripped) instead of feeding the whole SaveAsText
   layout DSL to the parser, which had silently produced zero items for every
   form/report. Degraded parses now emit `VBA_PARSE_DEGRADED` (mode + diagnostic
   count) / `VBA_PARSE_FAILURE` into the manifest `Diagnostics` instead of being
   discarded at the call site.

2. **Legal legacy VBA stops killing modules** (F-3-2 / F-3-3, Session 2).
   Grammar accepts `TYPED_IDENTIFIER` in function/property-get name position and
   the safely-contextual keywords (`STEP`, `COMPARE`, `EXPLICIT`, `PRESERVE`,
   `LIB`, `ALIAS` — not `ERROR`); the AstBuilder guards module items into
   recovered stubs instead of NRE / whole-module loss; type-declaration suffixes
   are stripped into `TypeSuffix` at every declaration site (and the
   function/property-get return type is derived from the suffix — review H4);
   `SymbolDbBuilder` Build Pass 2 gained a per-module catch so one bad module
   degrades that module, not the whole build. New corpus case 14.

3. **Diagnostics and unresolved reasons reach the consumer** (F-3-6, Session 3).
   Unresolved references in `ParserOutput` now carry `ResolutionReason` (the
   resolver's append-only closed reason set, previously erased at the projection
   layer); query-SQL parse failures emit `QUERY_SQL_PARSE_FAILURE` instead of
   silently downgrading to Raw-only. The ParserOutput sub-document `SchemaVersion`
   bumped 1.0 → 1.1 (a free pattern string in the schema, so the manifest
   envelope stays v1.1 with no ~15-file churn); `ResolutionReason` is documented
   additively on the shared `ResolvedReference` `$def` and the diagnostic codes
   + inline-reason are documented in the interpretation guide.

4. **The conformance-contract question is decided and recorded** (F-3-9,
   Session 3, by unanimous high-confidence cross-provider consensus — Option B).
   `project-guidance.md` and the test-cases README now state the *actually
   enforced* two-layer contract — schema-shape conformance
   (`TestCaseFixtureConformanceTests`) + targeted behavioral tests + the
   HomeHealthCare byte golden — rather than the aspirational "the expected.json
   files are the contract, validated by re-running and diffing" language that
   was never implemented. The `<n>.expected.json` files are re-classified as
   design-intent/onboarding artifacts; the README case count is corrected
   (12 → 14) along with the stale v0 schema sentence. No conformance runner was
   built; if whole-corpus pinning is ever wanted, the recorded guidance is to
   author a *separate* small golden suite in the real `DatabaseManifest` shape,
   not to retrofit the design-intent fixtures.

## Known, accepted intermediate state (disclosed, not a defect)

Once F-3-1 landed, form/report modules produce parsed items whose cross-module,
control, and `Me` references resolve `Unresolved` — the regenerated golden shows
**2306 `ResolutionReason` entries**. This is the deliberately-sequenced
post-F-3-1 "Unresolved wall": the resolver/navigation wiring (F-3-4) is **P3**
(`resolver-and-navigation-wiring`), which ships after this set. The reasons make
the wall honest and triageable; they are not a regression.

## Validation

- **Hermetic:** build clean (0 errors). Grammar/Parser/Resolver/SymbolDb green
  across sessions; Session 3 — `AccessHarvester.Pipeline.Tests` 798/798,
  `ManifestVbaIngestionTests` 6/6 (new query-diagnostic + ResolutionReason pins).
- **Golden:** HomeHealthCare-only, one regeneration per session. Session 3's
  regen changed `manifest.json` only (kb/ir byte-stable): all 34 ParserOutput
  sub-docs → 1.1, 0 left at 1.0, 2306 `ResolutionReason` (the disclosed wall).
- **Integration (COM, TRX `<Counters>`):** total=61, passed=58, failed=0; 3
  skipped (2 guarded golden regenerators + the quarantined sanitize test, which
  stays quarantined per 007).
- **Cross-provider verification:** Session 1 VERIFIED R1; Session 2 VERIFIED R1
  (after a non-routed in-IDE devil's-advocate interim review caught H2/H3/H4);
  Session 3 VERIFIED R2 — R1 raised one Major that was a context/evidence gap
  (the per-module diagnostic fold is a Session-1 deliverable that R1's packet
  did not show), resolved by embedding the S1 code+test, no code change.

## Deferred work opened by this set

`docs/planning/deferred-work.md` carries **DW-1** (contextual-keyword slots
beyond variable-class: proc names / type & enum members / labels / declare),
**DW-2** (`.Line`/`.Circle` graphics-method coordinate syntax — the lone
`rptClientProfile` parse failure on the fixture), **DW-3** (variable
suffix→type emission, the F-3-5 class), and **DW-4** (`!`-suffixed
function/property-get names — BANG-vs-suffix lexer ambiguity). All are
parser-grammar-completeness candidates; none is consumer-blocking and none was
triggered this set.

## Next session set

**`009-output-encoding-correctness`** (006 P1b, 1 session) — authored with this
set (spec + not-started v4 stub). Fixes `SaveAsTextTranslator` reading cp1252
SaveAsText dumps as UTF-8 (F-1-1, Major — silent, irreversible U+FFFD corruption
of non-ASCII text in the kb/ and ir/ tiers the consumer reads) via
BOM-detect-then-cp1252, plus hermetic translator encoding pins (F-1-8).
Independent of this set; no shared golden churn. See
`next-session-set-recommendation.md` for the routed analysis and the P2→P5
ordering.
