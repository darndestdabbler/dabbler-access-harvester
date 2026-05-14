**Next Session Set:** `table-extractor-coverage`

This set is the logical next step. It is a foundational, incremental improvement that addresses specific, identified gaps in the C# `TableExporter`. Achieving near-parity with the YAML extractor for critical table schemas is lower-risk and provides more immediate value than starting the larger, more complex form/report extractor.

*   **Orchestrator:** `Claude Code / claude-opus-4-7 / effort=medium`
    *   **Rationale:** The task is a well-defined implementation on existing code, not a broad spike. A capable model at medium effort is efficient.
*   **Prerequisites:** Verify the `AccessHarvester.Core` project builds cleanly and the existing `TableExporter` tests pass.
*   **Estimated Sessions:** 1-2