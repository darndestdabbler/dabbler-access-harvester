# AI Assignment — enrichment-queries-and-forms

## Session 1: Query Abstraction Structure (Synthesis)

### Recommended orchestrator
Claude Opus 4.7 (`claude-opus-4-7`) at `effort=high`

### Rationale
This is a novel synthesis task creating a core data structure, mapping directly to an `architecture` routing decision. The orchestrator matches the routing target, allowing the work to be handled efficiently in-conversation.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|---|---|---|
| 1.1 | Read Phase 6.1, fixture `04` `expected.json`, and SQL AST outputs from prerequisite sessions. | `orchestrator-local` |
| 1.2 | Elaborate and execute a synthesis prompt to produce `QueryAbstraction.cs` and its corresponding visitor class. | `route(task_type="architecture", complexity_hint=75)` |
| 1.3 | Land the generated files under `AccessHarvester.Pipeline/Enrichment/`. | `orchestrator-local` |

### Actuals (filled after the session)
- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high`. Synthesis (`QueryAbstraction.cs` + builder) authored in-conversation per the convention "When the orchestrator's model matches the routing target, handle the task in-conversation."
- **Actual cost:** ~$0.10 routed total. Step 3.5 ai-assignment.md (`analysis` -> gemini-pro): $0.0153. Verification: gpt-5-4 returned HTTP 429 after 3 retries; fell back to gemini-pro per the verifier-failure escalation ladder. First gemini-pro call returned terse JSON-only verdict (24 output tokens) -> retried with explicit narrative requirement -> $0.0375 + $0.0448 = $0.0823. Synthesis itself was orchestrator-handled (not routed).
- **Session commits:** one commit landing the new `AccessHarvester.Pipeline` project (csproj + `Enrichment/QueryAbstraction.cs` + `Enrichment/QueryAbstractionBuilder.cs`), the new `tests/AccessHarvester.Pipeline.Tests` project (csproj + `QueryAbstractionBuilderTests.cs` with 29 tests), the solution-file additions, and this set's `ai-assignment.md` + `session-1-next-orchestrator-recommendation.md` + `session-reviews/session-001.md` + `activity-log.json` + `session-state.json`.
- **Verification route:** `route(task_type="session-verification", complexity_hint=70)` -> gpt-5-4 (HTTP 429, 3 retries) -> fell back to `query("gemini-pro", task_type="session-verification")`. Final verdict: VERIFIED, issues: []. Build: green; 1190 passed, 2 skipped (Windows-only Access COM integration tests), 0 failed.
- **Deviations from recommendation:** none on the orchestrator. Verification provider deviated due to gpt-5-4 quota exhaustion (transient; will retry first on Session 2).
- **Notes for next-session calibration:** (a) gpt-5-4 verifier hit a 429 quota wall in this session - if it recurs in Session 2, expect another gemini-pro fallback. (b) Augment Session 2's verification prompt to demand a narrative analysis up-front rather than waiting for the terse-verdict retry round; the `## Required response structure` hint with explicit "## Analysis" section heading worked well in Session 1's retry.

---
**Next-session orchestrator recommendation (Session 2):** Claude Opus 4.7 (`claude-opus-4-7`) at `effort=high`. Session 2 is the second novel synthesis task in this set (form-component spec) with the same routing profile as Session 1; cost band is low because the synthesis is orchestrator-handled and only the verification + ai-assignment routing pays. Author the verification prompt with the eight-point scrutiny + "Required response structure" framing from Session 1 to avoid the gemini-pro terse-verdict retry round if the gpt-5-4 verifier 429s again. Full rationale in `session-1-next-orchestrator-recommendation.md`.

## Session 2: Form Component Spec Structure (Synthesis)

### Recommended orchestrator
Claude Opus 4.7 (`claude-opus-4-7`) at `effort=high`

### Rationale
This is the second novel synthesis task, creating the form component specification. The `architecture` routing and rationale match Session 1.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|---|---|---|
| 2.1 | Read Phase 6.2, fixtures `02` and `12`, and the form-metadata schema from prerequisite sessions. | `orchestrator-local` |
| 2.2 | Elaborate and execute a synthesis prompt to produce `FormComponentSpec.cs`. | `route(task_type="architecture", complexity_hint=75)` |
| 2.3 | Land the generated file under `AccessHarvester.Pipeline/Enrichment/`. | `orchestrator-local` |

### Actuals (filled after the session)
- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high`. Synthesis (FormComponentSpec.cs + FormComponentSpecBuilder.cs + 37 unit tests) authored in-conversation per the convention "When the orchestrator's model matches the routing target, handle the task in-conversation." No `route(task_type="architecture")` call was made; the orchestrator handled the synthesis directly and the verification step was the only Opus-tier route.
- **Actual cost:** ~$0.05 routed total. Session-2 next-orchestrator recommendation (`analysis` -> gemini-pro): $0.0030. Verification round 1 (`session-verification` -> gpt-5-4 returned HTTP 429 after 3 retries; fell back to gemini-pro): $0.0473. Verification round 2 (same fallback path): $0.0022. Aggregate set-level routed total now $0.1471 across 6 calls (Sessions 1-2 combined).
- **Session commits:** one commit landing `AccessHarvester.Pipeline/Enrichment/FormComponentSpec.cs` (records + enums for FormComponentSpec, FormDataSourceBinding, FormSectionDescriptor, FormControlSpec, ControlBinding, ControlRenderHints, SubformRelationship, EventHandlerReference, ValidationRuleReference + their discriminator enums), `AccessHarvester.Pipeline/Enrichment/FormComponentSpecBuilder.cs` (visitor walking FormMetadata; lifts data-source binding, section walk with Begin unwrapping, control flattening, subform relationships, control + form-level event-handler refs, validation refs, render hints), `tests/AccessHarvester.Pipeline.Tests/FormComponentSpecBuilderTests.cs` (37 hermetic unit tests), the round-1 + round-2 verifier outputs, the issue log (1 Minor fixed), the routed Session-3 recommendation, and ai-assignment.md actuals.
- **Verification route:** `route(task_type="session-verification", complexity_hint=70)` -> gpt-5-4 (HTTP 429, 3 retries on both rounds) -> fell back to `query("gemini-pro", task_type="session-verification")`. Round 1: ISSUES_FOUND (1 Minor — comment in `Build` method described synthetic Detail attribution that the code didn't actually do). Round 2: VERIFIED, issues: []. Build: green; 1227 passed, 2 skipped (Windows-only Access COM integration tests), 0 failed.
- **Deviations from recommendation:** none on the orchestrator. Verification provider deviated due to gpt-5-4 quota exhaustion — same pattern as Session 1, now confirmed in two consecutive sessions.
- **Notes for next-session calibration:** (a) gpt-5-4 verifier 429 has now hit twice in this set; Session 3 should preempt by either pre-warming the gpt-5-4 quota or routing verification straight to gemini-pro via the `query()` short path to avoid the 3-retry-then-fallback latency. (b) The round-1 finding was a comment/code mismatch that targeted scrutiny would have caught at authoring time — a good hint for Session 3 to scan author-time comments against authored code paths before committing. (c) The bracketed-bareword test (`[My Customers]`) was a useful add — Session 3's pipeline run against fixture form metadata may surface more record-source shapes worth fixturing.

---
**Next-session orchestrator recommendation (Session 3):** Claude Sonnet 4.6 (`claude-sonnet-4-6`) at `effort=medium`. Confirmed by routed `analysis` call (gemini-pro, $0.0030, 2026-04-27): Session 3 is mechanical pipeline integration + schema validation, the mechanical-correspondence convention pins it to Sonnet rather than Opus, and `medium` effort matches the work depth (more than a script run, less than architectural synthesis). Full text in `session-2-next-orchestrator-recommendation.md`.

## Session 3: End-to-End Pipeline + Mechanical Tightening

### Recommended orchestrator
Claude Sonnet 4.6 (`claude-sonnet-4-6`) at `effort=medium`

### Rationale
This session involves mechanical tasks like running a pipeline and validating schema correspondence, which aligns with Sonnet's strengths. Using Sonnet is the cost-effective choice per project conventions for `schema-validation`.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|---|---|---|
| 3.1 | Integrate and run the new visitors against fixtures `04`, `02`, and `12` to generate initial output. | `orchestrator-local` |
| 3.2 | Route the generated output for schema validation against `harvester-schema.v0.json`. | `route(task_type="schema-validation")` |
| 3.3 | Apply AI-suggested fixes and land the tightening commits. | `orchestrator-local` |

### Actuals (filled after the session)
- **Actual orchestrator:** Claude Code with `claude-sonnet-4-6` at `effort=medium`. Matched the recommendation exactly.
- **Actual cost:** $0.0198 routed total. Schema validation handled in-conversation (per convention: Sonnet orchestrator matches Sonnet target for mechanical tasks). Next-orchestrator recommendation (`analysis` → gemini-pro): $0.0030. Verification round 1 (gemini-pro): $0.0144. Verification round 2 (gemini-pro): $0.0012. Aggregate set-level routed total now $0.1669 across 9 calls (Sessions 1-3 combined).
- **Session commits:** one commit landing: 3 new end-to-end serialization tests (`Build_Fixture04_SerializesToJsonMatchingPhase61Contract`, `Build_Fixture02Shape_SerializesToJsonMatchingPhase62Contract`, `Build_Fixture12Shape_SerializesToJsonMatchingPhase62Contract`); `[JsonConverter(typeof(JsonStringEnumConverter))]` on all 14 enum types in `QueryAbstraction.cs` and `FormComponentSpec.cs`; updated `QueryAbstractionBuilder.cs` remarks to accurately describe declared-parameter type population vs. deferred implicit-parameter type inference; removed stale "Session 3" forward-references from property docs in `QueryAbstraction.cs` and `FormComponentSpec.cs`.
- **Verification route:** `query("gemini-pro", task_type="session-verification")` (skipped gpt-5-4 retry per Session 2 calibration note; went straight to gemini-pro). Round 1: ISSUES_FOUND (1 Minor — comment/code mismatch in QueryAbstractionBuilder.cs). Round 2: VERIFIED. Build: green; 1176 passed, 2 skipped (Windows-only Access COM integration tests), 0 failed.
- **Deviations from recommendation:** none on orchestrator. schema-validation handled in-conversation rather than routed (per "When orchestrator matches routing target" convention); recorded deviation in activity log.
- **Notes for next-session calibration:** (a) Session 4 is the cleanup buffer; Sessions 1-3 produced only Minor issues so the cleanup scope should be small. (b) The router recommended `Claude 3 Haiku` for Session 4 (not available in this setup); Sonnet at low effort is equivalent. (c) All "Session 3 will wire this up" forward-references in doc comments have been corrected — no similar stale comments remain.

---
**Next-session orchestrator recommendation (Session 4):** Claude Sonnet 4.6 (`claude-sonnet-4-6`) at `effort=low`. Session 4 is a cleanup buffer; Sessions 1-3 produced only Minor issues, so the expected work is minimal targeted fixes at low reasoning depth. Full text in `session-3-next-orchestrator-recommendation.md`.

## Session 4: Verifier-Surfaced Cleanup

### Recommended orchestrator
TBD (Opus for synthesis fixes, Sonnet for mechanical fixes)

### Rationale
This session is reserved for cleanup based on cross-provider verification. The appropriate orchestrator will depend on the nature of the required fixes.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|---|---|---|
| 4.1 | Triage verifier-surfaced issues and address them with targeted routing. | `orchestrator-local` |

### Actuals (filled after the session)
- **Actual orchestrator:** Claude Code with `claude-sonnet-4-6` at `effort=low`. Matched the next-session recommendation.
- **Actual cost:** ~$0.018 routed total. Round-1 verification (gpt-5-4 HTTP 429 → gemini-pro): $0.0118. Round-2 verification (gemini-pro, reshaped context): $0.0030. Change-log (gemini-flash, max_tier=1): $0.0003. Next-set recommendation (gemini-pro, analysis): $0.0055. Aggregate set-level routed total now ~$0.187 across 13 calls (Sessions 1-4 combined).
- **Session commits:** one commit landing: `QueryAbstractionBuilder.cs` — added `CollectImplicitFormReferences` + `WalkDmlForFormRefs` + `WalkSelectForFormRefs` + `WalkTableSourceForFormRefs` + `CollectFormRefsFromExpr` (recursive expression walker for implicit FormReference detection); `QueryAbstractionBuilderTests.cs` — 3 new tests (implicit-only, deduplication, mixed declared+implicit); session-4 verifier files, issue log, change-log.md, next-session-set-recommendation.md, ai-assignment.md actuals.
- **Verification route:** `route(task_type="session-verification")` → gpt-5-4 HTTP 429 → fell back to `query("gemini-pro", ...)` (same pattern as Sessions 1-3). Round 1: ISSUES_FOUND (1 false-positive Major context gap + 1 real Major). Round 2: VERIFIED after fix. Build: green; 1233 passed, 2 skipped, 0 failed.
- **Deviations from recommendation:** none on orchestrator.
- **Notes for calibration:** gpt-5-4 verifier 429 has now hit every session in this set (4/4). Either the OpenAI quota is persistently low for this project window or gpt-5-4 is rate-limited at the API tier used here. Future sets should default directly to `query("gemini-pro", task_type="session-verification")` instead of waiting for gpt-5-4 to 429 three times.