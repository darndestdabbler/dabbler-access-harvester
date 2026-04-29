### Concern 1
**Issue:** None.  
**Location:** `FqnResolver.cs`  
**Fix:** None needed.

The M1 fix is functionally consistent with the Session 1 local-symbol naming scheme. `DeclarationFqn` now uses `ProcedureFqn(ps)` as the base when a declaration belongs to a `ProcedureScope`, and then appends `.{declaration.Name}`. That produces:

- standard procedure local: `<Module>.<Proc>.<Local>`
- property accessor local: `<Module>.<Name>#get.<Local>`, `<Module>.<Name>#let.<Local>`, or `<Module>.<Name>#set.<Local>`

That is the correct extension of the existing procedure FQN scheme. The important point is that property procedures already carry the `#kind` suffix in their own symbol FQN, so locals under those procedures should naturally inherit that as part of the containing procedure segment. The implementation does exactly that.

The only thing slightly off is the explanatory comment saying `LocalVariableExtractor emits these as <ModuleName>.<ProcFqn>.<LocalName>`. If `ProcFqn` already includes the module segment, that wording is imprecise. The code itself is correct: it is effectively `<ProcFqn>.<LocalName>`, not `<ModuleName>.<ProcFqn>.<LocalName>`. This is a comment-quality issue, not a functional mismatch.

### Concern 2
**Issue:** None.  
**Location:** Session scope / upstream resolver contract  
**Fix:** None needed in this session.

The M2 dismissal is acceptable for this verification round. The key question is whether `variable_read` for writes is being emitted silently as if it were fully correct, or whether it is an explicitly acknowledged limitation caused by an upstream API constraint. Based on the round summary, it is the latter:

- the inability to distinguish writes comes from the upstream `ReferenceResolver` contract
- changing that contract is out of Session 2 scope
- the fallback to `variable_read` is intentional
- the limitation is documented

Given those facts, this is not a verification blocker for Session 2. It remains a known semantic limitation, but not a silent defect in the submitted fix set. If read/write distinction becomes required later, it should be handled as a new upstream/backlog item rather than treated as a regression here.

### Concern 3
**Issue:** Minor.  
**Location:** `ReferencesExtractorTests.cs`  
**Fix:** Strengthen the three new tests to assert fixture-specific presence before asserting shape.

The three added tests improve coverage directionally, but they do not fully close the gap identified in the original Minor finding because they are too permissive and can pass vacuously.

Specifically:

- `References_ContainEnumValueUseRows_WhenEnumMembersReferenced` filters `enum_value_use` rows and then only asserts that each such row has kind `enum_value_use`. If extraction regresses and produces zero such rows, the test still passes.
- `LocalVariable_ToFqn_IncludesProcedureSegment` returns early if the `strDlgMsg` reference is unresolved or has `ToFqn == null`. If the exact regression being guarded against reappears as a resolution failure, the test still passes.
- `References_BangAccessExpressions_HaveControlAccessKind` iterates only over existing `control_access` rows. If none are emitted, the test still passes.

Because the Northwind fixture is described as containing these patterns, these tests should assert that at least one target row exists and then validate the expected shape/value. As written, they verify formatting only conditionally, not actual coverage of the intended behaviors. That means the Minor finding is improved but not fully verified closed.

### Concern 4
**Issue:** No additional functional issues beyond the test-strength gap above.  
**Location:** `FqnResolver.cs`, overall Session 2 fix set  
**Fix:** Only the test hardening described above.

No new major or critical functional issue is evident in the M1 code change. The local-FQN behavior now aligns with the intended symbol naming scheme, including property accessors. The M2 dismissal is acceptable as a documented scope limitation. The remaining concern is confined to verification quality: the new tests do not yet fail reliably if those behaviors regress.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "ReferencesExtractorTests.cs",
      "summary": "The three new coverage tests are partially vacuous and can still pass if enum_value_use rows disappear, control_access rows are not emitted, or the local strDlgMsg reference stops resolving.",
      "fix": "Make the tests fixture-assertive: require at least one enum_value_use row, at least one control_access row, and a resolved strDlgMsg reference with non-null to_fqn, then validate the expected kind/FQN shape."
    }
  ]
}
```