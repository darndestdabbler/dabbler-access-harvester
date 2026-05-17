"""Round-2 cross-provider verification for Session 1 of
`harvester-finish-canonical-parse-success-report`.

Round 1 (gpt-5-4) returned ISSUES_FOUND with 4 Major + 1 Minor. Round 2
sends the verifier the round-1 review verbatim, the fixes applied, and
the regenerated artifacts. complexity_hint=85 per the workflow's Step 7
escalation rule for Major findings.
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


def read(p: str | Path, limit: int | None = None) -> str:
    text = Path(p).read_text(encoding="utf-8")
    if limit is not None and len(text) > limit:
        return text[:limit] + f"\n... (truncated; {len(text) - limit} more chars)"
    return text


def main() -> int:
    r1_review = read(SESSION_SET / "session-reviews" / "session-001.md")

    spec_md = read(SESSION_SET / "spec.md")
    inventory_md = read(SESSION_SET / "property-sheet-inventory.md", limit=4000)
    ai_assignment_md = read(SESSION_SET / "ai-assignment.md")

    model_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs")
    generator_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs")
    tests_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs")

    hhc_report = read("samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json")
    nw_report = read("samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json")

    prompt = f"""Round 2 of session-verification for Session 1 of
`harvester-finish-canonical-parse-success-report`.

Round 1 returned ISSUES_FOUND with 4 Major and 1 Minor findings. The
review is reproduced verbatim below, followed by the fixes applied
and the regenerated artifacts. Confirm each fix is in place and the
report is fit for commit as the interim golden.

## Round-1 review (verbatim, do not edit)

```
{r1_review}
```

## Round-1 fixes applied

### R1 Major #1 — `unresolvedItems` was hardcoded zero/empty

**Fix:** `UnresolvedItemsSection` is now `{{ "status": "not-measured",
"note": ... }}` with `totalCount` and `bySourceAndReason` omitted via
`JsonIgnore(WhenWritingNull)`. The future "measured" shape grows the
section without changing existing fields. The spec's
`unresolvedItems` paragraph + the inline JSON sketch reflect the new
shape; consumers MUST check `status` rather than treating absence as
zero. The Session-1 deliverable list says the section ships
`"status": "not-measured"`.

### R1 Major #2 — ValidationRule / DefaultValue dialect mismatch

**Fix:** `ParseSuccessReportGenerator.NormaliseCriteria` detects
criteria-style values (those that begin with a comparison operator
or `Is`/`Like`/`Between`/`In`/`Not` and lack an explicit operand)
and prepends a synthetic `x ` placeholder operand before
SQL-wrapping. `ParseAsJetWhere` and `ParseDefaultValue` both call
the helper. Net effect on HHC ValidationRule: 14/2/12 (R1) → 15/11/4
(R2). Net effect on Northwind ValidationRule: 3/0/3 (R1) → 3/3/0
(R2). Counts now reflect parser coverage in the intended dialect.

### R1 Major #3 — Count reconciliation with the inventory tool

**Fix:** `CountPropertySheetBuckets` now walks every `*.yaml` file
under the fixture mirror via `Directory.EnumerateFiles(..., "*.yaml",
SearchOption.AllDirectories)`, dispatching each `Key: "value"` match
to its bucket by the key name. Reconciles with
`tools/parse_success_inventory.py` (which walks the same set of files
with the same regex). Inventory totals (HHC ValidationRule=15,
DefaultValue=8; Northwind ValidationRule=3, DefaultValue=15, Filter=8,
OrderBy=13) now match the report's `total` counts exactly.

### R1 Major #4 — Macro fields misleadingly named

**Fix:** Renamed `actionsParsed` → `actionLines` to reflect that the
count is structural (count of `Action ="..."` lines in the SaveAsText
body) rather than enricher-derived parser coverage. Dropped the
always-zero `actionsUnresolved` field — the macro action enricher
runs as a live-pipeline step and is wired by
`harvester-finish-canonical-golden-output`. The spec's "Macros"
bucket description was updated to match.

### R1 Minor #5 — UPDATE env var not wired

**Fix:** `ParseSuccessReportTests.AssertOrUpdate` honors
`UPDATE_PARSE_SUCCESS_REPORTS=1` (writes the file) or no env var
(asserts content matches the committed golden via byte-equal
comparison). The default path includes a `FindFirstDivergence` helper
that surfaces the first byte mismatch in a human-readable error
message; consumers re-run with the env var to regenerate. The test
contract that Session 2 inherits is unchanged.

## Files to re-review

### 1. Spec (updated for fixes #1, #4)

```markdown
{spec_md}
```

### 2. Inventory verdict (unchanged)

```markdown
{inventory_md}
```

### 3. ai-assignment.md (unchanged)

```markdown
{ai_assignment_md}
```

### 4. Updated harness

#### ParseSuccessReportModel.cs (fixes #1, #4)

```csharp
{model_cs}
```

#### ParseSuccessReportGenerator.cs (fixes #2, #3, #4)

```csharp
{generator_cs}
```

#### ParseSuccessReportTests.cs (fix #5)

```csharp
{tests_cs}
```

### 5. Regenerated reports (interim golden)

#### HomeHealthCare.accdb.parse-success-report.json

```json
{hhc_report}
```

#### NorthwindDevEdition.accdb.parse-success-report.json

```json
{nw_report}
```

## Build + test status (round 2)

- `dotnet build` clean (0 errors).
- `dotnet test` from solution root: 1710 passed, 2 skipped, 0 failed
  across 8 test projects. The 2 new ParseSuccessReportTests run in
  diff mode by default and pass (no env var set).

## Disclosure of related files NOT changed this round

Same as round 1: parser entry points and aggregator types are
unchanged; the report-generation harness reads them but the harness
does not invoke the live enrichment pipeline.

The high VBA parseFailed rate (HHC 29/32, Northwind 52/66) is
unchanged from round 1 and remains the genuine Phase 4 parser-scope
gap (form / report code-behind modules begin with `Attribute
VB_GlobalNameSpace = ...` lines the Vba.g4 grammar does not accept).
This is disclosed in the Session 1 close notification body and
deferred to a strategy-level decision; it is NOT a Session 1 fix.

## Review focus (round 2)

Confirm each round-1 finding is resolved by the corresponding fix.
Flag any new issues introduced by the fixes themselves. Do not
re-litigate the deferred items (live aggregator wiring; Attribute
grammar extension) — those are out of scope per the stub.

After your analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or test name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.

Your response MUST begin with a detailed prose analysis for each
round-1 finding (was it fixed? any regression?) BEFORE the JSON block.
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    review_path = SESSION_SET / "session-reviews" / "session-001-round2.md"
    review_path.write_text(result.content, encoding="utf-8")

    print(f"[wrote {review_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")

    log = SessionLog(str(SESSION_SET), total_sessions=2)
    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=10,
        step_key="session-1/verify-round2",
        description="Cross-provider session-verification round 2 routed (complexity_hint=85)",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
