"""Session 2 verification Round 2 — after fixing the Round 1 findings.

Persist-then-display per the Lessons Learned convention.
"""

from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog


SESSION_SET = Path("docs/session-sets/generalization-validation-on-non-northwind")


def read_file(path: str) -> str:
    # Some YAML mirror files were authored with a code page that includes
    # bytes that are not valid UTF-8 (the form/module YAMLs have curly
    # quotes etc.). Be forgiving for the verifier prompt — we only need
    # human-readable content, not byte fidelity.
    raw = Path(path).read_bytes()
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp1252", errors="replace")


def build_prompt() -> str:
    spec_excerpt = read_file(str(SESSION_SET / "spec.md"))
    package = read_file(str(SESSION_SET / "packages" / "add-a-client.md"))
    findings = read_file(str(SESSION_SET / "generalization-findings.md"))
    s2_tests = read_file(
        "tests/AccessHarvester.CharacterizationTests/AddAClient/Session2FalsificationTests.cs"
    )
    round_1 = read_file(str(SESSION_SET / "session-reviews" / "session-002.md"))
    issues_log = read_file(str(SESSION_SET / "issue-logs" / "session-002.json"))
    format_phone_source = read_file(
        "samples/DatabaseYaml/HomeHealthCare.accdb/Modules/basUtilities.yaml"
    )

    prompt = f"""You are performing Round 2 verification for Session 2 of the
`generalization-validation-on-non-northwind` session set. Round 1 returned
ISSUES_FOUND with one Major and two Minor findings (the raw review is included
below). The orchestrator addressed all three findings before this Round 2 call.

Your response MUST begin with a detailed prose analysis confirming whether each
Round 1 issue is resolved, BEFORE the trailing JSON block. Do not return a
JSON-only or bare-verdict response.

# What changed since Round 1

1. **Major fix (FormatPhone test).** The orchestrator located and read
   `basUtilities::FormatPhone` (included below in full). The package's
   phone-normalization rule was upgraded from "confirmed normalization happens;
   inferred exact mask" to **fully confirmed** with the exact mask documented
   in the rule's evidence (10-digit -> "(NNN) NNN-NNNN"; null -> empty;
   otherwise raw `CStr(PhoneRaw)`). The test stub was aligned to match the
   source exactly (null returns `string.Empty` rather than `null`; non-10-digit
   returns the raw input). The corresponding Section 7 ambiguity-log entry
   ("What canonical mask does FormatPhone emit?") was removed since it is now
   resolved.

2. **Minor fix (OpenArgs overclaim).** The OpenArgs pattern-divergence finding
   in `generalization-findings.md` was narrowed to the variants actually
   exercised by `Add a Client` (`"ADD"` and numeric PK), with an explicit note
   that the pipe-delimited `BuildContextArgs` payload variant is sibling-workflow
   territory (`frmClientServiceNeedDetail`, `frmBudgetServicePlan`) and not a
   finding of this set.

3. **Minor fix (5-vs-4 count drift).** The deactivation invariant in Section 4
   was split into two bullets — one for cancel-reversibility, one for
   confirm-with-warning — so the package's Section 4 now has five explicit
   Session 2 falsification bullets aligned with the change-log and findings
   narrative.

# What I want you to verify

Confirm that:

1. Round 1 Major (FormatPhone test): the test now mirrors the actual
   `basUtilities::FormatPhone` source, and the package's rule + invariant
   wording is now backed by the source rather than by a test-local stub.
2. Round 1 Minor 1 (OpenArgs overclaim): the findings now describe only the
   variants this workflow exercises, with the broader pattern explicitly
   deferred to a follow-up.
3. Round 1 Minor 2 (count drift): Section 4 of the package now shows five
   explicit Session 2 falsification invariant bullets.
4. No new issues were introduced by the fixes.

# Files

## `basUtilities::FormatPhone` source (the new evidence)

```
{format_phone_source}
```

## `packages/add-a-client.md` (post-fix)

{package}

## `generalization-findings.md` (post-fix)

{findings}

## `Session2FalsificationTests.cs` (post-fix)

```csharp
{s2_tests}
```

## Round 1 verifier review (for context)

{round_1}

## Issue log (orchestrator's fix actions)

```json
{issues_log}
```

## Spec (unchanged from Round 1)

{spec_excerpt}

# Files OUT of scope but referenced

Same set as Round 1: the inventory, Session 1's existing test files (the shared
`ClientDraft` model in `AddAClientWorkflowModels.cs` plus the three Session 1
test files), `change-log.md`, `next-session-set-recommendation.md`, and
`disposition.json` are all in the repo but not duplicated here. The Round 1
review confirmed those as substantially compliant.

# Build and test status

`dotnet build` clean (0 errors). The characterization-tests project re-built
and re-ran post-fix: **131 passed, 0 failed, 0 skipped** (same count as Round 1
since the fix only adjusted assertion expectations in one existing test plus
the helper's null-return shape).

# Required response shape

Begin with a per-fix prose analysis. After your prose analysis, emit a single
trailing JSON block in this exact format:

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

If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.
"""
    return prompt


def main() -> int:
    prompt = build_prompt()
    print(f"Prompt size: {len(prompt)} chars (approx {len(prompt) // 4} tokens)")

    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=85,
        session_set=str(SESSION_SET),
        session_number=2,
    )

    output_path = SESSION_SET / "session-reviews" / "session-002-round-2.md"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.save_session_review(
        session_number=2,
        review_text=result.content,
        round_number=2,
    )

    log.log_step(
        session_number=2,
        step_number=13,
        step_key="session-2/verification-round-2",
        description=(
            f"Round 2 cross-provider session-verification after fixing the Round 1 "
            f"Major (FormatPhone test overspecified) and two Minor findings. "
            f"Model: {result.model_name}."
        ),
        status="complete",
        api_calls=[
            {
                "model": result.model_name,
                "taskType": "session-verification",
                "inputTokens": result.input_tokens,
                "outputTokens": result.output_tokens,
                "costUsd": result.cost_usd,
            }
        ],
    )

    print(f"Model: {result.model_name}")
    print(f"Input tokens: {result.input_tokens}; Output tokens: {result.output_tokens}")
    print(f"Cost: ${result.cost_usd:.4f}")
    print(f"Review saved to: {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
