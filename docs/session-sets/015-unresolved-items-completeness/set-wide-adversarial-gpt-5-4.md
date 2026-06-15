# Set 015 — set-wide adversarial review (GPT-5.4)

Reviewed against the current tree at/after `7769957`. Focused executable validation on the shipped slice passed: `UnresolvedItemsCompletenessTests` + `UnresolvedItemAggregatorTests` = `44/44`.

## 1. Hypothesis 1 — HOLDS

The Layer A / Layer B partition is exhaustive over the current production set: `ResolvedModule.References`.

The key fact is that both sides are defined by the exact same predicate in `ParserOutputProjector`: walk `reference.EnclosingScope` upward with `FindEnclosingProcedureScope(...)`; procedure-scoped references are the `non-null` side, module-level references are the `null` side. `ResolvedReference.EnclosingScope` is a non-null `Scope` on the resolver contract, so a reference does not have a third explicit "no scope object at all" state. Intermediate scopes such as `WithBlockScope` or `ForEachScope` do not create another bucket because the predicate is ancestry-based, not kind-based.

I do not see a reproducible path on the shipped tree where one `ResolvedReference` can land in neither feed. The only way to produce a true third bucket would be an upstream resolver bug that emitted a corrupted/orphaned scope chain for a reference in `ResolvedModule.References`; that would be a resolver-integrity failure, not a gap in this set's partition logic.

## 2. Hypothesis 2 — HOLDS

I do not see a cross-layer double-count path.

`EnumerateProcedureScopedReferences(...)` and `EnumerateModuleLevelReferences(...)` are exact complements over the same `ResolvedModule.References` set. The former emits only when `FindEnclosingProcedureScope(...)` succeeds; the latter emits only when it fails. Nested helper scopes do not matter because both feeds reduce them through the same ancestry walk. VBA also has no nested procedure construct that could create an ambiguous "procedure-like but not ProcedureScope" container.

If the resolver itself emitted the same `ResolvedReference` twice in `ResolvedModule.References`, that would duplicate upstream data, but it would not be a Layer A / Layer B overlap. I found no evidence of such duplication here.

## 3. Hypothesis 3 — HOLDS

The superset claim holds for the current unresolved-reason classes, including the with-flattening and late-bound cases the prompt names.

The important alignment is three-way:

1. `ParserOutputProjector.MapOutcome(...)` and `MapOutcomeWithFlattening(...)` write inline `Body.References[].ResolutionReason` by recursively preserving the inner unresolved reason.
2. `FunctionClassificationBuilder.DispatchOutcome(...)` does the same recursive flattening before populating `FunctionClassification.UnresolvedReferences`.
3. `UnresolvedItemAggregator.UnresolvedReasonOf(...)` also recursively unwraps `ResolvedViaWithFlattening` before deciding whether to emit an unresolved item.

That means `default-member-not-yet-resolved`, `member-access-on-unresolved-target`, and unresolved inner outcomes behind with-flattening are all preserved consistently between the inline view and the run-wide index. The Layer A dedup also mirrors the classifier's member-name extraction logic, so a body-scoped unresolved reference already present in `FunctionClassification.UnresolvedReferences` collapses correctly instead of being spuriously duplicated or spuriously suppressed.

The S1 undercount was a coverage gap (header refs missing from the body-only feed), not a reason-mapping mismatch. I do not see a surviving reason-class hole.

## 4. Hypothesis 4 — HOLDS

Layer B cannot collide with Layer A in the actual dedup path because Layer B never enters the dedup `HashSet` at all.

`UnresolvedItemAggregator.Build(...)` builds `bodyKeys` only from pre-existing `FunctionClassification` items and applies that set only while iterating `FromProcedureScopedReferences(...)`. The `FromModuleLevelReferences(...)` feed is appended afterward with no dedup against either procedure-scoped feed. So a module-level item cannot be silently suppressed by the Layer A dedup key.

On ordering: yes, the human-readable sort folds `ProcedureName == null` to `""`, but the final `CanonicalResidual` tie-breaker serializes the whole item, so a module-level item and a procedure-scoped item that happen to compare equal on the folded semantic keys still separate at the residual stage. I do not see a real mis-ordering or suppression bug here.

## 5. Hypothesis 5 — HOLDS

The determinism hardening is sound on the current object model.

`CanonicalResidual` is `JsonSerializer.Serialize(item)` over the full `UnresolvedItem`. That closes the classes the semantic comparer intentionally folds: case-insensitive module/procedure names, `null` vs empty-string folds, and any remaining fields not compared earlier. `AdditionalContext` is normalized to a `SortedDictionary` first, so dictionary key order is no longer ambient. The serializer options are compact and culture-invariant; there is no locale-sensitive formatting in the item shape.

This is not a proof against hypothetical future serializer-behavior changes in a different runtime, but for the shipped code and runtime contract it is a materially total order: if two residuals match, the emitted JSON item is byte-identical, so order does not matter.

## 6. Hypothesis 6 — HOLDS

The in-repo schema / contract story is coherent after the `7769957` guide fix.

- The living v1.1 schema includes `ModuleLevelReference` in `UnresolvedItem.Source` and documents the `ProcedureName == null` asymmetry.
- The CLI validate path still supports both `1.0` and `1.1` manifests and dispatches per-document by embedded schema version.
- The interpretation guide's unresolved-items section now explicitly states the DW-17 asymmetry: procedure-scoped unresolved refs appear both inline and in the run-wide index; module-level unresolved refs appear in `unresolved-items.json` only.
- The committed HomeHealthCare fixtures already carry the new `ModuleLevelReference` entries.

I did not find an in-repo consumer still hardcoding the old five-value `Source` enum. An out-of-repo consumer that snapshots the old schema and rejects unknown enum values can still break, but that is a generic living-schema compatibility risk, not a contradiction in this repo's own contract surface.

## 7. Hypothesis 7 — HOLDS, with an important separate caveat

The golden delta is fully explained as a DW-11 change: the committed HomeHealthCare output now carries exactly two `ModuleLevelReference` items for the two module-scope `vbObjectError` const initializers in `basDataOperations`, and those are real module-level unresolved references under the current resolver semantics.

But there is a second truth here, and it matters: those same two entries are also evidence of a separate resolver-completeness gap. The resolver has a `BuiltInFunctionRegistry`, but no intrinsic-constant registry. The repo's own deferred-work log tracks this as DW-18 and explicitly calls `vbObjectError`, `vbCrLf`, `vbNullString`, color constants, and related VBA intrinsics false-positive unresolveds.

So the set's golden claim is honest for DW-11: the harvester now surfaces all unresolved references it currently sees, including module-level ones. What it does **not** prove is that every surfaced unresolved is semantically a "real miss." `vbObjectError` is currently unresolved because the resolver does not yet know built-in constants, not because the Layer B feed invented a bogus site.

## 8. Hypothesis 8 — HOLDS

I do not see evidence that the tests were weakened to force green.

More importantly, the tests pin the production path directly enough to matter:

- `Pipeline.EmitManifestArtifacts(...)` now builds `procedureScopedReferences` via `ParserOutputProjector.EnumerateProcedureScopedReferences(...)`, builds `moduleLevelReferences` via `EnumerateModuleLevelReferences(...)`, then calls `UnresolvedItemAggregator.Build(...)` with both feeds.
- `UnresolvedItemsCompletenessTests` recreates that same chain over hermetic parse/scope/resolve/classify inputs and explicitly proves the pre-fix red path versus the post-fix green path for both layers.
- `UnresolvedItemAggregatorTests` pins the per-emitter shapes and the determinism behavior directly.

The focused rerun passed `44/44`, so I do not see a parallel-test-path problem here.

## 9. Hypothesis 9 — HOLDS, with one low-risk drift note

The sessions stayed on DW-11. The real residuals are explicitly carved out rather than silently deferred:

- DW-17: module-level unresolved refs are index-only, not surfaced inline in per-module `ParserOutput`.
- DW-18: intrinsic VBA constants are still unresolved false positives until the resolver gains a built-in constant registry.

I did not find another unrecorded DW-11 sub-case left hanging.

The only residual drift I found is commentary, not behavior: one test comment in `UnresolvedItemsCompletenessTests` still repeats the now-false "HHC has zero module-level unresolved cases / golden-neutral" framing even though the set deliberately regenerated HHC with `+2 ModuleLevelReference` items. The assertions are correct; the explanatory comment is stale.

## Set-wide verdict — SOUND-WITH-CAVEATS

As a whole, Set 015 achieves what it says it achieves: `unresolved-items.json` is now a complete run-wide structural superset of the unresolved references the current harvester can see, across both procedure-header and module-level layers. I did not find a surviving third bucket, a cross-layer double-count, a post-fix schema/guide contradiction, or a test-path dodge.

The main caveat is that completeness of the unresolved index is not the same thing as semantic cleanliness of the unresolved index. This set makes the current resolver's misses surface completely; it does not decide which of those misses should have resolved in the first place.

## Highest-risk thing the combined verification most likely still missed

The highest-risk miss is **not another undercount layer**. It is the now-more-visible resolver false-positive class tracked as DW-18: missing intrinsic VBA constants.

Why this is the real risk:

- Set 015 makes the unresolved index more complete, so it also makes resolver false positives surface more completely.
- HomeHealthCare only shows the small edge of that iceberg (`vbObjectError` twice).
- The repo's own deferred-work log already records broader local-corpus evidence (`vbCrLf`, `vbNullString`, color constants, etc.).

So the thing most likely to surprise a downstream consumer next is not "the index is still missing unresolved references" but "the index is now faithfully carrying unresolved references that are actually built-in constants the resolver should have bound." That does not overturn Set 015, but it is the next place where the newly-complete index can still mislead.