"""Round 4 final-confirm verification for harvester-object-cleanup-identifier Session 1.

R4 is beyond Step 7's literal "max 2 retries" budget but was requested
to confirm clean closeout after R3's 3 trivial doc-consistency fixes.
"""
from __future__ import annotations

import os
import pathlib

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route, SessionLog  # noqa: E402

REPO = pathlib.Path.cwd()
SESSION_SET = REPO / "docs" / "session-sets" / "harvester-object-cleanup-identifier"
SESSION_NUMBER = 1
log = SessionLog(str(SESSION_SET))

r3_issues = [
    {
        "round": 3,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §§3.1, 3.3, 6, 7.3",
        "summary": "Event-handler code-behind module exclusion was §3.3-only, contradicting §6's checklist-level guarantee.",
        "fix": "§7.3 retitled 'Event-handler code-behind modules (global)' and rewritten to globally exclude Form_*/Report_* from §3.1, §3.2, and §3.3 — never present in object-cleanup.json or the checklist.",
        "status": "fixed",
    },
    {
        "round": 3,
        "severity": "Minor",
        "location": "docs/planning/object-cleanup-strategy.md §§5, 10",
        "summary": "SQL-prereq impact described as query-only; tables also lose inbound query-reference evidence.",
        "fix": "§10 expanded to call out the bidirectional impact (queries lose outbound, tables lose inbound). §5 degradationNotes example mentions both sides explicitly.",
        "status": "fixed",
    },
    {
        "round": 3,
        "severity": "Minor",
        "location": "docs/planning/object-cleanup-strategy.md §5 + object-cleanup-schema.json DateSignal.maxValue",
        "summary": "DateSignal.maxValue example lacked timezone but schema enforces RFC 3339 date-time which requires Z or offset.",
        "fix": "Example updated to '2022-03-15T00:00:00Z'. Schema description for maxValue now explicitly requires a timezone offset and instructs callers to emit Z for DAO LastUpdated sources.",
        "status": "fixed",
    },
]
log.save_issue_log(session_number=SESSION_NUMBER, issues=r3_issues)
print(f"Saved R3 issue log ({len(r3_issues)} issues)")

strategy_path = REPO / "docs" / "planning" / "object-cleanup-strategy.md"
schema_path = REPO / "AccessHarvester.Pipeline" / "Cleanup" / "object-cleanup-schema.json"
strategy_text = strategy_path.read_text(encoding="utf-8")
schema_text = schema_path.read_text(encoding="utf-8")

prose_lead = """\
This is the **Round 4 final-confirm** verification. R3 produced 1
Major + 2 Minor doc-consistency findings; all have been addressed
in-tree. The human operator authorized this R4 round beyond the
Step 7 "max 2 retries" budget so the deliverables can close with
a clean cross-provider VERIFIED rather than a defer.

Your response MUST begin with a focused prose analysis BEFORE the
trailing JSON block. Do not skip the analysis.

Confirm these R3 fixes:

1. **R3 Major (§§3.1, 3.3, 6, 7.3) — Event-handler code-behind
   global exclusion.** §7.3 is now titled "Event-handler
   code-behind modules (global)" and explicitly says Form_*/
   Report_* are globally excluded from §3.1, §3.2, and §3.3 — and
   are never present in either output. Is the §3.1↔§3.3↔§6↔§7.3
   contract now internally consistent?

2. **R3 Minor (§§5, 10) — Bidirectional SQL impact.** §10 now
   states the missing-SQL impact in both directions (queries
   lose outbound reference evidence; tables lose inbound). §5
   degradationNotes example mentions both sides. Is the
   degradation surface now described accurately?

3. **R3 Minor (§5 + schema) — maxValue timezone.** Strategy
   example uses `2022-03-15T00:00:00Z`. Schema description of
   maxValue now explicitly requires timezone offset and
   instructs DAO-source callers to emit `Z`. Is the
   strategy↔schema contract now consistent for this field?

4. **Closeout-grade final pass.** Are there ANY remaining
   contradictions, stale cross-references, or
   implementation-blocking ambiguities between the strategy doc
   and the schema? This is the last verification before the
   strategy ships to Session 2.

After the four prose sections, output the JSON block in this exact
format. Do NOT omit it.

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [ ... ]
}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.

==============================================================================
OUT-OF-SCOPE DISCLOSURE (UNCHANGED)
==============================================================================

- samples/HomeHealthCare.accdb and samples/MiniComplex.accdb
  binary diffs are Access-on-open timestamp mutations.
- 3 `dotnet test` integration tests time out at 60s under the
  test harness (pre-existing environmental issue; direct CLI
  invocation completes in <30s).
- docs/planning/object-use-identification-strategy.md is the
  downstream consumer contract; not changed in this session.

==============================================================================
DELIVERABLE 1 OF 2: docs/planning/object-cleanup-strategy.md (POST-R3 FIXES)
==============================================================================

"""

verification_prompt = (
    prose_lead
    + strategy_text
    + "\n\n==============================================================================\n"
    + "DELIVERABLE 2 OF 2: object-cleanup-schema.json (POST-R3 FIXES)\n"
    + "==============================================================================\n\n"
    + schema_text
)

prompt_path = REPO / "tmp" / "session-1-verification-prompt-r4.md"
prompt_path.write_text(verification_prompt, encoding="utf-8")

result = route(
    content=verification_prompt,
    task_type="session-verification",
    complexity_hint=85,
    context="R4 final-confirm after fixing 3 R3 doc-consistency findings on harvester-object-cleanup-identifier Session 1.",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

response_path = REPO / "tmp" / "session-1-verification-response-r4.md"
response_path.write_text(result.content, encoding="utf-8")

log.save_session_review(
    session_number=SESSION_NUMBER,
    review_text=result.content,
    round_number=4,
)

print(f"Verifier model: {result.model_name}")
print(f"Cost (USD): {result.cost_usd}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Prompt saved: {prompt_path}")
print(f"Response saved: {response_path}")
print("=" * 70)
print(result.content[-3000:] if len(result.content) > 3000 else result.content)
