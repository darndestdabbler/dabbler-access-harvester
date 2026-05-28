"""Session 3 Round-2 cross-provider verification after R1 fix."""

from __future__ import annotations

import os
import sys
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route, SessionLog

ROOT = Path(__file__).resolve().parent.parent
SESSION_SET = ROOT / "docs" / "session-sets" / "harvester-data-classifier"

EDITOR_PATH = ROOT / "tools" / "classification-editor.html"
PY_TEST_PATH = ROOT / "tools" / "classification-editor-self-test.py"
JS_TEST_PATH = ROOT / "tools" / "classification-editor-js-test.mjs"
SCHEMA_PATH = ROOT / "AccessHarvester.Pipeline" / "Classification" / "classification-schema.json"
R1_REVIEW = SESSION_SET / "session-reviews" / "session-003-r1.md"


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8")


def main() -> int:
    editor_src = read(EDITOR_PATH)
    py_test_src = read(PY_TEST_PATH)
    js_test_src = read(JS_TEST_PATH)
    schema_src = read(SCHEMA_PATH)
    r1_review_src = read(R1_REVIEW)

    prompt = f"""You are the independent cross-provider verifier for Session 3 of 4 of the
harvester-data-classifier session set. This is Round 2 — your prior review
file is included verbatim below. Your job is to confirm the R1 Minor finding
was correctly resolved AND to scan the changed file for any new regression
the fix may have introduced. Do not re-flag anything outside the R1 finding
unless you genuinely believe it is new and substantive.

R1 finding to verify the fix for
================================
Severity: Minor
Location: tools/classification-editor-js-test.mjs
Summary: "The JS headless test does not actually compare the editor's
category, masking-strategy, and version constants against
classification-schema.json, so enum/version drift or a misspelled code could
pass the automated substitute."
Fix recommended: "Load
AccessHarvester.Pipeline/Classification/classification-schema.json in the JS
test and assert exact equality against properties.version.const,
definitions.ColumnCategory.enum, and definitions.MaskingStrategy.enum; also
add explicit phone/email cases for findSuspectNotes to match the helper's
intended coverage."

What changed in R1 fix
======================
Only tools/classification-editor-js-test.mjs was touched:

  - The header comment block was updated to describe the schema-loading
    behavior.
  - The script now reads
    AccessHarvester.Pipeline/Classification/classification-schema.json into
    a parsed `schema` constant alongside the fixture.
  - The "constants sanity" section was replaced with an exact-equality
    check against schema.properties.version.const,
    schema.definitions.ColumnCategory.enum, and
    schema.definitions.MaskingStrategy.enum. A `setsEqual` helper does the
    equality check; missing schema fields are themselves failures.
  - The single-SSN-only suspect-notes probe was replaced with a PROBES list
    that exercises every RAW_VALUE_HEURISTICS pattern: SSN-dashed,
    SSN-dotted, US phone, and email. Each is asserted to be flagged.
  - The closing log lines now report the per-enum match status.

The editor HTML, the Python self-test, and the schema were not changed.

Result of `node tools/classification-editor-js-test.mjs` (post-fix):
    OK: 8 JS check groups passed
      - 10 categories match schema enum
      - 7 masking strategies match schema enum
      - schema-version constant: 1.0 (matches schema.properties.version.const)

Result of `python tools/classification-editor-self-test.py` (unchanged):
    OK: 8 checks passed

What to check this round
========================
1. The fix actually loads the schema and compares enum sets and version
   const as the R1 finding required, not a weaker derivative.
2. The PROBES coverage in fact exercises every entry of the
   RAW_VALUE_HEURISTICS array in tools/classification-editor.html (4
   regexes). If a probe is missing for a heuristic that exists, flag it.
3. No regression in the unchanged checks (FK authority, plausible-doc,
   defaults shape).
4. No new logical errors (e.g., setsEqual helper hoisted into the wrong
   scope, schema path wrong, etc.).
5. If you spot a substantive new issue unrelated to the R1 finding, flag
   it — but be conservative.

Out of scope
============
- Anything covered in the R1 review whose verdict was pass. Do not re-flag
  those.
- The editor HTML itself (unchanged this round).
- Browser-based UI testing (waived; see R1).

R1 review (verbatim — do not re-author findings already closed)
================================================================
{r1_review_src}

Editor source (UNCHANGED this round — included for context only)
=================================================================
```html
{editor_src}
```

Python self-test (UNCHANGED this round — included for context only)
====================================================================
```python
{py_test_src}
```

Schema (UNCHANGED — for cross-checking enum equality this round)
=================================================================
```json
{schema_src}
```

UPDATED JS self-test (the file that landed the R1 fix)
=======================================================
```javascript
{js_test_src}
```

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
"""

    print(f"Prompt size: {len(prompt):,} chars", file=sys.stderr)

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=55,
        session_set=str(SESSION_SET),
        session_number=3,
    )

    out_path = SESSION_SET / "session-reviews" / "session-003-r2.md"
    out_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {out_path.relative_to(ROOT).as_posix()}")
    print(f"Model: {result.model_name}")
    print(f"Cost: ${result.cost_usd:.4f}")
    print(f"Input tokens:  {result.input_tokens}")
    print(f"Output tokens: {result.output_tokens}")

    log = SessionLog(str(SESSION_SET))
    log.log_step(
        session_number=3,
        step_number=5,
        step_key="session-3/verification-r2",
        description=(
            "Round 2 cross-provider session-verification after R1 fix. Updated only "
            "tools/classification-editor-js-test.mjs to load the schema and assert exact "
            "equality of CATEGORIES/MASKING_STRATEGIES/SCHEMA_VERSION against the schema "
            "enums and version const, and to exercise every RAW_VALUE_HEURISTICS pattern "
            f"(SSN-dashed, SSN-dotted, US phone, email). Model: {result.model_name}; "
            f"cost ${result.cost_usd:.4f}. R1 review file included in the prompt so the "
            "verifier checks the fix landed correctly rather than re-flagging from scratch."
        ),
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
