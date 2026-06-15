# Set 015 S2 — devil's-advocate review

I reviewed the shipped tree directly and ran the narrow pipeline tests for this surface: `UnresolvedItemsCompletenessTests` + `UnresolvedItemAggregatorTests` passed 44/44.

### Hypothesis 1 — HOLDS

The Layer A/Layer B partition is sound on the current tree. The important detail is that this is not two similar heuristics; it is the exact complement of one predicate applied to the same `ResolvedReference` set.

`GroupReferencesByProcedure` includes a reference iff `FindEnclosingProcedureScope(reference.EnclosingScope)` is non-null. `EnumerateModuleLevelReferences` includes a reference iff that same call is null. `ResolvedReference.EnclosingScope` is not optional, and the concrete intermediate scope types in play (`WithBlockScope`, `ForEachScope`) still reduce cleanly to one question: does the parent chain reach a `ProcedureScope` or not?

That means a weird non-procedure intermediate scope does not create overlap. It still lands on exactly one side of the partition. An upstream resolver bug could mis-tag a reference onto the wrong side, but I do not see a way for the same `ResolvedReference` instance to satisfy both predicates or neither predicate on the shipped production path.

### Hypothesis 2 — HOLDS

I do not see a leak path for resolved module-level references. `FromModuleLevelReferences` immediately calls `UnresolvedReasonOf`, and that helper returns null for `Resolved` and for `ResolvedViaWithFlattening` whose inner outcome is resolved. The feed only emits when the post-flattening outcome is actually unresolved.

So a module-level reference that really binds, such as a const initializer referring to another declaration that the resolver can see, does not surface in `unresolved-items.json`. The current code matches the intended contract here.

### Hypothesis 3 — HOLDS

The current determinism fix is materially stronger than the earlier `SurroundingText`-only tie-breaker. The final residual is the full-item JSON serialization, not a hand-rolled lossy key. If two entries serialize identically, they are output-identical and their relative order is irrelevant. If they differ in any emitted field, the residual separates them.

I also do not see a realistic same-input / different-JSON drift source in the current implementation:

- `AdditionalContext` is sorted ordinally before emission.
- `System.Text.Json` serialization here is culture-invariant.
- `UnresolvedSource` serializes as a string, not an ordinal.
- Null handling is fixed by the serializer options in code, not by ambient process state.

The narrow sort tests passing matters here because they exercise exactly the two classes that previously broke the total-order claim: same-prefix non-identical items and folded-collision items.

### Hypothesis 4 — HOLDS

Inside this repo, I could not find a consumer or test that hardcodes the pre-S2 five-value `Source` set. The schema widened in place, the enum serializes as a string, and the in-tree schema/tests already accept the new discriminator.

There is still the ordinary forward-compatibility caveat that any external consumer which snapshots the old schema or exhaustively switches on `Source` without an unknown-value path can reject the new value. But that is a generic external compatibility risk, not a break I could prove on the current tree. Under the repo's stated living-schema model, the in-place widening is defensible.

### Hypothesis 5 — CONFIRMED-BROKEN

The implementation asymmetry itself is intentional and acceptable for DW-11: module-level unresolved references now have a manifest home in the run-wide index, but not in per-module `ParserOutput`. The break is the consumer-facing documentation.

The public interpretation guide still describes the pre-S2 world. It still says module-level unresolved references have no manifest-level signal at all, and it still enumerates only the old five `Source` values. That is now false. A consumer reading the guide rather than the code/schema would be told the wrong story about both completeness and the discriminator set.

This is the one concrete place where I think the shipped response is not honest enough: the code and schema say "module-level refs are index-only under `ModuleLevelReference`", while the public guide still says "no manifest signal at all". That should be corrected.

There is additional unresolved-items drift in the same guide section as well, including a stale top-level `Severity` description, which reinforces that this documentation path did not get regrounded after the implementation changed.

### Hypothesis 6 — HOLDS

I do not see evidence that assertions were weakened to force green. The production wiring is additive, the Layer B tests are additive, and the targeted pipeline tests are green without changing the scope of what they assert.

The only thing I would not overread is commentary. There is still stale narrative/comment text in this area, including the old "HHC has zero module-level unresolved cases" framing that the session outcome itself disproved. But that is commentary drift, not a test-integrity problem.

## Overall verdict

SOUND-WITH-CAVEATS.

The shipped implementation looks sound on the actual Layer B mechanics: no reproducible cross-feed double-count, no resolved-reference leak, and no surviving evidence that the canonical residual still leaves the sort non-total. The one real finding is contract drift in the public interpretation guide. If the repo treats that guide as a consumer-facing source of truth, it needs a follow-up update so the docs match the shipped `ModuleLevelReference` index-only behavior.

```json
{
  "issues": [
    {
      "severity": "Major",
      "location": "docs/harvester/interpretation-guide.md",
      "summary": "The public unresolved-items contract is stale after S2: it still says module-level unresolved references have no manifest-level signal and still lists only the pre-S2 five Source values, so a consumer following the guide is misled about the shipped ModuleLevelReference index-only path.",
      "fix": "Update the unresolved-items section to state the current asymmetry explicitly: procedure-scoped unresolved refs appear inline and in the index; module-level unresolved refs appear in unresolved-items.json only, under Source=ModuleLevelReference with ProcedureName null. Refresh the source table and example field list in the same pass."
    }
  ]
}
```