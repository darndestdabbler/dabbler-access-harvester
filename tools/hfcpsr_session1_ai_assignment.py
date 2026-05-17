"""Author docs/session-sets/harvester-finish-canonical-parse-success-report/ai-assignment.md
via routed analysis per Rule #17.

Run from main/ in this repo with the ai_router venv active.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
SESSION_NUMBER = 1

SPEC_EXCERPT = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
INVENTORY = (SESSION_SET / "property-sheet-inventory.md").read_text(encoding="utf-8")

CONTEXT = """
Author `ai-assignment.md` for this two-session set per the schema in
docs/ai-led-session-workflow.md Step 3.5.

This is a no-UI repo (harvester / parser / aggregator). The set is
Phase 4 closure work — produce a committed per-fixture
`parse-success-report.json` plus a regression test that diffs against
it. Session 1 is in progress and is being orchestrated by Claude Opus
4.7 @ effort=high; Session 2 has not yet started.

Session 1's work is mostly synthesis (spec elaboration, inventory
classification, report-shape design) plus mechanical scaffolding (the
YAML loader + parser-invoking harness in C# under
tests/AccessHarvester.Pipeline.Tests/). Synthesis stays in-conversation
on Opus per the established Convention; routed reasoning is only the
cross-provider session-verification step.

Session 2's work is the diff-flip + sentinel test + per-reason
narrative + closeout artifacts. Mechanical with a synthesis layer for
the narrative.

The set is small (~$0.10-$0.40 routed across both sessions per the
spec's cost band). The router's per-task-type defaults from
router-config.yaml should pick the cheapest capable model for each
step; the ai-assignment.md should reflect that economics, not the
orchestrator's self-image.

Required output: a Markdown file with exactly two session blocks
(Session 1 actuals + Session 2 forward recommendation) per the schema
in the workflow doc (Recommended orchestrator / Rationale / Estimated
routed cost / per-step table / Actuals filled after / Next-session
orchestrator recommendation). Be concise — one to two sentences per
rationale, no padding. Use the spec and inventory verdict below as
ground truth.
"""

PROMPT = f"""Spec for the set:

```
{SPEC_EXCERPT[:6000]}
```

Property-sheet inventory verdict:

```
{INVENTORY[:3500]}
```

{CONTEXT}
"""


def main() -> int:
    result = route(
        content=PROMPT,
        task_type="analysis",
        context="ai-assignment.md authoring per Step 3.5",
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )
    out_path = SESSION_SET / "ai-assignment.md"
    out_path.write_text(result.content, encoding="utf-8")
    print(f"[wrote {out_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")

    log = SessionLog(str(SESSION_SET), total_sessions=2)
    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=3,
        step_key="session-1/ai-assignment-routed",
        description="Authored ai-assignment.md via route(task_type='analysis')",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "analysis",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
