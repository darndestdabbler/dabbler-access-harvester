# AI Assignment: resolver-default-members-and-inference (Phase 3.5-3.7)

This session set implements the default-member resolver, type inference, and a cross-reference index for the `AccessHarvester.Vba.Resolver`. The assignment follows the repository convention of using the Opus-tier orchestrator for complex, in-conversation synthesis sessions where the orchestrator tier matches the task tier (architecture). Mechanical and validation tasks are routed to a Sonnet-tier model for efficiency and precision on structured tasks, per the established `adopt-now #10` retrospective action item.

---

## Session 1: Default-Member Registry (Synthesis)

**Recommended orchestrator**: `claude-opus-4-7`

**Rationale**: The task is a complex architecture synthesis call with a complexity hint of 80. Per repository convention, when the orchestrator tier (Opus) matches the task's target tier (architecture synthesis), the session is handled in-conversation to leverage the model's full context and reasoning capabilities.

**Estimated routed cost**: High

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 3.5 | Analyze session spec and select orchestrator. | Human analysis. |
| 3.6 | Author session prompt. | `claude-opus-4-7` (in-conversation synthesis) |
| 3.7 | Execute session prompt. | `claude-opus-4-7` (in-conversation synthesis) |

**Actuals (filled after the session)**:
*   Actual orchestrator: `claude-opus-4-7` @ effort=high (Claude Code), 2026-04-27
*   Actual routed cost: ~$0.378
    - ai-assignment.md (gemini-pro, analysis): $0.0122
    - Round 1 session-verification (gpt-5-4): $0.1699
    - Round 2 session-verification (gpt-5-4): $0.0944
    - Next-orchestrator recommendation (gemini-pro, analysis): $0.0015
    - Synthesis itself: $0 (in-conversation per the matching-orchestrator convention)
*   Deviations from recommendation: none — synthesis authored in-conversation as expected; routed verification + ai-assignment + next-orc went out as planned.
*   Session outcome: VERIFIED with minor doc-fix round (Round 1: 5 findings — 2 Major, 3 Minor — all fixed; Round 2: 2 Minor doc-comment fixes, both applied; final state pass-with-confidence-high). Resolver tests 247 passed; full suite 995 passed, 2 skipped.
*   Notes for next-session calibration: round 1 raised 2 Major real defects (Collection/Dictionary kind misclassification; Control row over-broad). Worth keeping the verifier-disclosure context tight in Session 2 — list both in-scope deferrals and explicit non-goals up front so the verifier doesn't repeat the "where's the wiring?" false-positive shape.

**Next-session orchestrator recommendation (Session 2)**: `claude-opus-4-7` @ effort=high
**Rationale (routed via gemini-pro analysis):** Session 2 has the same complexity hint and architecture target as Session 1, where a high-effort Opus orchestrator produced an artifact that still required two rounds of verification. Reducing the effort level for this new, logically complex task would risk a lower-quality initial draft, likely increasing total cost and duration due to more extensive rework.

---

## Session 2: Type Inference (Synthesis)

**Recommended orchestrator**: `claude-opus-4-7`

**Rationale**: This session is another complex architecture synthesis task, mirroring the complexity of Session 1. The Opus-tier orchestrator is best suited for this level of code generation and design, continuing the established approach for consistency and high-quality output.

**Estimated routed cost**: High

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 3.5 | Analyze session spec and select orchestrator. | Human analysis. |
| 3.6 | Author session prompt. | `claude-opus-4-7` (in-conversation synthesis) |
| 3.7 | Execute session prompt. | `claude-opus-4-7` (in-conversation synthesis) |

**Actuals (filled after the session)**:
*   Actual orchestrator: `claude-opus-4-7` @ effort=high (Claude Code), 2026-04-27.
*   Actual routed cost: ~$0.57
    - Round 1 session-verification (gpt-5-4): $0.1699
    - Round 2 session-verification (gpt-5-4): $0.2268
    - Round 3 session-verification (gpt-5-4): $0.2278
    - Next-orchestrator recommendation (gemini-pro, analysis): $0.0020
    - Synthesis itself: $0 (in-conversation per the matching-orchestrator convention)
*   Deviations from recommendation: none — synthesis authored in-conversation as expected.
*   Session outcome: 3 verification rounds (initial + 2 retries — cap hit). Round 1 = 2 Major + 1 Minor (all fixed: TypeSuffix fallback, With-flattening unwrap in EvaluateCall, ReadOnlyCollection wrap on InferredType.Evidence). Round 2 = 2 Major (all fixed: ConcreteName filters Variant from evidence; EvaluateDeclarationOutcome routes bare-name procedure refs through ReturnTypeOf). Round 3 = 1 Minor (fixed: replaced misleading With-flattening test with Form-module + StubRegistry test that actually exercises the path). Resolver tests 295 passed; full suite 1043 passed, 2 skipped.
*   Notes for next-session calibration: Synthesis doc was overconfident about Variant-as-evidence semantics — verifier correctly pushed back. For Session 3 the synthesis should explicitly enumerate every ResolutionOutcome variant the new code touches (the bare-name vs. call distinction was missed at synthesis time and surfaced at round 2). Three rounds is at the cap; if a future session needs a fourth round the spec should be revisited.

**Next-session orchestrator recommendation (Session 3)**: `claude-opus-4-7` @ effort=medium
**Rationale (routed via gemini-pro analysis):** The task is a well-defined implementation (map inversion) rather than novel design, warranting a step down in effort from Session 2's high. Retaining the incumbent orchestrator provides continuity and leverages its proven context handling for the complex resolver system, while the reduced effort level appropriately matches the task's lower complexity.

---

## Session 3: Cross-Reference Index

**Recommended orchestrator**: `claude-opus-4-7` @ effort=medium

**Rationale (routed via gemini-pro analysis after Session 2)**: The task is a well-defined implementation (map inversion) rather than novel design, warranting a step down in effort from Session 2's high. Retaining the incumbent orchestrator provides continuity and leverages its proven context handling for the complex resolver system, while the reduced effort level appropriately matches the task's lower complexity.

**Estimated routed cost**: Low

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 3.5 | Analyze session spec and select orchestrator. | Human analysis. |
| 3.6 | Author session prompt. | `claude-opus-4-7` (medium effort, in-conversation if Opus stays orchestrator; otherwise routed analysis/architecture) |
| 3.7 | Execute session prompt. | `claude-opus-4-7` |

**Actuals (filled after the session)**:
*   Actual orchestrator: `claude-opus-4-7` @ effort=medium (Claude Code), 2026-04-27.
*   Actual routed cost: ~$0.146
    - Round 1 session-verification (gpt-5-4): $0.1267
    - Round 2 session-verification (gpt-5-4): $0.0171
    - Next-orchestrator recommendation (gemini-pro, analysis): $0.0018
    - Implementation itself: $0 (in-conversation per the matching-orchestrator convention)
*   Deviations from recommendation: none — handled in-conversation as expected; routed verification + next-orc went out as planned.
*   Session outcome: VERIFIED with one minor doc-fix round. Round 1 raised 1 Medium issue (Build_WithFlattening_IndexesUnderInnerDeclaration test did not actually exercise ResolvedViaWithFlattening because the source-driven flattening produces an Unresolved inner outcome rather than Resolved); fixed by hand-building a synthetic ResolvedReference whose Outcome wraps a real Resolved(targetDecl). Round 2 (gpt-5-4) confirmed Resolved. Resolver tests 305 passed (was 295); full suite 999 passed, 2 skipped, 0 failed.
*   Notes for next-session calibration: Round 1 cost dropped from session 2's $0.17 to $0.13 thanks to tighter scope (one new file plus tests). Source-driven flattening tests against Variant targets land at DefaultMemberNotYetResolved, not Resolved — when a future session needs to test the unwrap path, prefer hand-built outcomes over parsing a `With rs / .Foo` source whose inner outcome is unresolved by design.

**Next-session orchestrator recommendation (Session 4)**: `claude-sonnet-4-6` @ effort=low
**Rationale (routed via gemini-pro analysis):** The tasks are mechanical verification and routing, aligning with the project's explicit "Sonnet-pinned" convention for schema-validation. This session requires less novel synthesis than Session 3, making a more cost-effective model at a lower effort setting appropriate.

---

## Session 4: Cross-cut + Mechanical Tightening

**Recommended orchestrator**: `claude-3-sonnet-20240229`

**Rationale**: This session involves mechanical tasks like schema validation and running tests, which do not require advanced architectural reasoning. Per repository conventions, these well-defined, mechanical-correspondence tasks are routed to a Sonnet-tier model for efficiency and cost-effectiveness.

**Estimated routed cost**: Low

| Step | Action | Routing Decision |
| :--- | :--- | :--- |
| 3.5 | Analyze session spec and select orchestrator. | Human analysis. |
| 3.6 | Author session prompt. | Routed to `claude-3-sonnet-20240229`. |
| 3.7 | Execute session prompt. | Routed to `claude-3-sonnet-20240229`. |

**Actuals (filled after the session)**:
*   Actual orchestrator:
*   Actual cost:
*   Session outcome:

**Next-session orchestrator recommendation (Session 5)**: n/a - last session