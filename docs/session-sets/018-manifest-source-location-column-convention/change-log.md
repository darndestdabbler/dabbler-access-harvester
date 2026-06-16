# Change Log — Set 018: Manifest Source-Location Column Convention (DW-26)

**Tier:** Full (1/1) · **Verification mode:** out-of-band-or-none · **Date:** 2026-06-15 → 2026-06-16
**Orchestrator:** Claude Opus 4.8 (claude-code) · **Verdict:** VERIFIED

## What shipped

DW-26 closed via **Option A — normalise source-location columns to 1-based at projection**
(unanimous cross-provider consensus + human sign-off, recorded *before* any code change).

- **`ParserOutputProjector.BuildLocation`** now adds `+1` to **both** `StartColumn` and
  `EndColumn`. `SourceRange` is half-open `[Start, End)` with `EndColumn` exclusive, so an
  11-char token at 0-based `[0,11)` projects to 1-based `[1,12)` — **width preserved**,
  `EndColumn` stays exclusive. Lines were already 1-based and are untouched. The synthetic
  per-module top-level location was already hardcoded `StartColumn = 1` and is left as-is
  (now consistent). Schema **unchanged** — the producer was fixed to honour the existing
  `SourceLocation minimum: 1` contract.
- **HHC golden regenerated in the same commit.** A structured value-level diff of the
  committed-before vs regenerated-after manifest proved: **11,882 differing leaves, all
  `StartColumn`/`EndColumn`, 5,941 + 5,941, every shift exactly `+1`, zero non-column /
  non-`+1` violations, zero top-level `Source/Location` diffs** (the 32 synthetic roots
  stay at 1). `kb/`, `ir/`, and `unresolved-items.json` byte-neutral.
- **`StudentIctLocalInvariantTests` pin FLIPPED** from the DW-26 known-limitation
  (asserted schema-**invalid** on the `StartColumn [minimum]` class) to asserting the
  manifest **schema-validates** + a structural witness that no `StartColumn == 0` survives.
  Validated locally (COM): the StudentICT manifest now schema-validates.
- **New hermetic pins** (`ParserOutputProjectorTests`): a line-leading (AST column 0)
  token projects `StartColumn 1`, width preserved (`EndColumn 13`); and a leak-guard that
  no `StartColumn`/`EndColumn == 0` remains anywhere in a projected tree.
- **Docs:** schema `SourceLocation` `$def` + interpretation guide document 1-based columns
  and the **exclusive** `EndColumn`.
- **DW-26 closed** in `deferred-work.md`; **DW-27 recorded** (see below).

## DW-27 (new deferred item)
The `unresolved-items.json` index preserves each producer's column **verbatim**, and
**every** feed (resolver, SQL form-reference, navigation-graph, function-classification)
carries the parser's **0-based** ANTLR column — so the index is **uniformly 0-based**.
After DW-26 the manifest inline column is 1-based, so the index is a **uniform `+1`** behind
it for every reference (pinned by `LayerA_HeaderRef_IndexPositionCorrelatesWithInlineReference`
asserting `inline == index + 1`). Normalising the index is a uniform `+1` but
golden-affecting (out of DW-26's "manifest column base only" scope), deferred.

## Verification
- **R1 — routed `session-verification` (gpt-5-4):** ISSUES_FOUND → 1 Minor (a test comment
  overstated the leak-guard proof) → fixed (comment reworded). Endorsed correctness, the
  golden-delta proof, the DW-27 scope call, and test integrity.
- **R2 — in-IDE devil's-advocate pair (gpt-5-4 + gemini-3.1-pro), PRIMARY:** both UPHELD
  the core work (no missed leak, width-preserving `+1`, DW-27 boundary, schema edit inert).
  **1 Major (both): the DW-27 / guide / schema-description / pre-existing record comments
  mischaracterised the index as mixed-base** — the producers are all raw 0-based, so the
  index is uniformly 0-based. **Fixed** (docs/comments corrected to "uniformly 0-based";
  code unchanged — no producer was ever 1-based). Independently re-verified against the
  producer code + the HHC golden.
- **Final: VERIFIED** — both findings were documentation-accuracy only; no behavioural,
  golden, or schema-validation change.

## Validation
- **Hermetic — 2,614 passing:** Pipeline 958, Vba.Grammar 295, Vba.Parser 92, Vba.Resolver
  410, Sql.Grammar 85, SymbolDb 71, AccessHarvester.Tests 703.
- **COM Integration — 77 passed / 3 skipped:** `CanonicalGoldenRegressionTests` (fresh COM
  run reproduces the new committed golden byte-equal), `StudentIctLocalInvariantTests` (now
  schema-valid), `NorthwindLocalInvariantTests` (still valid).

## Files changed (source/test/schema/docs)
- `AccessHarvester.Pipeline/Manifest/ParserOutputProjector.cs` (BuildLocation `+1`; top-level note)
- `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs`, `FunctionClassification.cs`,
  `NavigationGraph.cs` (column-base doc corrections — comments only)
- `docs/test-cases/harvester-schema.v1.1.json` (`SourceLocation` `$def` descriptions — inert)
- `tests/AccessHarvester.Pipeline.Tests/Manifest/ParserOutputProjectorTests.cs` (new pins)
- `tests/AccessHarvester.Pipeline.Tests/UnresolvedItemsCompletenessTests.cs` (LayerA re-pin `inline == index + 1`)
- `tests/AccessHarvester.Integration/Pipeline/StudentIctLocalInvariantTests.cs` (pin flipped)
- `docs/planning/deferred-work.md` (DW-26 closed; DW-27 recorded)
- `docs/harvester/interpretation-guide.md` (column-base note)
- `samples/GoldenOutput/HomeHealthCare/manifest.json` (regenerated; uniform `+1` columns)
- Set artifacts under `docs/session-sets/018-manifest-source-location-column-convention/`

## Operational note (Step 9 lesson candidate)
A whole-solution `dotnet test` wedged a COM testhost for ~3h (zero output). Reaped via WMI
`Terminate`; re-validated in **targeted, foreground, per-project** batches. **Do not run
the whole solution's COM tests in one invocation.** Also: an Evidence-Over-Inference miss —
the DW-27 mixed-base claim came from trusting a pre-existing comment instead of the producer
code; the in-IDE devil's-advocate caught it.

## Next set
**Consumer-driven-only STOP** — no concrete set authored. See
`next-session-set-recommendation.md` (standing DW bundles; DW-18 and DW-27 are the
golden-affecting ones-to-watch).
