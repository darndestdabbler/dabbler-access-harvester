# Set-Wide Adversarial Evaluation - Set 015 - Gemini 3.1 Pro (Preview)

## Hypothesis 1: The partition is NOT exhaustive
**HOLDS.** The partition is airtight by mathematical construction. Layer A enumerates items where `FindEnclosingProcedureScope(reference.EnclosingScope)` is `not null`. Layer B enumerates items where it `is null`. Since every reference falls into exactly one of those two states, the partition covers 100% of `ResolvedModule.References` with no third layer possible.

## Hypothesis 2: Double-count across the two layers
**HOLDS.** Because the partition relies on a strict binary (`is null` vs `is not null`) over the exact same `FindEnclosingProcedureScope` check, the sets are disjoint by definition. A single reference cannot simultaneously have and lack an enclosing procedure scope.

## Hypothesis 3: Superset claim vs. the inline view
**HOLDS.** `UnresolvedItemAggregator` determines the reason via `UnresolvedReasonOf(Reference.Outcome)`, which explicitly unwraps `ResolutionOutcome.ResolvedViaWithFlattening` to extract the inner `Reason`. This perfectly perfectly mirrors `ParserOutputProjector.MapOutcomeWithFlattening`, ensuring both feeds produce the exact same reason string so the dedup matches properly and doesn't over-suppress.

## Hypothesis 4: Layer A / Layer B interaction in the dedup + sort
**HOLDS.** Layer B items are appended to the `items` list *after* the `procedureScopedReferences` dedup loop has finished comparing items against `bodyKeys`. Layer B items are never checked against the dedup HashSet, making cross-layer suppression mathematically impossible. In sorting, `UnresolvedSource.ModuleLevelReference` separates them safely from Layer A items.

## Hypothesis 5: Determinism end-to-end
**HOLDS.** `CanonicalResidual` serializes the *entire object* into JSON, with `AdditionalContext` explicitly populated via a `SortedDictionary<string, string>(StringComparer.Ordinal)`. Because any field-level difference, regardless of how subtle, yields a distinct JSON string, the tiebreaker enforces a provably total order, ensuring stable sorts across platforms and iterations. If two objects produce identical JSON strings, they are mathematically identical, and their sort order is perfectly interchangeable without affecting the output byte stream.

## Hypothesis 6: Schema / contract honesty across the whole set
**HOLDS.** The DW-17 asymmetry is directly explicitly recorded in `docs/test-cases/harvester-schema.v1.1.json` without bumping the schema version, staying true to living schema invariants. `interpretation-guide.md` honestly documents that `ModuleLevelReference` will surface in the index ONLY, omitting it from `Body.References`.

## Hypothesis 7: Golden integrity
**CONFIRMED-BROKEN.** The two newly surfaced `ModuleLevelReference` items from the Home Health Care golden (pointing to `vbObjectError`) are indeed positioned properly at module level, so Layer B correctly did its job. **However**, `vbObjectError` is an intrinsic VBA constant. It is unresolved strictly because `AccessHarvester.Vba.Resolver/References/BuiltInFunctionRegistry.cs` is missing standard VBA intrinsic constants (like `vbObjectError`, `vbCrLf`). The golden file change exposes a literal bug in the Upstream Resolver's capability, rather than an unresolvable user construct.

## Hypothesis 8: Test integrity across both sessions
**HOLDS.** Both `UnresolvedItemAggregatorTests` and `UnresolvedItemsCompletenessTests` invoke the exact same composition of `VbaParserPipeline.Parse`, `FunctionClassificationBuilder.BuildAll`, and `UnresolvedItemAggregator.Build()` utilized by `AccessHarvester.Core/Pipeline.cs`. They do not use parallel/mock constructions.

## Hypothesis 9: Scope discipline
**CONFIRMED-BROKEN (API Capability Gap).** While the code correctly surfaces VBA references, the `UnresolvedItemAggregator.Build()` method signature accepts merely a single scalar `string? sqlFormReferenceModuleName` to attribute form reference annotations and dynamic SQL sites. Because `Pipeline.cs` invokes `Build()` exactly ONCE for the entire run, the aggregator is structurally incapable of incorporating Form References from multiple different modules into the run-wide index simultaneously. The pipeline bypasses this currently by not feeding SQL Form References into `Build()` at all.

## Set-Wide Verdict
**SOUND-WITH-CAVEATS.** Layer A and Layer B accurately satisfy DW-11 constraints by capturing and deduplicating VBA references exhaustively without omissions. The implementation logic matches the thesis. However, the aggregator signature is fundamentally broken for non-VBA inputs, restricting future expansion (DW-8 SQL work), and the VBA builtin registry is incomplete.

## Highest Risk Finding
The single highest-risk thing missed during verifications is the signature block of `UnresolvedItemAggregator.Build()`. It treats `Build` conceptually as a per-module API for SQL structures (taking a single `sqlFormReferenceModuleName`), but `Pipeline.cs` consumes it as a run-wide singleton. At the current state, if you wire up SQL Form References, you can only pass a single module's annotations, fundamentally breaking the "run-wide index" guarantee for anything other than standard VBA references.
