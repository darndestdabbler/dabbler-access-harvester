**Next session-set recommendation (after parser-infrastructure):**
Execute session sets #7 and #12 in parallel.

### Execution Plan & Orchestrator Strategy

| Session Set | Plan Phase | Task | Orchestrator | Rationale |
| :--- | :--- | :--- | :--- | :--- |
| **#7** | 3.1 | Synthesis: Declaration model | **Opus-class** | High-complexity initial design and synthesis. |
| **#7** | 3.2 | Synthesis: Scope tree visitor | **Opus-class** | Complex logic for traversing AST and building scope. |
| **#7** | N/A | Verification & Refinement | **Sonnet-class** | Cost-efficient for tightening, testing, and mechanical changes. |
| **#12** | 5.1 | Synthesis: JET SQL grammar | **Opus-class** | High-complexity initial design; requires precision. |
| **#12** | 5.2 | Synthesis: SQL parser/AST/recovery | **Opus-class** | Complex logic mirroring VBA parser but for a new grammar. |
| **#12**| N/A | Verification & Refinement | **Sonnet-class** | Cost-efficient for grammar adjustments and test cases. |

### Supporting Rationale

1.  **Maximize Throughput:** Sets #7 and #12 are independent and unblocked. Executing them in parallel using separate worktrees is the most efficient path forward, enabling simultaneous progress on VBA semantic analysis and SQL parsing capabilities.
2.  **Resource Allocation:** The calibration data from set #6 provides a clear model for orchestrator selection. Use a high-capability, higher-cost orchestrator (Opus) for the initial, complex synthesis tasks where the core intellectual property is generated. Use a lower-cost, high-speed orchestrator (Sonnet) for subsequent mechanical refinement, verification, and testing rounds. This strategy optimizes for both quality of output and cost-efficiency.
3.  **Contingency (Sequential Execution):** If parallel execution is not feasible due to resource constraints, both paths are equally valid to start.
    *   **Start #7:** To vertically deepen the VBA analysis capabilities, building directly on the AST from #6.
    *   **Start #12:** To broaden the project's capabilities by introducing a new parser, de-risking the SQL-related workstream early.