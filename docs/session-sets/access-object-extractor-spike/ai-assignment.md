# AI Assignment — access-object-extractor-spike

## Session 1: Gap Analysis + Integration Strategy (Research)

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** Single-session research spike — complex analysis, no code
synthesis. Sonnet at `effort=high` matches the profile without Opus-level
cost. No `route()` call needed for the analysis itself if Sonnet handles
it in-conversation; route only for session-verification and summarization.
**Estimated routed cost:** Low–Medium

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read spec, project-guidance, lessons-learned, session-set authoring guide. | `In-conversation` |
| 2 | Read `scripts/run_extraction.ps1`, sample YAML files under `samples/DatabaseYaml/HomeHealthCare.accdb/`, and the four C# extractor source files. | `In-conversation` |
| 3 | Produce coverage comparison table + gap analysis (YAML fields vs. C# JSON fields per object type). | `route(task_type="analysis", complexity_hint=55)` |
| 4 | Produce integration-strategy recommendation using gap analysis as input. | `route(task_type="analysis", complexity_hint=60)` |
| 5 | Land session-001.md with both outputs. | `tool-use` |
| 6 | Run session-verification against the written analysis. | `route(task_type="session-verification", complexity_hint=50)` |
| 7 | Generate change-log. | `route(task_type="summarization", max_tier=1)` |
| 8 | Generate next-session-set recommendation. | `route(task_type="analysis", complexity_hint=30)` |

### Actuals (filled after the session)

*(To be completed by the orchestrator running Session 1.)*
