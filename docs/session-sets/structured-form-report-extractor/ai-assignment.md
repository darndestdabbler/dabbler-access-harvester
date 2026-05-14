# AI Assignment — structured-form-report-extractor

> Per-session ledger of cheapest-capable orchestrator + per-step routing.
> Recommendations are authored via `route(task_type="analysis")` (Gemini Pro) —
> never self-opinion. Actuals are appended after each session closes.

## Session 1: Audit the Current ir/ Output

### Recommended orchestrator
Claude Code Sonnet 4.6 @ effort=medium

### Rationale
This session is primarily file analysis and orchestration of a single high-complexity analysis call; a mid-tier model is sufficient and cost-effective for driving the workflow.

### Estimated routed cost
moderate

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Read AccessTextParser/SaveAsTextTranslator/AccessNode C# sources | direct |
| 2 | Check characterization-test fixtures for ir/ samples | direct |
| 3 | Read enricher sources (Geometry, Label, MacroActions) | direct |
| 4 | dotnet build && dotnet test | build+test |
| 5 | Compare ir/ JSON vs YAML vs coverage table | `route(task_type="analysis", complexity_hint≈55)` |
| 6 | Write findings summary deliverable | direct (mechanical writeup of Step 5 output) |
| 7 | End-of-session cross-provider verification | `route(task_type="session-verification")` |

### Actuals (filled after the session)
- Orchestrator used: claude-code / claude-opus-4-7 / effort=high (the human launched this session on the Opus orchestrator — one tier above the routed Sonnet recommendation)
- Total routed cost: ~$0.17 across three routed calls — `route(analysis)` for ai-assignment authoring ($0.009, gemini-pro), `route(analysis)` for the audit comparison ($0.021, gemini-pro), `route(session-verification)` cross-provider ($0.137, gpt-5-4)
- Deviations from recommendation: orchestrator was Opus rather than Sonnet (human-driven choice via trigger phrase, not a calibration miss). Per-step routing matched the plan: file reads / build & test / two routed analysis calls / one routed verification call.
- Notes for next-session calibration: Session 1's audit surfaced a strategic question (Option A vs B vs C) that the human must adjudicate before Session 2 can start. Until that adjudication happens, the Session 2 plan below is provisional. If the human picks Option B (recommended in audit-findings.md §4), Session 2 scope grows from "one-line RecordSource promotion" to "write FormMetadataExtractor + ReportMetadataExtractor + extend SyntheticMetadataExtractorIntegrationTests" — still mechanical, still Sonnet-appropriate, but more keystrokes.

**Next-session orchestrator recommendation (Session 2):**
Claude Code Sonnet 4.6 @ effort=medium.
Rationale: Option B (chosen 2026-05-14) is mechanical pattern application — writing two extractor classes that follow the verbatim `TableMetadataExtractor` precedent plus extending one integration test. Mid-tier orchestrator handles this cleanly at a fraction of premium-tier cost.

---

## Session 2: FormMetadataExtractor + ReportMetadataExtractor

> **Scope locked at end of Session 1 (2026-05-14).** Human adjudicated
> **Option B** from `audit-findings.md` §4: apply the
> `TableMetadataExtractor` pattern to forms and reports rather than the
> original spec's narrow `SaveAsTextTranslator` patch. `PageSetup` decoding
> is deferred per the caveat in the recommendation.

### Recommended orchestrator
Claude Code Sonnet 4.6 @ effort=medium

### Rationale
Mechanical pattern application — the `TableMetadataExtractor` precedent is
two files away in the same project, and the typed records
(`FormMetadata`, `ReportMetadata`, `AccessControl`, `GroupLevel`) are
already designed. Mid-tier orchestrator handles this cleanly at low cost.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Verify prior session committed (incl. residual closeout flips from Session 1) | direct (`git status`) |
| 2 | Read `audit-findings.md` for typed-schema contracts + `TableMetadataExtractor` precedent | direct |
| 3 | Implement `AccessHarvester.Access/FormMetadataExtractor.cs` (RecordSource/Filter/OrderBy promotion + DefaultView decoding + Children deserialisation into typed `AccessControl` tree) | direct (mechanical edit) |
| 4 | Implement `AccessHarvester.Access/ReportMetadataExtractor.cs` (everything in step 3 plus structured `GroupLevels` from `GroupLevel<N>Header/Footer` sections + KeepTogether decoding; PageSetup stubbed to null per deferral) | direct (mechanical edit) |
| 5 | Extend `SyntheticMetadataExtractorIntegrationTests` to validate Forms and Reports categories against `09-report-grouping-events.expected.json` (add `AssertForms` / `AssertReports` comparators; un-hard-code the category filter) | direct (mechanical edit) |
| 6 | Code review | `route(task_type="code-review")` |
| 7 | dotnet build && dotnet test | build+test |
| 8 | End-of-session cross-provider verification | `route(task_type="session-verification")` |

### Actuals (filled after the session)
- Orchestrator used: claude-code / claude-opus-4-7 / effort=high (one tier above the Sonnet/medium recommendation; human launched on the Opus orchestrator).
- Total routed cost: ~$0.52 across 4 routed calls — `route(code-review)` Sonnet ($0.237) auto-verified by Gemini Pro (verifier cost included), `route(session-verification)` gpt-5-4 Round 1 ($0.223) and Round 2 ($0.059), `route(analysis)` for next-orchestrator recommendation Gemini Pro ($0.002).
- Deviations from recommendation: orchestrator was Opus rather than Sonnet (human-driven launch, not a calibration miss). Step 6 code review correctly went through the router (Sonnet via task_type_overrides) per Delegation Discipline — the mechanical-correspondence convention applies. Step 8 mandatory cross-provider verification took 2 rounds; Round 1 surfaced one Major (loose-match scalar comparison was unconditional, treating omitted fixture keys as expected-null), fix applied in-session via `AssertIfFixturePresent` helper, Round 2 VERIFIED. The Step 6 review also surfaced two real bugs (Reports DefaultView wrong label table; GroupLevel<N> keys not removed from Properties after lift) both fixed before Step 7 build+test ran.
- Notes for next-session calibration: Pattern application went smoothly — Sonnet's code review caught both real bugs at low cost ($0.24); the cross-provider verifier added a third (also real). The routed-rigor pipeline is paying for itself on this kind of work. Session 3 is synthesis-heavy (novel hermetic unit tests + new typed-schema doc from scratch) — Opus orchestrator is the right call per the routed recommendation and the prior session-set's pattern.

**Next-session orchestrator recommendation (Session 3):**
Claude Code Claude Opus 4.7 @ effort=high
Rationale (routed via `route(task_type="analysis")` → gemini-pro, 2026-05-14): Session 3 requires generating numerous, detail-oriented unit tests and clear technical documentation with a worked example, demanding a high-capability model to ensure precision and correctness across both coding and prose generation.

---

## Session 3: Tests and ir/ Schema Documentation

### Recommended orchestrator
Claude Code Claude Opus 4.7 @ effort=high

### Rationale
This session requires synthesis for both novel test case authoring and creating schema documentation from scratch, justifying the use of a premium-tier model.

### Estimated routed cost
high

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Verify prior session committed | direct (`git status`) |
| 2 | Author hermetic unit tests | `route(task_type="test-generation")` or in-conversation if orchestrator is Opus |
| 3 | Write `docs/ir-schema-forms-reports.md` | direct (in-conversation if Opus orchestrator) or `route(task_type="documentation")` |
| 4 | Documentation review | `route(task_type="documentation")` if not already routed in Step 3 |
| 5 | dotnet build && dotnet test | build+test |
| 6 | End-of-session cross-provider verification | `route(task_type="session-verification")` |
| 7 | Change-log + next-session-set recommendation | `route(task_type="summarization", max_tier=1)` + `route(task_type="analysis")` |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session-set recommendation:**
<filled on Session 3 close-out via `route(task_type="analysis")`>
