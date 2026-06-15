# Set 016 — set-wide evaluation (GPT-5.4)

`ISSUES_FOUND`.

I do not see a real determinism or HomeHealthCare-golden-neutrality defect on the current tree, and I do not think `onLoad` inclusion is scope creep after the 016 S2 narrowing of DW-20. The problems I do see are resolution fidelity, ribbon-source detection, and the consumer-facing contract.

## Finding 1

Severity: Major  
File: `AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs:764,858`; `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs:603`

Summary: Ribbon callback resolution indexes every harvested procedure by bare name only, so a real callback can be reported as `Ambiguous` or even `Resolved` to a class-module, private, or wrong-kind declaration that Access cannot actually bind as a ribbon callback.

Concrete fix: Keep the authored `CallbackName`, but add a bindable-candidate filter before final resolution: prefer `Public` standard-module procedures with the expected callback shape (`IRibbonControl` for `OnAction`, `IRibbonUI` for `OnLoad`), and only fall back to raw-name ambiguity when multiple bindable matches remain; if preserving the current conservative trail matters, surface both the raw match count and the bindable match count.

Why this is real: the routed VERIFIED pass treated name-only matching as a principled refusal to interpret, but the shipped code goes further than that. `BuildProcedureIndex` takes every `ParserProcedure` in every harvested module and de-duplicates only on `(module, procedure name)`. That means a realistic project with `Public Sub cmdAbout_OnAction(control As IRibbonControl)` in a standard module and a same-named private helper or property in a form/report class module will ship `Ambiguous`, even though only one declaration is ribbon-bindable at runtime. That is not just conservative; it hides the real target.

## Finding 2

Severity: Major  
File: `AccessHarvester.Core/Extraction/RibbonSignatureDetector.cs:27`; `AccessHarvester.Core/Extraction/RibbonExporter.cs:55`

Summary: Any non-system table carrying `RibbonName` and `RibbonXML` columns is harvested as a ribbon source, so an unrelated XML-bearing table can fabricate `NavigationGraph.Ribbon` and downstream `RibbonEntryPoints`.

Concrete fix: Treat literal `USysRibbons` as the canonical source when present, and either ignore non-canonical shape matches or demote them to a diagnostic unless an explicit opt-in or stronger proof exists, such as expected field types plus the absence of a canonical table.

Why this is real: the switchboard signature precedent was imported too mechanically here. The shipped code does not just preserve extra raw rows for later inspection; it promotes the first parseable match into the singular ribbon surface via `RibbonStructureBuilder.Build`, and S2 then resolves that surface into callback entry points. So a false-positive table does not stay harmlessly in `raw/Navigation/ribbon-xml.json`; it can become the database's published ribbon navigation surface.

## Finding 3

Severity: Major  
File: `docs/test-cases/harvester-schema.v1.1.json:966,1020`; `docs/harvester/interpretation-guide.md:1256,1659`

Summary: The public contract is internally inconsistent after 016: the schema says `RibbonEntryPoints` covers every control callback, while the interpretation guide still says no ribbon producer exists, and the source/deferred-work notes say non-`<tabs>` sections and dynamic `get*` callbacks are not projected.

Concrete fix: Update the schema text and interpretation guide together to describe the real 016 surface: `Ribbon` and `RibbonEntryPoints` now ship, `RibbonEntryPoints` covers only the structured `tabs`/`contextualTabs` view plus the root `onLoad`, dynamic `get*` callbacks stay raw-only, and an empty array means "no surfaced static callbacks," not "the ribbon has no callbacks."

Why this is real: this is the single most likely downstream-AI misread. A consumer that trusts the schema literally will over-read `RibbonEntryPoints` as the complete ribbon to VBA edge set, while a consumer that trusts the interpretation guide will ignore ribbon support entirely because the guide still says `Ribbon` has no producer. The source comments and `DW-20`/`DW-21` notes record the real boundary, but those are not the primary consumer contract.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs:764,858; AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs:603",
      "summary": "Ribbon callback resolution matches every harvested procedure by bare name only, so non-bindable declarations can force false Ambiguous results or become the reported Resolved target.",
      "fix": "Filter or rank matches by ribbon-bindable callback shape before final resolution, and only fall back to raw-name ambiguity when multiple bindable candidates remain."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Extraction/RibbonSignatureDetector.cs:27; AccessHarvester.Core/Extraction/RibbonExporter.cs:55",
      "summary": "The ribbon source detector accepts any table with RibbonName and RibbonXML columns, which can fabricate a ribbon surface and bogus ribbon entry points from unrelated data.",
      "fix": "Prefer the canonical USysRibbons table and treat non-canonical signature matches as diagnostic-only unless stronger proof or explicit opt-in exists."
    },
    {
      "severity": "Major",
      "location": "docs/test-cases/harvester-schema.v1.1.json:966,1020; docs/harvester/interpretation-guide.md:1256,1659",
      "summary": "The public ribbon contract is inconsistent: one doc says RibbonEntryPoints is complete, another still says no ribbon producer exists, while the implementation only projects tabs/contextualTabs plus root onLoad and leaves dynamic/non-tab callbacks out of the typed surface.",
      "fix": "Align the schema and interpretation guide with the actual 016 behavior and explicitly document the raw-only callback gaps and empty-array semantics."
    }
  ]
}
```