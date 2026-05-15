"""Session 2 verification Round 3 — after fixing the Round 2 Minor finding."""

from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog


SESSION_SET = Path("docs/session-sets/generalization-validation-on-non-northwind")


def read_file(path: str) -> str:
    raw = Path(path).read_bytes()
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp1252", errors="replace")


def build_prompt() -> str:
    findings = read_file(str(SESSION_SET / "generalization-findings.md"))
    package = read_file(str(SESSION_SET / "packages" / "add-a-client.md"))
    round_2 = read_file(str(SESSION_SET / "session-reviews" / "session-002-round-2.md"))

    prompt = f"""You are performing Round 3 verification for Session 2 of the
`generalization-validation-on-non-northwind` session set. Round 2 returned
ISSUES_FOUND with one Minor finding — stale `FormatPhone` references in
`generalization-findings.md` that contradicted the upgraded package + test.
The orchestrator addressed that finding.

Your response MUST begin with a prose analysis confirming whether the Round 2
Minor finding is resolved, BEFORE the trailing JSON block.

# What changed since Round 2

The orchestrator scanned `generalization-findings.md` for all references to
`FormatPhone`, `inferred`, `not read`, and `exact mask` and updated each to
reflect that `basUtilities::FormatPhone` was read and the rule is now
`confirmed`:

- **Template Fit table, Section 3 row:** rewritten to say the phone-mask
  rule "initially flagged as `inferred` pending a read of
  `basUtilities::FormatPhone`; after the read it landed as `confirmed`,
  leaving every Session-2 rule at `confirmed`".
- **Template Fit table, Section 7 row:** rewritten to say Session 2 added
  one ambiguity-log entry (state-city auto-fill intent), not two, "after
  the in-flight `FormatPhone` open-mask question was resolved by reading
  `basUtilities`".
- **Harvester Coverage / Required cross-referencing → Helper module
  bodies bullet:** rewritten to describe the *experience* — the peer
  review had to grep for `FormatPhone` across the YAML mirror; a
  symbol-index extension would have made the upgrade from `inferred` to
  `confirmed` mechanical. The narrative now matches the resolved state
  rather than treating the helper body as "not read".
- **Honest Negatives caveat #3:** rewritten as a process-shortcut
  retrospective. Session 2 initially pinned only "phone is
  deterministically normalized" and pushed the mask to Section 7; the
  Round-1 verifier correctly flagged that the falsification test
  hardcoded a mask the package didn't claim to have read; the fix was
  reading `FormatPhone`, upgrading the rule to `confirmed`, aligning the
  test, and dropping the resolved ambiguity-log entry.

# What I want you to verify

1. The four `FormatPhone`-adjacent passages in `generalization-findings.md`
   now consistently describe the resolved state, not the unresolved state.
2. No other stale references to "inferred / not read / open question on the
   mask" remain.
3. No new internal contradictions were introduced by the rewrites.
4. The verdict `generalizes-cleanly` is still well-supported.

# Files

## generalization-findings.md (post-Round-2 fix)

{findings}

## packages/add-a-client.md (unchanged from Round 2)

{package}

## Round 2 verifier review (for context)

{round_2}

# Build and test status

`dotnet build` clean (0 errors). Characterization tests all pass (131 / 0
failed / 0 skipped). No further test or package changes since Round 2.

# Required response shape

Begin with a prose analysis. After your prose analysis, emit a single
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
        complexity_hint=80,
        session_set=str(SESSION_SET),
        session_number=2,
    )

    output_path = SESSION_SET / "session-reviews" / "session-002-round-3.md"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(result.content, encoding="utf-8")

    log = SessionLog(SESSION_SET)
    log.save_session_review(
        session_number=2,
        review_text=result.content,
        round_number=3,
    )

    log.log_step(
        session_number=2,
        step_number=14,
        step_key="session-2/verification-round-3",
        description=(
            f"Round 3 cross-provider session-verification after fixing the Round 2 "
            f"Minor finding (stale FormatPhone references in generalization-findings.md). "
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
