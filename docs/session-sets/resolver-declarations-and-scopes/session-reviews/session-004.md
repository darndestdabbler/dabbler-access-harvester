# Verification Round 1

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "ScopeBuildingVisitor.cs",
      "summary": "ExtractOptionState matches `Option Compare` arguments case-sensitively, so valid directives such as `Option Compare text` or `OPTION COMPARE DATABASE` will incorrectly fall back to `Binary`.",
      "fix": "Canonicalize `od.Argument` with `StringComparison.OrdinalIgnoreCase` (or normalize once before the switch) and add mixed-case `Option Compare` tests in `ScopeBuildingVisitorTests.cs`."
    },
    {
      "severity": "Minor",
      "location": "DeclarationModelTests.cs; ScopeBuildingVisitorTests.cs",
      "summary": "Parameter coverage exercises ByVal and Optional, but does not explicitly pin ByRef and ParamArray preservation, leaving part of the Phase 3.1 contract untested.",
      "fix": "Add declaration-model and scope-builder tests that construct ByRef and ParamArray parameters and assert `PassMode`, `Modifier`, and scope ownership remain unchanged."
    },
    {
      "severity": "Minor",
      "location": "ProcedureScope.cs; ScopeBuildingVisitor.cs",
      "summary": "Comments still state that Declare/Event parameters attach to ModuleScope with a ModuleScope back-pointer, which contradicts the implemented and tested invariant that their `Scope` stays null.",
      "fix": "Update the comments to match the shipped contract: Declare/Event parameters are exposed only via `decl.Parameters`, are not added to `ModuleScope.Declarations`, and keep `Scope = null`."
    }
  ]
}
```

---

# Verification Round 2

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
