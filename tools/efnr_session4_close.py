"""Session 4 close-out: change-log + next-set recommendation via routed analysis."""
from ai_router import route, query

SESSION_SET_DIR = 'docs/session-sets/enrichment-functions-navigation-review'

# ── 1. Generate change-log via summarization (max_tier=1 per convention). ──

with open(f'{SESSION_SET_DIR}/spec.md', 'r', encoding='utf-8') as f:
    spec_md = f.read()
with open(f'{SESSION_SET_DIR}/ai-assignment.md', 'r', encoding='utf-8') as f:
    ai_assign = f.read()

change_log_prompt = (
    "Compose a Markdown change log for the enrichment-functions-navigation-review "
    "session set (4 of 4-5 regular sessions complete; Session 5 buffer not needed).\n\n"
    "FORMAT (use exactly this structure):\n\n"
    "# Change Log: enrichment-functions-navigation-review\n\n"
    "**Summary:** <one paragraph on the set's deliverable>\n\n"
    "**Key Deliverables:**\n"
    "- <feature with files in parentheses>\n"
    "- <feature ...>\n\n"
    "**Output Files:**\n"
    "- <path/to/file.cs>\n"
    "- <path/to/file.cs>\n\n"
    "**Test Count:** <N> hermetic xUnit tests added across the set.\n\n"
    "Keep it under 400 words. Mirror the tone of accessexportertemp1-import / "
    "enrichment-events-and-validation change logs (factual, ledger-style).\n\n"
    "INPUTS\n"
    f"=== spec.md ===\n{spec_md}\n\n"
    f"=== ai-assignment.md (Sessions 1-3 actuals) ===\n{ai_assign}\n\n"
    "Session 4 actuals not yet appended to ai-assignment.md but were:\n"
    "- Authored tests/AccessHarvester.Pipeline.Tests/EndToEndFixtureTests.cs (20 hermetic tests covering full parser→resolver→FunctionClassificationBuilder→NavigationGraphBuilder→UnresolvedItemAggregator chain across the 5 anchor fixtures, plus envelope-shape + determinism theory tests + Declare-guard theory).\n"
    "- Routed schema-validation analysis to Sonnet ($0.1384) which surfaced 1 Critical (Declare→schema gap deferred to #17 v0-G5), 1 Major (0-based ANTLR Column emitting startColumn:0), 1 Minor (test assertion masked Critical). All 3 fixed.\n"
    "- Build green; 1371 passed, 4 skipped (Windows-only Access COM), 0 failed.\n\n"
    "Total set test growth: 1278 (start) → 1371 (end) = +93 tests across all 4 sessions."
)

result = route(
    content=change_log_prompt,
    task_type='summarization',
    max_tier=1,
    session_set=SESSION_SET_DIR,
    session_number=4,
)
with open(f'{SESSION_SET_DIR}/change-log.md', 'w', encoding='utf-8') as f:
    f.write(result.content)
print(f"Change-log written. model={result.model_name} cost=${result.cost_usd:.4f}")

# ── 2. Next-session-set recommendation. Plan-driven repo (VBA-Parser-Migration-Harvester-Plan.md). ──

with open('docs/VBA-Parser-Migration-Harvester-Plan.md', 'r', encoding='utf-8') as f:
    plan = f.read()
with open('docs/session-sets/vba-parser-session-set-generator/session-set-tree.md', 'r', encoding='utf-8') as f:
    tree = f.read()

# List completed sets (those with change-log.md)
import os
completed = []
for d in sorted(os.listdir('docs/session-sets')):
    full = f'docs/session-sets/{d}'
    if os.path.isdir(full) and os.path.exists(f'{full}/change-log.md'):
        completed.append(d)

next_set_prompt = (
    "Recommend the next session set to start for the dabbler-access-harvester repo.\n\n"
    "PROJECT CONTEXT\n"
    "This is a plan-driven repo. The master plan is "
    "docs/VBA-Parser-Migration-Harvester-Plan.md (Phase 0 through Phase 8). The "
    "session-set-tree.md (under docs/session-sets/vba-parser-session-set-generator/) "
    "is the DAG mapping plan phases to concrete session sets. Each set lives in "
    "docs/session-sets/<slug>/ and is complete when it has a change-log.md.\n\n"
    "Just-completed set: enrichment-functions-navigation-review (#16 in the tree). "
    "Phase coverage 6.6-6.8 (Function Classification, Navigation Graph, Manual Review Routing). "
    "Sibling set #15 (enrichment-events-and-validation) merged to main earlier today.\n\n"
    f"COMPLETED SETS (have change-log.md):\n" + "\n".join(f"- {s}" for s in completed) + "\n\n"
    "TASK\n"
    "1. Read the session-set-tree.md DAG below. Identify the next set whose "
    "Prereqs column is fully satisfied by the completed-sets list above.\n"
    "2. Confirm it matches the master plan's natural phase order. If you would "
    "deviate from the plan order, justify the deviation explicitly.\n"
    "3. Output a recommendation:\n\n"
    "```\n"
    "**Next session set:** <slug>\n"
    "**Plan-phase coverage:** <phase>\n"
    "**Prereqs satisfied by:** <list of slugs>\n"
    "**Recommended orchestrator:** <engine> <model> @ effort=<low|medium|high|max>\n"
    "**Rationale:** <2-3 sentences>\n"
    "**Plan-vs-DAG check:** confirm | deviate (with reason)\n"
    "```\n\n"
    "Keep it under 250 words.\n\n"
    f"=== session-set-tree.md (the DAG) ===\n{tree}\n\n"
    f"=== master plan (truncated to first 6000 chars) ===\n{plan[:6000]}\n"
)

result2 = query(
    model='gemini-pro',
    content=next_set_prompt,
    task_type='analysis',
    session_set=SESSION_SET_DIR,
    session_number=4,
)
with open(f'{SESSION_SET_DIR}/next-session-set-recommendation.md', 'w', encoding='utf-8') as f:
    f.write(result2.content)
print(f"Next-set recommendation written. model={result2.model_name} cost=${result2.cost_usd:.4f}")

# ── 3. Log both routed calls ──
from ai_router.session_log import SessionLog
log = SessionLog(SESSION_SET_DIR)
log.log_step(
    4, 9, 'session-4/route-change-log',
    f'Routed summarization (max_tier=1) for change-log. model={result.model_name} cost=${result.cost_usd:.4f}.',
    'complete',
    [{'model': result.model_name, 'taskType': 'summarization',
      'inputTokens': result.input_tokens, 'outputTokens': result.output_tokens,
      'costUsd': result.cost_usd}],
)
log.log_step(
    4, 10, 'session-4/route-next-set-recommendation',
    f'Routed analysis (gemini-pro) for next-session-set recommendation. cost=${result2.cost_usd:.4f}.',
    'complete',
    [{'model': result2.model_name, 'taskType': 'analysis',
      'inputTokens': result2.input_tokens, 'outputTokens': result2.output_tokens,
      'costUsd': result2.cost_usd}],
)
print('logged steps 9, 10')
