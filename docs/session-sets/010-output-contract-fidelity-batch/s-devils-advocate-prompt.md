# Devil's-Advocate Review Prompt — Set 010 (output-contract-fidelity-batch), whole set

> Paste this into a **path-aware** in-IDE reviewer (GitHub Copilot → Gemini Pro
> AND GPT-5.4, run separately for cross-provider divergence). The reviewer reads
> the repo at `HEAD` (commits `7c4b43d` + `ee89e0c`). This is **additive to**, not
> a replacement for, the routed cross-provider `session-verification` already run.

## Bias-cautions preamble

You are a skeptical reviewer. **Your null hypothesis is that this work is wrong.**
Do NOT be agreeably contrarian or theatrically negative — that produces low-value
noise. For each item below, a **specific contrarian hypothesis** is named: try
hard to *prove that specific hypothesis true* using the actual code and the
committed golden. If you cannot substantiate it after a genuine attempt, say so
plainly and move on — a steelman that fails is a real signal of correctness. Cite
file:line and concrete evidence (bytes, golden values, test assertions), never
vibes. Distinguish *proven defect* from *inferred risk* from *style preference*.

Two passes:
1. **Pass 1 (per-item):** attempt each named hypothesis below.
2. **Pass 2 (completeness critic):** what did the session NOT verify? Name any
   modality unrun, claim unproven, or golden delta unexplained. What would you
   check that no one checked?

## Context

Set 010 is 006's P2 "output contract fidelity." Two sessions:
- **S1** (verified): F-2-1 DAO query-type decode was shifted one slot (crosstabs
  decoded `MakeTable`, mis-classified `PossiblyUnused`); F-3-7 `Sql.Parsed.QueryType`
  now derives from the parsed AST shape; F-3-5 emit explicit `ByVal` via the
  schema-declared `PassingMechanism` enum (retiring a bare `ByRef` bool).
- **S2** (verified R1→R2→R3): the Minors (F-2-3 BOM, F-2-4 Q6, F-2-5 inline-SQL
  table credit, F-2-7 numeric/bit validation, F-1-4 diagnostic, F-1-7 system-name
  helper), the doc reconciles (F-2-6 / O-1-2 / O-1-1), a **single coordinated
  HomeHealthCare golden regen**, and an **emergent Set-009 encoding-regression
  fix** (mixed-encoding SaveAsText) that the regen surfaced.

Authoritative records to read first: `docs/session-sets/010-output-contract-fidelity-batch/change-log.md`,
the 006 findings (`docs/session-sets/006-.../s1,s2,s3-review-findings.md`), and
`docs/session-sets/010-.../s2-decision-consensus.md`.

## Per-item contrarian hypotheses to steelman

1. **F-2-1 / F-3-7 (decode + AST QueryType).** Hypothesis: the centralized
   `DaoQueryTypeCodes` (S1) still mis-maps at least one DAO `QueryDefTypeEnum`
   code, OR `QuerySqlProjector`'s AST-derived `Sql.Parsed.QueryType` disagrees
   with the DAO `QueryTypeCode` for some shape such that a consumer joining the
   two fields gets contradictory signals. Check the full code table against the
   DAO enum and the 4 HHC crosstabs in `samples/GoldenOutput/HomeHealthCare/ir/Queries/qxt*.ir.json`.

2. **F-3-5 (PassingMechanism).** Hypothesis: omitting `PassingMechanism` for
   `Unspecified` is wrong here — a consumer cannot now distinguish "Access wrote
   no pass-mode (default ByRef)" from "the projector dropped the field," because
   some path that *did* carry an explicit mode now omits it. Verify against
   `ParserOutputProjector.BuildParameter` and the 103 `ByVal` / 5 `ByRef` entries
   in the regenerated `manifest.json`.

3. **F-2-4 (Q6).** Hypothesis: implementing Q6 changed classification for some
   real query family beyond the intended "references only other queries," OR the
   OutOfBand-suppression I added now hides a legitimately-needed question for a
   non-Q6 query. Also: is `targets.Count > 0 && targets.All(KnownQueryNames)` the
   right predicate, or can a query with an unresolved/aliased FROM name slip
   through as "all queries"? See `UsageClassifier.QueryRefsNoTables`,
   `SignalIndex`, `QuestionGenerator`, and `LauncherSurfaceTests` (the updated
   assertion — is it a legitimate correction or a weakening?).

4. **F-2-5 (inline-SQL table credit).** Hypothesis: crediting all catalog FROM/JOIN
   names into `UsedAsRecordSource` over-credits — a table merely *mentioned* in an
   inline-SQL record source (e.g. in a subquery or a JOIN it doesn't drive) now
   reads as a primary record source, flipping its classification wrongly. Is the
   `tblServiceRateLimits` StandaloneUtility→MainDataEntry flip actually correct, or
   did T4b fire on a weak signal? Trace `SignalIndex.ExtractNamedTargetsFromSql`
   and the HHC form whose RecordSource names that table.

5. **F-2-7 (numeric/bit validation).** Hypothesis: the regex
   `^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?$` rejects a *legitimate* numeric value Access
   can emit (leading `.5`, `1.`, `1d2` D-exponent, locale forms), turning real data
   into NULL with a degradation. OR `IsInvalidDataToken` and `Format` still disagree
   on some cell class. Check `SqlValueFormatter` + the new tests; try adversarial
   tokens.

6. **F-1-4 / F-1-7.** Hypothesis: the `FORM_SPEC_MISSING` set-difference is keyed on
   the wrong name form (case / extension) so it false-positives or misses; OR the
   shared `SystemObjectNames` now over-skips a user object named `MSys…`/`~…` that
   the prior per-exporter logic kept. Check `AddMissingSpecDiagnostics`,
   `SystemObjectNames`, and the exporters.

7. **Encoding fix (highest-risk — scrutinize hardest).** Hypothesis: the
   `SaveAsTextDecoder` "BOM → strict UTF-8 → cp1252" heuristic mis-decodes a real
   cp1252 SaveAsText file whose bytes happen to form valid UTF-8 (e.g. a cp1252
   "Ã©" = `C3 A9`), silently corrupting consumer output — a *worse* failure than
   the mojibake it fixed, because it's now wrong on the common ANSI path, not a
   rare one. Quantify: scan the committed `samples/GoldenOutput/HomeHealthCare`
   and any local `.txt` SaveAsText output for byte sequences that are valid UTF-8
   but were authored cp1252. Also: is leaving `RunParse` on the old narrow decode
   (DW-5) a latent divergence that will bite a consumer? Is the static-init
   ordering and `ReadLines` equivalence actually correct under all inputs?

8. **Golden regen integrity.** Hypothesis: a golden delta is unexplained, or a
   delta that *should* have appeared did not (a fix that silently no-op'd). The
   change-log claims every delta maps to a named finding and that `basResetFixture`
   is unchanged (section-sign preserved). Independently diff the regenerated golden
   against `HEAD~2` and confirm each changed line maps to F-2-1/F-3-5/F-2-3/F-2-5
   or the 009 em-dash repair — and that nothing else moved. Confirm `migration/`
   and all Forms/Reports/Tables goldens are genuinely untouched.

9. **Decision-consensus soundness.** Hypothesis: the F-2-4-implement and
   F-2-5-fix-now decisions were wrong despite both providers agreeing — the
   agreement reflects a shared blind spot (e.g. both under-weighted the
   golden-churn / scope-discipline cost). Re-argue the *defer* side for each and
   judge whether the set should have deferred either.

## Completeness-critic pass

- Did the set weaken or delete any test to accommodate a behavioral change?
  (Look hard at the `LauncherSurfaceTests` edit and any assertion changes.)
- The COM gate (`CanonicalGoldenRegressionTests`) covers manifest/kb/ir/
  reports-baseline but NOT usage/cleanup/migration. Those slices were regenerated
  by hand-run CLI + `generatedAt`/`harvestDate` preservation. Is that preservation
  honest, or did it mask a real content change?
- What did NOT get re-grounded against a fresh tool run that should have been?
- Output the usual JSON verdict block (`VERIFIED` | `ISSUES_FOUND` + issues[]).
