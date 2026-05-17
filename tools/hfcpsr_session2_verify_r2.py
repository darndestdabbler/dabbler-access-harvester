"""Session 2 cross-provider session-verification, Round 2.

Round 1 surfaced 2 Major + 1 Minor against the narrative only. R1 fixes:
  * member-not-found-at-segment-<N> rewritten as an explicit
    dynamic-prefix family entry with the fallback nature frontloaded.
  * unknown-expression reconciled: prose now says the analyzer
    treats Replace/IIf as unknown holes (per DynamicSqlAnalyzer.cs's
    deferred-gap comment), matching the verbatim suggested-next-steps.
  * FunctionClassification gained five short per-pair subentries
    cross-referencing Resolver, satisfying the spec's "one paragraph
    per (Source, Reason) pair" shape.
Intro section was swept for stale framing about the stable-reason
contract (Post-Fix Sweep convention) and updated to acknowledge the
dynamic-prefix family exception.

R1 also confirmed the code/test changes are clean (sentinel test
exercises the production builder; diff-gate continuity preserved);
no resends of those files are needed.
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


narrative = read("docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md")
r1_review = read("docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews/session-002.md")

prompt = f"""\
You are the cross-provider session-verifier for Session 2 of the session set
`harvester-finish-canonical-parse-success-report` in the dabbler-access-harvester
repo. This is Round 2 verification — Round 1 surfaced 2 Major + 1 Minor on
`unresolved-reason-narrative.md` only (code/tests passed clean). The narrative
has been revised; this round is scoped to confirming the R1 findings are
resolved without introducing new drift.

**Your response MUST begin with a detailed prose analysis covering each R1
finding below BEFORE the JSON block at the end.** Do not skip the prose
analysis — the JSON block alone is not sufficient.

Analyse the following concerns in order:

1. **R1 Issue 1 — `member-not-found-at-segment-<N>` framing.** Round 1
   reported that the subsection was styled like a stable lookup-backed
   reason entry. The revised section is now titled
   "Dynamic-prefix family: `member-not-found-at-segment-<N>`" and
   leads with a paragraph that explicitly states it is not a stable
   lookup-backed entry. Is the new framing adequate, or does any
   residual phrasing still imply `<N>` is a literal stable code?
2. **R1 Issue 2 — `unknown-expression` analyzer-support contradiction.**
   Round 1 reported that the prose claimed `Replace` and `IIf` were
   already recognised while the verbatim suggested-next-steps said
   to consider adding support for them. The "Why the producer could
   not finish" paragraph is now rewritten to state that
   `Replace`/`IIf`/`Format`/`String$` are treated as unknown holes per
   `DynamicSqlAnalyzer.cs`'s own deferred-gap comment. Is the
   reconciliation correct, and is the prose now internally
   consistent with the verbatim suggested-next-steps?
3. **R1 Issue 3 — FunctionClassification per-pair coverage.** Round 1
   reported that the section collapsed five `(Source, Reason)` pairs
   into a cross-reference. The section now carries five short
   subentries (one per reason code), each cross-referencing
   Resolver and pinning the verbatim suggested-next-steps. Does the
   new shape match the spec's "one paragraph per `(Source, Reason)`
   pair" requirement?
4. **Post-fix sweep.** The intro section was updated to acknowledge
   the dynamic-prefix family as an exception to "one subheading per
   stable reason code" and to qualify the matching guidance accordingly.
   Are there any other places in the document that still carry stale
   pre-fix language (e.g., implying every reason is a stable lookup
   key, or implying Replace/IIf are recognised today)?
5. **New drift.** Did the R1 fixes introduce any new inconsistency,
   typo, or contradiction with the rest of the narrative?

The full revised narrative follows. The R1 verifier review is also
included so you can read your own prior findings side by side.

### Files NOT in this round (unchanged since R1 and already verified clean)

These were R1-VERIFIED and have not been touched in the R1→R2 fix
batch. Do not flag them as missing:

- `ParseSuccessReportDiff.cs` (helper)
- `ParseSuccessReportDiffTests.cs` (sentinel)
- `ParseSuccessReportTests.cs` (live gate)

---

## R1 verifier review (your prior findings)

```
{r1_review}
```

---

## Revised `unresolved-reason-narrative.md` (post-R1)

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

# Persist prompt before call.
out_dir = SESSION_SET / "session-reviews"
prompt_path = out_dir / "session-002-r2-prompt.md"
prompt_path.write_text(prompt, encoding="utf-8")
print(f"Wrote R2 prompt ({len(prompt)} chars) to {prompt_path}")

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=85,
    session_set=str(SESSION_SET),
    session_number=2,
)

# Persist response before display.
review_path = out_dir / "session-002-round2.md"
review_path.write_text(result.content, encoding="utf-8")
log.save_session_review(session_number=2, review_text=result.content, round_number=2)

log.log_step(
    session_number=2,
    step_number=6,
    step_key="session-2/verify-round2",
    description="Cross-provider session-verification round 2 (complexity_hint=85, post R1 narrative fixes)",
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
