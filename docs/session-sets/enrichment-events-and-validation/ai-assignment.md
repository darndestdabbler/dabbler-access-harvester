# AI Assignment — enrichment-events-and-validation

## Session 1: Event Handler Taxonomy (Synthesis)

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** The primary task is high-complexity `architecture` synthesis, for which Opus is the target model. Using an Opus orchestrator allows this core synthesis to be handled in-conversation, simplifying the session workflow per repo conventions.
**Estimated routed cost:** High

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Elaborate the session plan from the spec's bullet points. | `In-conversation` |
| 2 | Read and synthesize context from the spec (Phase 6.3), fixtures (`01`, `02`, `09`, `12`), and prerequisite outputs from #14. | `In-conversation` |
| 3 | Synthesize the C# event handler classifier and the `EventHandlerClassification.cs` taxonomy. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 4 | Write generated C# files to `AccessHarvester.Pipeline/Enrichment/`. | `tool-use` |
| 5 | Run session-verification against the generated code and project context. | `query("gemini-pro")` (task type `session-verification`, `complexity_hint=70`) |
| 6 | Summarize the session's work and produce a change-log entry. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high`. Synthesis (`EventHandlerClassification.cs` + `EventHandlerClassifier.cs` + 53 unit tests, then a 6-test follow-up after verifier-driven With-block fix) authored in-conversation per the convention "When the orchestrator's model matches the routing target, handle the task in-conversation." No `route(task_type="architecture")` call was made.
- **Actual cost:** $0.0949 routed total across 4 calls. ai-assignment.md (`analysis` -> gemini-pro): $0.0187. Verification round 1 (`session-verification` -> gemini-pro): $0.0497. Verification round 2 (`session-verification` -> gemini-pro): $0.0240. Next-orchestrator recommendation (`analysis` -> gemini-pro): $0.0025. Verification skipped gpt-5-4 entirely per the prereq's calibration note about recurring 429s; routed straight to gemini-pro via `query()`.
- **Session commits:** one commit landing `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs` (records + EventHandlerClassificationCategory and EventHandlerSignalKind enums), `AccessHarvester.Pipeline/Enrichment/EventHandlerClassifier.cs` (statement walker, assignment + call classifier, chain helpers including ImplicitWithMember/Bang handling), `tests/AccessHarvester.Pipeline.Tests/EventHandlerClassifierTests.cs` (59 hermetic tests covering each category, mixed handlers from anchor fixtures 01/02/09/12, and With-block coverage), `AccessHarvester.Pipeline.csproj` + `AccessHarvester.Pipeline.Tests.csproj` ProjectReference additions to `AccessHarvester.Vba.Parser`, both verifier rounds in `session-reviews/session-001.md`, `issue-logs/session-001.json`, the routed Session-2 recommendation in `session-1-next-orchestrator-recommendation.md`, this set's `ai-assignment.md`, `activity-log.json`, and `session-state.json`.
- **Verification route:** `query("gemini-pro", task_type="session-verification")` (skipped gpt-5-4 per Session 1-3 calibration of prereq set). Round 1: ISSUES_FOUND (1 Major — With-block ImplicitWithMember/ImplicitWithBang nodes were unhandled in chain helpers, causing assignments and calls in `With X` blocks to misclassify or emit no signal). Fix: extended `LastMemberName` and `WalkChain` to surface implicit-with member names; added explicit-silence branch for implicit-with `.Requery` whose receiver is genuinely unresolvable per `project-guidance.md` -> VBA Parser Strategy ("Unresolvable constructs are explicit, not silent") rather than guessing FormRequery. 6 new tests covering With-block scenarios. Round 2: VERIFIED, issues: []. Build: green; 1290 passed, 4 skipped (Windows-only Access COM integration tests), 0 failed.
- **Deviations from recommendation:** none on the orchestrator. Verification provider deviated by design (proactively skipped gpt-5-4 per prereq calibration note; saved one 3-retry-then-fallback round of latency).
- **Notes for next-session calibration:** (a) gemini-pro round 1 verification produced strong findings AND well-shaped fixes — the trailing JSON block + structured `## Analysis` framing held up cleanly across both rounds (no terse-verdict retry). Continue this prompt shape. (b) The verifier proposed a specific fix-detail (default `.Requery` to FormRequery via heuristic) that was rejected in favor of explicit silence; the disagreement was a *design philosophy* split, not a context gap, so it did not invoke the four-option adjudication path. Worth flagging to the human if the pattern recurs. (c) When ImplicitWith* nodes appear in the AST, the classifier's chain walking needs to handle them — Session 2's validation extractor will encounter them in BeforeUpdate handler patterns and should reuse the WalkChain helper rather than re-implementing.

---

**Next-session orchestrator recommendation (Session 2):** Claude Opus 4.7 (`claude-opus-4-7`) at `effort=high`. Confirmed by routed `analysis` call (gemini-pro, $0.0025, 2026-04-27). Session 2 is the second of two `architecture` synthesis tasks (validation rule extractor + ValidationRule.cs + BeforeUpdate-pattern parsing) — same routing profile as Session 1, same in-conversation synthesis convention. Verification should again `query("gemini-pro", task_type="session-verification")` directly to skip the gpt-5-4 quota wall. Full text in `session-1-next-orchestrator-recommendation.md`.

## Session 2: Validation Rule Extraction (Synthesis)

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** This session is the set's second high-complexity `architecture` synthesis task, matching the profile of Session 1. An Opus orchestrator handles the core synthesis in-conversation for consistency and efficiency.
**Estimated routed cost:** High

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Elaborate the session plan from the spec's bullet points. | `In-conversation` |
| 2 | Read context: Phase 6.4 spec, fixtures `01` and `06`, and table validation metadata from prereq #10. | `In-conversation` |
| 3 | Synthesize the C# validation rule extractor and the `ValidationRule.cs` model. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 4 | Write generated C# files to `AccessHarvester.Pipeline/Enrichment/`. | `tool-use` |
| 5 | Run session-verification against the generated code and project context. | `query("gemini-pro")` (task type `session-verification`, `complexity_hint=70`) |
| 6 | Summarize the session's work and produce a change-log entry. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high`. Synthesis (`ValidationRule.cs` model + `ValidationRuleExtractor.cs` extractor + 32 hermetic test cases across 21 `[Fact]`/`[Theory]` declarations) authored in-conversation per the convention "When the orchestrator's model matches the routing target, handle the task in-conversation." No `route(task_type="architecture")` call was made — same pattern as Session 1.
- **Total routed cost:** $0.2284 across 7 calls (Session 2 only). gemini-pro: $0.1253 (6 calls including the original verifier round and the Session 1 + Session 2 ai-assignment / next-orc analyses recorded against this set). Sonnet tiebreaker: $0.1031 (1 call). Session-2-specific spend: round 1 verification (gemini-pro): $0.0244; tiebreaker (sonnet): $0.1031; next-orc recommendation (gemini-pro): ~$0.0030. **Verifier-failure ladder activation:** the configured cross-provider verifier `gpt-5-4` and the next-tier `gpt-5-4-mini` both 429-throttled before sonnet succeeded — recorded as `verifier_fallback` events in router-metrics. The original verifier (gemini-pro) was reached via the prereq's calibration-note workaround (`query("gemini-pro")` directly, skipping `task_type_overrides`).
- **Session commits:** one commit landing `AccessHarvester.Pipeline/Enrichment/ValidationRule.cs`, `AccessHarvester.Pipeline/Enrichment/ValidationRuleExtractor.cs`, `tests/AccessHarvester.Pipeline.Tests/ValidationRuleExtractorTests.cs` (32 hermetic tests covering table-side / form-side / BeforeUpdate-handler shapes plus the two anchor fixtures `01` and `06c`), the verifier outputs (`session-reviews/session-002.md`, `session-reviews/session-002-tiebreaker.md`), `issue-logs/session-002.json`, the routed Session-3 recommendation in `session-2-next-orchestrator-recommendation.md`, this Session 2 actuals block, `activity-log.json`, and `session-state.json`.
- **Verification route:** `query("gemini-pro", task_type="session-verification")` round 1 returned ISSUES_FOUND with one Minor finding ("BeforeUpdate handler comparisons should be Range, not CustomPredicate"). Per the workflow Step 7 disagreement procedure, surfaced to human; human selected option (d) second-opinion. Tried `query("gpt-5-4", ...)` then `query("gpt-5-4-mini", ...)` — both 429-throttled. Fell back to `query("sonnet", ...)` — different provider from the original verifier (Anthropic vs Google), satisfying the "different from original verifier" rule for tiebreakers though same provider as orchestrator. Sonnet adjudicated **orchestrator-right** with rationale: the verifier's fix is semantically inverted (BeforeUpdate guards are *rejection* predicates; naive Range parsing would describe the rejected window not the valid one; compound `>=...And...<=` guards negate to a disjunction that does not fit a single ValidationRangeConstraint). Finding marked `deferred-by-design` in `issue-logs/session-002.json`. A clarifying comment was added on `ClassifyBeforeUpdateCondition` per sonnet's recommended optional-action.
- **Deviations from recommendation:** none on the orchestrator. Verification provider deviated by design (proactively skipped `gpt-5-4` per prereq calibration note; sonnet tiebreaker added in response to genuine-split disagreement). Adjudication logged via `record_adjudication(cause="genuine-split", resolution="second-opinion")` for manager-report aggregation.
- **Notes for next-session calibration:** (a) Session 2's gemini-pro round 1 verification produced one well-shaped finding (the negation-semantics issue) AND a wrong fix — the trailing JSON block held up cleanly (no terse-verdict retry). Continue this prompt shape. (b) The OpenAI 429-quota wall now blocks both `gpt-5-4` and `gpt-5-4-mini` for tiebreakers, not just primary verification. Sessions 3 and 4 should plan around this — sonnet is a viable tiebreaker even though it's same-provider as an Opus orchestrator (the *original* verifier provides the cross-provider check; the tiebreaker just needs to be different from the original verifier). (c) Session 3's RowSource spec is well-trodden (`analysis` complexity 55) — a routed Sonnet `analysis` call would also be valid if cost-window pressure pushes against another Opus session, per the spec's explicit "may be in-conversation if the orchestrator is Opus, otherwise routed `analysis`."

---

**Next-session orchestrator recommendation (Session 3):** Claude Opus 4.7 (`claude-opus-4-7`) at `effort=medium`. Confirmed by routed `analysis` call (gemini-pro, ~$0.0030, 2026-04-28). Session 3 (RowSource Specification) is the well-trodden enrichment shape (complexity 55) — Opus at `medium` is a logical step-down from Session 2's `high` while keeping the in-conversation synthesis convention applicable per the spec ("may be in-conversation if the orchestrator is Opus, otherwise routed `analysis`"). Verification should again `query("gemini-pro", task_type="session-verification")` directly. Full text in `session-2-next-orchestrator-recommendation.md`.

## Session 3: RowSource Specification

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The task is a medium-complexity `analysis` type, fitting the profile of mechanical correspondence. Sonnet is the cost-effective model pinned for this work per repo conventions.
**Estimated routed cost:** Moderate

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Elaborate the session plan from the spec's bullet points. | `In-conversation` |
| 2 | Read context: Phase 6.5 spec, fixture `02` (combo box patterns), and parsed SQL AST from prereq #12. | `In-conversation` |
| 3 | Generate the `RowSourceSpec.cs` model and the pipeline visitor to populate it. | `In-conversation` (task type `analysis`, matches orchestrator) |
| 4 | Write generated C# files to `AccessHarvester.Pipeline/Enrichment/`. | `tool-use` |
| 5 | Run session-verification against the generated code and project context. | `query("gemini-pro")` (task type `session-verification`, `complexity_hint=70`) |
| 6 | Summarize the session's work and produce a change-log entry. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- **Actual orchestrator:** Claude Code with `claude-opus-4-7` at `effort=high` (deviation from the routed Session 2 recommendation of Opus at `effort=medium` — driven by the human's session-trigger choice, not by spec needs). Synthesis (`RowSourceSpec.cs` + record/enums/sub-records and `RowSourceSpecBuilder.cs` with `Build` / `BuildAll` / `AnnotateCascadingDrivers` plus 31 hermetic xUnit tests) authored in-conversation per the matches-orchestrator convention. No `route(task_type="analysis")` call was made for the synthesis itself — the well-trodden enrichment shape mirrored the existing `FormComponentSpecBuilder` and `ValidationRuleExtractor` structurally.
- **Total routed cost:** $0.0702 across 3 calls (Session 3 only). Verification round 1 (`session-verification` -> gemini-pro): $0.0288. Verification round 2 (`session-verification` -> gemini-pro): $0.0369. Next-orc recommendation (`analysis` -> gemini-pro): $0.0045. Verification routed via `query("gemini-pro", ...)` directly per the prereq calibration note (skip gpt-5-4 / gpt-5-4-mini quota wall confirmed in Session 2 actuals).
- **Session commits:** one commit landing `AccessHarvester.Pipeline/Enrichment/RowSourceSpec.cs`, `AccessHarvester.Pipeline/Enrichment/RowSourceSpecBuilder.cs`, `tests/AccessHarvester.Pipeline.Tests/RowSourceSpecBuilderTests.cs` (31 hermetic tests including the anchor fixture #02 round-trip), the verifier outputs (`session-reviews/session-003.md`, with both rounds appended in-file), the routed Session-4 recommendation in `session-3-next-orchestrator-recommendation.md`, this Session 3 actuals block, `activity-log.json`, and `session-state.json`.
- **Verification route:** Round 1 — gemini-pro returned a JSON-only `VERIFIED` verdict with empty `issues` and no analysis prose. Per the lessons-learned "JSON block must follow substantive analysis" pattern, re-prompted with an explicit analysis-first addendum. Round 2 — gemini-pro returned `VERIFIED` with substantive per-section analysis covering Phase 6.5 coverage, Lean By Default, Output JSON Conventions, Determinism, Test Coverage, and naming consistency. No issues raised. Build: green; 1353 passed, 4 skipped (Windows-only Access COM integration tests), 0 failed. New tests: 31 passed.
- **Deviations from recommendation:** orchestrator effort deviated upward (`high` vs. recommended `medium`) — human trigger-side choice, not a spec-driven need. The `medium` recommendation would likely have been adequate for this well-trodden shape; the work landed cleanly without consuming extra reasoning budget at `high` either way. Verification provider deviated by design (proactively skipped gpt-5-4 per prereq calibration note).
- **Notes for next-session calibration:** (a) gemini-pro's tendency to emit JSON-only verdicts on terse-shape prompts recurred this session — the trailing JSON block format alone is not sufficient to elicit analysis prose; the prompt structure should explicitly request "analysis first, then JSON block" up front rather than discovering the omission post-hoc. Worth noting if a third occurrence happens in Session 4. (b) Cascading-driver detection (RowSourceSpec) and event-handler classification (Session 1) compose cleanly via the `AnnotateCascadingDrivers` static method — Session 4's end-to-end pipeline pass should call `RowSourceSpecBuilder.BuildAll(form, classifications)` after the EventHandlerClassifier has run, not before. (c) `RowSourceSpec.LimitToList` deliberately mirrors the flag the `ValidationRuleExtractor` already emits as a `ControlLimitToList`-origin rule. Session 4's schema validation should accept this duplication as intentional rather than flag it as a normalization issue.

---

**Next-session orchestrator recommendation (Session 4):** Claude Sonnet 4.6 (`claude-sonnet-4-6`) at `effort=medium`. Confirmed by routed `analysis` call (gemini-pro, $0.0045, 2026-04-28). Session 4 is mechanical end-to-end + schema-validation tightening — Sonnet at default effort is the right cost band per the project guidance's "pin mechanical-correspondence task types to Sonnet rather than Opus" convention. Verification should `query("gemini-pro", task_type="session-verification")` directly per the prereq calibration. Full text in `session-3-next-orchestrator-recommendation.md`.

## Session 4-5: End-to-End + Mechanical Tightening

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The session's core AI task is `schema-validation`, a mechanical-correspondence type pinned to Sonnet. Using a Sonnet orchestrator aligns with the task's profile and repo conventions for cost-effectiveness.
**Estimated routed cost:** Low

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Elaborate the session plan from the spec's bullet points. | `In-conversation` |
| 2 | Run the full enrichment pipeline against anchor fixtures (`01`, `02`, `06`, `09`, `12`). | `tool-use` |
| 3 | Validate the generated output against `harvester-schema.v0.json`. | `In-conversation` (task type `schema-validation`, matches orchestrator) |
| 4 | Apply any mechanical tightening commits based on validation results. | `In-conversation` / `tool-use` |
| 5 | Run session-verification against the final state. | `query("gemini-pro")` (task type `session-verification`, `complexity_hint=70`) |
| 6 | If verifier surfaces significant issues, promote cleanup to the optional Session 5. | `Manual decision` |
| 7 | Summarize the session's work and produce a change-log entry. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- **Actual orchestrator:** Claude Code with `claude-sonnet-4-6` at `effort=medium`. Session 4 work is mechanical-correspondence (pipeline wiring + schema validation), matching the Sonnet recommendation. All synthesis authored in-conversation per the "matches orchestrator" convention; no `route(task_type="analysis")` call was made for the pipeline runner itself.
- **Total routed cost:** $0.0642 across 5 calls (Session 4 only). Verification round 1 (gemini-pro, JSON-only terse): $0.0126. Verification round 2 (gemini-pro, analysis-first reprompt): $0.0240. Change-log (Gemini Flash, summarization max_tier=1): $0.0004. Next-session-set recommendation (gemini-pro, analysis): $0.0072. Build/test step had no routed calls. Cumulative set total: $0.3307 across 11 routed calls.
- **Session commits:** one commit landing `AccessHarvester.Pipeline/Enrichment/EnrichmentResult.cs`, `AccessHarvester.Pipeline/Enrichment/EnrichmentPipelineRunner.cs`, `tests/AccessHarvester.Pipeline.Tests/EnrichmentPipelineRunnerTests.cs` (14 hermetic integration tests including anchor fixture 02 round-trip), `docs/session-sets/enrichment-events-and-validation/change-log.md`, `docs/session-sets/enrichment-events-and-validation/next-session-set-recommendation.md`, verifier output (`session-reviews/session-004.md`), updated `ai-assignment.md`, `activity-log.json`, `session-state.json`.
- **Verification route:** `query("gemini-pro", task_type="session-verification")` round 1 returned JSON-only VERIFIED (24 output tokens — terse-verdict recurrence, see lessons-learned). Round 2 with explicit "analysis first, then JSON block" instruction returned VERIFIED with substantive per-section analysis covering ordering, classification strings, single-call guarantee, reference equality, FormatHandlerClassification fallback, test coverage, and anchor fixture 02. No issues found. Build: green; 1367 passed, 4 skipped (Windows-only COM), 0 failed. 14 new pipeline tests.
- **Deviations from recommendation:** none. Schema validation handled in-conversation per the "matches orchestrator" convention (Sonnet is the routing target for schema-validation). Session 5 (optional buffer) was not needed — verifier found no issues.
- **Notes for calibration:** (a) gemini-pro terse-verdict recurrence is now 3/4 sessions in this set. The "analysis-first" explicit instruction in the reprompt consistently resolves it. Future sessions should include this instruction in the initial verification prompt rather than discovering it via reprompt. (b) EnrichmentPipelineRunner's `FormatHandlerClassification` returns "Mixed" for handlers with 0 categories (no recognized patterns) rather than null. The verifier agreed this is "reasonable and defensible" but flagged semantic precision concern; future sessions extending the classifier should consider null for unclassified rather than "Mixed" if the distinction matters to downstream consumers. (c) The `beforeUpdateCache` pattern (extract once, cache classification string) cleanly prevents double-calls to `ExtractFromBeforeUpdateHandler` — worth reusing in future pipeline stages that have similar dual-use (extract rules + derive classification from the same extraction).

**Next-session-set recommendation:** `enrichment-functions-navigation-review`. Both prerequisites (`enrichment-queries-and-forms` and `enrichment-events-and-validation`) are now complete. This set finalizes the enrichment phase (Phase 6.6-6.7: function navigation, code intelligence). Full text in `next-session-set-recommendation.md`.