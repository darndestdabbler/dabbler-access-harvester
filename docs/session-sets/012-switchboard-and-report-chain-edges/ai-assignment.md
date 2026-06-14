# AI Assignment — 012-switchboard-and-report-chain-edges

> Per-session orchestrator recommendations and step-level routing.
> Recommendations are authored via **routed** analysis (see
> `ai-assignment-analysis.md` for the raw routed output) — never
> self-opined, per `project-guidance.md`. Cross-provider
> `session-verification` is mandatory regardless of orchestrator.

## Session 1: Switchboard Row Materialization + Entry Points (R2-1, DW-9)

### Recommended orchestrator
Claude Code Opus 4.8 @ effort=high

### Rationale
Spec named Opus@high for S1; re-grounding turned the session synthesis-heavy
(the canonical-Switchboard-Manager premise was disproven, forcing a generic
faithful-mechanical design decision + model extension).

### Estimated routed cost
Moderate (decision-grounding + one mandatory cross-provider verification).

| Step | Action | Routing Decision |
|------|--------|------------------|
| 0–1 | Re-ground + register | Orchestrator (Opus); OLE DB probe of the 52 rows |
| 3.5 | ai-assignment | `route(analysis)` → gemini-pro |
| 2 | Switchboard signature detector (pure) | Orchestrator (mechanical, hermetic) |
| 3 | DAO row-read exporter + raw artifact | Orchestrator (COM-tier code) |
| 4 | Structure/macro builders + Build wiring | Orchestrator (synthesis + wiring) |
| 6 | Golden regen + COM confirmation | Orchestrator (COM run, TRX-as-truth) |
| 8 | Verification | `route(session-verification)` → gpt-5-4 (cross-provider) |

### Actuals (filled after the session)
- Orchestrator used: Claude Code Opus 4.8 @ effort=high
- Total routed cost: ~$0.319 (ai-assignment $0.0026 gemini-pro; verification R1
  $0.1799 + R2 $0.1366 gpt-5-4).
- Deviations from recommendation: row-read via DAO (not OLE DB) honoring the
  OleDbValueSampler escaping/degradation discipline; switchboard structure is
  generic faithful-mechanical (human decision) not canonical-Switchboard-Manager;
  StartUpForm DAO read added (human-approved) for DefaultFormName.
- Notes for next-session calibration: re-grounding made S1 more synthesis-heavy
  than a pure wiring session (premise disproven → human decision → generic design
  + model extension). Verification took 2 rounds (R1 found 2 Major + 1 Minor, all
  fixed; R2 1 Minor accepted-by-design). Opus@high was the right call.

**Next-session orchestrator recommendation (Session 2):**
Claude Code Opus 4.8 @ effort=high
Rationale: the high-leverage task is designing the closed chain-edge
kind/provenance set (synthesis); mechanical producer sub-tasks can route to
cheaper models.

## Session 2: Chain-Edge Model + Mechanical Producers (R2-2 part 1, DW-10)

### Recommended orchestrator
Claude Code Opus 4.8 @ effort=high

### Rationale
(Routed) The high-leverage task is designing the closed edge-kind/provenance
set — synthesis-heavy, justifying Opus — while mechanical producer-wiring
sub-tasks can be routed to cheaper models.

### Estimated routed cost
Moderate.

### Actuals (filled after the session)
- Orchestrator used: Claude Code Opus 4.8 @ effort=high.
- Total routed cost: ~$0.64 (verification gpt-5-4 ×4 rounds $0.2122 + $0.1513 +
  $0.1379 + $0.1121; next-orchestrator analysis gemini-pro $0.0014). All
  implementation (model design, producers, wiring, tests, golden regen, docs)
  handled in-conversation by Opus per the "orchestrator matches routing target"
  convention; only the mandatory cross-provider verification + the
  do-not-self-opine next-orch analysis were routed.
- Deviations from recommendation: two spec-wording deviations reconciled by
  re-grounding (consume the enricher's structured `EventMacros` rather than
  re-parse `OnClickEmMacro` block text; recover `TempVars!` textually because
  the JET grammar models bang-access only for Forms/Reports roots), both
  disclosed to the verifier.
- Verification: 4 convergent rounds (R1 3 Major + 2 Minor → R2 2 follow-ons →
  R3 2 narrower variants → R4 VERIFIED). The R3 fix replaced the regex
  TempVars recovery with a token-aware scanner (structural, not another patch),
  which closed the class. Exceeded the nominal 2-retry guidance but the
  sequence was convergent (each round strictly narrower, ending clean) and the
  HHC golden was byte-identical across all rounds. Opus@high was the right call.

**Next-session orchestrator recommendation (Session 3, routed gemini-pro):**
Claude Code Opus 4.8 @ effort=high — S3's code-behind chain edges hinge on
tracing stateful VBA data flow (`DoCmd.OpenForm … acDialog` → `Forms!dlg!ctl`
reads → `TempVars!x =` assignment pairing), which is static-analysis reasoning
beyond pattern matching; S3 also authors the next set and runs closeout-grade
verification.

## Session 3: Code-Behind Chain Edges + Coordinated Closeout (R2-2 part 2, DW-10)

### Recommended orchestrator
Claude Code Opus 4.8 @ effort=high

### Rationale
(Routed) Dominated by synthesis-heavy tasks: resolving runtime control-flow
edges, authoring the next work set, and closeout-grade critical analysis.

### Estimated routed cost
High.
