"""
Route the Session-3 orchestrator recommendation per Rule #17.
Forces cross-provider routing (complexity_hint=50 → tier 2 → Gemini Pro,
distinct from the Opus orchestrator).
"""
from pathlib import Path
import json
import sys

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/remaining-work-planner")
SESSION_NUMBER = 2

REPO = Path(".")
def read(p): return (REPO / p).read_text(encoding="utf-8")

planner_spec_excerpt = read("docs/session-sets/remaining-work-planner/spec.md")
# Pull Session 3 plan only
s3_start = planner_spec_excerpt.find("### Session 3 of 3")
s3_end = planner_spec_excerpt.find("---", s3_start + 50)
session3_plan = planner_spec_excerpt[s3_start:s3_end]

tree = read("docs/session-sets/remaining-work-planner/remaining-work-tree.md")

# Session 2 actuals summary
session2_actuals = """
**Session 2 actuals (for calibration):**
- Orchestrator: Claude Opus 4.7 @ effort=high.
- Total routed cost: $0.6722 across 3 verification rounds
  (R1 $0.2710, R2 $0.2422, R3 $0.1590 — all gpt-5-4 cross-provider).
- Verification verdict: VERIFIED (Round 3).
  R1 found 3 Major (tree prereq inconsistency; missing skip
  rationale for tree-amendments.md / routing-playbook-delta.md;
  missing freeze-boundary bullets across all 5 stubs).
  R2 found 1 Major + 1 Minor introduced by the R1 fixes / deeper
  sweep (F3 determinism gate didn't cover extract/translate;
  cost rollup mismatch).
  R3 confirmed clean.
- Deliverables: 5 stub specs (F1-F5), remaining-work-tree.md,
  no tree-amendments.md (no proposals), no routing-playbook-delta.md
  (no new task types).
- Calibration note: the same retry-rounds pattern as Session 1
  ($0.337 there). The "pre-fix narrative sweep" lesson would have
  caught the R2 cost-rollup mismatch but not the F3 determinism
  finding (R2 surfaced that via deeper criterion-by-criterion
  reading). Suggests the lesson is sound but doesn't eliminate
  legitimate R2-discovery rounds.
"""

prompt = f"""You are routing-economics analyst. Recommend the orchestrator
(engine + model + effort level) for Session 3 of `remaining-work-planner`.
Session 3's plan and the Session 2 actuals are below. Return:

1. **Recommended orchestrator:** engine + model + effort (e.g.,
   "claude-code claude-opus-4-7 @ effort=medium").
2. **Rationale:** 2 sentences on why this engine fits Session 3's
   work mix.
3. **Estimated routed cost:** qualitative ("low" / "moderate" / "high").
4. **Calibration deltas from Session 2:** anything specific the
   Session 3 orchestrator should do differently.

Bias your recommendation by:

- Session 3 is mostly synthesis: enumerate every session set under
  `docs/session-sets/`, build dependency DAG, write ROADMAP.md
  (pointer doc — one paragraph per set + link), update strategy.md
  with the planned-sequence actuals, write change-log.md.
- The harvester's outsource-last pattern (in-conversation authoring
  under subscription windows when the orchestrator matches the
  routing target; route() for cross-provider verification only).
- The orchestrator-matches-target convention favours Opus or Sonnet
  for ROADMAP/change-log work since both are eligible
  in-conversation authoring targets.

# Session 3 plan

{session3_plan}

# Session 2 actuals

{session2_actuals}

# remaining-work-tree.md (Session 2 output — input to Session 3's ROADMAP)

```markdown
{tree}
```

Be concise. Total response under 500 words.
"""

print(f"Prompt size: {len(prompt)} chars", file=sys.stderr)

result = route(
    content=prompt,
    task_type="analysis",
    complexity_hint=50,   # force tier 2 → Gemini Pro (cross-provider)
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

out_path = SESSION_SET / "session-3-next-orchestrator-recommendation.md"
out_path.write_text(result.content, encoding="utf-8")

meta_path = SESSION_SET / "session-3-next-orchestrator-recommendation-meta.json"
meta_path.write_text(json.dumps({
    "model_name": result.model_name,
    "input_tokens": result.input_tokens,
    "output_tokens": result.output_tokens,
    "cost_usd": result.cost_usd,
    "task_type": "analysis",
    "session_set": str(SESSION_SET),
    "session_number": SESSION_NUMBER,
}, indent=2), encoding="utf-8")

import re
safe = re.sub(r'[^\x00-\x7f]', '?', result.content)
print(f"Recommendation model: {result.model_name}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Cost USD: ${result.cost_usd:.4f}")
print(f"Output written to: {out_path}")
print("--- FULL ROUTED RECOMMENDATION (ASCII-safe) ---")
print(safe)

log = SessionLog(SESSION_SET)
log.log_step(
    session_number=SESSION_NUMBER,
    step_number=13,
    step_key="session-2/next-orc-recommendation",
    description="Routed next-orchestrator recommendation via Gemini Pro (cross-provider)",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "analysis",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)
print("Step logged.")
