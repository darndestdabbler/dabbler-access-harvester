## Changelog: Resolver Declarations and Scopes (Phase 3.1-3.2)

**Completed:** 2026-04-27

This session set implements the core declaration model and scope tree for the VBA Resolver, completing Phases 3.1 and 3.2 of the resolver plan.

### What's New

*   **Declaration Model:** Introduced a comprehensive C# type hierarchy (`Declaration`, `VariableDeclaration`, `ConstantDeclaration`, `ProcedureDeclaration` and its subtypes, `TypeDeclaration`, `EnumDeclaration`, `EventDeclaration`, `ClassDeclaration`, `ParameterDeclaration`, `DeclareDeclaration`, `ImplicitDeclaration`, `DeclarationProvenance`, `AccessLevel`) for representing VBA declarations.
*   **Scope Tree and Visitor:** Implemented the full scope hierarchy (`Scope`, `ProjectScope`, `ModuleScope`, `ProcedureScope`, `WithBlockScope`, `ForEachScope`) and the `ScopeBuildingVisitor` for atomically constructing the scope tree from parsed VBA ASTs. Includes `AccessLevelCanonicalizer` for mapping parser access modifiers.
*   **Comprehensive Testing:** Added 49 end-to-end fixture tests, 60 unit tests for the declaration model, and 41 unit tests for the scope building visitor. Total resolver tests: 112. All tests across resolver, parser, grammar, and harvester are green.
*   **Refinements and Fixes:** Addressed minor issues including `Option Compare` case-insensitivity, and added `ByRef`/`ParamArray` parameter tests.

### Downstream Impact

This foundational work directly enables subsequent resolver phases, specifically:
*   `#8 resolver-references-and-with-blocks` (resolving symbol references).
*   `#9 resolver-default-members-and-inference` (handling default members and type inference).

---

**Next session-set recommendation (after resolver-declarations-and-scopes):**
`resolver-references-and-with-blocks` and `jet-sql-grammar-and-parser` (run in parallel -- both are DAG roots)

**Recommended orchestrator for `resolver-references-and-with-blocks`:** claude-code Opus @ effort=high
**Rationale:** This set is on the critical path and requires significant synthesis for the core resolution algorithm and with-block flattening. A high-capability model is necessary to ensure a correct and performant design, minimizing costly rework later in the resolver chain.

**Recommended orchestrator for `jet-sql-grammar-and-parser`:** claude-code Opus @ effort=high
**Rationale:** This set involves complex synthesis of a formal grammar and robust parser error-recovery logic, which are foundational. A high-capability model is critical for generating a correct, maintainable, and efficient grammar and AST structure from the outset.