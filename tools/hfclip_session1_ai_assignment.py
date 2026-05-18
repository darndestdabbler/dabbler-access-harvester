"""Author docs/session-sets/harvester-finish-cli-packaging/ai-assignment.md
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

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
SESSION_NUMBER = 1

SPEC_EXCERPT = (SESSION_SET / "spec.md").read_text(encoding="utf-8")

CONTEXT = """
Author `ai-assignment.md` for this two-session set per the schema in
docs/ai-led-session-workflow.md Step 3.5.

This is a no-UI repo (harvester / parser / aggregator). The set is the
final gate for Phase 4 — ship the harvester CLI as a self-contained
Windows binary release that a downstream consumer can install without
cloning the repo. Session 1 (in progress) is being orchestrated by
Claude Opus 4.7 @ effort=high; Session 2 has not yet started.

Session 1's work is small and mechanical: edit
`AccessHarvester.Cli/AccessHarvester.Cli.csproj` to add
`dotnet publish --self-contained` properties (RuntimeIdentifier=win-x64,
PublishSingleFile=true, PublishTrimmed=false because of ANTLR runtime
reflection + DAO COM interop, IncludeNativeLibrariesForSelfExtract=true
because Microsoft.Data.Sqlite has native deps), add a small publish
script under `tools/publish/`, gitignore the publish output dir, and
manually validate `<artifact>/AccessHarvester.exe --help`. Synthesis is
the trim/reflection trade-off explanation (which is brief). No
production code logic changes; the dispatcher / pipeline are untouched.

Session 2's work is the path-resolution helper + a small `[SkippableFact]`
black-box smoke test that re-runs the existing CLI usage / parse /
generate-docs tests against the published artifact, install + invoke
docs (schema-version alignment string included), README addendum,
closeout artifacts (`disposition.json`, `change-log.md`,
`next-session-set-recommendation.md`), commit, push, mark complete,
notify, then the Step 9 reorganization review after notify.

The set's routed cost target is ~$0.05-$0.20 per session
(cross-provider session-verification only), ~$0.10-$0.40 across the
set. Per project-guidance: when the orchestrator's model matches the
routing target for a given task type, handle the task in-conversation
rather than calling route(). Session-verification still routes (the
one non-negotiable cross-provider call).

Required output: a Markdown file with exactly two session blocks
(Session 1 actuals + Session 2 forward recommendation) per the schema
in the workflow doc (Recommended orchestrator / Rationale / Estimated
routed cost / per-step table / Actuals filled after / Next-session
orchestrator recommendation). Be concise — one to two sentences per
rationale, no padding. Use the spec excerpt below as ground truth.
"""

PROMPT = f"""Spec for the set:

```
{SPEC_EXCERPT[:7000]}
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
        step_number=2,
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
