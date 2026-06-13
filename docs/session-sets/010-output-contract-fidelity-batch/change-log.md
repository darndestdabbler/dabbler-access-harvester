# Change Log — 010-output-contract-fidelity-batch

**Set:** `010-output-contract-fidelity-batch` (006 P2) · **Sessions:** 1 of 2
(in progress) · **Orchestrator:** Claude (Opus 4.8) · **Verification:** gpt-5-4
(cross-provider), R1 ISSUES_FOUND → R2 VERIFIED

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
