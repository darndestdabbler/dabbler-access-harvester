"""
Round 3 cross-provider session-verification for remaining-work-planner Session 2.
Focused re-verify after the R2 fixes.
"""
from pathlib import Path
import json
import sys

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/remaining-work-planner")
SESSION_NUMBER = 2

REPO = Path(".")

def read(p: str) -> str:
    return (REPO / p).read_text(encoding="utf-8")

tree = read("docs/session-sets/remaining-work-planner/remaining-work-tree.md")
f3_stub = read("docs/session-sets/harvester-finish-cli-e2e-test-audit/spec.md")
r2_review = read("docs/session-sets/remaining-work-planner/session-reviews/session-002-round2.md")

prompt = f"""You are the same cross-provider verifier for `remaining-work-planner`
Session 2, Round 3.

In Round 2 you returned ISSUES_FOUND with one Major + one Minor finding:

1. **Major (F3):** Determinism gate cited Criterion #4 (every subcommand
   deterministic) but only covered parse / generate-docs / run, not
   extract or translate.
2. **Minor (tree):** Cost rollup `~$1.00–$1.40` did not match the sum
   of per-stub ranges (`~$1.00–$3.20`).

# Round 3 task

For each finding, confirm whether the fix below resolves it. Then do a
fresh drift sweep.

# Fix 1 — F3 stub revised (Out of Scope unchanged; the determinism-
# gate language now explicitly states `run` subsumes `extract` and
# `translate`)

The relevant excerpts:

**Session 2 Step 1 (revised):**

```
1. Add determinism tests (run-twice-diff) for the three
   subcommands that produce non-trivial output: `parse`,
   `generate-docs`, and `run`. **The `run` determinism gate
   subsumes `extract` and `translate`** because `run = extract +
   translate + enrich` per `PrintUsage()` and the dispatcher — a
   determinism failure in either sub-phase surfaces as a `run`
   diff. This satisfies Criterion #4's "every harvester subcommand
   produces deterministic output" without authoring duplicative
   single-stage gates that would re-test the same code paths.
   Document the subsumption in the test class's XML doc-comment so
   a future reader sees the intent. The `run` determinism gate may
   reuse the `harvester-finish-canonical-golden-output` regression
   harness directly rather than duplicating it; Session 1 decides.
```

**In-scope item 4 (revised):**

```
4. Determinism gate: for each of `parse`, `generate-docs`,
   `run` (the three with non-trivial output), run twice and assert
   normalized output is byte-identical. **The `run` gate subsumes
   `extract` and `translate`** since `run` invokes both as
   sub-phases; a determinism failure in either surfaces as a
   `run` diff. This is the deliberate satisfaction of Criterion
   #4's "every harvester subcommand produces deterministic output"
   without redundant single-stage gates over the same code paths.
```

Full revised stub:

```markdown
{f3_stub}
```

# Fix 2 — tree's cost-rollup section revised

The relevant excerpt from `remaining-work-tree.md`:

```
**Net: 5 session sets, 13 sessions total** to close Phase 4.
(Original gap-audit estimate: ~6 sets, 10-14 sessions. The
fold-in of GAP #5 into F2 trims one set; F2's medium-band cost
absorbs the additional surface.)

**Routed-cost rollup: ~$1.00–$3.20.** Sums the per-stub ranges
declared in each F-set's "Cost Expectations":

| Set | Per-stub routed-cost range |
|---|---|
| F1 | ~$0.10–$0.40 |
| F2 | ~$0.40–$0.80 |
| F3 | ~$0.10–$0.40 |
| F4 | ~$0.30–$1.20 |
| F5 | ~$0.10–$0.40 |
| **Total** | **~$1.00–$3.20** |

This is higher than the gap-audit's earlier rough estimate of
~$1.00–$1.40 (which assumed a flat ~$0.10/session × 10–14 sessions
under the cross-provider-verification-only model). The
per-stub-derived sum reflects two stub-level decisions Session 2
made that the audit could not have priced:

1. F4's audience-check + downstream-AI probes add 2 extra routed
   calls per probe-bearing session beyond bare verification.
2. F2's architecture call for the manifest aggregator design plus
   any code-review calls on the wiring change push the per-session
   routed budget up modestly.

The per-stub ranges are the authoritative budget; the rollup is
their honest sum. Treat the upper bound as the planning cap.
```

Full revised tree:

```markdown
{tree}
```

# Drift-sweep checklist

Confirm:

- The 13-sessions total still matches F1=2 + F2=4 + F3=2 + F4=3 + F5=2.
- The 5-sets cell still matches the 5 stubs.
- No other reference to `$1.00–$1.40` remains in the tree (the
  reference is now in a reconciling sentence, not as an active
  estimate).
- F3's Cost Expectations line (`$0.10–$0.40`) is consistent with
  the tree's F3 row.
- F3's "Out of Scope" still has the freeze-boundary bullet.
- F3's "Stop Rules" + "Why this is a stub" still align with the
  revised Steps language.

# Response format

Begin with prose per-finding-resolution and a drift-sweep paragraph.
Then output:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{"severity": "...", "location": "...", "summary": "...", "fix": "..."}}
  ]
}}
```

If verdict is VERIFIED, `issues` must be `[]`.

# Round 2 review (for reference)

```markdown
{r2_review}
```
"""

print(f"Round 3 prompt size: {len(prompt)} chars", file=sys.stderr)

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

# PERSIST FIRST
out_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}-round3.md"
out_path.write_text(result.content, encoding="utf-8")

meta_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}-round3-meta.json"
meta_path.write_text(json.dumps({
    "model_name": result.model_name,
    "input_tokens": result.input_tokens,
    "output_tokens": result.output_tokens,
    "cost_usd": result.cost_usd,
    "task_type": "session-verification",
    "session_set": str(SESSION_SET),
    "session_number": SESSION_NUMBER,
    "round": 3,
}, indent=2), encoding="utf-8")

import re
safe_preview = re.sub(r'[^\x00-\x7f]', '?', result.content)
print(f"Verifier model: {result.model_name}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Cost USD: ${result.cost_usd:.4f}")
print(f"Output written to: {out_path}")
print("--- LAST 1500 CHARS (ASCII-safe) ---")
print(safe_preview[-1500:])

log = SessionLog(SESSION_SET)
log.log_step(
    session_number=SESSION_NUMBER,
    step_number=12,
    step_key="session-2/verify-round3",
    description="Cross-provider session-verification (Round 3) via gpt-5-4",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)
print("Step logged.")
