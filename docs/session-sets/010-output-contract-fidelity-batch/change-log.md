# Change Log — 010-output-contract-fidelity-batch

**Set:** `010-output-contract-fidelity-batch` (006 P2) · **Sessions:** 2 of 2
(**complete**) · **Orchestrator:** Claude (Opus 4.8) · **Verification:** gpt-5-4
(cross-provider) — S1: R1 ISSUES_FOUND → R2 VERIFIED; S2: R1 ISSUES_FOUND →
R2 ISSUES_FOUND → R3 VERIFIED

> **Post-review follow-up (2026-06-13, golden-neutral).** A manual whole-set
> devil's-advocate review (in-IDE Gemini Pro + GPT-5.4) ran after closeout.
> GPT returned VERIFIED; Gemini raised 5 mostly-theoretical points. Two were
> real-but-latent and fixed as a follow-up (routed re-verify VERIFIED,
> `s2-postreview-verification.md`): **(1)** `QuerySqlProjector` no longer lets a
> generic `Select` AST shape override a more-specific DAO action type — a MakeTable
> (`SELECT … INTO …`, which parses as Select) would otherwise emit
> `QueryType:"MakeTable"` but `Sql.Parsed.QueryType:"Select"` (Transform→Crosstab /
> Union refinements still win); **(2)** the F-2-7 numeric regex broadened to accept
> valid T-SQL `.5` / `1.` while still rejecting injection and non-T-SQL forms.
> Both golden-neutral on HHC (no action queries; no such numeric tokens). The other
> three (Q6 Unknown-vs-PossiblyUnused — the documented strategy-doc design;
> F-2-5 subquery/secondary-join over-credit — a known precision limit of the
> 006-recommended regex extraction, no HHC over-fire; QueryExporter MSys-skip —
> per the F-1-7 recommendation, a practical no-op since Access exposes no
> MSys-prefixed queries) were dispositioned as accept-with-reason / not-a-defect.

## Session 1 of 2 — Query-Type Decode + ByVal Emission (F-2-1, F-3-7, F-3-5)

The two golden-affecting Majors plus the F-3-7 parser-reconcile Minor. Emission
changes are complete; the single coordinated HomeHealthCare golden regeneration
is **deferred to Session 2** per the set's regen discipline. Every shipped change
is pinned by a hermetic test.

### F-2-1 (Major) — DAO query-type decode table was shifted one slot

`QueryExporter.QueryTypeName` and the `UsageClassifier` type constants both
encoded a decode table shifted by one DAO slot, so crosstabs (DAO `dbQCrosstab`
= 16) decoded as `"MakeTable"` and the usage classifier routed them through the
action-query branch (`PossiblyUnused` 0.65, Q9) instead of `Reporting` 0.70
(Q1). Every non-Select query family was mislabeled; the hermetic suite pinned the
classifier's own wrong constant (`typeCode 112` for crosstab), so it could not
catch the shift.

**Re-grounded (2026-06-13):** the 4 committed HHC crosstabs carry
`QueryTypeCode:16 / QueryTypeName:"MakeTable"` in `ir/Queries/*.ir.json`. Bug
confirmed live.

**Fix:**
- New `AccessHarvester.Pipeline.UsageIdentification.DaoQueryTypeCodes` — the single
  source of truth for the DAO `QueryDefTypeEnum` codes (the 006 finding's "share
  one decode table"). `UsageClassifier` and `QuestionGenerator` now key off it
  (the shifted private constants and the duplicate name-recompute are gone — the
  emission-vs-decision drift the lessons-learned register warns about).
- `QueryExporter.QueryTypeName` (COM/raw tier) corrected to the real DAO enum
  (0 Select, 16 Crosstab, 32 Delete, 48 Update, 64 Append, 80 MakeTable, 96 DDL,
  112 SqlPassthrough, 128 Union/SetOperation, 144 SqlPassthroughBulk, 160
  Compound, 224 Procedure) and exposed `internal` for a decode pin.
- Strategy doc §4 decode table + Q1/Q8/Q9/Q10/Q11 conditions corrected (they had a
  third, also-wrong small-integer scheme).

### F-3-7 (Minor) — parser TRANSFORM result no longer discarded/re-stamped

`QuerySqlProjector` derived `Sql.Parsed.QueryType` from the (shifted) DAO decode,
discarding the Jet parser's structural knowledge. It now derives
`Sql.Parsed.QueryType` from the parsed AST `Shape` (Transform→Crosstab,
Select→Select, …), falling back to the DAO-derived type only on parse failure;
the DAO numeric code is retained as `QueryTypeCode` provenance. Part (b) of the
006 recommendation — projecting the embedded SELECT of a TRANSFORM — is deferred
as **DW-7**.

### F-3-5 (Major) — explicit `ByVal` now emitted via the schema-declared field

`ParserOutputProjector.BuildParameter` emitted only a bare `ByRef:true` boolean,
so explicit `ByVal` was bytewise indistinguishable from default ByRef. The schema
`Parameter` $def declares `PassingMechanism` (enum, documented as the D10
replacement for the "v0 byRef boolean"); the bare boolean was merely tolerated
(no `additionalProperties:false`), and a sibling emitter (`VbaSignatureEnricher`)
already used `PassingMechanism`. The projector now emits the schema-declared
`PassingMechanism` enum (`"ByVal"`/`"ByRef"`) for explicit pass modes and omits it
for Unspecified (omission convention → consumer reads default ByRef). The
undeclared `ByRef` boolean is retired.

**DW-3 decision:** the suffix→type emission for variable/const declarations was
evaluated and **explicitly deferred** (golden-neutral, zero corpus occurrences,
separate projection paths) — recorded in `docs/planning/deferred-work.md`.

## Tests

- New `QueryExporterDecodeTests` — pins all 12 DAO codes + unknown (the test that
  would have caught the shift).
- `UsageClassifierTests` / `QuestionGeneratorTests` — query-type codes corrected to
  DAO truth; new pins: crosstab(16)→Reporting, MakeTable(80)→action/PossiblyUnused,
  PassThrough(112) & SptBulk(144)→StandaloneUtility.
- `QuerySqlProjectorTests` — F-3-7 AST-derived `Sql.Parsed.QueryType` (TRANSFORM →
  Crosstab even when the DAO label disagrees; DAO fallback on parse failure).
- `ParserOutputProjectorTests` — F-3-5 `PassingMechanism` (ByVal/ByRef explicit;
  Unspecified omitted; old boolean retired).

`dotnet build` 0 errors. `AccessHarvester.Pipeline.Tests` 803/803;
`AccessHarvester.Tests` affected hermetic classes green (decode pin 13/13). COM
golden-regression deliberately not run (Session 2 owns the single coordinated
regen).

## Expected Session-2 golden deltas (to be regenerated + explained then)

- 4 crosstabs: `QueryType` & `Sql.Parsed.QueryType` `"MakeTable"`→`"Crosstab"`
  (raw `QueryTypeName` likewise), and `object-use.json` reclassification
  `PossiblyUnused`→`Reporting` (Q1) with the `CrosstabQuery` question.
- Procedure parameters: `"ByRef":true` → `"PassingMechanism":"ByRef"`; explicit
  ByVal params gain `"PassingMechanism":"ByVal"`.

## Verification

Cross-provider gpt-5-4. R1 ISSUES_FOUND ($0.164565): two Majors, both DAO code 144
(`dbQSPTBulk`) — the decode label collided with 112's and Q11 excluded 144 (an
internal inconsistency the session left). Both accepted and fixed (distinct
`SqlPassthroughBulk` label + map entry; Q11 includes 144; strategy doc + new
classifier pin). R2 VERIFIED ($0.124255), empty issues. Total ~$0.2888.

---

## Session 2 of 2 — BOM, Casing, Dead-Code, Docs + Coordinated Golden Regen

The Minors, the documentation ride-alongs, the single end-of-set golden
regeneration with COM confirmation, **and** an emergent encoding-regression fix
the regen surfaced. The F-2-4 (implement-or-strike) and F-2-5 (fix-now-or-defer)
forks were resolved by **cross-provider decision consensus** (gpt-5-4 + gemini-pro
both agreed: implement Q6, fix F-2-5 now — `s2-decision-consensus.md`).

### F-2-3 (Minor) — UTF-8 BOM stripped from the four usage/cleanup JSON outputs

`UsageChecklistWriter` and `CleanupChecklistWriter` passed
`System.Text.Encoding.UTF8` (BOM-emitting) to `File.WriteAllText` for
`object-use.json`, `object-use-checklist.json`, `object-cleanup.json`,
`object-cleanup-checklist.json` — the only 4 of 182 outputs with a BOM, which
RFC 8259 §8.1 forbids and strict parsers reject. Now use `new UTF8Encoding(false)`
(mirrors the sanitization writers). Pinned by read-back tests asserting first byte
is `{`/`[` and strict parse succeeds.

### F-2-4 (Minor) — strategy rule Q6 ("references no tables") was dead code

`QueryRefsNoTables` unconditionally returned `false`, so the documented Q6 rule
(Unknown 0.40 + the `QueryReferencesNoTables` question) and its question were
unreachable — a silent divergence from the authoritative strategy doc.
**Implemented** (decision-consensus): `targets.Count > 0 &&
targets.All(KnownQueryNames.Contains)`. Also wired the `references-no-tables`
marker into the `QueryTableReference` signal evidence (the gate the
`QuestionGenerator` reads — never emitted before) and suppressed the generic
OutOfBand pile-on when Q6 fires (mirroring the PossiblyUnused suppression).
**Golden-neutral on HHC** (no HHC query references only queries). One existing
test (`LauncherSurfaceTests.Query_Consumed_By_Another_Query`) pinned the OLD
dead-Q6 behavior; updated to the corrected behavior (a legitimate correction, not
a weakening — endorsed by the verifier).

### F-2-5 (Minor) — inline-SQL RecordSource tables now earn referenced-ness credit

The `SignalIndex` phase-2 inline-SQL scan credited only **query** names, so a
table bound only via an inline-SQL RecordSource (`SELECT * FROM tblFoo`) got no
`used-as-RecordSource` credit. **Fixed** (decision-consensus): credit all
catalog-resolved (`db.Tables ∪ db.Queries`) FROM/JOIN names into the broad
`UsedAsRecordSource` set; the form/report-specific sets stay query-only.
**Golden-affecting on HHC** (the live case): `tblServiceRateLimits` (bound via a
form's inline-SQL `FROM tblServiceRateLimits`) flips `StandaloneUtility 0.70` →
`MainDataEntry 0.70` (rule T4b) and gains a `RecordSource` signal.

### F-2-7 (Minor) — `data.sql` numeric/bit tokens validated, not interpolated bare

`SqlValueFormatter` emitted numeric-family and bit tokens unquoted into INSERT
VALUES, so a stale/hand-edited CSV token could inject malformed or arbitrary SQL.
Now validates numeric tokens against `^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?$` and bit
tokens against the known set; on mismatch emits `NULL` and the generator records
one `InvalidDataToken` degradation per column (the project's validate-or-degrade
posture). Blank/whitespace and binary tokens are explicitly **not** flagged
(verifier R1/R2 fixes). Golden-neutral (HHC data is clean).

### F-1-4 (Minor) — FORM_SPEC_MISSING diagnostic now emitted

`Pipeline.BuildFormOrReportExports`' header promised a diagnostic for a parsed
module with no matching ir spec, but emitted none (and early-returned when the
whole ir directory was absent). Now emits a sorted `FORM_SPEC_MISSING` warning for
the set-difference, via an `internal AddMissingSpecDiagnostics` helper (unit
tested incl. the missing-directory case — verifier R1 fix). Golden-neutral on HHC.

### F-1-7 (Minor) — one shared system-object-name predicate

`TableExporter`/`QueryExporter` used bare culture-sensitive
`StartsWith("MSys")`/`StartsWith("~")` (CA1310) while the schema reader used
explicit `StringComparison`. Added `SystemObjectNames.IsSystemObjectName` (MSys
OrdinalIgnoreCase, `~` Ordinal), used by both exporters. Golden-neutral.

### F-2-6 / O-1-2 / O-1-1 (docs)

- **F-2-6:** the convention docs showed lowercase `Label.source` tokens; the
  emitter ships a closed PascalCase set (`Attached`/`Proximity`/`FromName`/
  `FromBinding`/`Unresolved`, verified against the golden + `LabelResolver`).
  Reconciled `project-guidance.md`, `ir-schema-forms-reports.md`, and the
  interpretation guide (+ outline) to the emitter (cheaper side).
- **O-1-2:** documented the database name-form drift (manifest keeps `.accdb`;
  usage/cleanup strip it) as a join-key table in the interpretation guide.
- **O-1-1:** added the interim note that `unresolved-items.json` is always empty
  until P3 wires the aggregator.

### Encoding regression fix (Set-009 follow-on — beyond the 5 named findings)

The mandated golden regen surfaced a real correctness bug: Set 009's
**fixed-cp1252** SaveAsText decode mojibakes UTF-8-without-BOM output. Ground truth
from the raw bytes — `basResetFixture`'s section sign is stored as UTF-8 (`C2 A7`)
and decoded to `Â§` under 009's cp1252 read, where the pre-009 golden correctly had
`§`; meanwhile the em-dashes in `basUtilities`/`basDataOperations` are cp1252
(`0x97`) and 009 correctly fixes those (`U+FFFD`→`U+2014`). The corpus has **mixed
per-file encoding** and a single fixed decode is wrong for one side. Added a shared
`SaveAsTextDecoder` (honour BOM → strict UTF-8 → cp1252 fallback) adopted at the
two run-pipeline read sites (`SaveAsTextTranslator` + `Pipeline.cs` module read);
`RunParse` left as DW-5. Result: the golden is fully correct (`§` preserved, em-dashes
fixed). DW-5/DW-6 updated to note the shared helper now covers 2 of 3 sites.

## Golden deltas (HomeHealthCare; single coordinated regen; all explained)

- ir/kb `qxt*` (8 files): `QueryTypeName` `MakeTable`→`Crosstab` (F-2-1/F-3-7).
- manifest.json: 8 `QueryType` `MakeTable`→`Crosstab`, 5 `ByRef:true`→
  `PassingMechanism:ByRef`, 103 `PassingMechanism:ByVal` added, 100 `Type` lines
  repositioned net-zero (F-3-5). No count/diagnostic changes (F-1-4 golden-neutral).
- ir/kb `basDataOperations` + `basUtilities` (4 files): `U+FFFD`→`U+2014` em-dash
  (009 F-1-1 landing, preserved by the new decoder via the cp1252 fallback).
  `basResetFixture` unchanged — `§` preserved by the UTF-8 decode (no mojibake).
- usage/object-use.json + checklist: 4 crosstabs `PossiblyUnused 0.65`→
  `Reporting 0.70` (F-2-1/F-3-7); `tblServiceRateLimits`
  `StandaloneUtility`→`MainDataEntry` +RecordSource (F-2-5); BOM stripped (F-2-3).
  `generatedAt` preserved (volatile field).
- cleanup/object-cleanup.json + checklist: BOM stripped (F-2-3) only; `generatedAt`
  + `harvestDate` preserved (volatile/COM-touch fields).
- migration/: unchanged (F-2-7 golden-neutral on clean HHC data).

## Tests

- `UsageClassifierTests` — Q6 (references-only-queries → Unknown + question) +
  guard; F-2-5 inline-SQL table credit + catalog guard.
- `CsvTableReaderAndValueTests` — F-2-7 valid/invalid numeric & bit, blank/
  whitespace, binary; `SqlServerDataLoadGeneratorTests` — InvalidDataToken once
  per column.
- `UsageChecklistWriterTests` + new `CleanupChecklistWriterTests` — F-2-3 BOM
  read-back (first byte + strict parse).
- new `SystemObjectNamesTests` (F-1-7), `SaveAsTextDecoderTests` (encoding, 4
  cases), `MissingSpecDiagnosticsTests` (F-1-4, 3 cases).

`dotnet build` 0 errors. `AccessHarvester.Pipeline.Tests` 828/828;
`AccessHarvester.Tests` hermetic subset green. COM `CanonicalGoldenRegressionTests`
GREEN (gated slice: manifest/kb/ir/reports-baseline).

## Verification

Cross-provider gpt-5-4. R1 ISSUES_FOUND ($0.265877): Major (F-2-7 blank/whitespace
cells spuriously flagged) + Minor (F-1-4 missing-ir-dir early-return) — both
accepted and fixed + tests. R2 ISSUES_FOUND ($0.14801): Major
(`IsInvalidDataToken` binary-family hole) — accepted-with-clarification
(unreachable: binary columns omitted upstream) + defensive guard + test. R3
VERIFIED ($0.01444), empty issues. Decision-consensus ~$0.044. Total routed
~$0.47.

## Set complete

Both sessions VERIFIED; set 010 (006 P2) closed. Next set authored:
`011-resolver-and-navigation-wiring` (006 P3, 3 sessions) — wire F-3-4 (resolver)
+ F-2-2 (navigation/enrichment substrate), delivering consumer R2-1/R2-2.
