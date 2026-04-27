## ISSUES_FOUND

- **Issue** → Reported resolver test totals are internally inconsistent.  
  **Location** → Session summary vs. provided resolver test inventory.  
  **Fix** → Re-run and reconcile `dotnet test` counts. `37` new fixture tests + stated existing `60` (`ScopeBuildingVisitorTests.cs`) + `23` (`DeclarationModelTests.cs`) implies `120` resolver tests, not `97`. If `97` is correct, one of the stated per-file test counts or project assignments is wrong.

- **Issue** → Form/report fixtures are being constructed as `ModuleKind.Standard`, which does not match their actual module type.  
  **Location** → `ScopeBuildingFixtureTests.cs`; default `BuildFromFixture(..., ModuleKind.Standard)` and all callers for fixtures `01`, `02`, `09`, and `12`.  
  **Fix** → Pass the real module kind for document modules (`Class` or the document-specific kind, if the enum has one) for `frmCustomers`, `frmCaseManagement`, `rptSummary`, and `frmMainSwitchboard`.

- **Issue** → The “bidirectional invariant” tests are not sound as written.  
  **Location** → `AllFixtures_ProcedureDeclarationAndScopeCountsMatch` and `AllFixtures_EachProcedureScopeHasDeclarationInModule` in `ScopeBuildingFixtureTests.cs`.  
  **Fix** → Assert correspondence by a discriminated key, not just count and name. Property `Get`/`Let` procedures share names, so count equality plus `Contains` by name does not prove a 1:1 declaration/scope match. Add the reverse check (`each ProcedureDeclaration has a matching ProcedureScope`) and include procedure kind/accessor in the comparison.

- **Issue** → Scope-shape validation is incomplete for fixtures documented as having no nested scopes.  
  **Location** → Fixture-specific tests for `01`, `02`, `09`, `11`, and `12` in `ScopeBuildingFixtureTests.cs`.  
  **Fix** → Add explicit assertions that the relevant `ProcedureScope.Children` collections are empty where the expected shape is “module → procedures only,” so the tests verify nesting shape rather than only top-level procedure counts.