# Verification Round 1

VERDICT: ISSUES_FOUND

## Issues

### Major — Non-root scopes can still be created with `Range == null`
- **Issue:** Feature 2 requires each scope to carry a source range, with `ProjectScope` the only synthetic exception. The visitor currently passes `Provenance.Range` straight through when constructing `ModuleScope`, `ProcedureScope`, `WithBlockScope`, and `ForEachScope`. In this repo’s current AST contract, `Provenance.Range` is still nullable, so these scopes can violate the contract on real inputs.
- **Location:** `AccessHarvester.Vba.Resolver/Scopes/ScopeBuildingVisitor.cs:81-89` (`BuildModuleScope`), `:308-312` (`EnterProcedure`), `:323-327` (`VisitWithStatement`), `:338-342` (`VisitForEachStatement`)
- **Fix:** Add a `ToSourceRange(Provenance)` helper, mirroring `ToSourceProvenance`, that synthesizes a degenerate `[Line, Column .. Column+1)` range when `p.Range` is null. Use that helper for every non-root scope constructor call so `ModuleScope`, `ProcedureScope`, `WithBlockScope`, and `ForEachScope` always have a non-null range. Add a test that builds AST nodes with `Range: null` and asserts the concrete scopes still get non-null ranges.

### Major — `Declare`/`Event` parameters claim `ModuleScope` ownership without being registered in `ModuleScope.Declarations`
- **Issue:** `VisitDeclareStatement` and `VisitEventDeclaration` set `p.Scope = _currentModule` directly instead of adding the parameter through the scope. That breaks the bidirectional ownership invariant documented on `Scope.AddDeclaration` and leaves the scope tree internally inconsistent: declarations whose `Scope` says “module” are absent from that module’s `Declarations` collection.
- **Location:** `AccessHarvester.Vba.Resolver/Scopes/ScopeBuildingVisitor.cs:242-244` (`VisitDeclareStatement`), `:271-273` (`VisitEventDeclaration`); invariant documented at `AccessHarvester.Vba.Resolver/Scopes/Scope.cs:47-55`
- **Fix:** Register declare/event parameters through an internal scope-owned path that keeps both directions synchronized. The minimal correction is to add each parameter with `_currentModule.AddDeclaration(p)` instead of assigning `p.Scope` directly. If later resolution must exclude these parameters from ordinary identifier lookup, represent that explicitly in resolver logic rather than leaving scope ownership inconsistent. Update the tests to assert both `parameter.Scope == module` and presence in `module.Declarations`.

---

# Verification Round 2

**VERDICT: VERIFIED**
