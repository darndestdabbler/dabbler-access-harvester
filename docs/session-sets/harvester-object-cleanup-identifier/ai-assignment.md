# AI Assignment — harvester-object-cleanup-identifier

## Session 1 of 3: Schema + Detection Strategy

**Recommended orchestrator:** `claude-opus-4-7`
**Rationale:** The primary task is high-complexity `architecture` synthesis —
designing a multi-signal detection schema and its mapping to an external
JSON format. Opus is the target model for `architecture` tasks per
repo conventions, and using an Opus orchestrator allows the core synthesis
to be handled in-conversation.
**Estimated routed cost:** Moderate ($0.05–$0.15)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read project-guidance.md, lessons-learned.md, and object-cleanup-identifier spec. | `In-conversation` |
| 2 | Synthesise the `object-cleanup.json` schema and `uat-checklist-editor` mapping. | `In-conversation` (task type `architecture`, matches orchestrator) |
| 3 | Draft `docs/planning/object-cleanup-strategy.md`. | `In-conversation` |
| 4 | Write strategy doc and JSON Schema files. | `tool-use` |
| 5 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 6 | Summarise and produce change-log entry. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- **Orchestrator used:** claude-code / claude-opus-4-7 @ effort=medium
- **Total routed cost:** ~$0.94 USD across four verification rounds
  (R1 $0.215, R2 $0.219, R3 $0.261, R4 $0.243 — all gpt-5-4
  session-verification rounds).
- **Deviations from recommendation:**
  - Architecture synthesis handled in-conversation (Opus orchestrator
    matches the architecture-task routing target — per
    project-guidance.md "When the orchestrator's model matches the
    routing target, handle in-conversation").
  - Four cross-provider verification rounds instead of the workflow's
    Step 7 "max 2 retries" cap. Human authorized R4 explicitly to
    close out with a clean VERIFIED rather than defer the R3 doc-
    consistency findings. R4 itself returned 1 trivial Minor
    (example arithmetic error 1080→1535 days); fixed post-R4
    without R5 per closeout budget.
  - Three pre-existing HomeHealthCare CLI integration tests time out
    at 60s under the dotnet test harness (direct CLI invocation
    succeeds in <30s; orphan MSACCESS process cleanup did not
    resolve). Documented as out-of-scope in every verification
    prompt — Session 1's deliverables are docs/schema-only and
    cannot have caused this. 42/45 integration tests pass.
- **Notes for next-session calibration:**
  - The strategy doc compresses several cross-section invariants
    (Subarea no-transformation rule, code-behind global exclusion,
    bidirectional SQL impact). Session 2's implementation should
    treat each invariant as a test target — at minimum: a unit
    test asserting Subarea equals objectName byte-for-byte, a unit
    test asserting Form_*/Report_* never appear in findings, and
    a unit test exercising the inline-SQL parse in
    RecordSource/RowSource against a fixture.
  - The four verification rounds surfaced that closeout-grade
    strategy docs benefit from re-reading the OUTPUT examples
    (especially DateSignal) for arithmetic and timezone
    consistency before sending to verification — the R4 finding
    was caught only because the verifier did the date math.
    Pre-verification orchestrator self-check on numeric examples
    would have saved one round.

---

**Next-session orchestrator recommendation (Session 2):** claude-code
/ claude-sonnet-4-6 @ effort=medium. Rationale: Session 2 is
mechanical correspondence work — translate the Session 1 strategy
verbatim into C# detectors, output writers, and unit tests under
`AccessHarvester.Pipeline/Cleanup/`. No architecture decisions
remain; the regexes, signal-combinator rules, and JSON output
shapes are fully specified. Sonnet at medium is the right cost
band per the repo's mechanical-vs-synthesis routing convention
(`project-guidance.md` → Conventions → Workflow Expectations).

## Session 2 of 3: CLI Detection Code

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** The core task is `mechanical` implementation — translating
the Session 1 schema and strategy into C# code. Three detectors are
well-scoped and independently testable. Sonnet at `effort=medium` is
the appropriate cost band for `mechanical` tasks per repo conventions.
**Estimated routed cost:** Low–Moderate ($0.03–$0.10)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Read strategy and schema from Session 1. | `In-conversation` |
| 2 | Implement `IdentifyCleanupCommand.cs` and detector types. | `In-conversation` (task type `mechanical`, matches orchestrator) |
| 3 | Write C# files to `AccessHarvester.Pipeline/Cleanup/`. | `tool-use` |
| 4 | Write unit tests. | `tool-use` |
| 5 | Run `dotnet build && dotnet test`. | `tool-use` |
| 6 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 7 | Summarise. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- **Orchestrator used:** claude-code / claude-opus-4-7 @ effort=medium
  (DEVIATION from recommendation: user invoked Opus rather than Sonnet for
  Session 2; orchestrator proceeded per user direction.)
- **Total routed cost:** ~$0.82 USD across four verification rounds
  (R1 $0.225, R2 $0.255, R3 $0.291, R4 $0.049 — all gpt-5-4
  session-verification rounds) + ~$0.003 for Session 3 recommendation
  via gemini-pro analysis route.
- **Deviations from recommendation:**
  - Opus orchestrator instead of Sonnet (user-invoked).
  - Implementation delegated to general-purpose subagent (Sonnet) for
    R0 mechanical write + each fix round, matching the Sonnet
    recommendation in spirit while keeping Opus orchestration.
  - Four verification rounds. R1 had 5 issues (per-table date
    fallback, VBA DoCmd parsing, event-property scope, matchedSubstring
    boundary, test coverage). R2 had 4 issues including a regression in
    IrLoader that R1 fixes introduced (generic-properties branch
    swallowed Begin macro-action nodes — caught by R2 verifier).
    R3 had 2 Minors (missing usage message on dual-flag, contradictory
    degradation notes). R4 (human-authorized override of 2-retry cap
    per Session 1 precedent) VERIFIED with no issues.
  - Architectural deviation from spec Creates list: `DateMaximaSampler.cs`
    was split — the interface + DAO fallback stayed in
    `AccessHarvester.Pipeline/Cleanup/DateMaximaSampler.cs` per spec,
    but the COM-using implementation moved to
    `AccessHarvester.Core/Com/AccessDateMaximaSampler.cs` because
    Pipeline cannot reference Core (Core → Pipeline is the existing
    project-reference direction). One-line comment in
    AccessDateMaximaSampler.cs documents the reason.
  - Pre-existing 3 HomeHealthCare CLI integration test timeouts persist
    (42/45 integration tests pass, unchanged from Session 1 baseline).
- **Notes for Session 3 calibration:**
  - HomeHealthCare smoke run produced 30 Low-tier findings only —
    expected because HHC names lack staleness vocabulary and
    LastUpdated is 15 days old (well under 90-day threshold).
    NorthwindDevEdition pilot should exercise the High/Medium tiers.
  - `frmHolders` does NOT match the vocabulary regex (trailing
    CamelCase boundary `d→e` is lowercase→lowercase, not a transition);
    confirmed by inspection. Calibration may consider whether this is
    desired or whether the regex should be more permissive.
  - `frmReports2024` does NOT match the year regex (requires
    non-alphanumeric before the year; `s` precedes `2024`). Calibration
    may consider whether CamelCase year suffixes without separator
    should match.
  - The 4 verification rounds revealed that the spec list of "Creates"
    files can drift from project-reference constraints discovered at
    implementation time. Future session-set spec authoring should
    pre-check the project dependency direction for any new
    cross-assembly references implied by the file list.
  - `--no-com` HHC smoke run validates cleanly against the JSON schema
    (0 errors). Subarea byte-identical to objectName invariant holds.

---

**Next-session orchestrator recommendation (Session 3):** claude-code /
claude-sonnet-4-6 @ effort=medium. Rationale (routed via `gemini-pro`
analysis task): Session 3 scope is calibration and analysis — tuning
vocabulary, thresholds, reviewing output clarity, committing golden
fixtures. This aligns with the repo convention of Sonnet for analysis
tasks. The multi-round verification pattern across Sessions 1 and 2
indicates iterative refinement is inherent to this project's workflow,
not a symptom of model under-performance — using Opus would be an
unnecessary cost and a deviation from established routing principles.
Estimated routed cost: Low ($0.02–$0.08).

## Session 3 of 3: Pilot + Calibration

**Recommended orchestrator:** `claude-sonnet-4-6`
**Rationale:** Pilot execution and threshold tuning is a `analysis`-type
task — Sonnet at `effort=medium` is appropriate. No architecture
decisions are expected; the work is calibration and golden-fixture
production.
**Estimated routed cost:** Low ($0.02–$0.08)

| Step | Action | Routing Decision |
|---|---|---|
| 1 | Run `identify-cleanup` against HomeHealthCare and NorthwindDevEdition golden outputs. | `tool-use` |
| 2 | Review results; tune detector thresholds and description templates. | `In-conversation` (task type `analysis`, matches orchestrator) |
| 3 | Write calibrated sample output files as golden fixtures. | `tool-use` |
| 4 | Run `dotnet build && dotnet test`. | `tool-use` |
| 5 | Run session verification. | `query("gemini-pro")` (task type `session-verification`) |
| 6 | Closeout: disposition.json, change-log.md, next-session-set-recommendation.md. | `route(summarization, max_tier=1)` |

### Actuals (filled after the session)

- (To be filled after the session completes.)
