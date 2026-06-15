# AI Assignment — 017-studentict-local-invariant-corpus

> Per-session routing decisions + next-session recommendation. The orchestrator
> does not self-opine on model selection; the body below was authored via a routed
> `analysis` call (gemini-pro) per docs/ai-led-session-workflow.md Step 3.5.

## Session 1: Onboard StudentICTDatabase as a Local-Only Split FE/BE Invariant Corpus
### Recommended orchestrator
Claude Code on Opus 4.8 (1M context) at effort=high.

### Rationale
The session requires tracking multiple, precise implementation details for a new test class, deferred work items, and documentation updates based on a single complex goal. A high-effort, large-context model is best suited to manage these intertwined tasks without context degradation.

### Estimated routed cost
Low

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Author the new skip-gated xUnit test class `StudentIctLocalInvariantTests.cs`, mirroring the structure and intent of `NorthwindLocalInvariantTests.cs`. | In-conversation |
| 2 | Implement assertions per session goal: clean pipeline run, linked table structure (without asserting secrets), custom ribbon resolution, known unresolved references, and manifest schema validity. | In-conversation |
| 3 | Update `deferred-work.md` with DW-24 (Connect-string redaction) and a new DW-25 (missing linked-table metadata in manifest). | In-conversation |
| 4 | Document the observed `Relationships:0` count for split-FE databases in the appropriate invariant behavior documentation. | In-conversation |
| 5 | Confirm the HHC golden file is byte-level neutral, as no HHC-affecting producers were modified. | In-conversation |
| 6 | Conduct in-IDE devil's-advocate review of all generated code, documentation, and deferred-work items. | In-conversation |
| 7 | Perform session verification, including a review of the completed work against the session goal. | **Routed (gpt-5-4)** |
| 8 | Author the `ai-assignment.md` for the next session set, completing the session-set handoff. | In-conversation |

### Actuals (filled after the session)
**Session complete:** {YYYY-MM-DD}
**Cost:** {USD}
**Discrepancies:** {None}

**Next-session orchestrator recommendation:** This session set is complete. The final step produced the assignment for the subsequent session set.