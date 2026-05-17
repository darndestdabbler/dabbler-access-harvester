"""Cross-provider end-of-session verification for Session 1 of
`harvester-finish-canonical-parse-success-report`.

Per the workflow's Step 6 + the project-guidance Conventions:
  * Include all session deliverables in the prompt.
  * Disclose related files (UnresolvedItemAggregator, parser entry
    points) that the verifier might reasonably look for but that this
    session does not change.
  * Append the structured JSON response block so the verdict is
    machine-parseable.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

REPO = Path(".").resolve()
SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
SESSION_NUMBER = 1


def read(p: str | Path, limit: int | None = None) -> str:
    text = Path(p).read_text(encoding="utf-8")
    if limit is not None and len(text) > limit:
        return text[:limit] + f"\n... (truncated; {len(text) - limit} more chars)"
    return text


def main() -> int:
    spec_md = read(SESSION_SET / "spec.md")
    inventory_md = read(SESSION_SET / "property-sheet-inventory.md")
    ai_assignment_md = read(SESSION_SET / "ai-assignment.md")

    model_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs")
    generator_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportGenerator.cs")
    tests_cs = read("tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs")

    hhc_report = read("samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json")
    nw_report = read("samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json")

    inventory_py = read("tools/parse_success_inventory.py", limit=4000)

    prompt = f"""You are reviewing Session 1 of the
`harvester-finish-canonical-parse-success-report` session set in the
dabbler-access-harvester repo.

## Session 1 charter (from spec)

- Read the gap-audit GAP #1 + stub end-to-end.
- Elaborate the stub spec (file paths, JSON shape, Session 1-vs-2
  step lists).
- Author ai-assignment.md via routed analysis (Rule #17).
- Run the property-sheet inventory pass and decide fold-in vs.
  exclude for path (d).
- Implement a first-pass report generator that walks the YAML mirror
  and counts parser coverage.
- Generate and commit the per-fixture parse-success report JSONs as
  the INTERIM golden.
- Flag surprises BEFORE pinning the regression test in Session 2.
- Build + test sequentially.

## Files to review

### 1. The elaborated spec

```markdown
{spec_md}
```

### 2. The property-sheet inventory verdict

```markdown
{inventory_md}
```

### 3. The ai-assignment.md (routed analysis output, not orchestrator-authored)

```markdown
{ai_assignment_md}
```

### 4. The first-pass report generator harness

#### ParseSuccessReportModel.cs

```csharp
{model_cs}
```

#### ParseSuccessReportGenerator.cs

```csharp
{generator_cs}
```

#### ParseSuccessReportTests.cs

```csharp
{tests_cs}
```

### 5. The first-pass committed reports (interim golden)

#### HomeHealthCare.accdb.parse-success-report.json

```json
{hhc_report}
```

#### NorthwindDevEdition.accdb.parse-success-report.json

```json
{nw_report}
```

### 6. The property-sheet inventory tool

```python
{inventory_py}
```

## Build + test status

`dotnet build` succeeded with no errors (322 warnings — pre-existing
xUnit2013/CA1416/NU1903 warnings in sibling projects, not introduced
by this session).

`dotnet test` from the solution root passed:
- Vba.Grammar.Tests: 294 / 294
- Vba.Parser.Tests: 54 / 54
- Vba.Resolver.Tests: 400 / 400
- Sql.Grammar.Tests: 85 / 85
- Pipeline.Tests: 357 / 357 (includes 2 new
  ParseSuccessReportTests — HHC `[Fact]` + Northwind `[SkippableFact]`)
- SymbolDb.Tests: 70 / 70
- Integration: 14 / 14
- Tests: 436 / 438 (2 [SkippableFact] skipped on this run — Access COM
  fixtures gated separately)

Total: 1710 passed, 2 skipped, 0 failed.

## Disclosure of related files NOT changed this session

These files are referenced by the harness but were NOT modified:

- `AccessHarvester.Vba.Parser/VbaParserPipeline.cs` — provides the
  `VbaParserPipeline.Parse()` entry point the generator calls.
- `AccessHarvester.Sql.Parser/SqlParserPipeline.cs` — provides
  `SqlParserPipeline.Parse()`.
- `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs` and
  `UnresolvedItemAggregator.cs` — the aggregator surface the report
  shape mirrors. Session 1 does NOT invoke the aggregator's
  `Build()` — that requires the live enrichment pipeline (resolver +
  dynamic-SQL + navigation + function-classification) and is deferred
  to `harvester-finish-canonical-golden-output`. The Session 1
  `unresolvedItems` JSON section is therefore an empty list with a
  documenting `note`.
- `AccessHarvester.Vba.Grammar/Vba.g4` — the VBA grammar.
- `samples/DatabaseYaml/HomeHealthCare.accdb/` — the YAML mirror
  the generator reads from.

## Known surprises Session 1 surfaced (will be in the notification body)

1. **VBA parse-failure rate is high**: HHC 29/32 modules, Northwind
   52/66 modules. Root cause (confirmed by per-module diagnostic):
   form / report code-behind modules begin with `Attribute
   VB_GlobalNameSpace = False` declarations that the harvester's
   Vba.g4 grammar does not accept. The grammar does not have an
   `attributeStatement` rule. This is a genuine parser-scope gap
   surfaced by the report — exactly what Phase 4 Criterion #1 was
   intended to surface. Per the stub stop-rules, grammar extensions
   are a strategy-level decision and do NOT absorb into this set.
   The interim JSON records the failures as `parseFailed` counts;
   the human decides at Session 2 (or via spec amendment) whether
   to extend the grammar or to declare Attribute statements as
   expected-non-parseable.

2. **ValidationRule parse-failure rate is high in HHC** (12/14)
   and Northwind (3/3). Root cause: the generator's
   `SELECT 1 FROM t WHERE <expr>` wrapping does not accommodate
   Access-criteria-style values (`Is Not Null`, `Between #date#
   And Date()`, `In ("M","F","X")`) that omit the implicit-operand
   field reference. The wrapping heuristic is documented inline in
   the JSON `note`. Improving the wrapping is a Session 2 refinement
   that will shift counts (and require regenerating the committed
   golden).

3. The first-pass report's `unresolvedItems` section is populated
   with `totalCount: 0`, empty `bySourceAndReason`, and a note
   explaining that full aggregator output requires the live
   enrichment pipeline downstream. This is intentional and disclosed
   in the spec.

## Review focus

Please review for:

1. **Spec elaboration quality**: does the elaborated spec faithfully
   carry the stub's contract forward without scope widening? Are the
   file paths, JSON shape, and Session-1-vs-Session-2 step lists
   internally consistent and operationally executable?
2. **Inventory verdict soundness**: is the (a)+(b)+(c)+(d-partial)
   verdict supported by the inventory data, and is the dialect
   partition correct?
3. **Harness correctness**: does the generator emit a deterministic,
   diff-stable JSON that matches the spec shape? Are there bugs in
   the YAML block-scalar reader, regex counters, or property-sheet
   wrapping logic?
4. **Self-disclosure adequacy**: does the report's `note` field and
   the spec's stop-rules cover the heuristic limitations and
   deferred work transparently enough that a downstream consumer
   (Session 2 orchestrator, human reviewer, or AI consumer of the
   golden JSON) understands what the first-pass measures and what
   it doesn't?
5. **Convention compliance**: any violations of project-guidance
   Conventions (`ai-assignment.md` self-opining instead of routed,
   missing `register_session_start`, build/test parallel, etc.)?

Flag any Critical / Major / Minor findings per the project-guidance
JSON schema requirement below. Treat a finding as Minor if it's a
heuristic limit that's already disclosed in the report `note` or the
spec; treat it as Major if the disclosure is missing or misleading.

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
review focus area BEFORE the JSON block.
"""

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    review_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}.md"
    review_path.parent.mkdir(parents=True, exist_ok=True)
    review_path.write_text(result.content, encoding="utf-8")

    print(f"[wrote {review_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")

    log = SessionLog(str(SESSION_SET), total_sessions=2)
    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=9,
        step_key="session-1/verify",
        description="Cross-provider session-verification routed",
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
