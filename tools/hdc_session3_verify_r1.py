"""Session 3 Round-1 cross-provider verification for harvester-data-classifier."""

from __future__ import annotations

import os
import sys
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route, SessionLog

ROOT = Path(__file__).resolve().parent.parent
SESSION_SET = ROOT / "docs" / "session-sets" / "harvester-data-classifier"

# Files included in the prompt verbatim.
EDITOR_PATH = ROOT / "tools" / "classification-editor.html"
PY_TEST_PATH = ROOT / "tools" / "classification-editor-self-test.py"
JS_TEST_PATH = ROOT / "tools" / "classification-editor-js-test.mjs"
SCHEMA_PATH = ROOT / "AccessHarvester.Pipeline" / "Classification" / "classification-schema.json"
FIXTURE_PATH = ROOT / "samples" / "classification-fixture.json"
SPEC_PATH = SESSION_SET / "spec.md"
PLAN_PATH = ROOT / "docs" / "planning" / "data-classification-plan.md"


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8")


def main() -> int:
    spec_full = read(SPEC_PATH)
    # The Session 3 block runs from "### Session 3 of 4" to "### Session 4 of 4".
    spec_s3_start = spec_full.find("### Session 3 of 4")
    spec_s3_end = spec_full.find("### Session 4 of 4")
    spec_s3 = spec_full[spec_s3_start:spec_s3_end].strip() if spec_s3_start >= 0 and spec_s3_end > spec_s3_start else spec_full

    plan_full = read(PLAN_PATH)
    # § 3.5, § 4, § 7 are the relevant Session 3 anchors.
    plan_idx = plan_full.find("### 3.5 Category taxonomy")
    plan_excerpt = plan_full[plan_idx:] if plan_idx >= 0 else plan_full

    editor_src = read(EDITOR_PATH)
    py_test_src = read(PY_TEST_PATH)
    js_test_src = read(JS_TEST_PATH)
    schema_src = read(SCHEMA_PATH)
    fixture_src = read(FIXTURE_PATH)

    prompt = f"""You are the independent cross-provider verifier for Session 3 of 4 of the
harvester-data-classifier session set on the dabbler-access-harvester repo.
The orchestrator is claude-opus-4-7; you are NOT the orchestrator. Review the
session's deliverables against the spec, the JSON schema from Session 1, the
data-classification-plan section it implements, and the headless tests the
orchestrator added to prove the contract.

What this session shipped
=========================
1. tools/classification-editor.html — a single-file zero-install HTML editor
   per the Session 3 "in-repo" branch of the spec's open decision (the human
   chose tools/classification-editor.html over a new standalone repo at
   session start, taking the spec's stop-rule default).
2. tools/classification-editor-self-test.py — Python self-test that proves
   the schema round-trip and FK-authority constraints. Replaces the spec's
   "manual test" step (a browser is unavailable in this orchestrator's
   environment).
3. tools/classification-editor-js-test.mjs — Node test that extracts the
   editor's pure-helpers block, evaluates it in a vm context, and asserts
   the exported constants and functions behave correctly.

The editor is browser-only at runtime; it has no build step, no server, no
network calls, and no external dependencies.

Out of scope for Session 3 (do NOT flag as missing)
===================================================
- AccessHarvester.Pipeline/Classification/* (scanner) — shipped in Session 2.
- AccessHarvester.Core/Com/* (COM readers) — shipped in Session 2.
- AccessHarvester.Cli/Commands/ClassifyCommand.cs — shipped in Session 2.
- AccessHarvester.Pipeline/Sanitization/* (sanitizer) — Session 4.
- Any CI/test-runner integration of the two new tools/ test scripts. The
  session-set authoring guide says these are headless dev-time checks for
  this single editor; they live under tools/, not under tests/.
- Browser-based UI tests. Not possible from this orchestrator environment
  and explicitly waived in the Session Set Configuration block
  (requiresUAT: false, requiresE2E: false).

Concerns to check (please go through each in prose, then emit the JSON block)
=============================================================================
A. Schema contract. Does the editor write a JSON document that round-trips
   through classification-schema.json? In particular: does it touch only the
   humanOverride blocks, and never modify scanner-emitted fields like
   piiValueMatch, valueFormatPattern, firedHeuristics, confidence, etc.?
B. FK authority enforcement. The schema's column allOf (when isForeignKey is
   true, humanOverride.category must be null or "foreign-key";
   maskingStrategy must be null, "preserve", or "remap-id"). Does the editor's
   UI disable disallowed choices for FK columns? Does the save-time guard
   refuse to overwrite a violating document silently? Are the pure helpers
   (isFkAuthorityValid, findFkAuthorityViolations) correctly implementing the
   constraint?
C. Privacy invariant. The schema description (the $id block) explains that
   scanner-emitted fields never carry raw sampled values, but free-form notes
   can. Does the editor:
     - never render the valueFormatPattern as if it were a sample value;
     - warn the reviewer in the table-override notes textarea and the
       per-column notes textarea about not pasting raw values;
     - include heuristics in findSuspectNotes that catch SSN/phone/email
       patterns reviewers might accidentally paste?
D. 10-category taxonomy completeness. Are the editor's CATEGORIES exactly
   the 10 codes defined in the schema's ColumnCategory enum and the plan
   section 3.5? Same question for MASKING_STRATEGIES vs. the schema's
   MaskingStrategy enum.
E. Per-spec controls. The spec lists:
     - 10-category radio group per column (implemented as a vertical radio
       group with an extra "(use AI guess)" sentinel that maps to null);
     - preserve-autonumber checkbox per column (disabled when isAutoNumber
       is false, since it is meaningless then);
     - masking-strategy selector per column (rendered as a select with a
       "(category default)" sentinel that maps to null);
     - lookup-override per table (rendered as a 3-state radio group rather
       than a checkbox because the schema field is "boolean" | null and a
       checkbox cannot represent the null = "use AI guess" state — flag if
       you disagree with this interpretation);
     - sticky reviewer at the top bar that auto-populates new edits'
       reviewedBy when blank;
     - file-open and file-save for classification.json.
F. Save UX. The Save button uses URL.createObjectURL on a JSON blob and
   triggers a download; Save As prompts for a filename. There is no
   File System Access API integration — flag whether this matters for the
   spec's "loads from / saves to the file system" requirement.
G. Test quality. Are the two test scripts strong enough as the headless
   "manual test" substitute? Specifically: do they exercise every constraint
   on the editor's save output that the spec implies a human reviewer would
   exercise?
H. Spec ambiguity. The spec step 2 says "10-category radio group" but the
   plan and the schema both use "Option buttons for the category taxonomy".
   The editor uses HTML radio inputs (one per category) with the AI-guess
   sentinel as the first option. Flag any reading that says the editor
   should instead use a single dropdown or a different control.

Session set configuration block (from spec.md)
==============================================
requiresUAT: false
requiresE2E: false
uatScope: none

The configuration explicitly waives a human-UAT step for this session set
(the editor is judged by automated unit tests + this cross-provider review).
Do not flag the absence of a UAT checklist as a finding.

Editor source (tools/classification-editor.html)
================================================
```html
{editor_src}
```

Python self-test (tools/classification-editor-self-test.py)
============================================================
```python
{py_test_src}
```

Result of `python tools/classification-editor-self-test.py`:
    OK: 8 checks passed
      - schema:  AccessHarvester.Pipeline/Classification/classification-schema.json
      - fixture: samples/classification-fixture.json

Node self-test (tools/classification-editor-js-test.mjs)
=========================================================
```javascript
{js_test_src}
```

Result of `node tools/classification-editor-js-test.mjs`:
    OK: 8 JS check groups passed
      - 10 categories, 7 masking strategies
      - schema-version constant: 1.0

Classification schema (AccessHarvester.Pipeline/Classification/classification-schema.json)
==========================================================================================
```json
{schema_src}
```

Classification fixture (samples/classification-fixture.json)
============================================================
```json
{fixture_src}
```

Session 3 spec (docs/session-sets/harvester-data-classifier/spec.md)
====================================================================
```markdown
{spec_s3}
```

Data classification plan § 3.5+ (docs/planning/data-classification-plan.md)
===========================================================================
```markdown
{plan_excerpt}
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
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=3,
    )

    out_path = SESSION_SET / "session-reviews" / "session-003-r1.md"
    out_path.write_text(result.content, encoding="utf-8")
    print(f"Wrote {out_path.relative_to(ROOT).as_posix()}")
    print(f"Model: {result.model_name}")
    print(f"Cost: ${result.cost_usd:.4f}")
    print(f"Input tokens:  {result.input_tokens}")
    print(f"Output tokens: {result.output_tokens}")

    log = SessionLog(str(SESSION_SET))
    log.log_step(
        session_number=3,
        step_number=4,
        step_key="session-3/verification-r1",
        description=(
            "Round 1 cross-provider session-verification via route(task_type='session-verification'). "
            f"Model: {result.model_name}; cost ${result.cost_usd:.4f}. "
            "Prompt included the editor HTML, both headless test scripts, the schema, the fixture, "
            "the Session 3 spec block, and plan section 3.5+. Out-of-scope disclosures: scanner code "
            "from Sessions 1 & 2, sanitizer code from Session 4, browser-based UI tests."
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
