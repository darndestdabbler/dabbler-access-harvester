## ISSUES_FOUND

---

### Issue 1 — Wrong Module Names in All Cross-Fixture Theory Tests

**Severity:** Major  
**Location:** All four `[Theory]` methods (`AllFixtures_ProcedureDeclaration...`, `AllFixtures_EachProcedureScope...`, `AllFixtures_TryFindLocal...`, `AllFixtures_AllNonRootScopes...`) — the `[InlineData]` rows

**Issue:** Every Theory data row passes synthetic placeholder names ("F01", "F02", "F09", "F10", "F11", "F12") as `moduleName`, while the corresponding individual `[Fact]` tests pass the real module names ("frmCustomers", "frmCaseManagement", "rptSummary", "clsAuditLogger", "modStartup", "frmMainSwitchboard"). The `moduleName` argument is forwarded to `visitor.BuildModuleScope(project, ast, moduleName, kind)`. If the scope builder uses the module name for any name-resolution logic (e.g., `Me` binding, cross-module reference, class constructor matching), these Theory tests will silently build a scope that differs from the one validated by the individual Facts — the structural invariants may still pass while masking real defects.

**Fix:**
```csharp
[InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Standard)]
[InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Standard)]
[InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Standard)]
[InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
[InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
[InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Standard)]
```
Apply identically to all four Theory methods.

---

### Issue 2 — Comment Arithmetic in `Fixture10_ClassModule_HasExpectedProcedureCount` is Off by One (and Mislabelled)

**Severity:** Minor  
**Location:** `Fixture10_ClassModule_HasExpectedProcedureCount`, the comment block

**Issue:** The comment enumerates six groups totalling 13, not 14:

| Comment group | Procedures listed | Count |
|---|---|---|
| "3 plain Subs that implement interface" | Class_Initialize, Class_Terminate, AttachToForm | 3 |
| "3 form-event Subs" | m_Form_BeforeUpdate, m_Form_AfterUpdate, m_Form_Close | 3 |
| "2 utility Subs" | CaptureSnapshot, LogChanges | 2 |
| "2 Property Get/Let pairs" | EntityName×2, Enabled×2 | 4 |
| "1 interface Property Get" | IAuditLogger_Enabled | 1 |
| **Omitted** | IAuditLogger_LogEvent | ~~0~~ **1** |
| **Total** | | **13** (comment says 14) |

The assertion `Assert.Equal(14, …)` is **correct** — all 14 procedures appear in the source. The comment simply omits `IAuditLogger_LogEvent` from its breakdown and consequently gets the sum wrong. Additionally, the label "3 plain Subs that implement interface" is wrong; only `IAuditLogger_LogEvent` implements the interface. The three Subs actually listed are lifecycle/attachment methods.

**Fix:**
```csharp
// Class_Initialize, Class_Terminate, AttachToForm (3 lifecycle/public Subs) +
// 3 event-sink Subs (m_Form_BeforeUpdate, m_Form_AfterUpdate, m_Form_Close) +
// 2 private utility Subs (CaptureSnapshot, LogChanges) +
// 1 interface implementation Sub (IAuditLogger_LogEvent) +
// 2 Property Get/Let pairs (EntityName, Enabled) = 4 Property procedures +
// 1 interface Property Get (IAuditLogger_Enabled)
// = 3 + 3 + 2 + 1 + 4 + 1 = 14 procedure scopes total
Assert.Equal(14, module.Children.Count);
```

---

### Issue 3 — `Fixture10_ModuleLevelVariables_AreRegistered` Only Validates Count and One Name

**Severity:** Minor  
**Location:** `Fixture10_ModuleLevelVariables_AreRegistered`

**Issue:** The test asserts `vars.Count == 4` and that one of the four carries `IsWithEvents` with `Name == "m_Form"`, but never asserts the names of the other three (`m_AuditEntityName`, `m_AuditEnabled`, `m_LastSavedSnapshot`). A renaming in the fixture or a parser regression that swapped one declaration would not be caught.

**Fix:**
```csharp
var names = vars.Select(v => v.Name).OrderBy(n => n).ToArray();
Assert.Equal(
    new[] { "m_AuditEnabled", "m_AuditEntityName", "m_Form", "m_LastSavedSnapshot" },
    names);
```

---

### Issue 4 — `Fixture02` Does Not Assert the Procedure Declaration Name

**Severity:** Minor  
**Location:** `Fixture02_ProducesOneProcedureScopeAndOneDeclaration`

**Issue:** The test confirms there is exactly one `ProcedureScope` child whose `.Procedure.Name` is `"cboCaseworker_AfterUpdate"`, but never asserts that a matching `ProcedureDeclaration` with that name exists in `module.Declarations`. If the visitor creates the scope under one name and registers the declaration under a different name the scope-name check passes while the declaration-name bug goes undetected. (Compare with Fixture01 which does assert `decl.Name`.)

**Fix:**
```csharp
var decl = Assert.IsType<SubDeclaration>(Assert.Single(
    module.Declarations.OfType<ResolverProcedureDeclaration>()));
Assert.Equal("cboCaseworker_AfterUpdate", decl.Name);
```