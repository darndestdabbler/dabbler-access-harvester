# AI Assignment Ledger
**Session Set:** `parser-foundations`
**Project:** VBA Parser Migration

This document outlines the planned AI orchestrator and model routing for each session in the `parser-foundations` set.

---

## Session 1: Verification Harness Design + Skeleton

### Recommended orchestrator
**Opus@max**

### Rationale
This session contains a single synthesis task (`task_type="architecture"`) which, per the playbook, qualifies the session for an `Opus@max` orchestrator. This allows the high-reasoning architecture design to be handled in-conversation, which mitigates the timeout risks associated with routing large synthesis prompts documented in `lessons-learned.md`.

**Deviation Note:** This session is currently being executed by `Claude Sonnet 4.6`. Per `lessons-learned.md`, a Sonnet orchestrator *must* route the `architecture` task. This triggers the documented timeout risk, as the routed prompt will be sent to Opus/max externally. The team has accepted this risk for the current execution.

### Estimated routed cost
**High.** One high-complexity `architecture` call to Opus/max and one high-complexity `session-verification` call to `gpt-5-4`.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Read Phase 0.2 plan and test case README | `In-conversation` |
| 2 | Design verification harness output shape and semantics | `route(task_type="architecture", complexity_hint=70)` |
| 3 | Land harness CLI surface, schema, and smoke test | `In-conversation` |
| 4 | Verify session deliverables against plan | `route(task_type="session-verification", complexity_hint=70)` |
| 5 | Generate change log for session commits | `route(task_type="summarization", max_tier=1)` |

### Actuals
*(to be filled)*

---

## Session 2: Schema Reconciliation Pass

### Recommended orchestrator
**Sonnet@medium**

### Rationale
This session is primarily a mechanical comparison and documentation task. The single routed call is for `schema-validation`, which is pinned to Sonnet per the spec. A `Sonnet@medium` orchestrator is the cheapest-capable model for these tasks.

### Estimated routed cost
**Medium.** One medium-complexity `schema-validation` call to Sonnet and one high-complexity `session-verification` call to `gpt-5-4`.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Read v0 parser schema and tier output schemas | `In-conversation` |
| 2 | Perform mechanical correspondence check and produce delta | `route(task_type="schema-validation", complexity_hint=40)` |
| 3 | Land `docs/schema/v0-vs-tiers.md` | `In-conversation` |
| 4 | Verify session deliverables against plan | `route(task_type="session-verification", complexity_hint=70)` |
| 5 | Generate change log for session commits | `route(task_type="summarization", max_tier=1)` |

### Actuals

- Orchestrator used: Claude Sonnet 4.6 @ effort=medium (in-conversation).
- Routed cost: ~$0.25 (3 verification rounds at gpt-5-4 + next-orc rec).
- Deviations: (1) `schema-validation` task type not yet in router-config.yaml (scheduled for vba-parser-session-set-generator child set #4); (2) Sonnet orchestrator = Sonnet routing target → in-conversation convention per project-guidance.md applied. Schema reconciliation done in-conversation. (3) Verification required 3 rounds: round 1 found 3 real issues (DeclareFunction/DeclareSub gap, provenance shape description, root-level mapping missing). Round 2 fixed D19 summary row. Round 3 returned bare 'ISSUES_FOUND' with no issues listed (ambiguous/malformed response at max retry; logged as deferred).
- Notes for next-session calibration: Session 3 is purely mechanical (csproj, sln, .g4 placeholder). Router recommends Claude Haiku 4.5 @ effort=low as the cheapest capable orchestrator.

**Next session orchestrator recommendation (Session 3):**
Claude Haiku 4.5 @ effort=low

**Rationale:** Session 3's scope is limited to mechanical file system and project file manipulations, which do not require the advanced reasoning of a mid-tier model. A low-cost, high-speed model is sufficient and the most cost-effective choice for these well-defined, command-line driven tasks.

---

## Session 3: ANTLR4 Build Wiring + Solution Update

### Recommended orchestrator
**Sonnet@medium**

### Rationale
This session consists of purely mechanical project file modifications and file system operations. No routed reasoning calls are expected. `Sonnet@medium` is the cheapest-capable model.

### Estimated routed cost
**Medium.** The only routed cost is the standard high-complexity `session-verification` call to `gpt-5-4`.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Create and configure `AccessHarvester.Vba.Grammar.csproj` | `In-conversation` |
| 2 | Add placeholder grammar, tests skeleton, and update solution file | `In-conversation` |
| 3 | Confirm `dotnet build` triggers ANTLR generation and commit gitignore | `In-conversation` |
| 4 | Verify session deliverables against plan | `route(task_type="session-verification", complexity_hint=70)` |
| 5 | Generate change log for session commits | `route(task_type="summarization", max_tier=1)` |

### Actuals
*(to be filled)*

---

## Session 4: Verifier-Surfaced Cleanup (Optional Buffer)

### Recommended orchestrator
**Sonnet@medium**

### Rationale
This is a buffer session for resolving any issues found by the verifier in previous sessions. Such tasks are typically mechanical code cleanup. `Sonnet@medium` is the appropriate default.

### Estimated routed cost
**Medium.** Assuming the session is used, the only routed cost is the standard high-complexity `session-verification` call to `gpt-5-4`.

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 1 | Resolve any deferred verifier findings from previous sessions | `In-conversation` |
| 2 | Verify session deliverables against plan | `route(task_type="session-verification", complexity_hint=70)` |
| 3 | Generate change log for session commits | `route(task_type="summarization", max_tier=1)` |

### Actuals
*(to be filled)*

---

## Session 1 Actuals

- Orchestrator used: Claude Sonnet 4.6 @ effort=medium (current conversation).
- Routed cost: ~$0.17 (ai-assignment $0.014 + verify r1 $0.129 + verify r2 $0.029 + next-orc rec ${result_rec.cost_usd:.4f}).
- Deviations: (1) architecture route to Opus/max timed out; harness designed in-conversation by Sonnet per lessons-learned.md deviation procedure. (2) Verification needed 2 rounds: round 1 found 4 real correctness issues (exit-code handling, schema required fields, diff path-key, perf machine-readable output). Both gaps closed; round 2 VERIFIED.
- Notes for next-session calibration: Session 2 has no architecture calls; schema-validation is Sonnet-pinned and in-conversation. Sonnet@medium is correct.

**Next session orchestrator recommendation (Session 2):**
Claude Sonnet @ effort=medium

**Rationale:** Session 2's scope is a mechanical, Sonnet-pinned schema validation task with no architecture calls, making Sonnet the direct choice for orchestrator. Session 1 revealed Sonnet's output is adequate for bounded tasks but requires careful verification to catch correctness gaps, justifying the `medium` effort rating.