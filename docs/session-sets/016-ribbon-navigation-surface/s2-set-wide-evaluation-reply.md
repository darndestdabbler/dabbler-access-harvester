# Set-wide evaluation prompt reply — `016-ribbon-navigation-surface` (In-IDE review)

This is the adversarial review of Set 016 (Sessions 1 & 2), evaluating the claims of faithful-mechanical fidelity, determinism, cross-session contract integrity, and the overarching logic, in search of disproving the "VERIFIED" verdicts.

### A. Faithful-mechanical fidelity (the core principle)

1. **Resolution semantics.**
**Severity:** Major
**File:Location:** `AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs:BuildProcedureIndex` (lines 789-810)
**Summary:** Filtering by name alone across *all* modules (including Private methods and Form/Report code-behinds) pollutes the namespace, causing false `Ambiguous` resolutions when a ribbon callback shares a name with an uncallable private/form method, masking the real `Public` target in a standard module. 
**Concrete Fix:** Update `BuildProcedureIndex` to only index procedures inside modules where `module.ModuleScope.ModuleKind == ModuleKind.Standard` and where the procedure `Visibility` is `Public`. Access UI ribbons strictly only execute Public methods in Standard modules.

2. **`onLoad` inclusion.**
**Severity:** Minor
**File:Location:** `AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs:BuildRibbonEntryPoints` (lines 732-733)
**Summary:** `onLoad` correctly identifies a ribbon load event, but `onLoad` expects an `IRibbonUI` parameter instead of the `IRibbonControl` expected by `onAction` events. Grouping them identically under `RibbonEntryPoint` relies on the downstream consumer innately knowing the signature difference.
**Concrete Fix:** Ensure the AI consumer/schema documentation explicitly logs the distinct signature bindings required for `Kind = OnLoad` vs `OnAction` so the interpreter doesn't blindly apply an `IRibbonControl` delegate template to both.

3. **Dropped callbacks.**
**Severity:** Minor
**File:Location:** `AccessHarvester.Pipeline/Enrichment/RibbonXmlParser.cs:SlottedAttrs` (lines 183-184)
**Summary:** The `get*` dynamic callbacks (like `getVisible`, `getEnabled`, `getImage`) are relegated to the unslotted `Attributes` bag, losing their first-class status as executable VBA entry points (they invoke code identically to `onAction`). The "raw-tier-preserves-everything" claim is technically true, but structurally false for navigation edge analysis.
**Concrete Fix:** Extract the `get*` attributes into a dedicated list property on `RibbonControl` (e.g., `DynamicCallbacks`) and iterate them in `BuildRibbonEntryPoints` alongside `onAction`.

### B. Determinism & golden neutrality

4. **Determinism.**
**Severity:** Minor
**File:Location:** `AccessHarvester.Pipeline/Enrichment/RibbonXmlParser.cs:UnslottedAttributes` (lines 185-195)
**Summary:** The `XElement.Attributes()` enumeration preserves the exact string order of XML attributes as read from the DB; if Access internally restructures the XML attribute order between saves, the extracted JSON array for `Attributes` will drift without a deterministic sort.
**Concrete Fix:** Add an `.OrderBy(a => a.Name.LocalName, StringComparer.Ordinal)` clause to the attributes enumeration in `UnslottedAttributes` to enforce strict attribute determinism independent of DAO XML emission order.

5. **Golden leak.**
**Severity:** Minor
**File:Location:** `AccessHarvester.Core/Extraction/RibbonExporter.cs:Export` (lines 86-88)
**Summary:** A database containing an empty `USysRibbons` table (0 rows) will still pass the shape-gate due to `detected.Count > 0`, generating a `.json` artifact with `[]` instead of nothing, which differs from a DB with no `USysRibbons` table at all.
**Concrete Fix:** Change the degradation check in `RibbonExporter.cs` to `if (rows.Count == 0) return outcomes;` so that an empty ribbon table yields no raw payload, identical to a missing one.

### C. Cross-session & contract integrity

6. **S1 claims re-tested.**
**Severity:** Major
**File:Location:** `AccessHarvester.Pipeline/Enrichment/RibbonXmlParser.cs:Parse` (line 103-108)
**Summary:** Because `RibbonSignatureDetector` uses loose column detection (Name + XML), an unrelated logging table (e.g. `SystemEventLog` with columns `Id`, `RibbonName`, `RibbonXML`) could be erroneously surfaced; if its XML payload isn't valid `customUI`, `RibbonXmlParser.Parse()` intentionally throws an unhandled `FormatException` that crashes the entire pipeline instead of degrading cleanly.
**Concrete Fix:** Catch `FormatException` inside `Pipeline.BuildNavigationInputs` when applying the parser, emit an `ExportOutcome(..., false, ...)`/Diagnostic warning, and fallback to a `null` ribbon so false-positive tables do not hard-crash the harvester.

7. **Schema contract.**
**Severity:** Minor
**File:Location:** `docs/test-cases/harvester-schema.v1.1.json`
**Summary:** The `ProcedureReference` implementation loosely accepts both Ribbons (where `ModuleName` is always populated) and Macros (where it is occasionally `null`), but the schema does not enforce this distinction contextually.
**Concrete Fix:** Nothing strictly breaking, but modifying the schema validations to accurately depict conditional property mandates based on the parent Node (`NavigationEntryPoint` vs `RibbonEntryPoint`) would be more robust.

8. **`ProcedureReference` reuse.**
**Severity:** Major
**File:Location:** `AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs` (lines 834 & 805)
**Summary:** Reusing `ProcedureReference` when `ModuleName` is strictly populated for ribbon entry points but silently `null` for macro `RunCode` actions creates an implicit null-dereference trap; consumers assuming 100% population for resolving Ribbon entries might crash when blindly walking Macro entries using the same model.
**Concrete Fix:** Create a distinct `ResolvedProcedureReference` strong type (where `ModuleName` is non-nullable) for Ribbon entries and keep the partial `ProcedureReference` type strictly for unresolved/partially-resolved Macro/Navigation endpoints.

### D. Anything else

9. **Misinterpretation risk.**
**Severity:** Critical
**File:Location:** Across the downstream AI consumer target (conceptual).
**Summary:** If downstream AI implicitly trusts instances of `RibbonEntryPoint.Resolution == Resolved`, it will aggressively generate stub delegates and bindings without knowledge of parameter integrity. Because `BuildProcedureIndex` strips parameter counts and signatures, `Resolution: Resolved` might map `onAction` to a parameterless `Sub MyCallback()`, completely ignoring that Microsoft Access requires a `Sub MyCallback(control As IRibbonControl)` signature, triggering runtime application faults.
**Concrete Fix:** Expand `RibbonEntryPoint` to capture `ParameterCount` from the discovered `ParserProcedure` so downstream consumers can actively flag and handle signature mismatches rather than generating fatal bindings.