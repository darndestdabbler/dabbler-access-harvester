Here is the set-wide devil's-advocate review for Set 011, assessing the soft spots from Sessions 1 and 2.

### C7 — S1: does a degraded module contaminate sibling resolution, or resolve to an effectively-absent target?
**Analysis:** I tested hypotheses (a) and (b). 
- Hypothesis (a) is structurally disproved by C# typing. A `LocalDeclarationStatement` cannot appear directly at the module root because `Module.Items` only accepts `IReadOnlyList<ModuleItem>`. The AST nodes for `Statement` are not `ModuleItem` objects. Even during error recovery, the parser wraps garbled items in an `UnknownModuleItem`, which prevents `VisitLocalDeclarationStatement` from ever being called outside of a `ProcedureScope`. Thus, an unreachable post-attach exception throwing isn't physically possible here.
- Hypothesis (b), however, is demonstrably **TRUE**. Resolution is separated into Pass 1 (Scope Building) and Pass 2 (Cross-Module Resolution). In Pass 1, a module's Public declarations are attached to the shared `ProjectScope`. If that module subsequently throws an exception during Pass 2 (Resolution), it caught, a `VBA_RESOLVE_FAILURE` diagnostic is emitted, and the module gets shipped as a degraded stub (`ParserOutput: null`). Because the `ProjectScope` still contains the failed module's scopes from Pass 1, any healthy sibling module can successfully resolve against those declarations. The sibling will emit a `"ResolutionStatus": "Resolved"` reference to a module that failed resolution and has no body. Because the `interpretation-guide.md` does not warn consumers that a `"Resolved"` status could point to a `null` ParserOutput, a consumer following the guide will crash on dereferencing. This is a noticeable documentation defect.

### C8 — S1: does `Me` / bare-control binding silently miss when object names diverge?
**Analysis:** I tested the contrarian hypothesis that name divergence breaks bindings by attempting to construct a scenario where the metadata `Name` does not equal the file stem. This is fundamentally impossible due to how `Pipeline.cs` strings data. The loop reads `ir/**/*.json` via `Path.GetFileNameWithoutExtension(file)` into the `name` variable. This precise `name` scalar is then independently passed to BOTH `FormMetadataExtractor.Extract(name, json)` (which assigns it identically to `FormMetadata.Name`) AND `NewOwningClass(name, …)` (establishing the form's `owningClass.Name`). There is never a scenario where the registry's by-Name index mismatches the resolution lookup, because both are populated by identical deterministic file stem reads. The only true divergence vector is `FormMetadata.Name` != `ModuleName` (which comes from `Attribute VB_Name`), and that is fully guarded by the dual-key fallback and explicitly proven by the `MeControl_ResolvesViaByNameIndex_WhenModuleNameDivergesFromFormName` pin. C8 relies on a non-existent threat and is disproved. 

### C9 — S2: is `unresolved-items.json` actually complete, or only complete on HHC?
**Analysis:** The contrarian hypothesis claims the unresolved items list is structurally short. This is unequivocally **TRUE**, and it is a critical flaw. In `AccessHarvester.Core/Pipeline.cs:612`, `UnresolvedItemAggregator.Build()` is explicitly fed only `navigationGraph` and `functionClassifications`. A comment purposefully omits `resolverDiagnostics` claiming that all unresolved references are already collected via `FunctionClassification.UnresolvedReferences`. This assumption is disastrously wrong because `FunctionClassificationBuilder` ONLY classifies procedure AST nodes (Subs, Functions, Properties). Therefore, any unresolved reference located globally at the module scope—such as `Implements <unresolved>`, `Private x As New <unresolved>`, or a module-level `Const` initializing with an unresolved symbol—is skipped by the function classifier entirely. These items correctly triggered warnings in `resolverDiagnostics` but since that source was cut from the aggregator feed, module-level unresolved references are completely dropped from `unresolved-items.json`. This oversight was only hidden because the HHC database happens to contain zero module-level unresolved entities. This is a severe structural omission in the code.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/harvester/interpretation-guide.md",
      "summary": "Documentation fails to warn that cross-module resolution allows a fully 'Resolved' reference to point to a degraded/stubbed module that failed resolution.",
      "fix": "Add a warning in the interpretation guide §4 and §6 that consumers must anticipate resolving to a module where `ParserOutput` is optionally null due to VBA_RESOLVE_FAILURE."
    },
    {
      "severity": "Critical",
      "location": "AccessHarvester.Core/Pipeline.cs:612-614",
      "summary": "unresolved-items.json drops all module-level unresolved references because the aggregator only ingests procedure-scoped FunctionClassifications instead of resolverDiagnostics.",
      "fix": "Feed 'parsed.SelectMany(p => p.Resolved.Diagnostics)' into UnresolvedItemAggregator alongside FunctionClassifications, and update the aggregator to gracefully handle overlap without dropping module-level failures."
    }
  ]
}
```