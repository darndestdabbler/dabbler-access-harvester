# Verification Round 1

- **Issue** → `ResolvedModule` does not satisfy the synthesis’ immutability contract. `References` keeps the caller-supplied list by alias, and `UnresolvedReferences` / `Diagnostics` expose raw `List<T>` instances behind `IReadOnlyList<T>`, so consumers can still mutate them and desynchronize those surfaces from `_byExpression` and each other.  
  **Location** → `AccessHarvester.Vba.Resolver/References/ResolvedModule.cs`  
  **Fix** → Freeze all three collections at construction time from a copied source: copy `references` first, build lookups/derived lists from that frozen copy, and expose `References`, `UnresolvedReferences`, and `Diagnostics` via `ReadOnlyCollection<T>` or `ImmutableArray<T>`-backed views.

- **Issue** → The new test suite does not cover several synthesis-required semantics. It never verifies `TryGetDeclaration` for `ResolvedToObjectModel` or `ResolvedViaWithFlattening`, does not use an actual AST node the walker skipped for the “no recorded reference” case, omits severity assertions for `WithBlockFlatteningDeferred` and `MemberAccessOnUnresolvedTarget`, and the additive-wrapper test does not assert exact reference equivalence on the same AST/scope pair.  
  **Location** → `tests/AccessHarvester.Vba.Resolver.Tests/References/ResolvedModuleTests.cs`  
  **Fix** → Add targeted tests using either a fake `IAccessObjectModelRegistry` or direct `ResolvedModule` construction to cover `ResolvedToObjectModel`, `ResolvedViaWithFlattening`, and every closed-set unresolved reason; use a real skipped AST node such as `IndexOrCallExpression`/`BinaryExpression`; and compare `Resolve(...).References` against `ResolveModule(...)` on the same inputs with exact per-entry equality.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Resolver/References/ResolvedModule.cs",
      "summary": "ResolvedModule exposes mutable collection instances and aliases the caller-supplied references list, violating the documented immutable-view contract and allowing drift between references, lookups, unresolved references, and diagnostics.",
      "fix": "Copy the incoming references into a frozen collection first, build all derived state from that copy, and expose References, UnresolvedReferences, and Diagnostics via read-only wrappers or immutable collections."
    },
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/References/ResolvedModuleTests.cs",
      "summary": "The tests do not cover key synthesis-required cases: ResolvedToObjectModel, ResolvedViaWithFlattening declaration peek-through, actual unvisited AST nodes, full closed-set diagnostic severity mapping, or exact same-input equivalence between ResolveModule and Resolve.",
      "fix": "Add targeted tests with a fake registry or direct ResolvedModule construction for the missing outcome variants and reason mappings, use a real non-emitting AST node for the null lookup case, and assert exact per-entry reference equivalence on the same AST/scope inputs."
    }
  ]
}
```

---

# Verification Round 2

No issues found.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
