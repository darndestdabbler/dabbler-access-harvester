"""Round-3 cross-provider verification for Session 1 of
`harvester-finish-canonical-parse-success-report`.

Round 2 (gpt-5-4) returned ISSUES_FOUND with one Major and one Minor.
Both are fixed. Round 3 is the final retry before commit per the
workflow's Step 7 retry cap (max 2 retries = max 3 rounds).
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
    r1_review = read(SESSION_SET / "session-reviews" / "session-001.md", limit=5000)
    r2_review = read(SESSION_SET / "session-reviews" / "session-001-round2.md")

    spec_md = read(SESSION_SET / "spec.md")
    inventory_md = read(SESSION_SET / "property-sheet-inventory.md", limit=4500)
    generator_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs")
    tests_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs", limit=8000)
    inventory_py = read("tools/parse_success_inventory.py", limit=4000)

    hhc_report = read("samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json")
    nw_report = read("samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json")

    prompt = f"""Round 3 of session-verification for Session 1 of
`harvester-finish-canonical-parse-success-report`.

Round 1 (4 Major + 1 Minor) and Round 2 (1 Major + 1 Minor) returned
ISSUES_FOUND. Round 3 is the final retry per the workflow's two-retry
cap. Confirm the R2 findings are resolved by the R2 fixes below and
the deliverables are fit to commit as the interim golden.

## Round-1 review (verbatim, abridged)

```
{r1_review}
```

## Round-2 review (verbatim, in full)

```
{r2_review}
```

## Round-2 fixes applied

### R2 Major #1 — Shared property-field regex skipped escaped-quote scalars

**Root cause:** Both the inventory tool and the report generator used
`[^"]*` to capture the YAML quoted-scalar body. That group stops at
the first inner `"`, so lines containing escaped quotes
(`ValidationRule: "In (\\"M\\",\\"F\\",\\"X\\")"`,
`ValidationRule: "Is Null Or Like \\"*@*.*\\""`,
`DefaultValue: "\\"Pending\\""`,
`Filter: "([OrderDetails].[ProductName] Like \\"*Beverages*\\")"`)
were silently dropped from BOTH scans — and reconciled-while-wrong as
R2 noted.

**Fix:** Updated both regexes to the standard escaped-string pattern
`(?:[^"\\\\]|\\\\.)*` which accepts any non-quote-non-backslash
character or any backslash-escaped pair. Net effect on the
regenerated reports (and the regenerated inventory output):

- HHC ValidationRule: 15 → 19 occurrences (4 newly-matched lines).
- HHC DefaultValue: 8 → 12 occurrences.
- Northwind DefaultValue: 15 → 16.
- Northwind Filter: 8 → 12 occurrences.
- ValidationRule parseOk in HHC: 11 / 15 (R2) → 13 / 19 (R3) — the
  newly-matched rules parse through the placeholder-operand wrapping.
- DefaultValue parseOk continues at 100% in both fixtures.

The `property-sheet-inventory.md` table was regenerated and now
contains the new totals with a traceability note pointing at the
fix.

### R2 Minor #2 — Spec / comment drift after the R1 fixes

**Fix:** Spec Session 1 step 5 / step 6 / step 7 paragraphs updated
to reflect the live diff-gate (no longer "Session 1 writes-and-passes,
Session 2 enables the diff arm"). Session 2 step 1 / step 7 / the
Touches list / the progress-keys list updated to drop the
"flip mode" narrative. The diff gate is live from Session 1 and the
Session 2 narrative focuses on per-reason narrative + sentinel test.

## Files to re-review

### Spec (R2 minor fixed)

```markdown
{spec_md}
```

### Property-sheet inventory verdict (R2 major numbers updated)

```markdown
{inventory_md}
```

### Generator harness (R2 major regex fix)

```csharp
{generator_cs}
```

### Test harness (R1 minor fix is final; no R2 changes here)

```csharp
{tests_cs}
```

### Inventory tool (R2 major regex parity)

```python
{inventory_py}
```

### Regenerated reports (interim golden)

#### HomeHealthCare.accdb.parse-success-report.json

```json
{hhc_report}
```

#### NorthwindDevEdition.accdb.parse-success-report.json

```json
{nw_report}
```

## Build + test status (round 3)

- `dotnet build` clean (0 errors).
- `dotnet test` on Pipeline.Tests in diff mode (no UPDATE env var)
  passes all 357 tests including the two new
  ParseSuccessReportTests.

## Disclosure of items NOT changed

- Live aggregator wiring still deferred to
  `harvester-finish-canonical-golden-output`. The interim shape
  ships `"status": "not-measured"`.
- Vba.g4 `Attribute` statement gap still surfaces as the high VBA
  parseFailed rate. That is the genuine Phase 4 finding the report
  is designed to surface and will be raised to the human in the
  session-complete notification body.

## Review focus (round 3)

Confirm the R2 Major (regex) and R2 Minor (spec drift) are resolved.
Do not re-litigate the R1 findings (all five were either fixed in R2
or, in the case of R1 Major #3, the underlying reconciliation issue
is now genuine after the regex fix). Do not re-open the deferred
strategy-level items (live aggregator wiring, Attribute grammar
extension).

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

Your response MUST begin with a detailed prose analysis confirming
(or refuting) each round-2 fix BEFORE the JSON block.
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    review_path = SESSION_SET / "session-reviews" / "session-001-round3.md"
    review_path.write_text(result.content, encoding="utf-8")

    print(f"[wrote {review_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")

    log = SessionLog(str(SESSION_SET), total_sessions=2)
    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=11,
        step_key="session-1/verify-round3",
        description="Cross-provider session-verification round 3 routed (final retry)",
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
