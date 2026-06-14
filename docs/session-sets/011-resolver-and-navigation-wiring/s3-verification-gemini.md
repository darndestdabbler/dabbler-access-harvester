Here is the devil's-advocate review for Set 011, Session 3.

### C1 — Does §5.7 accurately describe shipped output?
**Analysis:** I tested the contrarian hypothesis that a consumer coding against §5.7 would interpret the golden output incorrectly by executing PowerShell probes against `samples/GoldenOutput/HomeHealthCare/manifest.json`. The analysis disproves the contrarian hypothesis.
- **Edge/Target shapes & counts:** The `NavigationGraph.Edges` and `.UnresolvedTargets` collections contain exactly 22 items each. The edge breakdown is indeed 13 `OpenForm`, 8 `Close`, and 1 `OpenReport`. The UnresolvedTargets list correctly carries `non-literal-target` as the `Reason`. The source fields (`SourceModule`, `SourceProcedure`, etc.) accurately map to Classification and Module entries.
- **Null omission of keys:** The claim that `Ribbon` and `Switchboard` are omitted when null is true. Reflecting on the `manifest.NavigationGraph` objects using `Get-Member` confirms that the keys `EntryPoints`, `Edges`, `Macros`, and `UnresolvedTargets` are present (even when empty, arrays serialize), while `Ribbon` and `Switchboard` are completely absent structurally because of `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` applied globally in `AccessHarvester.Core/Pipeline.cs`. There is no misdescription or overclaiming here.

### C2 — Is the `SchemaVersion 1.0 → 1.1` correction right and complete?
**Analysis:** Testing the contrarian hypothesis that the version fix is partial or incorrect. A grep through `docs/harvester/interpretation-guide.md` finds that `SchemaVersion` is correctly stated as `"1.1"` in the JSON envelope example and the surrounding narrative. The only remnants of `"1.0"` and `1.1` are explicit historical references explaining the `1.0 → 1.1` backward-compatible additive bump and pointing out the load-time gating behavior that accepts both versions. Therefore, the fix is comprehensive and accurate.

### C3 — Is the R2-1/R2-2 deferral represented honestly, or does the doc let a consumer believe the switchboard tree / report chains are delivered?
**Analysis:** I evaluated whether the documentation obscures the fact that switchboards and report chains are deferred. In the interpretation guide (§5.7), a dedicated "What is NOT populated today" block clearly explains that the Switchboard and Ribbon facets are null, and the EntryPoints list is empty. Furthermore, it explicitly mentions that chain edges (launcher -> report -> dialog -> query) are not derived. This points directly to deferred work DW-9 and DW-10, demonstrating transparent expectation management for the consumer rather than implying the features are already available.

### C4 — Is the next-set spec (`012-switchboard-and-report-chain-edges/spec.md`) faithful and scope-disciplined?
**Analysis:** Testing the risk of scope creep in the next-set spec. The spec explicitly acknowledges that this is a new COM/data surface (the only exception forbidden in 011), but it strictly bounds it under "Hard Constraints." It mandates column-signature detection instead of name-based detection (`tblSwitchboardItems`), bounds the data pull using an ordered SELECT of canonical columns only, and emphatically states: "This is **not** a general row-data export surface — switchboard rows are navigation chrome, not business data." This constraint architecture prevents scope bloat perfectly and aligns with 006's P4 directives.

### C5 — Is the "committed golden equals a fresh run" grounding actually established?
**Analysis:** Testing the contrarian hypothesis that the current golden might be stale relative to the codebase. Session 3 made exactly zero modifications to any C# file. The codebase is identical to the end of Session 2, where the golden was intentionally regenerated and committed. Session 3 strictly ran `CanonicalGoldenRegressionTests` and `ManifestSchemaConformanceTests`, both returning 6/6 green under the same unchanged code. This reliably guarantees the committed golden represents the exact runtime behavior of the current codebase without drift.

### C6 — Is closing set 011 now premature?
**Analysis:** Evaluating if the set closure is jumping the gun. The Deliverables required F-3-4 resolution implementation and F-2-2 (Navigation Graph) wiring. Analysis of the shipped golden proves that the F-2-2 surfaces are demonstrably present (`NavigationGraph` is fully shaped, `unresolved-items.json` contains 1414 targets). The omit-null fix accurately prevents the invalid `null` kind from polluting the resolver output downstream. Since all code changes landed functionally and structurally, and are documented clearly in the consumer guides without introducing scope creep, there is no unmet End-Of-Set deliverable. Closure is appropriate.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
