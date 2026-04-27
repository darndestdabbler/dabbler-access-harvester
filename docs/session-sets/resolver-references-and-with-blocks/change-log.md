# Resolver References & With-Blocks

*   **Reference Resolution:** Implemented `ReferenceResolver` with scope-chain, object-model registry, and built-in fallback.
*   **Built-in Functions:** Added `BuiltInFunctionRegistry` for ~100 VBA/Access built-in functions.
*   **Resolution Outcomes:** Introduced `ResolutionOutcome` discriminated union (Resolved, ResolvedToObjectModel, ResolvedToBuiltIn, ResolvedToMe, Unresolved).
*   **With-Block Flattening:** Developed `With-block` flattening logic, including `ResolvedViaWithFlattening` outcome and nested `With` innermost-wins.
*   **ResolvedModule:** Created `ResolvedModule` for immutable AST, scope, references, and diagnostics, with `TryGetReference`/`TryGetDeclaration` lookups.
*   **Diagnostics:** Integrated `ResolverDiagnostic` for mapping reasons to severity.
*   **Test Suite Expansion:** Expanded resolver test suite to 195 specific tests for resolution provenance, With-blocks, `Me`/bang, and module variables.

---

**Next session-set recommendation (after resolver-references-and-with-blocks):**
`resolver-default-members-and-inference`

**Recommended orchestrator for `resolver-default-members-and-inference`:** claude-code Opus 4.7 @ effort=high

**Rationale:** Set #9 (Phase 3.5-3.7) implements default-member resolution and type inference — the two remaining synthesis tasks that replace the DefaultMemberNotYetResolved placeholders left by this set. Both features require novel algorithmic design (default-member registry, type inference pass), making Opus the right tier; this session set validated that the resolver's foundation is solid and the interfaces are stable, so the synthesis work in #9 can proceed without rework risk.