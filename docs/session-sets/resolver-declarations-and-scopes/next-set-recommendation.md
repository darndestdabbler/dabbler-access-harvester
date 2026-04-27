**Next session-set recommendation (after resolver-declarations-and-scopes):**
`resolver-references-and-with-blocks` and `jet-sql-grammar-and-parser` (run in parallel -- both are DAG roots)

**Recommended orchestrator for `resolver-references-and-with-blocks`:** Opus @ effort=high
**Rationale:** This set is on the critical path and requires significant synthesis for the core resolution algorithm and `with`-block flattening. A high-capability model is necessary to ensure a correct and performant design, minimizing costly rework later in the resolver chain.

**Recommended orchestrator for `jet-sql-grammar-and-parser`:** Opus @ effort=high
**Rationale:** This set involves complex synthesis of a formal grammar and robust parser error-recovery logic, which are foundational. A high-capability model is critical for generating a correct, maintainable, and efficient grammar and AST structure from the outset.