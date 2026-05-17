"""Session 2 cross-provider session-verification, Round 3 (final retry).

Round 2 resolved the 2 Major R1 findings + 1 Minor R1 finding cleanly,
and surfaced 1 new Minor: stale global authority framing (opening
"Authority for the reason set" + closing "Stability commitment") that
still spoke of SuggestedNextStepsLookup as the full source-of-truth.
R2 fix: both spots now qualify the lookup's role as authoritative for
stable reasons only, and explicitly call out the
`member-not-found-at-segment-<N>` family as the documented
dynamic-prefix exception backed by the
`MemberNotFoundAtSegment(int)` factory.

This is the final retry under the max-2-retries cap (R1 + R2 + R3 = 3
rounds total).
"""
import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
log = SessionLog(SESSION_SET)


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


narrative = read(SESSION_SET / "unresolved-reason-narrative.md")
r2_review = read(SESSION_SET / "session-reviews" / "session-002-round2.md")

prompt = f"""\
You are the cross-provider session-verifier for Session 2 of the session set
`harvester-finish-canonical-parse-success-report` in the dabbler-access-harvester
repo. This is Round 3 — the final retry under the workflow's max-2-retries cap.

Round 2 confirmed the R1 Majors resolved and surfaced one new Minor:
the global authority framing (opening "Authority for the reason set"
block, closing "Stability commitment" block) still claimed
`SuggestedNextStepsLookup` was the full source-of-truth even though the
narrative intentionally includes `member-not-found-at-segment-<N>` as a
documented dynamic-prefix exception. Both spots have been rewritten to
qualify the lookup's role to stable lookup-backed reasons only and to
name the `MemberNotFoundAtSegment(int)` factory as the basis for the
dynamic-prefix exception.

**Your response MUST begin with a detailed prose analysis covering each
concern below BEFORE the JSON block at the end.** Do not skip the prose
analysis.

Analyse the following concerns in order:

1. **R2 Minor — opening Authority block.** The opening block now
   describes `SuggestedNextStepsLookup` as authoritative for stable
   lookup-backed reasons and names `MemberNotFoundAtSegment(int)` as
   the basis for the dynamic-prefix exception. Is the new framing
   complete and internally consistent?
2. **R2 Minor — closing Stability commitment.** The closing section
   now distinguishes stable lookup-backed reasons (which land via the
   lookup) from dynamic-prefix families (which use
   `GenericSuggestedNextSteps`). Is the distinction explicit enough,
   and is the "new dynamic-prefix family appears" path described
   clearly enough that a future author would follow the pattern?
3. **Residual stale phrasing anywhere else.** Is there any other
   place in the document that still implies every reason in the
   document is a lookup key, or that the lookup is the singular
   reason-set source-of-truth?
4. **New drift.** Did the R2 fixes introduce any new typo,
   contradiction, or shape mismatch?

Reminder: the four in-scope code/test files were R1-VERIFIED and have
not been touched since. Do not flag them as missing.

---

## R2 verifier review (your prior findings)

```
{r2_review}
```

---

## Revised `unresolved-reason-narrative.md` (post-R2)

```
{narrative}
```

---

## Required response format

After your detailed prose analysis, output a JSON block in this exact format:

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

out_dir = SESSION_SET / "session-reviews"
prompt_path = out_dir / "session-002-r3-prompt.md"
prompt_path.write_text(prompt, encoding="utf-8")
print(f"Wrote R3 prompt ({len(prompt)} chars) to {prompt_path}")

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=85,
    session_set=str(SESSION_SET),
    session_number=2,
)

review_path = out_dir / "session-002-round3.md"
review_path.write_text(result.content, encoding="utf-8")
log.save_session_review(session_number=2, review_text=result.content, round_number=3)

log.log_step(
    session_number=2,
    step_number=7,
    step_key="session-2/verify-round3",
    description="Cross-provider session-verification round 3 (final retry; post R2 global-framing fixes)",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)

print("=" * 72)
print(f"Model: {result.model_name}")
print(f"Cost: ${result.cost_usd:.4f}")
print(f"Input tokens: {result.input_tokens}, Output tokens: {result.output_tokens}")
print(f"Review saved to {review_path}")
