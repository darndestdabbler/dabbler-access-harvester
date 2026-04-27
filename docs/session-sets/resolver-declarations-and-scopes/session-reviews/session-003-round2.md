## ISSUES_FOUND

All 6 anchor fixtures have dedicated tests; issues below are gaps in assertion strength.

- **Major** — Missing verification of resulting module metadata  
  **Location:** `ScopeBuildingFixtureTests.cs`, all fixture-specific tests and cross-fixture theories; `BuildFromFixture(...)` accepts `moduleName` and `ModuleKind`, but no test asserts the built module actually uses them.  
  **Recommended fix:** Add a `[Theory]` over all 6 fixtures asserting the produced `ModuleScope`/module declaration has the expected module name and `ModuleKind` (`Form`/`Report`/`Class`/`Standard`). This would catch the previously-fixed kind regression if it reappears.

- **Major** — Fixture 10 and Fixture 12 validate procedure totals, not the exact procedure set  
  **Location:** `Fixture10_ClassModule_HasExpectedProcedureCount`, `Fixture10_PropertyDeclarations_AreInModuleDeclarations`, `Fixture12_ProducesTwelveProcedureScopes`  
  **Recommended fix:** Assert the full expected procedure inventory, not just counts.  
  - For Fixture 10, compare exact non-property procedure names and exact property `(Name, Kind)` pairs.  
  - For Fixture 12, compare the exact 12 expected procedure names from the fixture.  
  Current tests would not fail if one procedure were dropped and another duplicated while keeping the same total count.

- **Minor** — Fixture 10 nested-scope coverage is incomplete  
  **Location:** `Fixture10_ForEachBlocks_ProduceForEachScopesUnderProcedure`  
  **Recommended fix:** Assert the complete child-scope map: `CaptureSnapshot` and `LogChanges` each have exactly one `ForEachScope`, and every other `ProcedureScope` has no children. As written, extra unintended nested scopes under other procedures would pass.

- **Minor** — `TryFindLocal` test is weak for duplicate property names  
  **Location:** `AllFixtures_TryFindLocal_FindsAllModuleLevelDeclarations`  
  **Recommended fix:** Either:
  1. assert only distinct declaration names, or  
  2. use a name+kind-aware lookup/assertion if available.  
  In Fixture 10, `EntityName` Get/Let and `Enabled` Get/Let share names, so the same lookup result can satisfy multiple iterations and mask missing/incorrect accessor handling.

- **Minor** — Fixture 10 comment conflicts with the asserted procedure count  
  **Location:** comment block above `Fixture10_ClassModule_HasExpectedProcedureCount`  
  **Recommended fix:** Update the comment to match the asserted total of 14 procedure scopes, or clarify what the “11 Sub/Function/Property procedures” count excludes.