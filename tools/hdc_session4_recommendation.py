"""Route the Session 4 orchestrator recommendation via gemini-pro analysis."""

from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

ROOT = Path(__file__).resolve().parent.parent
SESSION_SET = ROOT / "docs" / "session-sets" / "harvester-data-classifier"

SPEC = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
PLAN = (ROOT / "docs" / "planning" / "data-classification-plan.md").read_text(encoding="utf-8")
GUIDANCE = (ROOT / "docs" / "planning" / "project-guidance.md").read_text(encoding="utf-8")

# Pull just the Session 4 block from the spec.
s4_start = SPEC.find("### Session 4 of 4")
s4_end = SPEC.find("## Stop Rules")
s4_block = SPEC[s4_start:s4_end].strip() if s4_start >= 0 and s4_end > s4_start else SPEC

# Pull the plan sections Session 4 relies on (§ 3.5 taxonomy + § 5 ID/FK invariants + § 8 Access gotchas).
plan_start = PLAN.find("### 3.5 Category taxonomy")
plan_end = PLAN.find("## 9. Open questions")
plan_block = PLAN[plan_start:plan_end] if plan_start >= 0 and plan_end > plan_start else PLAN

# Pull the relevant project-guidance Conventions.
gud_idx = GUIDANCE.find("Pin mechanical-correspondence task types to Sonnet")
gud_excerpt = GUIDANCE[gud_idx:gud_idx + 3000] if gud_idx >= 0 else GUIDANCE[:3000]

prompt = f"""You are routing a recommendation for the **Session 4 of 4 orchestrator** on
session set `harvester-data-classifier` in the `dabbler-access-harvester` repo.
Sessions 1-3 are complete; you are picking the orchestrator for the final
session (`Test-Data Generator CLI`). Sessions 1 and 2 both ran on
`claude-opus-4-7` at effort=medium despite routed Sonnet-at-high
recommendations (operator override; Anthropic Max window). Session 3 also ran
on `claude-opus-4-7` for the same reason.

Your output is a strict three-line block:

    Recommended orchestrator: <engine> <model> @ effort=<low|medium|high|max>
    Rationale: <2-3 sentences>
    Estimated routed cost: <Low | Low-Moderate | Moderate | High> ($x.xx-$y.yy)

Be honest about what the work demands. Session 4 ships the AccessHarvester
sanitize subcommand with a FK graph builder, per-category masking handlers,
and lockstep PK/FK remap, plus unit + skippable integration tests. It's
larger and trickier than Session 3 (which was a single HTML editor) — the FK
consistency invariant has a real correctness risk if the wrong choice is made
on output format or remap ordering. But the core algorithm is well-specified
in the plan section 5 + section 3.5 + section 8.

Cite which project-guidance Convention or lesson supports your tier choice
(mechanical-correspondence-pins-to-Sonnet, orchestrator-matches-routing-target,
or synthesis-document-Opus-orchestrator-when-large). Do not propose
`task_type="planning"` for any sub-task.

Session 4 spec block
====================
```markdown
{s4_block}
```

Data classification plan sections 3.5 + 5 + 8
==============================================
```markdown
{plan_block[:5000]}
```

Project-guidance conventions excerpt
=====================================
```markdown
{gud_excerpt}
```

Output ONLY the three-line block. No preamble, no JSON, no markdown headers.
"""

result = route(
    content=prompt,
    task_type="analysis",
    session_set=str(SESSION_SET),
    session_number=3,
)

out_path = SESSION_SET / "session-4-recommendation.md"
out_path.write_text(result.content, encoding="utf-8")
print(f"Wrote {out_path.relative_to(ROOT).as_posix()}")
print(f"Model: {result.model_name}")
print(f"Cost: ${result.cost_usd:.4f}")
print(f"Input tokens:  {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print()
print(result.content)

log = SessionLog(str(SESSION_SET))
log.log_step(
    session_number=3,
    step_number=7,
    step_key="session-3/session-4-recommendation",
    description=(
        f"Routed Session 4 orchestrator recommendation via route(task_type='analysis') "
        f"-> {result.model_name} (cost ${result.cost_usd:.4f}). Sent the Session 4 spec block, "
        f"data-classification-plan sections 3.5+5+8, and the project-guidance excerpt on "
        f"mechanical-correspondence pinning. Wrote {out_path.name}."
    ),
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "analysis",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)
