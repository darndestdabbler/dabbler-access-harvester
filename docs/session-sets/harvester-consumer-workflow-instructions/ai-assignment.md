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
Orchestrator: claude-code / Opus 4.8 @ effort=medium (Session 1, 2026-06-01).
Workflow section authored in-conversation by the Opus orchestrator (full context
on the exact `--in` path and the C# template literal). Cross-provider verification
routed to gpt-5-4 (OpenAI): ISSUES_FOUND round 1 (one Minor self-contradiction,
one Major premature-claim), VERIFIED round 2 after fixes. The "balanced-writer for
prose" recommendation was not used because the section's value was correctness on
exact details, which the orchestrator already held — consistent with the
"Opus authors synthesis docs in-conversation" convention.

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

### Actuals (filled after the session)
Orchestrator: claude-code / Opus 4.8 @ effort=medium (2026-06-01).

The "editor-hosting open decision" was escalated to **cross-provider consensus**
(gpt-5-4 + gemini-pro, bias-cautions preamble; journal in
`ai_router/consensus-decisions.jsonl`) rather than AskUserQuestion — both
providers agreed on every point, so no human call was needed. Outcomes: (a) defer
classification-editor GitHub Pages hosting, document local-open; (b) confirm
uat-checklist-editor for both checklists, after fixing a discovered bug.

Scope expanded beyond pure documentation: investigation of open-decision (b)
("verify the format matches") surfaced that `object-use-checklist.json` was
serialized camelCase and would load empty in the PascalCase-only editor. Fixed
`UsageChecklistWriter` (mechanical, hermetic — handled directly, ~15 lines),
updated the unit test that encoded the bug, regenerated the two golden samples via
the hermetic `identify-usage` path.

Routing actuals:
- Developer guide draft → `route(documentation)` → gemini-pro ($0.0111); the
  routed draft's own auto-verification (gpt-5-4-mini) flagged two minor misses
  (omit `sanitize`, missing "same format" note) which were folded into the final
  hand-assembled file.
- Next-set recommendation → `route(analysis)` → gemini-pro ($0.0034).
- Consensus → 2× `query(analysis)` → gpt-5-4 ($0.0215) + gemini-pro ($0.0031).
- §3.3 AI-agent instruction + READ-ME-FIRST wiring authored in-conversation
  (exact URLs / mandatory-rule phrasing the orchestrator held).
