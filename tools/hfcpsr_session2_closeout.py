"""Session 2 closeout artifacts via routed calls.

Authors (in order):
  1. next-session-set-recommendation.md via route(task_type="analysis")
     — plan-driven repo per CLAUDE.md, so the recommendation must read
     the master plan + roadmap and confirm/deviate.
  2. change-log.md via route(task_type="summarization", max_tier=1)
     — Convention pins summarization (not documentation) for narrative
     prose, and max_tier=1 upfront avoids Gemini 400s on >2KB prompts.

Output is persisted to disk BEFORE display side effects per the
'Persist Routed Output To Disk Before Any Display Or Logging Side
Effects' Convention.
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
log = SessionLog(SESSION_SET)


def read(path) -> str:
    return Path(path).read_text(encoding="utf-8")


# ── 1. Next-session-set recommendation ────────────────────────────────
spec = read(SESSION_SET / "spec.md")
roadmap_excerpt = read("docs/session-sets/ROADMAP.md")[:5000]
tree_excerpt = read("docs/session-sets/remaining-work-planner/remaining-work-tree.md")[:4500]

# Session-1 + Session-2 disposition signals
session1_dispo = read(SESSION_SET / "disposition.json")
# Brief Session-2 outcomes summary for the analyst:
session2_summary = """\
Session 2 of 2 outcome:
- Reconfirmed Session 1's diff gate against committed golden — both
  fixtures pass byte-identical match.
- Authored unresolved-reason-narrative.md covering all reasons
  emitted by UnresolvedItemAggregator.SuggestedNextStepsLookup:
  5 Resolver + 6 stable SqlFormReference (plus the dynamic-prefix
  family member-not-found-at-segment-<N>) + 3 DynamicSqlSite +
  2 NavigationTarget + 5 FunctionClassification per-pair entries.
- Extracted ParseSuccessReportDiff helper (byte-identical message
  text to Session 1's inline body); ParseSuccessReportTests now
  delegates to it. Added ParseSuccessReportDiffTests sentinel
  exercising the same builder (byte-equality returns null,
  mutated count produces actionable diff message with fixture
  name, offset, line, both snippets, regeneration hint).
- dotnet build + dotnet test sequential: 1853 pass, 2 skipped,
  no regressions.
- Cross-provider session-verification: 3 rounds against gpt-5-4,
  all findings (R1 2M+1m, R2 1m, R3 1m — all narrative-only,
  none in code/tests) resolved in-session. Final state: VERIFIED-
  after-fix per Session 1 disposition pattern.
"""

next_set_prompt = f"""\
You are authoring the next-session-set recommendation at the close of
session set `harvester-finish-canonical-parse-success-report` in the
dabbler-access-harvester repo. This is a plan-driven repo — the master
plan is `docs/planning/strategy.md` Phase 4 + the DAG in
`docs/session-sets/remaining-work-planner/remaining-work-tree.md` +
the top-level `docs/session-sets/ROADMAP.md`. Per workflow Step 8 for
plan-driven repos, you must (a) identify the next set per the plan,
(b) either confirm the planned next set or propose a deviation with
explicit justification.

Required output: a roughly one-page markdown document
(`next-session-set-recommendation.md`) that:

1. Names the recommended next session set (slug).
2. Cites the authority (which plan, which row, what prereq link).
3. Confirms whether the spec for that set is present (stub or
   elaborated) and, if a stub, what Session 1 of that set will need
   to elaborate.
4. States the recommended orchestrator + model + effort for that
   set's Session 1, grounded in the actuals of this set.
5. States the cost band the plan declared for that set, and any
   prereq-shift this set's close caused.

Spec for this set (`harvester-finish-canonical-parse-success-report`):

```
{spec[:3000]}
```

Session 1 disposition.json:

```
{session1_dispo}
```

{session2_summary}

Roadmap excerpt (`docs/session-sets/ROADMAP.md`):

```
{roadmap_excerpt}
```

Remaining-work-tree excerpt:

```
{tree_excerpt}
```

Write the document directly (no preamble like "Here is the
recommendation:" — just the markdown).
"""

next_set_path = SESSION_SET / "next-session-set-recommendation.md"
print("Routing next-session-set recommendation analysis...")
nss_result = route(
    content=next_set_prompt,
    task_type="analysis",
    context="Author next-session-set-recommendation.md per workflow Step 8 plan-driven repo path.",
    session_set=str(SESSION_SET),
    session_number=2,
)
next_set_path.write_text(nss_result.content, encoding="utf-8")
print(f"Wrote {next_set_path} ({len(nss_result.content)} chars, "
      f"${nss_result.cost_usd:.4f}, model={nss_result.model_name})")

log.log_step(
    session_number=2,
    step_number=8,
    step_key="session-2/next-session-set-recommendation",
    description="Routed analysis: next-session-set recommendation (plan-driven: confirm F2 harvester-finish-canonical-golden-output)",
    status="complete",
    api_calls=[{
        "model": nss_result.model_name,
        "taskType": "analysis",
        "inputTokens": nss_result.input_tokens,
        "outputTokens": nss_result.output_tokens,
        "costUsd": nss_result.cost_usd,
    }],
)

# ── 2. Change-log.md ──────────────────────────────────────────────────
narrative = read(SESSION_SET / "unresolved-reason-narrative.md")
issue_log_s1 = read(SESSION_SET / "issue-logs" / "session-001.json")
issue_log_s2 = read(SESSION_SET / "issue-logs" / "session-002.json")
next_set_content = next_set_path.read_text(encoding="utf-8")

change_log_prompt = f"""\
You are authoring `change-log.md` for session set
`harvester-finish-canonical-parse-success-report` in the
dabbler-access-harvester repo. This is the final session of a 2-session
set and `change-log.md` is the canonical end-of-set artifact.

Required structure (~600–900 lines is too long; aim for ~150–250 lines):

# Change Log: `harvester-finish-canonical-parse-success-report`

> Frontmatter: created date, sessions ran, total routed cost,
> verification verdict, prerequisite for next set.

## Summary
2–3 paragraphs describing what the set delivered overall.

## Deliverables
Bulleted list of artifacts committed (per-fixture reports, the
generator/harness, the diff gate, the sentinel test, the
property-sheet inventory, the per-reason narrative, etc.).

## Phase 4 Criterion #1 status
Explicit statement of how this set closes (or fails to close) Phase 4
Criterion #1 from `docs/planning/strategy.md`.

## Per-session summary
Two short subsections — Session 1 and Session 2 — each naming the
work, the verification outcome, and routed cost.

## Findings that may interest the human
- The HHC + Northwind VBA parseFailed rates (HHC 29/32, Northwind 52/66)
  driven by form/report code-behind `Attribute VB_*` lines the Vba.g4
  grammar does not handle. Strategy-level decision per stub stop-rules,
  not folded into Phase 4.
- The property-sheet wrapping heuristic for criteria-style values.

## Lessons learned candidates
2–3 bullets at most. Candidates from this set:
- Confirmed the "orchestrator-model-matches-routing-target" Convention
  for synthesis docs (Opus authored the narrative in-conversation; no
  route(architecture) timeout failure mode triggered).
- Confirmed the "Persist Routed Output To Disk Before Any Display"
  Convention (all routed scripts persist before print).
- Confirmed the "Write disposition.json before closeout commit"
  Convention (both Session 1 and Session 2 closeouts followed it).

## Handoff to next set
Pointer to next-session-set-recommendation.md.

---

Source material follows. Treat it as ground truth but compress
ruthlessly — the change log is a navigation document, not a thesis.

## Spec

```
{spec[:2500]}
```

## Session 1 disposition

```
{session1_dispo}
```

## Session 2 summary

{session2_summary}

## Session 1 issue log

```
{issue_log_s1}
```

## Session 2 issue log

```
{issue_log_s2}
```

## Next-session-set recommendation (just authored)

```
{next_set_content[:2500]}
```

Write the change log directly (no preamble — just the markdown).
"""

change_log_path = SESSION_SET / "change-log.md"
print("\nRouting change-log.md summarization (max_tier=1 per Convention)...")
cl_result = route(
    content=change_log_prompt,
    task_type="summarization",
    max_tier=1,
    session_set=str(SESSION_SET),
    session_number=2,
)
change_log_path.write_text(cl_result.content, encoding="utf-8")
print(f"Wrote {change_log_path} ({len(cl_result.content)} chars, "
      f"${cl_result.cost_usd:.4f}, model={cl_result.model_name})")

log.log_step(
    session_number=2,
    step_number=9,
    step_key="session-2/change-log",
    description="Routed summarization: change-log.md (max_tier=1, narrative prose)",
    status="complete",
    api_calls=[{
        "model": cl_result.model_name,
        "taskType": "summarization",
        "inputTokens": cl_result.input_tokens,
        "outputTokens": cl_result.output_tokens,
        "costUsd": cl_result.cost_usd,
    }],
)

print("\nClose-out routed work complete.")
print(f"Total routed cost this script: ${nss_result.cost_usd + cl_result.cost_usd:.4f}")
