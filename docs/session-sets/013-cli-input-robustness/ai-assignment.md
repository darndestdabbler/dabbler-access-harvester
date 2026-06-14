### Session 1 of 2: Exit-Code Correctness

-   **Recommended orchestrator engine+model+effort**: Claude Code (Opus 4.8), Medium
-   **Rationale**: The task involves precise changes to argument validation and file path sanitization across multiple commands, with specific hermetic test requirements. This requires strong code comprehension and generation, fitting a medium-sized task for a top-tier model.
-   **Estimated routed cost**: moderate
-   **Execution plan**:
| Step | Action | Routing-Decision |
| :--- | :--- | :--- |
| 1 | Implement `--types` argument validation and `ArgumentException` for `extract`/`translate`/`run` (F-1-3). | Claude Code |
| 2 | Implement `SaveAsTextExporter` filename sanitization and collision guard (F-1-2). | Claude Code |
| 3 | Add/update hermetic test pins for both fixes. | Claude Code |
| 4 | Run quality gate: `dotnet build` + affected hermetic test projects. | Human |
| 5 | Cross-provider session verification: review implementation against requirements. | gpt-5-4 |

### Session 2 of 2: ParseOptions Consolidation, Tokenization, and Closeout

-   **Recommended orchestrator engine+model+effort**: Claude Code (Opus 4.8), Large
-   **Rationale**: This session combines wide-impact refactoring (O-1-4), a high-risk logic change that may affect golden files (F-1-6), and session closeout. The complexity and risk, especially of the tokenization fix, warrant a Large effort estimate.
-   **Estimated routed cost**: high
-   **Execution plan**:
| Step | Action | Routing-Decision |
| :--- | :--- | :--- |
| 1 | Refactor `ParseOptions` across 7 subcommands for shared unknown-switch rejection (O-1-4). | Claude Code |
| 2 | Implement `OleDbConnectionStringBuilder` (F-1-5) and bracket-aware `PARAMETERS` tokenization (F-1-6) fixes. | Claude Code |
| 3 | Run quality gate; assess tokenization fix for golden file regeneration impact. | Human |
| 4 | Perform session set closeout: disposition all work items, update changelog, author next session set (O-1-7). | Human |
| 5 | Cross-provider session verification: review refactoring and critical tokenization logic. | gpt-5-4 |

---
**Next-session recommendation**: 014-perf-and-correctness: Tackle next-highest-priority correctness issues and introduce initial performance-sensitive query optimizations.