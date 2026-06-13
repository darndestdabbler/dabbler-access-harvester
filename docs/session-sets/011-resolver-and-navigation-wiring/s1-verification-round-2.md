A re-assessment of both R1 findings comes out clean on the evidence provided.

For **A**, I would close both sub-points:

- **A1 (scope-build failure leaking a partial ModuleScope):** the rebuttal is sufficient. The key point is not just the new comment, but the reachability argument: if `Module.Items` can only contain `ModuleItem`, and `LocalDeclarationStatement` / `LocalConstantStatement` are `Statement` rather than `ModuleItem`, then the only cited post-attach guards cannot be reached from a parser-produced module root. The additional traversal invariant — those statement visitors run only after entering a procedure scope — reinforces that `CurrentProcedureScope` is non-null on real input. Absent a concrete parser-produced AST that violates those invariants, I do not have a basis to keep the “partial-scope leak” finding open. The parse-failure path occurring before `BuildModuleScope` further removes the main realistic failure class from this concern.

- **A2 (resolve failure leaves module visible to siblings):** I agree this is correct-as-is, not contamination. If pass 1 completed, the module scope already reflects the source declarations. A later pass-2 failure does not make those public declarations fictitious; it only means that module’s own resolution/output degraded. Allowing sibling modules to resolve against real public symbols from source is semantically appropriate under the stated degradation model.

For **B**, the new hermetic pin discharges the concern. The original brittleness claim depended on `owningClass.Name` aligning only accidentally with form metadata naming. Your evidence says that alignment is structural for `Name`, while `ModuleName` divergence is tolerated by design via fallback from the by-`ModuleName` index to the by-`Name` index. The new test directly exercises the previously suspicious case (`ModuleName != form/file name`) and demonstrates successful resolution through the fallback path. That is enough to retire the finding.

I cannot name a concrete parser-producible input, from the evidence given, that makes `BuildModuleScope` throw after attaching the scope. So I would not re-raise A1.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```