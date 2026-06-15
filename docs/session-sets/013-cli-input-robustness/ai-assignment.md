### Session 1 of 2: Exit-Code Correctness

-   **Recommended orchestrator engine+model+effort**: Claude Code (Opus 4.8), Medium
-   **Rationale**: The task involves precise changes to argument validation and file path sanitization across multiple commands, with specific hermetic test requirements. This requires strong code comprehension and generation, fitting a medium-sized task for a top-tier model.
-   **Estimated routed cost**: moderate
-   **Execution plan**:
| Step | Action | Routing-Decision |
| :--- | :--- | :--- |
| 1 | Implement `--types` argument validation and `ArgumentException` for `extract`/`translate`/`run` (F-1-3). | Claude Code |
| 2 | Implement `SaveAsTextExporter` filename sanitization and collision guard (F-1-2). | Claude Code |
| 3 | Add/update hermetic test pins for both fixes. | Claude Code |
| 4 | Run quality gate: `dotnet build` + affected hermetic test projects. | Human |
| 5 | Cross-provider session verification: review implementation against requirements. | gpt-5-4 |

### Session 2 of 2: ParseOptions Consolidation, Tokenization, and Closeout

-   **Recommended orchestrator engine+model+effort**: Claude Code (Opus 4.8), Large
-   **Rationale**: This session combines wide-impact refactoring (O-1-4), a high-risk logic change that may affect golden files (F-1-6), and session closeout. The complexity and risk, especially of the tokenization fix, warrant a Large effort estimate.
-   **Estimated routed cost**: high
-   **Execution plan**:
| Step | Action | Routing-Decision |
| :--- | :--- | :--- |
| 1 | Refactor `ParseOptions` across 7 subcommands for shared unknown-switch rejection (O-1-4). | Claude Code |
| 2 | Implement `OleDbConnectionStringBuilder` (F-1-5) and bracket-aware `PARAMETERS` tokenization (F-1-6) fixes. | Claude Code |
| 3 | Run quality gate; assess tokenization fix for golden file regeneration impact. | Human |
| 4 | Perform session set closeout: disposition all work items, update changelog, author next session set (O-1-7). | Human |
| 5 | Cross-provider session verification: review refactoring and critical tokenization logic. | gpt-5-4 |

---
**Next-session recommendation**: 014-perf-and-correctness: Tackle next-highest-priority correctness issues and introduce initial performance-sensitive query optimizations.

---

## Actuals (recorded at closeout)

### Session 1 — actuals
- **Orchestrator:** Claude Code (Opus 4.8). **Verdict:** VERIFIED (gpt-5-4 R1,
  empty issues, $0.16). Delivered F-1-3 + F-1-2; no golden regen (proven
  byte-level). See `s1-verification.md`.

### Session 2 — actuals
- **Orchestrator:** Claude Code (Opus 4.8 @ high), as recommended.
- **Delivered:** O-1-4 (shared `OptionParser` with unknown- AND repeated-switch
  rejection across all 12 subcommands), O-1-7 (single `Commands` dispatch table),
  F-1-5 (`AceConnectionString` at **four** sites — re-grounding found four, not
  the spec's two), F-1-6 (bracket-aware `PARAMETERS` tokenisation, proven
  golden-neutral byte-level). DW-15 recorded (`identify-cleanup` exit-2 usage
  convention, preserved out of scope).
- **Validation:** hermetic `AccessHarvester.Tests` 691/0; black-box Integration
  15/15; COM tier 10/10; no golden regen (byte-level proof).
- **Verification:** routed cross-provider gpt-5-4 — **R1 ISSUES_FOUND** (1 Major:
  shared parser silently overwrote repeated options) → fixed → **R2 VERIFIED**.
  Routed cost: ~$0.18 (verification R1 $0.169 + R2 $0.011) + a small `planning`
  call for the next-set recommendation. A set-wide devil's-advocate prompt
  (`set013-devils-advocate-prompt.md`) was authored for human-driven GPT+Gemini
  review per the cross-session weak-evidence convention.
- **Routing-decision note:** the duplicate-option Major was a *pre-existing* gap
  in all seven prior `ParseOptions` copies; O-1-4's consolidation made it
  closeable in one place, which is exactly why the routed adversarial pass earned
  its keep here — it caught an honest-error gap squarely on this set's theme that
  the orchestrator's own implementation had carried forward unquestioned.

**Next session set:** `014-cleanup-zero-date-guard` (006 P6) — spec + v4 stub
authored; routed recommendation in `next-session-set-recommendation.md`.