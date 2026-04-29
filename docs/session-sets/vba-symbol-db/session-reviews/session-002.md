1. **FqnResolver correctness.** `ContainingFqn` is correct for `from_fqn`: it walks outward through nested scopes until it reaches the enclosing `ProcedureScope` or `ModuleScope`, and the property-procedure handling preserves the required `#get/#let/#set` suffixes. The problem is `DeclarationFqn`, not `ContainingFqn`: for every non-property declaration it emits `"<Module>.<Name>"`, which is wrong for procedure locals and parameters because Session 1’s FQN scheme for those symbols is `"<Module>.<Proc>.<Local>"`. It also only extracts module names from `ModuleScope`/`ProcedureScope`, so declarations owned by deeper nested declaration scopes can come back `null`. Result: resolved references to locals/parameters can persist a `to_fqn` that does not match `Symbols.fqn`.

2. **ClassifyKind dispatch.** Mapping `VariableDeclaration` and `ParameterDeclaration` to `variable_read` is conservative, but not semantically complete. If `ResolvedModule.References` includes assignment targets, the current extractor will still label them as reads because it never inspects l-value/write context; `variable_write` is therefore never produced by the shown code path. That is acceptable only if Session 2 explicitly defers write/read separation. If the schema contract expects `variable_write` now, this is a correctness gap.

3. **`caller_kind` heuristic vs the broader EventBindings idea.** For Session 2, the implementation matches the written requirement exactly: Form/Report module plus `'_'` in the procedure name yields `ui-event-handler`; all other procedures yield `non-ui`; module-level references yield `null`. That is sufficient for this session even if a later phase replaces the heuristic with a join against authoritative event-binding data. No Session 2 defect here.

4. **ImplicitWithMemberExpression with non-`ResolvedViaWithFlattening` outcome.** The fallback behavior is reasonable. It still emits a `MemberAccessRow`, preserves the accessed member name, and uses `"<with-block>"` when the receiver cannot be reconstructed. That loses receiver fidelity, but it avoids dropping the access entirely and still satisfies the persistence requirement. No blocker here.

5. **`to_fqn = null` for `ResolvedToObjectModel`.** This is correct if object-model declarations are synthetic and absent from the `Symbols` table, which is consistent with the comments and the Session 1 design. The row still carries meaning via `resolution_status = resolved-to-object-model`, so the information is not lost. No issue here.

6. **FK constraint on `from_fqn`.** Using the enclosing procedure/module FQN for `from_fqn` is the right design. A reference row should point back to the caller symbol, not to a local symbol that happened to contain the expression. `ContainingFqn` supports that correctly. The real integrity risk is not `from_fqn`; it is the incorrect `to_fqn` generation for locals/parameters from concern 1.

7. **Test coverage gaps.** The tests prove that extraction and persistence broadly work, and they cover the probe target and caller-kind heuristic well. But they do not pin several important Session 2 branches: `enum_value_use`, `resolved-to-built-in`, `control_access`/bang-access kind classification, and correct `to_fqn` generation for local/parameter references. Because the probe test for `strDlgMsg` only checks identifier presence, it would not detect the `DeclarationFqn` mismatch. This is a real verification gap.

8. **Backward compatibility of optional `Write()` params.** The new optional parameters are backward compatible for existing three-argument callers, and the null-guarded writes preserve prior behavior. The passing test run is consistent with that. No issue here.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "FqnResolver.cs",
      "summary": "DeclarationFqn does not generate Session 1-compatible FQNs for procedure-local declarations and parameters, so reference to_fqn values can be wrong or null.",
      "fix": "Walk the declaration's scope upward to the owning ProcedureScope/ModuleScope and emit local/parameter FQNs as <Module>.<Proc>.<Local>; preserve #get/#let/#set for property procedures and add tests that assert correct to_fqn values for local variables such as strDlgMsg and for parameters."
    },
    {
      "severity": "Major",
      "location": "ReferencesExtractor.cs",
      "summary": "ClassifyKind never distinguishes variable writes, so assignment targets are persisted as variable_read.",
      "fix": "Detect write context from the AST parent/statement shape or extend ResolvedReference with an access-mode flag, then map assignment targets to variable_write while keeping read sites as variable_read."
    },
    {
      "severity": "Minor",
      "location": "ReferencesExtractorTests.cs",
      "summary": "The tests do not cover enum_value_use, resolved-to-built-in, control_access kind rows, or correct local/parameter to_fqn mapping.",
      "fix": "Add fixture-based assertions for enum-member references, built-in function references, bang-access/control_access rows, and local/parameter to_fqn round-trips so Session 2 edge cases are explicitly verified."
    }
  ]
}
```