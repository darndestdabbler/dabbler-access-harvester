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
Claude Code Sonnet 4.6 @ effort=medium (conditional on Option A or B selection).
Rationale: under either option, Session 2 is mechanical code editing — under Option A a one-line promotion, under Option B writing two extractor classes that follow the verbatim `TableMetadataExtractor` pattern. A mid-tier orchestrator handles both cleanly. Under Option C (hybrid) the same recommendation applies. If the human picks "stop and re-scope" (audit-findings.md §4 implicit Option D), no orchestrator runs until a refreshed Session 2 spec is in place.

---

## Session 2: Close the Confirmed Gaps

> **Conditional on human adjudication of the strategic question raised in
> Session 1's `audit-findings.md` §4.** The routing below is robust to either
> Option A (narrow RecordSource promotion in `SaveAsTextTranslator` per the
> original spec) or Option B (write `FormMetadataExtractor` /
> `ReportMetadataExtractor` following the `TableMetadataExtractor` pattern,
> which the audit recommends). Under Option B Session 2's scope grows but the
> routing pattern is unchanged — the mechanical-edit work is just longer.

### Recommended orchestrator
Claude Code Sonnet 4.6 @ effort=medium

### Rationale
Either option resolves to mechanical code editing — Option A is a one-line
promotion; Option B applies the verbatim `TableMetadataExtractor` pattern
that already exists two files away in the same project. A mid-tier model
handles either at a fraction of the orchestrator's own token cost.

### Estimated routed cost
low

| Step | Action | Routing Decision |
|------|--------|------------------|
| 1 | Verify prior session committed | direct (`git status`) |
| 2 | Read Session 1 findings + confirm human's Option choice | direct |
| 3 (Option A) | Implement RecordSource promotion in `SaveAsTextTranslator.BuildPayload` | direct (mechanical edit) |
| 3 (Option B) | Implement `FormMetadataExtractor.cs` (RecordSource/Filter/OrderBy promotion + DefaultView decoding + Children deserialisation into typed `AccessControl` tree) | direct (mechanical edit) |
| 4 (Option B only) | Implement `ReportMetadataExtractor.cs` (above + structured `GroupLevels` from `GroupLevel<N>Header`/`Footer` sections, KeepTogether decoding; best-effort `PageSetup` from PrtMip/PrtDevMode) | direct (mechanical edit) |
| 5 (Option B only) | Extend `SyntheticMetadataExtractorIntegrationTests` to validate Forms and Reports categories against `09-report-grouping-events.expected.json` | direct (mechanical edit) |
| 6 | Code review | `route(task_type="code-review")` |
| 7 | dotnet build && dotnet test | build+test |
| 8 | End-of-session cross-provider verification | `route(task_type="session-verification")` |

### Actuals (filled after the session)
- Orchestrator used:
- Total routed cost:
- Deviations from recommendation:
- Notes for next-session calibration:

**Next-session orchestrator recommendation (Session 3):**
Claude Code Claude Opus 4.7 @ effort=high
Rationale: Synthesis-heavy session (novel hermetic unit tests + new ir/ schema doc from scratch) benefits from premium-tier reasoning.

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
