## Session 1: Add AI Consumer Workflow Documentation
### Recommended orchestrator
large/powerful-reasoning @ effort=low
### Rationale
This session requires high precision on a multi-step technical sequence and a small but critical code modification. A powerful model is recommended not for creative reasoning, but to ensure first-time accuracy on details like the exact `--in` path and the static template literal syntax in C#. This minimizes the risk of introducing subtle errors that would break determinism tests or require manual correction, making it the most cost-effective approach by avoiding rework.
### Estimated routed cost
low
| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read and analyze `docs/harvester/interpretation-guide.md` and `Pipeline.cs`. | `local/file-reader` |
| 2 | Draft the new "Workflow for AI consumers" section for `interpretation-guide.md`. | `large/powerful-reasoning` |
| 3 | Draft the concise C# template literal modification for `Pipeline.cs`. | `large/powerful-reasoning` |
| 4 | Apply changes to the two files. | `local/agent-executor` |
| 5 | Execute the build/regeneration script for the golden-corpus `READ-ME-FIRST.md` files. | `local/agent-executor` |
| 6 | Run determinism and schema tests to confirm correctness. | `local/agent-executor` |

### Actuals (filled after the session)

## Session 2: Author Artifact Review Guide and Wire Editor Links
### Recommended orchestrator
medium/balanced-writer + large/powerful-reasoning @ effort=moderate
### Rationale
This session's tasks have differing capability requirements. Authoring the new documentation and wiring static URLs are standard technical writing tasks well-suited for a cost-effective, balanced model. However, resolving the "editor-hosting open decision" requires weighing options and making a reasoned choice, justifying a one-time escalation to a more powerful (and expensive) reasoning model. This hybrid routing strategy delivers the necessary capability for the critical decision point while minimizing cost for the bulk of the implementation work.
### Estimated routed cost
moderate
| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Analyze options and resolve the editor-hosting open decision. | `large/powerful-reasoning` |
| 2 | Draft the new `reviewing-the-feedback-artifacts.md` document with a table, based on the decision. | `medium/balanced-writer` |
| 3 | Draft the explicit AI-agent instruction to explain artifacts and link to the new guide. | `medium/balanced-writer` |
| 4 | Draft the code/JSON modifications to wire the chosen editor URLs into the output. | `medium/balanced-writer` |
| 5 | Apply all file changes. | `local/agent-executor` |
| 6 | Verify the new document's clarity and the correctness of the wired links. | `medium/balanced-writer` |
