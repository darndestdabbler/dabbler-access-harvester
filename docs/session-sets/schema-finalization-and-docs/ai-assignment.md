# AI Assignment: schema-finalization-and-docs

This document outlines the step-by-step plan for the `schema-finalization-and-docs` session set.

## Session 1 of 3: Schema v0→v1 Review Pass (Synthesis)

### Recommendations

-   **Orchestrator**: Claude Sonnet 4.6
-   **Effort**: medium
-   **Estimated Routed Cost**: $0.25

### Routing Table

| Step | Action                                                                | Routing Decision                                                                                                                                              |
| :--- | :-------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1    | Elaborate stub spec                                                   | `Self-assign (Claude Sonnet 4.6)`                                                                                                                             |
| 2    | Update `ai-assignment.md` for this session                            | `Self-assign (Claude Sonnet 4.6)`                                                                                                                             |
| 3    | Route focused architecture sub-calls for D20 and D1/D2 design issues  | `Claude Opus / effort:max`. **Note:** Split into two sub-calls, each with input < 1500 tokens, to mitigate known timeout patterns documented in `lessons-learned.md`. |
| 4    | Apply pre-decided gap fixes to produce `harvester-schema.v1.0.json`   | `Self-assign (Claude Sonnet 4.6)`                                                                                                                             |
| 5    | Write `v1-adr.md` (ADR-style change log + back-compat plan)           | `Self-assign (Claude Sonnet 4.6)`                                                                                                                             |
| 6    | Build and test solution                                               | `Execute locally: dotnet build && dotnet test`                                                                                                                |
| 7    | Route end-of-session cross-provider verification                      | `Gemini Pro / effort:low`                                                                                                                                     |
| 8    | Commit and push                                                       | `Execute locally: git commit -am "..." && git push`                                                                                                           |

### Next Session

-   Proceed to **Session 2: Examples + Output Validation Pass**.

### Actuals

- **Orchestrator used:** Claude Code / claude-sonnet-4-6 @ effort=medium
- **Total routed cost:** $0.1087 (5 calls: 3× gemini-pro, 2× gpt-5-4)
- **Deviations from recommendation:**
  - Step 3 (architecture sub-calls): routed to `gemini-pro` via `query()` instead of `claude-opus` via `route()` because Anthropic API credits were depleted. Two focused sub-calls under 1500 input tokens each (D20 casing, D1/D2 DatabaseManifest shape).
  - Steps 4/5 (schema + ADR authoring): handled in-conversation on Sonnet orchestrator (per the "in-conversation alternative for >3K synthesis prompts" convention in project-guidance.md), not routed.
  - Step 7 (verification): `route(session-verification)` correctly dispatched to gpt-5-4. Round 1 found 2 Major issues (SchemaVersion exact discriminator, nullable fields); fixed and re-verified. Round 2: VERIFIED.
- **Notes for Session 2:** Anthropic API credits still depleted — any routing that would go to Sonnet/Opus will fail. Use gemini-pro or gpt-5-4 for all routed calls. Code generation (SchemaValidator.cs, tests) can be handled in-conversation.

**Next-session orchestrator recommendation (Session 2):**
Gemini CLI (Gemini 2.5 Pro) @ effort=medium — or Claude Sonnet 4.6 @ effort=medium if Anthropic credits are restored.
Rationale: Session 2 is primarily code generation (SchemaValidator.cs ~100 lines, tests ~50 lines) + one schema-validation route call. Both orchestrators handle this well; Gemini avoids the Anthropic credit constraint.

---

## Session 2 of 3: Examples + Output Validation Pass

### Recommendations

-   **Orchestrator**: Claude Sonnet 4.6
-   **Effort**: medium
-   **Estimated Routed Cost**: $0.10

### Routing Table

| Step | Action                                                               | Routing Decision                                                                                                    |
| :--- | :------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------ |
| 1    | Update `ai-assignment.md` and recommend Session 3 plan               | `Self-assign (Claude Sonnet 4.6)`                                                                                   |
| 2    | Generate example fixtures                                            | `Self-assign (Claude Sonnet 4.6)`                                                                                   |
| 3    | Route code-review on generated examples                              | `Gemini Pro / effort:medium`                                                                                        |
| 4    | Author `SchemaValidator.cs` using `JsonSchema.Net`                   | `Self-assign (Claude Sonnet 4.6)`                                                                                   |
| 5    | Author `SchemaValidatorTests.cs`                                     | `Self-assign (Claude Sonnet 4.6)`                                                                                   |
| 6    | Route `schema-validation` for examples vs. schema                    | `Claude Sonnet 4.6 (pinned, complexity_hint=40)`. **Note:** Task is Sonnet-pinned due to its mechanical nature.        |
| 7    | Build, test, verify, and commit                                      | `Execute locally: dotnet build && dotnet test ...`                                                                  |

### Next Session

-   Proceed to **Session 3: Documentation Generation**.

### Actuals

*(No actuals recorded for this session yet.)*

---

## Session 3 of 3: Documentation Generation

### Recommendations

-   **Orchestrator**: Claude Sonnet 4.6
-   **Effort**: medium
-   **Estimated Routed Cost**: $0.15

### Routing Table

| Step | Action                                                              | Routing Decision                                                                                                                                        |
| :--- | :------------------------------------------------------------------ | :------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1    | Update `ai-assignment.md`                                           | `Self-assign (Claude Sonnet 4.6)`                                                                                                                       |
| 2    | Author `SchemaDocGenerator.cs` and CLI wiring                       | `Self-assign (Claude Sonnet 4.6)`                                                                                                                       |
| 3    | Route documentation generation for XML doc comments on public API   | `Gemini Flash (complexity=30)`                                                                                                                          |
| 4    | Author `SchemaDocGeneratorTests.cs`                                 | `Self-assign (Claude Sonnet 4.6)`                                                                                                                       |
| 5    | Build and test                                                      | `Execute locally: dotnet build && dotnet test`                                                                                                          |
| 6    | Route summarization for change-log                                  | `Gemini Flash (max_tier=1)`. **Note:** Use `max_tier=1` to use the most cost-effective model and avoid potential Gemini 400 errors on simple summarization. |
| 7    | Route analysis for next session-set recommendation                  | `Self-assign (Claude Sonnet 4.6)`                                                                                                                       |
| 8    | Final verification and commit                                       | `Execute locally: ... && git commit -am "..." && git push`                                                                                              |

### Next Session

-   Session set complete. Recommend next set focuses on **Provider Integration and E2E Testing**.

### Actuals

*(No actuals recorded for this session yet.)*