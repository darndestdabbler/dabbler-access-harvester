# AI Assignment — enrichment-functions-navigation-review

### Session 1 of 4-5: Function Classification (Synthesis)

### Recommended orchestrator
`claude-opus-4-7`

### Rationale
This is the first of two novel synthesis tasks, routing to `architecture` to create `FunctionClassification.cs`. Per convention, using Opus as the orchestrator allows this primary task to be handled in-conversation, which is more efficient and leverages Opus's strength in synthesis and code generation from a complex specification.

### Estimated routed cost
High. The main synthesis task is in-conversation, but session verification routes to a workhorse model and the initial change-log summarization hits a high tier before any fallback. The `architecture` task type is also uncalibrated, representing a cost risk.

| Task type | Routing Decision | Rationale |
|---|---|---|
| `architecture` | In-conversation (no `route()` call) | Orchestrator matches the designated model for this task type. |
| `session-verification` | `query("gemini-pro", ...)` | Per calibration data, GPT-5.4 is hitting quota limits. Fallback directly to Gemini Pro to avoid latency. |
| `summarization` | `query("gemini-flash", ...)` | Low-stakes task suitable for the cheapest available model. |

### Actuals (filled after the session)
- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high`. Synthesis (`FunctionClassification.cs` + `FunctionClassificationBuilder.cs` + 47 hermetic xUnit tests + `AssemblyInfo.cs` for `InternalsVisibleTo`) authored entirely in-conversation per the convention "When the orchestrator's model matches the routing target, handle the task in-conversation."
- **Actual cost:** ~$0.110 routed total. ai-assignment authoring (`analysis` -> gemini-pro): $0.0172. Verification round 1 (`query("gemini-pro", task_type="session-verification")` per prereq-set calibration): $0.0333. Round 2: $0.0316. Round 3: $0.0229. Next-session recommendation (`analysis` -> gemini-pro): $0.0046. Synthesis itself was orchestrator-handled (not routed). Cross-set total now $0 because router-metrics records came in under non-zero per-call but this session set's tracking begins fresh — see `router-metrics.jsonl` for the per-call audit.
- **Session commits:** one commit landing: new `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs` (records + 4 enums), `AccessHarvester.Pipeline/Enrichment/FunctionClassificationBuilder.cs` (visitor + complexity collector + transitive-closure BFS + sort helpers), `AccessHarvester.Pipeline/AssemblyInfo.cs` (InternalsVisibleTo for tests), `tests/AccessHarvester.Pipeline.Tests/FunctionClassificationBuilderTests.cs` (47 hermetic tests), csproj updates on Pipeline (+ Vba.Parser, + Vba.Resolver references) and Pipeline.Tests (+ same), three rounds of verifier review files, the round-1+2+3 issue log (1 Major + 3 Minor, all fixed), session-1-next-orchestrator-recommendation.md, ai-assignment.md actuals, activity-log.json, session-state.json.
- **Verification route:** `query("gemini-pro", task_type="session-verification")` for all three rounds — per the prereq set's calibration, gpt-5-4 has 429'd on every prior session, so we skipped straight to gemini-pro. Round 1: ISSUES_FOUND (1 Major non-determinism + 1 Minor missing test coverage; both fixed). Round 2: ISSUES_FOUND (1 Minor doc-omission of the new sort guarantee; fixed). Round 3: ISSUES_FOUND (1 Minor doc-symmetry — Callers missing the self-skip note that DirectDependencies got; fixed without a fourth verifier round, retry budget being max-2-retries). All Critical/Major issues resolved per Rule 6. Build: green; 1278 passed, 4 skipped (Windows-only Access COM integration tests), 0 failed.
- **Deviations from recommendation:** none on the orchestrator. Verification provider deviated from the spec's hint by going straight to gemini-pro instead of attempting gpt-5-4 first — the prereq set's 4/4 429 rate made the preemption clearly justified.
- **Notes for next-session calibration:** (a) gpt-5-4 verifier still appears throttled; Session 2 should default to `query("gemini-pro", ...)` from round 1. (b) Round 1 caught a real Major (non-determinism) that came from HashSet iteration leaking into output; Session 2's NavigationGraphBuilder will likely have the same risk on its node and edge collections — proactively sort. (c) Round 1 also caught the missing dispatch-arm coverage; Session 2 should pre-emptively author Theory tests for every category dispatch in the navigation classifier. (d) Verifier reliably surfaces doc-symmetry inconsistencies (Callers vs. DirectDependencies); when Session 2 introduces parallel facets, write the docs in parallel from the start.

---
**Next-session orchestrator recommendation (Session 2):** Claude Opus 4.7 (`claude-opus-4-7`) at `effort=high`. Confirmed by routed `analysis` call (gemini-pro, $0.0046, 2026-04-27). Session 2 has the same routing profile as Session 1 (novel synthesis routed to `architecture`); the matching-orchestrator convention again makes in-conversation authoring the right call. Full text in `session-1-next-orchestrator-recommendation.md`.

### Session 2 of 4-5: Navigation Graph (Synthesis)

### Recommended orchestrator
`claude-opus-4-7`

### Rationale
This is the second novel synthesis task (`NavigationGraph.cs`), also routing to `architecture`. Maintaining Opus as the orchestrator is consistent with the task profile and allows it to leverage the context and patterns established in Session 1 for a cohesive implementation.

### Estimated routed cost
High. Same cost profile as Session 1: in-conversation synthesis with routed verification.

| Task type | Routing Decision | Rationale |
|---|---|---|
| `architecture` | In-conversation (no `route()` call) | Orchestrator matches the designated model for this task type. |
| `session-verification` | `query("gemini-pro", ...)` | Continue avoiding GPT-5.4 due to quota issues. |

### Actuals (filled after the session)
- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high`. Synthesis (`NavigationGraph.cs` records + `NavigationGraphBuilder.cs` visitor + 47 hermetic xUnit tests) authored entirely in-conversation per the matching-orchestrator convention, replicating Session 1's pattern.
- **Actual cost:** ~$0.044 routed total. Verification round 1 (`query("gemini-pro", task_type="session-verification")`): $0.0394. Next-session recommendation (`analysis` -> gemini-pro): $0.0048. No other routed calls — synthesis was orchestrator-handled.
- **Session commits:** one commit landing: new `AccessHarvester.Pipeline/Enrichment/NavigationGraph.cs` (record types + 4 enums + entry-point/edge/unresolved-target/ribbon/switchboard/macro shapes + `NavigationGraphInputs`), `AccessHarvester.Pipeline/Enrichment/NavigationGraphBuilder.cs` (CallStatement-walking visitor + DoCmd dispatch detector + literal/unresolved target classifier + `ExpressionTextFormatter` + `BuildEntryPoints` + sort helpers for determinism), `tests/AccessHarvester.Pipeline.Tests/NavigationGraphBuilderTests.cs` (47 hermetic tests), session-002 verifier review, session-2-next-orchestrator-recommendation.md, ai-assignment.md actuals, activity-log.json, session-state.json.
- **Verification route:** `query("gemini-pro", task_type="session-verification")` per Session-1 calibration (gpt-5-4 still throttled). Round 1: VERIFIED, issues=[]. Build: green; 1317 passed, 4 skipped (Windows-only Access COM integration tests), 0 failed. Pipeline.Tests grew from 111 to 158 (+47).
- **Deviations from recommendation:** none on the orchestrator or verification route.
- **Notes for next-session calibration:** (a) Verifier returned only the JSON block (24 output tokens, no preceding prose analysis) despite the prompt's request that prose precede JSON. The JSON-block-present + VERIFIED outcome cleared commit per Rule 6, but the terseness is worth tracking — if it recurs, consider strengthening the prompt's "prose before JSON" framing. (b) Session 1's pre-emptive sort guards paid off — every output collection sorted before return; no determinism issues surfaced. (c) Session 1's lesson about pre-emptive Theory tests for every dispatch arm paid off — Session 2 included a 5-row `[Theory]` over all `ProcedureKind` values, no missing-coverage findings.

---
**Next-session orchestrator recommendation (Session 3):** Claude Opus 4.7 (`claude-opus-4-7`) at `effort=high`. Confirmed by routed `analysis` call (gemini-pro, $0.0048, 2026-04-28). Recommendation revises Session 1's earlier proposal: although Session 3 is labelled `analysis`, the unification of every prior set's unresolved-emission shape into a unified `UnresolvedItem.cs` carries enough synthesis weight that Opus-as-orchestrator with in-conversation authoring is the safer, cheaper outcome (Session 2 cost ~$0.04 total). Full text in `session-2-next-orchestrator-recommendation.md`.

***

### Session 3 of 4-5: Manual Review Routing + Aggregator

### Recommended orchestrator
`gemini-pro-2-5`

### Rationale
The primary task shifts from novel synthesis to `analysis`: aggregating unresolved item shapes from prior sets. This is a well-defined integration task that does not require a frontier model. Gemini Pro provides the necessary analytical capability at a lower cost than Opus.

### Estimated routed cost
Medium. The main task is handled by a workhorse model, and verification is routed cross-provider to a frontier model, balancing cost and quality.

| Task type | Routing Decision | Rationale |
|---|---|---|
| `analysis` | In-conversation (no `route()` call) | Orchestrator matches the designated model for this task type. |
| `session-verification` | `query("claude-opus-4-7", ...)` | Cross-provider verification against the Gemini-driven session provides a robust check. |

### Actuals (filled after the session)
_To be filled after the session._

**Next-session orchestrator recommendation (Session N+1):** `claude-sonnet-4-6`

***

### Session 4 of 4-5: End-to-End + Mechanical Tightening

### Recommended orchestrator
`claude-sonnet-4-6`

### Rationale
This session is focused on mechanical pipeline integration and `schema-validation`. Per convention, these mechanical-correspondence tasks are pinned to Sonnet for its speed, low cost, and reliability on structured tasks.

### Estimated routed cost
Low. The orchestrator is a cheap workhorse, and the primary tasks are handled in-conversation. The only significant external cost is verification.

| Task type | Routing Decision | Rationale |
|---|---|---|
| `schema-validation` | In-conversation (no `route()` call) | Orchestrator is the pinned model for this mechanical task. |
| `session-verification` | `query("gemini-pro", ...)` | Standard cross-provider verification, avoiding GPT-5.4. |

### Actuals (filled after the session)
_To be filled after the session._

**Next-session orchestrator recommendation (Session N+1):** `claude-sonnet-4-6`

***

### Session 5 of 4-5 (optional buffer): Verifier-Surfaced Cleanup

### Recommended orchestrator
`claude-sonnet-4-6`

### Rationale
This buffer session addresses verifier-surfaced issues, which are typically mechanical fixes, refactorings, or minor logical adjustments. Sonnet is the ideal orchestrator for this type of cleanup work due to its cost-effectiveness and speed. A human can override this choice if the required fix involves significant re-synthesis.

### Estimated routed cost
Low to Medium. The cost depends on the number and complexity of the cleanup tasks. Assumes workhorse models for both orchestration and verification.

| Task type | Routing Decision | Rationale |
|---|---|---|
| `refinement`/`analysis` | In-conversation (no `route()` call) | Cleanup tasks are well-suited to a workhorse model. |
| `session-verification` | `query("gemini-pro", ...)` | Final verification of fixes. |

### Actuals (filled after the session)
_To be filled after the session._