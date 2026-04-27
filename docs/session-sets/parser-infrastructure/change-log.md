### New Features

- **Initial VBA Parser Infrastructure:** Introduced `AccessHarvester.Vba.Parser` project, providing the core pipeline, error recovery strategies, and diagnostic collection for parsing VBA code. This is foundational for all subsequent VBA analysis.
- **Abstract Syntax Tree (AST) Generation:** Implemented a comprehensive AST for VBA, including modules, procedures, 22 statement types (e.g., `IfStatement`, `ForStatement`, `AssignmentStatement`), and ~25 expression types. This structured representation is critical for semantic analysis and refactoring tools.
- **Source Location Tracking:** Added `SourceLocation` and `SourceRange` to AST nodes, enabling precise mapping between parsed code and its original source. This is crucial for diagnostics, IDE integrations, and user-facing error reporting.
- **Preprocessor Directive Support:** Extended the parser and AST to include support for in-body preprocessor directives (`#If`, `#Else`, `#End If`, `#Const`), preserving critical conditional compilation logic in the AST.
- **VBA Parser Corpus & Schema Validation:** Integrated new parser tests and updated the `harvester-schema.v0.json` to include `preprocessorStatement` for consistent output validation across the system.

### Improvements

- **Robust Error Recovery:** Developed `StatementBoundaryErrorStrategy` to improve parser resilience and diagnostic quality by syncing on key language constructs.
- **Extensible AST Traversal:** Provided `VbaSyntaxWalker` and `VbaSyntaxVisitor<T>` for easy traversal and manipulation of the AST, supporting various analytical passes.
- **Schema Alignment:** Corrected schema validation issues in test fixtures, ensuring consistency and correctness of generated harvester data.

### Outputs

- `AccessHarvester.Vba.Parser/` project: Core VBA parsing capabilities.
- `AccessHarvester.Vba.Parser/Ast/`: Full AST type hierarchy, builder, walker, and visitor.
- `docs/test-cases/13-preprocessor.bas` and `13-preprocessor.expected.json`: Test fixture for preprocessor parsing.
- `docs/session-sets/parser-infrastructure/error-recovery-strategy.md`: Documentation on error recovery.
- `docs/session-sets/parser-infrastructure/ast-shape.md`: Documentation on AST structure.
- `harvester-schema.v0.json`: Updated schema with `preprocessorStatement` kind.

---

**Next session-set recommendation (after parser-infrastructure):**
Execute session sets `resolver-declarations-and-scopes` (#7) and `jet-sql-grammar-and-parser` (#12) in parallel.

**Recommended orchestrator for `resolver-declarations-and-scopes`:** claude-code Opus 4.7 @ effort=max (synthesis sessions); claude-code Sonnet 4.6 @ effort=low (mechanical sessions)
**Rationale:** Synthesis-heavy — declaration model and scope tree visitor both require Opus-class reasoning. Calibration from parser-infrastructure confirms Opus for synthesis, Sonnet for tightening.

**Recommended orchestrator for `jet-sql-grammar-and-parser`:** claude-code Opus 4.7 @ effort=max (synthesis sessions); claude-code Sonnet 4.6 @ effort=low (mechanical sessions)
**Rationale:** JET SQL grammar and SQL parser/AST design are new-domain synthesis of similar complexity to Sessions 1-2 of this set. Parallel execution is safe — independent worktrees, no shared prereqs.