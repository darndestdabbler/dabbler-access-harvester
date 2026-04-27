### Recommendation

**Recommended Slug:** `#14 vba-semantic-resolution-and-typing`

**Rationale:** With the specialized Jet SQL resolution in #13 complete, the critical path is now unblocked for core VBA semantic analysis. This next phase must focus on building the foundational components for VBA name resolution: constructing symbol tables from declarations, resolving identifier references to their declarations (variables, functions, properties), handling scope (module, procedure, block), and inferring types. This work is the prerequisite for any higher-level code intelligence, transformation, or cross-module analysis, and the SQL resolution artifacts from #13 will serve as inputs to this more comprehensive semantic model.

**Next orchestrator suggestion:** A high-capability orchestrator (e.g., Opus 4.7+) is required. This session involves complex synthesis of semantic analysis components, including symbol table management, scope resolution logic, and type system implementation, which aligns with the project's established use of top-tier models for design-heavy tasks.