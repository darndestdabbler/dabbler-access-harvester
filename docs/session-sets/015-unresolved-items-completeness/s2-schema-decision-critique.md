# 015 S2 — routed schema-decision critique (opus)

> task_type=architecture; cost $0.2273. Raw routed output, unedited.

## Critique of 015 Session 2 — Layer B Design Note

---

### 1. Enum widening without an envelope bump: contract-correct?

**Argument for (the design's position):** The documented lesson's trigger is "additive *field* on an `additionalProperties`-tolerant `$def`." This is not an additive field — it is a domain widening of an existing field. Old documents remain valid (they never carry the new value). The `UnresolvedItem` `$def` is tolerant. The envelope's required/structural shape is untouched. Under the living-schema model (`harvester-schema.v1.1.json` is mutable within the v1.1 lifecycle), editing the enum in place is the documented procedure. The enum's own extension contract ("future producers append rather than reuse an existing value") explicitly anticipates this operation.

**Argument against:** The enum is described as a "closed set." In most API-contract frameworks, adding a value to a closed enum is a *breaking* change for any consumer performing an exhaustive switch/match. A consumer compiled against the pre-widening schema will encounter `"ModuleLevelReference"` and, depending on implementation, may reject or crash. The fact that `additionalProperties` is tolerant on the *object* is irrelevant — JSON Schema `enum` is itself a strict validation constraint. Widening it changes which documents validate. If any downstream system caches or snapshots the schema, they now disagree with the producer about document validity. This is exactly the kind of forward-compatibility hazard that version signals exist to communicate.

**My commitment:** The design is contract-correct *under the stated rules*, but it exposes a terminology gap that should be closed. "Closed set; future producers append" is internally contradictory — "closed" conventionally means immutable membership, not merely producer-controlled. The design note should either (a) amend the `UnresolvedSource` documentation to say *"Controlled vocabulary; future producers append"* to remove ambiguity, or (b) state explicitly that consumers of the index MUST handle unknown `Source` values gracefully (skip/log, not crash). If neither is done, this is a latent contract misunderstanding waiting to fire. But on the merits: no new property, no structural envelope change, living-schema model in place — no envelope bump is warranted. **Correct, but document the consumer obligation.**

---

### 2. No-double-count claim: airtight or leaky?

The claim is airtight **by construction**, and the specific VBA constructs raised don't break it. Here is why, and where I pushed hardest:

The partition is defined by a single deterministic function (`FindEnclosingProcedureScope`) applied to the same collection (`resolvedModule.References`). Every reference either has a `ProcedureScope` ancestor or it does not. The procedure-scoped feeds take the former partition; the module-level feed takes the latter. These are complementary by the law of excluded middle. Double-counting requires either (a) the same reference object appearing twice in the collection, or (b) `FindEnclosingProcedureScope` returning different results for the same reference on different invocations — both would be resolver bugs, not design flaws.

Specific constructs:

- **VBA has no nested procedures**, so a reference cannot belong to two procedure scopes simultaneously.
- **Module-level `Const`/`Dim` initializers** (`Public Const X = MissingThing`): the probe confirmed `enclosingScope = ModuleScope`. No `ProcedureScope` ancestor. Falls cleanly into the module-level partition.
- **`Implements ISomething`**: module-level statement, module scope. Clean.
- **Property Get/Let/Set header refs** (return types, parameter types): Layer A confirmed these have a `ProcedureScope` ancestor (they're inside the procedure's declaration node in the syntax tree). They fall into the procedure-scoped partition. No overlap with module-level.
- **`Enum` member initializers, `Type` member types**: module-level constructs; if the resolver captures references from them, they'd be under `ModuleScope` (no `ProcedureScope`). Falls into module-level partition.
- **`WithEvents` declarations**: module-level. If the class is unresolved, no synthetic event stubs exist. Clean.
- **`Declare` statements**: module-level; parameter type references would be module-scoped if captured by the resolver (though `Declare` may be handled as a separate item type entirely).

The only theoretical risk is a resolver implementation quirk where a construct creates an unexpected intermediate scope that is or is not a `ProcedureScope`. The empirical probe mitigates this for the `Const` case. The hermetic pin `LayerB_ModuleLevelFeed_DoesNotDoubleCount_*` covers it going forward.

**Verdict on this point: airtight. No dedup logic needed.**

---

### 3. Index-only vs. inline per-module surface: correct completeness boundary?

The design's reasoning is sound for "smallest correct change," but it creates a **real completeness asymmetry** that should be explicitly acknowledged as a known limitation, not merely a rejected alternative.

Today's state after this change:
- **Procedure-scoped unresolved refs** have TWO manifest signals: per-module `ParserOutput.Body.References` (inline) AND `unresolved-items.json` (index).
- **Module-level unresolved refs** have ONE manifest signal: `unresolved-items.json` only.

A consumer processing a single module's `ParserOutput` in isolation — which is a reasonable unit-of-work boundary — will see *no signal at all* for module-level unresolved refs. They must cross-reference the run-wide index. This is asymmetric and potentially surprising.

**However**, the design is right to defer the inline surface:
- Adding a module-level slot to `ParserOutput` IS a structural envelope change, requiring a version bump and broad golden churn.
- DW-11 asks for "a manifest signal," not "a signal at every granularity level." The index satisfies DW-11's requirement.
- HHC has zero real cases, so the practical cost of the asymmetry is zero today.
- The inline surface can be added later as a separate, properly versioned envelope change if consumers demand it.

**What I'd change:** The design note should carry a forward pointer or explicit known-limitation entry: *"Module-level unresolved refs are not visible in per-module `ParserOutput`. A consumer needing per-module completeness must join against the index. This asymmetry can be resolved by a future `ParserOutput` schema change (would require envelope bump)."* Burying this only in the "alternatives rejected" section risks it being forgotten.

---

### 4. Other flaws, omissions, or smaller alternatives

**A. Zero real-world validation.** The design acknowledges HHC has zero module-level unresolved cases, meaning the feed is inert on the only real corpus. The entire feature ships with synthetic-only testing. This is acceptable for correctness (the hermetic pins cover the logic), but it means any serialization quirk, pipeline ordering issue, or unexpected resolver behavior on real-world module-level constructs will only surface when a future corpus triggers the feed. The design should note this as a known confidence gap.

**B. The new record type may be unnecessary.** `ModuleLevelReference(string ModuleName, ResolvedReference Reference)` mirrors whatever the procedure-scoped feeds use. Could both feeds share a common `ScopedReference` record with an optional `ProcedureName`? The design argues for symmetry with Layer A, which is defensible from a separation-of-concerns perspective — each feed handles a disjoint set, and distinct types make that partition visible in the type system. But it's worth asking whether three new types (record, enumerator, feed) is truly the smallest code surface when a conditional within the existing feed pipeline could achieve the same outcome with one new enum value and a few lines of branching. The architectural symmetry argument wins on maintainability, but "smallest correct change" is slightly overstated.

**C. Schema file as golden artifact.** The design says "the schema file is documentation of the contract, not a golden artifact" but then immediately says `ManifestSchemaConformanceTests` must pass. If conformance tests validate produced documents against the schema, and the schema changes (widened enum), then the schema file IS a tested artifact even if not a golden-regression artifact. The distinction is real but the language is imprecise — if a future maintainer misreads this and excludes the schema from CI validation, the contract becomes unenforced.

**D. No consideration of `Source` enum ordering.** The design says "appended last." If any consumer or test relies on ordinal position of the C# enum (e.g., serialization as integer rather than string), appending is safe. But the design should confirm that `UnresolvedSource` is serialized as a *string* in JSON (which the schema's `enum: [...]` with string values implies). If it's ever serialized as an int, appending a value changes the ordinal meaning of existing values depending on insertion point. Minor, but worth a one-line confirmation.

---

### Verdict

**SOUND-WITH-CAVEATS.**

The caveats, in priority order:
1. **Document the consumer obligation** for unknown `Source` enum values, and fix the "closed set" / "future producers append" terminology contradiction.
2. **Promote the inline-surface asymmetry** from "rejected alternative" to an explicit known limitation with a forward pointer.
3. **Acknowledge the synthetic-only validation gap** for the inert-on-HHC feed.
4. **Confirm string-not-ordinal serialization** of the `Source` enum as a one-liner.

None of these block implementation. The core architecture (complementary partition, index-only surface, enum widening in the living schema) is correct.