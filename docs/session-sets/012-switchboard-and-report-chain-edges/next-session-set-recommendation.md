# Next session set recommendation (authored by 012 Session 3 closeout)

**Recommendation source:** routed analysis (`route(task_type="analysis")` → ?), per the "author the next-session-set recommendation via routed analysis — never self-opine" convention. Routed verdict verbatim below.

---

The next set should be `cli-input-robustness` (P5). This set directly addresses the highest-severity remaining consumer risk: a silent success (exit 0) on a failed harvest caused by a simple command-line typo. For an automated, non-interactive tool, incorrect exit codes are a fundamental breach of contract that silently poisons downstream data pipelines. This insidious failure mode is more critical to fix than the `cleanup-zero-date-guard` (P6) issue, whose incorrect output is at least noisy and obvious. P5 also includes a valuable consolidation of CLI parsing logic that improves the robustness of all subcommands, providing a high return on its two-session investment.

NEXT SET: cli-input-robustness
ORCHESTRATOR: Sonnet 4.6 @ effort=high