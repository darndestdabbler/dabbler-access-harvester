# Devil's-Advocate Review - Set 018 (Manifest Source-Location Column Convention) - Gemini 3.1 Pro (Preview)

This is an adversarial evaluation of Set 018 (DW-26).

## 1. Missed 0-based leak
**UPHELD.** The isolation is airtight. A comprehensive scan of the codebase confirms that `SourceLocation` components only ever receive `StartColumn` / `EndColumn` projection through `ParserOutputProjector.BuildLocation()` and the synthetic document root hardcode mapping. There are no other components constructing these keys within the manifest (Navigation targets do not emit into the `ParserOutput` manifest's AST-bound `SourceLocation` fields directly, they emit to `unresolved-items.json`).

## 2. EndColumn semantics
**UPHELD.** Shifting a half-open `[Start, End)` range by `+1` on both boundaries is perfectly width-preserving across all token sizes. A length-L token on line X from 0-based index `0` to `L` (`[0, L)`) shifts to `[1, L + 1)`. The column entirely immediately following the end of the text is exactly `L + 1` in a 1-based editor. A zero-width token `[N, N)` shifts to `[N+1, N+1)`. The width computation `(EndColumn - StartColumn)` functions precisely without off-by-one errors.

## 3. DW-27 boundary
**UPHELD.** Keeping `unresolved-items.json` verbatim (0-based) while projecting the `.json` manifest to 1-based is structurally necessary. The `UnresolvedItemAggregator` enforces cross-feed deduplication using an internal key (`DedupKey`) initialized fundamentally from AST `Provenance.Column` coordinates. Transforming the classification and resolver inputs to 1-based columns prematurely would risk breaking the deduplication engine if any upstream producer missed the base switch. Emitting the unchanged producer index supports true round-trip fidelity between the AST diagnostics and unresolved feeds.

## 4. The schema edit
**UPHELD.** Adding `description` metadata blocks into a JSON Schema is purely informative in Draft-07/2020-12 and explicitly does not alter any structural constraint (like the unchanged `minimum: 1`). The schema file `docs/test-cases/harvester-schema.v1.1.json` is mapped verbatim into `AccessHarvester.Cli` as an `<EmbeddedResource>`, guaranteeing the validation resource remains identical without copy-drift.

## 5. Consumer impact / The Documentation Flaw
**CHALLENGED (Moderate severity).** The documentation updates in `docs/harvester/interpretation-guide.md` (and the `UnresolvedItem.cs` XML docs) explicitly assert: *"navigation-graph and function-classification facts are 1-based, but resolver and SQL-form-reference facts are 0-based."*
This is factually incorrect. A deep codebase trace confirms that ALL `UnresolvedItem` feeds project columns strictly from 0-based underlying sources:
- `FunctionClassificationBuilder` extracts columns from `expr.Provenance.Column` (VBA AST implicitly 0-based).
- `NavigationGraphBuilder` emits targets from `call.Location.Column` (VBA AST explicitly 0-based).
- `DynamicSqlAnalyzer` pulls columns from the `CallNode`'s `Provenance`.
Since `UnresolvedItemAggregator.cs` deduplicates heavily on `Location.Column` identically matching between cross-layer feeds, a mixed-base feed would fail deduplication dramatically. The `unresolved-items.json` output is uniformly 0-based because it mirrors the ANTLR AST. This misdirection in the documentation must be fixed so downstream AI consumers do not erroneously shift valid columns conditionally based on the `Source` attribute.

## Overall Verdict
The implementation perfectly isolates and normalizes the AST projections without disrupting token lengths or the JSON schema boundaries. However, the documentation updates contain a critical flaw mischaracterizing the column base of `UnresolvedItem` feeds. The `interpretation-guide.md` and the `UnresolvedItem.cs` XML comments must be corrected to reflect that all `unresolved-items.json` columns reliably carry the 0-based native AST positions.
