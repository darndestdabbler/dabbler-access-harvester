"""Round 3 verification + R2 issue log for harvester-object-cleanup-identifier Session 1."""
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

r2_issues = [
    {
        "round": 2,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §3.1.3",
        "summary": "year-month regex month-first branch accepted only named months while prose promised numeric forms like 01-2024.",
        "fix": "Extended month-first branch to accept both numeric (0[1-9]|1[0-2]) and named months. Prose updated to list 202401 / 012024 as additional examples.",
        "status": "fixed",
    },
    {
        "round": 2,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §3.3.2",
        "summary": "Said candidate event properties start with 'On' but examples included BeforeUpdate / AfterUpdate that do not.",
        "fix": "Replaced the predicate with an explicit three-prefix rule: name starts with 'On' OR 'Before' OR 'After'. Covers the full Access event-property surface deterministically.",
        "status": "fixed",
    },
    {
        "round": 2,
        "severity": "Minor",
        "location": "docs/planning/object-cleanup-strategy.md §10",
        "summary": "Said §3.3.1 inline-SQL parsing depends on harvester-body-and-query-sql query Sql fields, which it does not.",
        "fix": "Removed §3.3.1 clause from the harvester-body-and-query-sql prereq note. Added explicit clarification that §3.3.1 reads RecordSource/RowSource property values directly, independent of ir/Queries/ SQL.",
        "status": "fixed",
    },
]
log.save_issue_log(session_number=SESSION_NUMBER, issues=r2_issues)
print(f"Saved R2 issue log ({len(r2_issues)} issues)")

strategy_path = REPO / "docs" / "planning" / "object-cleanup-strategy.md"
schema_path = REPO / "AccessHarvester.Pipeline" / "Cleanup" / "object-cleanup-schema.json"

strategy_text = strategy_path.read_text(encoding="utf-8")
schema_text = schema_path.read_text(encoding="utf-8")

prose_lead = """\
This is **Round 3** verification of the Session 1 deliverables.

Round 1 returned ISSUES_FOUND with 4 Major + 2 Minor; all were
fixed in R1. Round 2 returned ISSUES_FOUND with 2 Major + 1 Minor
(new issues introduced by the R1 fixes themselves); all R2
findings have been addressed in the current files. R3 is the
final allowed retry per the workflow's Step 7 max-2-retry rule.

Your response MUST begin with a detailed prose analysis BEFORE
the trailing JSON block. Do not skip the analysis. Both the prose
and the JSON block are required.

Concerns from R2 to confirm closed:

1. **R2 finding #1 — year-month regex (§3.1.3).** Month-first
   branch now accepts both numeric `(0[1-9]|1[0-2])` and named
   months. Prose lists `01-2024`, `012024`, `Jan2024`, `Jan_2024`
   as month-first examples. Does the regex now match every
   documented example?

2. **R2 finding #2 — event-property predicate (§3.3.2).** The
   predicate now reads: "name starts with `On` OR `Before` OR
   `After`". The examples (`OnLoad`, `OnClick`, `OnCurrent`,
   `OnDblClick`, `OnTimer`, `OnFormat`, `BeforeUpdate`,
   `BeforeInsert`, `BeforeDelConfirm`, `AfterUpdate`,
   `AfterInsert`, `AfterDelConfirm`) all satisfy the predicate.
   Is the rule deterministic and complete enough for Session 2
   to implement without ambiguity?

3. **R2 finding #3 — §10 prereq scope.** The
   `harvester-body-and-query-sql` prereq note now scopes the
   dependency to `QueryTableReference` detection only. An
   explicit clarifying sentence states that §3.3.1's inline-SQL
   parsing of `RecordSource` / `RowSource` is independent of
   this prereq. Does §10 now correctly state the dependency
   surface?

4. **Final closeout-grade regression check.** Are there any
   remaining inconsistencies between the strategy doc and the
   JSON Schema? Any stale cross-references? Any patterns in
   §3.1 that don't match an example the prose names? Any
   contract terms in §6 (checklist mapping) that diverge from
   §5 (object-cleanup.json output)? Is the document
   implementation-ready for Session 2?

After the four prose sections, output the JSON block in this
exact format. Do NOT omit it.

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [ ... ]
}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.

==============================================================================
OUT-OF-SCOPE DISCLOSURE (UNCHANGED FROM R1/R2)
==============================================================================

- samples/HomeHealthCare.accdb and samples/MiniComplex.accdb
  binary diffs are Access-on-open timestamp mutations.
- 3 `dotnet test` integration tests time out at 60s under the
  test harness (pre-existing environmental issue; direct CLI
  invocation succeeds in <30s; my docs/schema-only deliverables
  cannot have caused this).
- docs/planning/object-use-identification-strategy.md is the
  downstream consumer contract; not changed in this session.

==============================================================================
DELIVERABLE 1 OF 2: docs/planning/object-cleanup-strategy.md (POST-R2 FIXES)
==============================================================================

"""

verification_prompt = (
    prose_lead
    + strategy_text
    + "\n\n==============================================================================\n"
    + "DELIVERABLE 2 OF 2: object-cleanup-schema.json (POST-R2 FIXES)\n"
    + "==============================================================================\n\n"
    + schema_text
)

prompt_path = REPO / "tmp" / "session-1-verification-prompt-r3.md"
prompt_path.write_text(verification_prompt, encoding="utf-8")

result = route(
    content=verification_prompt,
    task_type="session-verification",
    complexity_hint=85,
    context="R3 final verification after fixing 3 R2 findings on harvester-object-cleanup-identifier Session 1.",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

response_path = REPO / "tmp" / "session-1-verification-response-r3.md"
response_path.write_text(result.content, encoding="utf-8")

log.save_session_review(
    session_number=SESSION_NUMBER,
    review_text=result.content,
    round_number=3,
)

print(f"Verifier model: {result.model_name}")
print(f"Cost (USD): {result.cost_usd}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Prompt saved: {prompt_path}")
print(f"Response saved: {response_path}")
print("=" * 70)
print(result.content[-3000:] if len(result.content) > 3000 else result.content)
