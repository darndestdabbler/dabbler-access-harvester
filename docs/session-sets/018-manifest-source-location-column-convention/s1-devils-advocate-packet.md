# S1 In-IDE Devil's-Advocate Packet — Set 018 (DW-26), PRIMARY review

> Run this with your in-IDE GPT + Gemini (full repo access). Per the spec this pair is
> the **primary** review on the convention choice and the golden delta. Routed
> verification (gpt-5-4) already ran (R1: one Minor comment overclaim, fixed) — your job
> is the adversarial second look the routed packet could not get, with the whole tree.

## Context
Set 018 closed **DW-26**: the VBA manifest emitted source-location **columns** 0-based
(AST/ANTLR) while the schema `SourceLocation` `$def` declares them 1-based (`minimum: 1`).
Lines were already 1-based. Decision: **Option A — normalise columns to 1-based at
projection** (`ParserOutputProjector.BuildLocation` now `+1`s both `StartColumn` and
`EndColumn`; half-open `[Start, End)` rebased, `EndColumn` stays EXCLUSIVE). Unanimous
cross-provider consensus + human sign-off. HHC golden regenerated in-commit.

## Files to read
- `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs` — `BuildLocation` (+1) and
  the synthetic top-level `StartColumn = 1`.
- `AccessHarvester.Vba.Parser/Ast/SourceRange.cs` / `SourceLocation.cs` — half-open
  `[Start,End)`, End exclusive; Column 0-based at the AST.
- `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs` — `UnresolvedLocation.Column`
  verbatim/mixed-base contract (the DW-27 surface).
- `docs/test-cases/harvester-schema.v1.1.json` — `SourceLocation` `$def` (minima
  UNCHANGED; only `description` annotations added).
- `tests/.../Manifest/ParserOutputProjectorTests.cs` — the new column-0 / width / leak pins.
- `tests/.../UnresolvedItemsCompletenessTests.cs` — `LayerA_HeaderRef...` re-pinned to
  `inline == index + 1`.
- `tests/.../Pipeline/StudentIctLocalInvariantTests.cs` — pin flipped to assert validity.
- `docs/planning/deferred-work.md` — DW-26 CLOSED banner + new **DW-27**.
- `samples/GoldenOutput/HomeHealthCare/manifest.json` — regenerated (every changed leaf a
  `+1` column shift; proof below).

## Golden delta proof (reproduce if you doubt it)
`git show HEAD:samples/GoldenOutput/HomeHealthCare/manifest.json` (before) vs the working
tree (after): a parallel JSON walk found **11882 differing leaves, ALL `StartColumn`/
`EndColumn`, 5941 + 5941, EVERY shift exactly +1, ZERO non-column/non-+1 violations, ZERO
top-level `Source/Location` diffs**. kb/ir/unresolved-items byte-neutral.

## Attack these specifically
1. **A missed 0-based leak**: is there ANY other code path that emits a schema-gated
   `SourceLocation` column besides `BuildLocation` and the hardcoded top-level `=1`?
   (Navigation graph? Chain edges? Ribbon entry points? Resolved-callee locations? SQL
   form references projected into the manifest?) If one exists and is still 0-based, the
   1-based contract is not actually uniform.
2. **EndColumn semantics**: is `+1` on an EXCLUSIVE end genuinely width-preserving for
   every token kind (multi-line spans where `End.Line != Start.Line`? zero-width spans?
   the synthetic top-level that has no `EndColumn`)? Any off-by-one or negative-width case?
3. **DW-27 boundary**: was it right to leave `unresolved-items.json` verbatim (0-based for
   resolver/SQL sources) while the manifest went 1-based? Is the `inline == index + 1`
   re-pin honest, or does it paper over a real consumer-facing inconsistency that should
   have been fixed now? Does the aggregator's dedup (which keys partly on column) still
   collapse body/header overlaps correctly now that the manifest inline column is 1-based
   but the index feed stays 0-based?
4. **The schema edit**: confirm the added `description` annotations cannot alter validation
   and that no committed copy of the schema (embedded resource vs file) drifted.
5. **Consumer impact**: any downstream artifact, doc, or example that still assumes 0-based
   columns and would now be wrong (the interpretation guide, examples/, READ-ME files)?

Report findings by severity with file:line and a concrete fix. Default to flagging a
plausible issue over staying silent.
