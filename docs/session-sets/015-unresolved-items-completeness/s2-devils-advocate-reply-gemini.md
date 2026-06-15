# Devil's-Advocate Review - Set 015 S2 - Gemini 3.1 Pro (Preview)

This is an adversarial evaluation of Set 015 (Session 2), examining the final 015-S2 closeout commit against the contrarian hypotheses.

## 1. Double-count via a non-procedure, non-module scope
**HOLDS (Hypothesis Defeated).** The partition is mathematically absolute. The check `FindEnclosingProcedureScope(reference.EnclosingScope)` either evaluates to `null` or `not null`. `EnumerateModuleLevelReferences` takes exclusively the `is null` branch, while `GroupReferencesByProcedure` relies strictly on `is not null`. Every single reference on `resolvedModule.References` must fall into exactly one of these two buckets. There is no edge case where a reference can be in both or neither.

## 2. A module-level ref that should NOT surface
**HOLDS (Hypothesis Defeated).** The code correctly guards against emitting successfully resolved module-level references. In `UnresolvedItemAggregator.FromModuleLevelReferences`, there is an explicit validation:
`var reason = UnresolvedReasonOf(mlr.Reference.Outcome); if (reason is null) continue;`
This ensures only genuinely unresolved references progress into the manifest.

## 3. Determinism residual injectivity
**HOLDS (Hypothesis Defeated).** `CanonicalResidual` serializes the instance to JSON as the total-order tiebreaker. To prevent non-deterministic JSON object keys from disrupting injectivity, `AdditionalContext` is safely wrapped in a `SortedDictionary<string, string>(StringComparer.Ordinal)` via `SortContext()`. `UnresolvedSource` enums stringify reliably. Null vs empty vs 0 are preserved. Thus, two identical runs will provably produce the same byte-for-byte JSON string sequence. 

## 4. Enum widening contract
**SOUND-WITH-CAVEATS.** Widening the JSON Schema `Source` enum in a "living" `v1.1` without an envelope bump is permitted by JSON Schema `$def` tolerance. However, any downstream C# consumer (e.g. Blazor) strictly parsing this into an Enum without `FallbackToUnknown=true` on their `JsonStringEnumConverter` will crash upon encountering the newly generated `ModuleLevelReference` string. 

## 5. Asymmetry (DW-17)
**CONFIRMED-BROKEN.** Module-level references are injected cleanly into the global index (`unresolved-items.json`), but are entirely absent from the module-specific output! `ParserOutputProjector` deliberately drops them from the local module `Items` array. Worse, `docs/harvester/interpretation-guide.md` is now dangerously out of date. It still states that module-level references appear in *neither* place "until DW-11 lands." DW-11 landed, updating only the index, yielding an undocumented asymmetry where consumers reading per-module outputs will silently miss required context.

## 6. Scope / test integrity
**HOLDS (Hypothesis Defeated).** The golden test regenerations were 100% legitimate. The exact +2 multiset delta corresponds exclusively to the two instances of `vbObjectError` in the `Const` declarations at the top of the `basDataOperations` module in `HomeHealthCare`. `UnresolvedItemCount` incremented cleanly from 1414 to 1416 with 0 removals. No assertions were artificially weakened to "force green."

## Overall Verdict
**SOUND-WITH-CAVEATS.** Layer B fulfills the requirement of closing the module-level unresolved reference leak into the global index with provable determinism. However, it abandons the per-module JSON output and fails to update the manual `interpretation-guide.md`, cementing **DW-17 (Asymmetry)** as a significant hazard for consumers who assume the module output matches the index.
