# AI Assignment: `harvester-finish-canonical-parse-success-report`

> Assignment manifest for the two-session set. See
> `docs/ai-led-session-workflow.md` for schema.

## Session 1

- **Recommended orchestrator:** Claude Opus 4.7 @ effort=high
- **Rationale:** Session requires high-level synthesis to elaborate the spec, classify inventory, and design the report JSON schema. The orchestrator's role is primarily design and reasoning.
- **Estimated routed cost:** $0.05 - $0.20

| Step | Task description                                                                 | Recommended model      | Rationale                                                                        |
| :--- | :------------------------------------------------------------------------------- | :--------------------- | :------------------------------------------------------------------------------- |
| 1.1  | Elaborate spec, perform property-sheet inventory, and design report JSON shape.  | Orchestrator (in-conv) | Core synthesis work; kept in-conversation with the primary orchestrator for context. |
| 1.2  | Implement C# harness to load YAML mirrors and invoke parsers for all sources.    | GPT-4o                 | Mechanical coding task. The router default selects the cheapest capable model.     |
| 1.3  | Implement C# logic to aggregate counts and `UnresolvedItem` data into the report. | GPT-4o                 | Mechanical coding task following the JSON shape designed in Step 1.1.            |
| 1.4  | Cross-provider verification of spec, inventory verdict, and harness logic.       | GPT-4o                 | Standard cross-provider check for correctness and adherence to project conventions.  |

### Actuals (filled after the session — 2026-05-17)

- **Orchestrator used:** Claude Opus 4.7 (1M context) @ effort=high.
- **Total routed cost:** ~$0.58
  - `analysis` (Gemini Pro, ai-assignment authoring): $0.0115
  - `session-verification` round 1 (gpt-5-4): $0.1823
  - `session-verification` round 2 (gpt-5-4, complexity_hint=85): $0.1819
  - `session-verification` round 3 (gpt-5-4, complexity_hint=85): $0.1004
- **Deviations from recommendation:**
  - The recommendation table named "GPT-4o" and "Claude 3.5 Sonnet" as illustrative model names; the router's actual selections were Gemini Pro (analysis) and gpt-5-4 (session-verification), per the active `router-config.yaml` task-type overrides. No human-driven override applied.
  - The recommendation envisioned a 1.4 routed verification step ($0.05-$0.20 band). Actual cost overshoot was driven by three verification rounds (R1 surfaced 4 Major + 1 Minor → fixed; R2 surfaced 1 Major + 1 Minor → fixed; R3 surfaced 1 Minor doc drift → fixed in-line). Each round was ~$0.10-$0.18; convergence took the retry cap (max-2-retries / max-3-rounds per the workflow doc).
- **Notes for next-session calibration:**
  - The first-pass interim-golden artifact in this set is committed. Session 2 inherits the live diff gate; no mode-flip is required.
  - The high VBA parseFailed rate (HHC 29/32, Northwind 52/66) is a genuine Phase 4 parser-scope gap (form/report code-behind modules carry `Attribute VB_GlobalNameSpace = ...` lines the `Vba.g4` grammar does not handle). The session-complete notification body raises this as the primary item for human attention before Session 2 starts. Per the stub stop-rules, grammar extensions are a strategy-level decision and do NOT absorb into Phase 4.
  - The property-sheet inventory tool and the C# generator now share a regex shape (`(?:[^"\\]|\\.)*` for quoted-scalar bodies) so future tweaks must update both sites in lock-step.

**Next-session orchestrator recommendation (Session 2):** see the routed recommendation in the Session 2 block below.

## Session 2

- **Recommended orchestrator:** Claude Opus 4.7 @ effort=medium
- **Rationale:** Session mixes mechanical test implementation with a synthesis task (narrative writing), justifying a high-capability orchestrator.
- **Estimated routed cost:** $0.05 - $0.20

| Step | Task description                                                                     | Recommended model      | Rationale                                                                      |
| :--- | :----------------------------------------------------------------------------------- | :--------------------- | :----------------------------------------------------------------------------- |
| 2.1  | Implement the regression test to diff generated reports against committed goldens.   | GPT-4o                 | Mechanical test-writing task. The router default is sufficient.                |
| 2.2  | Author `unresolved-reason-narrative.md` for any new `UnresolvedItem` reasons found.  | Orchestrator (in-conv) | Synthesis task requiring nuanced descriptions. Kept in-conversation for context. |
| 2.3  | Generate final reports, commit all artifacts, and author closeout documents.        | Claude 3.5 Sonnet      | Procedural tasks. Can be delegated to a fast, cost-effective model.            |
| 2.4  | Cross-provider verification of test logic, narrative, and final committed artifacts. | GPT-4o                 | Final quality gate for the session set's deliverables.                       |

### Actuals (filled after the session — 2026-05-17)

- **Orchestrator used:** Claude Opus 4.7 (1M context) @ effort=high (not effort=medium as the routed Session 1 recommendation suggested — see deviations).
- **Total routed cost:** ~$0.44
  - `session-verification` round 1 (gpt-5-4): $0.1886
  - `session-verification` round 2 (gpt-5-4, complexity_hint=85): $0.1068
  - `session-verification` round 3 (gpt-5-4, complexity_hint=85): $0.1280
  - `analysis` (gemini-pro, next-session-set recommendation): $0.0146
  - `summarization` (gemini-flash, change-log; `max_tier=1` per Convention): $0.0019
- **Deviations from recommendation:**
  - **Effort level.** Session 1's routed analysis recommended Opus @ effort=medium; the orchestrator ran at effort=high (no human-driven maxout — the start-up was registered as effort=high on the engine side; a future calibration round can decide whether to honor the medium recommendation downward). No observable quality difference; cost was driven by the 3 verification rounds, not orchestrator effort.
  - **Verification rounds.** Session 2 also took the full 3-round retry budget (R1 2M+1m → R2 1m → R3 1m), all on narrative drift. Pattern matches Session 1 (R1 4M+1m → R2 1M+1m → R3 1m). The narrative was a substantial synthesis doc (~720 lines pre-fix, ~830 lines post-fix), so a single round was unlikely to suffice; future synthesis-heavy sessions should budget for 3 rounds.
- **Notes for next-session calibration:**
  - The narrative's authority-framing slipped through Post-Fix Sweep twice (R2 caught Authority + Stability commitment; R3 caught Cross-cutting bullet). For future narrative-rule edits, sweep the *introduction*, *authority*, and *closing/stability* blocks together, not just the body — that's where global-framing language tends to lag the body fix.
  - The `ParseSuccessReportDiff` helper refactor was clean enough that the verifier never flagged any code-side issue; this validates the "extract helper, both production gate + sentinel consume same builder" pattern for future diff-gate sentinels.
  - The plan-driven next-set recommendation flow worked smoothly via `route(task_type="analysis")` with the ROADMAP excerpt + remaining-work-tree as context; gemini-pro handled it at $0.0146 with no escalation.

**Next-session orchestrator recommendation:** N/A (final session of the set; the cross-set handoff to `harvester-finish-canonical-golden-output` is in [`next-session-set-recommendation.md`](next-session-set-recommendation.md) — Claude Opus 4.7 @ effort=high recommended for that set's spec-elaboration Session 1, mirroring this set's Session 1 resourcing.)