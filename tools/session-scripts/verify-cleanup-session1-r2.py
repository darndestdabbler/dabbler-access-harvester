"""Round 2 verification + R1 issue log for harvester-object-cleanup-identifier Session 1."""
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

# Persist R1 issue log first.
r1_issues = [
    {
        "round": 1,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §3.1",
        "summary": "Regex contract bugs: \\b boundary fails on _2023; Jan2024 not matched; markdown-escaped alternation ambiguity.",
        "fix": "Rewrote §3.1 as five subsections with implementation-safe regexes outside markdown tables. Boundaries use [^0-9A-Za-z] character class. year-month pattern handles both year-first and month-first variants.",
        "status": "fixed",
    },
    {
        "round": 1,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §3.3",
        "summary": "Cross-reference matrix missed inline SQL in RecordSource/RowSource and underspecified event-property macro bindings.",
        "fix": "Added §3.3.1 (inline-SQL parsing for RecordSource/RowSource) and §3.3.2 (event-property bindings on form/report/control IR). Clarified Module covers both standard and class modules sharing ir/ModulesAndClasses/*.ir.json.",
        "status": "fixed",
    },
    {
        "round": 1,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §§6,10",
        "summary": "Subarea bracket-prefix mutated object identity, conflicting with §10 no-transformation rule and downstream --exclude-cleanup contract.",
        "fix": "Subarea now byte-identical to objectName. Confidence tier prefix moved to ItemLabel as [<Tier>] <explanation>. Calibration hook in §11 updated to reference ItemLabel.",
        "status": "fixed",
    },
    {
        "round": 1,
        "severity": "Major",
        "location": "docs/planning/object-cleanup-strategy.md §§5,8 and AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json",
        "summary": "harvestDate described as ISO date in strategy but enforced as date-time in schema.",
        "fix": "Both aligned on ISO 8601 calendar date (YYYY-MM-DD). Schema format changed to 'date'. CLI --harvest-date arg type clarified as YYYY-MM-DD. Rationale (day-based thresholding) documented in §5.",
        "status": "fixed",
    },
    {
        "round": 1,
        "severity": "Minor",
        "location": "docs/planning/object-cleanup-strategy.md §8",
        "summary": "CLI did not explicitly state --in / --manifest mutual exclusion.",
        "fix": "Added required-one-of rule with exit code 2 behavior. Argument format requirements (date-only, threshold positive integer) also documented.",
        "status": "fixed",
    },
    {
        "round": 1,
        "severity": "Minor",
        "location": "docs/planning/object-cleanup-strategy.md §10",
        "summary": "Graceful degradation when query SQL is missing was not surfaced in output.",
        "fix": "Added top-level degradationNotes array in §5 (schema and strategy). §10 references it for observability when QueryTableReference path runs with reduced coverage.",
        "status": "fixed",
    },
    {
        "round": 1,
        "severity": "Minor",
        "location": "AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json DateSignal.thresholdDays",
        "summary": "Description said 'Always >= daysSinceMax' which is backwards for a stale signal.",
        "fix": "Corrected to 'The DateSignal fires only when daysSinceMax > thresholdDays.'",
        "status": "fixed",
    },
]
log.save_issue_log(session_number=SESSION_NUMBER, issues=r1_issues)
print(f"Saved R1 issue log ({len(r1_issues)} issues)")

# Build R2 verification prompt — same files, mention R1 fixes.
strategy_path = REPO / "docs" / "planning" / "object-cleanup-strategy.md"
schema_path = REPO / "AccessHarvester.Pipeline" / "Cleanup" / "object-cleanup-schema.json"
spec_path = SESSION_SET / "spec.md"

strategy_text = strategy_path.read_text(encoding="utf-8")
schema_text = schema_path.read_text(encoding="utf-8")
spec_text = spec_path.read_text(encoding="utf-8")

prose_lead = """\
This is **Round 2** verification of the Session 1 deliverables.
Round 1 returned ISSUES_FOUND with 4 Major and 2 Minor findings. All
findings have been addressed in the current files. The R1 issues and
their fixes are summarised below for context.

Your response MUST begin with a detailed prose analysis BEFORE the
trailing JSON block. Do not skip the analysis. Both the prose
analysis and the JSON block are required.

Analyze:

1. **R1 finding #1 (Regex contract).** Strategy §3.1 was rewritten
   into five sub-sections with implementation-safe regexes outside
   markdown tables. Boundaries use `[^0-9A-Za-z]` character class
   (not `\\b`) so `_2023` and similar underscore-delimited tokens
   match. year-month handles both year-first and month-first forms.
   Are the regexes now correct and unambiguous for Session 2 to
   implement verbatim?

2. **R1 finding #2 (Cross-reference matrix).** §3.3 now has §3.3.1
   for inline-SQL parsing inside `RecordSource` / `RowSource`,
   §3.3.2 for event-property bindings, and a clarified Module
   taxonomy (covers both standard and class modules sharing one
   IR source path). Does this resolve the gap?

3. **R1 finding #3 (Subarea polarity).** `Subarea` is now
   byte-identical to `objectName`. The confidence-tier prefix moved
   to `ItemLabel` as `"[<Tier>] <explanation>"`. The §11 calibration
   hook was updated to reference `ItemLabel`. Is the §10
   no-transformation rule now consistently enforced?

4. **R1 finding #4 (harvestDate type).** Strategy §5 and the JSON
   Schema both treat `harvestDate` as ISO 8601 calendar date
   (YYYY-MM-DD). Schema `format` changed to `"date"`. CLI
   `--harvest-date` documented as `YYYY-MM-DD`. Are doc and schema
   now consistent?

5. **R1 finding #5 (CLI mutual exclusion).** §8 now states "Exactly
   one of `--in` or `--manifest` must be supplied" with exit code 2
   behavior. Is the contract explicit enough?

6. **R1 finding #6 (degradationNotes).** Top-level `degradationNotes`
   array added to both strategy §5 and the JSON Schema. §10 ties
   the missing-query-SQL case to this field. Is the observability
   gap closed?

7. **Regression check.** Did the R1 fixes introduce any new
   inconsistencies, contradictions, or unresolved references in
   either deliverable? Specifically check the schema's `required`
   list, the strategy's §11 calibration hooks (were they updated to
   reflect §6's new ItemLabel-prefix shape?), and the §10 inline-SQL
   prereq language now that §3.3.1 references it.

After the seven prose sections, output the JSON block in this exact
format:

```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [ ... ]
}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.

==============================================================================
OUT-OF-SCOPE DISCLOSURE
==============================================================================

The same out-of-scope items from R1 still apply:

- `samples/HomeHealthCare.accdb` and `samples/MiniComplex.accdb`
  binary diffs are Access-on-open timestamp mutations, not
  session deliverables.
- 3 `dotnet test` integration tests time out at 60s under the
  test harness (CliRunTests/CliExtractTests/CliTranslateTests
  against HomeHealthCare). Direct CLI invocation completes in
  under 30s. Pre-existing environmental issue unrelated to this
  session's docs/schema-only deliverables. 42 of 45 integration
  tests pass; all hermetic unit tests pass.
- `docs/planning/object-use-identification-strategy.md` is
  referenced as the downstream consumer contract; it is not
  changed in this session.

==============================================================================
DELIVERABLE 1 OF 2: docs/planning/object-cleanup-strategy.md (POST-R1 FIXES)
==============================================================================

"""

verification_prompt = (
    prose_lead
    + strategy_text
    + "\n\n==============================================================================\n"
    + "DELIVERABLE 2 OF 2: AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json (POST-R1 FIXES)\n"
    + "==============================================================================\n\n"
    + schema_text
    + "\n\n==============================================================================\n"
    + "SPEC EXCERPT (UNCHANGED)\n"
    + "==============================================================================\n\n"
    + spec_text
)

prompt_path = REPO / "tmp" / "session-1-verification-prompt-r2.md"
prompt_path.write_text(verification_prompt, encoding="utf-8")

result = route(
    content=verification_prompt,
    task_type="session-verification",
    complexity_hint=85,
    context="R2 verification after fixing 6 R1 findings on harvester-object-cleanup-identifier Session 1 strategy + JSON Schema.",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

response_path = REPO / "tmp" / "session-1-verification-response-r2.md"
response_path.write_text(result.content, encoding="utf-8")

log.save_session_review(
    session_number=SESSION_NUMBER,
    review_text=result.content,
    round_number=2,
)

print(f"Verifier model: {result.model_name}")
print(f"Cost (USD): {result.cost_usd}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Prompt saved: {prompt_path}")
print(f"Response saved: {response_path}")
print("=" * 70)
print(result.content[:2500])
print("...[truncated]..." if len(result.content) > 2500 else "")
