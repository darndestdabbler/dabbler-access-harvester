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
- **Actual orchestrator:**
- **Actual cost:**
- **Session commits:**
- **Verification route:** `route(task_type="session-verification", complexity_hint=70)` to `gpt-5-4`

---
**Next-session orchestrator recommendation (Session 3):** Claude Sonnet 4.6 (`claude-sonnet-4-6`) at `effort=medium`. Session 3 is a mechanical tightening task, which is Sonnet-pinned and does not require Opus.

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
- **Actual orchestrator:**
- **Actual cost:**
- **Session commits:**
- **Verification route:** `route(task_type="session-verification", complexity_hint=70)` to `gpt-5-4`

---
**Next-session orchestrator recommendation (Session 4):** TBD based on verifier findings. Use Opus for synthesis/redesign issues, or Sonnet for mechanical fixes.

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
- **Actual orchestrator:**
- **Actual cost:**
- **Session commits:**
- **Verification route:** `route(task_type="session-verification", complexity_hint=70)` to `gpt-5-4`