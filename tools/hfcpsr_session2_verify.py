"""Session 2 cross-provider session-verification call.

Per the 'Require A Trailing JSON Block In Session-Verification Prompts'
Convention plus the 'prose-first lead instruction' refinement, the
prompt:
  * leads with an explicit prose-first instruction,
  * lists in-scope and out-of-scope files explicitly,
  * embeds the spec's Session 2 plan + each modified file's content,
  * ends with the required-response JSON schema.
"""
import os
import sys
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
log = SessionLog(SESSION_SET)


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


# ── Spec excerpt for Session 2 ────────────────────────────────────────
spec_text = read(SESSION_SET / "spec.md")
spec_marker = "### Session 2 of 2"
spec_idx = spec_text.find(spec_marker)
if spec_idx == -1:
    raise SystemExit("Could not locate Session 2 block in spec.md")
spec_session2 = spec_text[spec_idx:]
# Trim at the next top-level marker after Session 2 (Cost Expectations
# section) so we send the focused excerpt, not the whole spec tail.
trim_idx = spec_session2.find("## Cost Expectations")
if trim_idx != -1:
    spec_session2 = spec_session2[:trim_idx].rstrip() + "\n"

# ── In-scope files ────────────────────────────────────────────────────
in_scope_files = [
    "docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md",
    "tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiff.cs",
    "tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportDiffTests.cs",
    "tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportTests.cs",
]
contents_block = []
for f in in_scope_files:
    contents_block.append(f"### `{f}`\n\n```\n{read(f)}\n```\n")
contents = "\n".join(contents_block)

# ── Related-but-out-of-scope files (disclose, don't include) ─────────
out_of_scope_note = """\
### Files present in the repo but NOT touched this session — DO NOT flag as missing

These files were committed in Session 1 and remain unchanged. Their contents
are authoritative for the construct definitions the narrative references and
for the live diff-gate path the sentinel test exercises. If the verifier
needs to confirm anything about them, the relevant excerpts are quoted in
the in-scope content above:

- `AccessHarvester.Pipeline/Enrichment/UnresolvedItem.cs` — defines
  `UnresolvedItem`, `UnresolvedSource` enum, and `UnresolvedLocation`.
- `AccessHarvester.Pipeline/Enrichment/UnresolvedItemAggregator.cs` — the
  closed `SuggestedNextStepsLookup` map the narrative pins as authoritative.
- `AccessHarvester.Vba.Resolver/References/ResolutionOutcome.cs` — `UnresolvedReasons`
  static class with the five resolver reason constants the narrative quotes.
- `AccessHarvester.Sql.Parser/FormReferences/UnresolvedReason.cs` — six stable
  SqlFormReference reason constants plus `MemberNotFoundAtSegment(int)` factory.
- `AccessHarvester.Sql.Parser/DynamicSql/UnanalyzableReason.cs` — three
  DynamicSqlSite reason constants.
- `tests/AccessHarvester.Pipeline.Tests/ParseSuccessReport/ParseSuccessReportModel.cs`
  + `ParseSuccessReportGenerator.cs` — Session 1 deliverables; the sentinel
  test does not exercise them.
- `samples/parse-success-reports/HomeHealthCare.accdb.parse-success-report.json`
  + `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`
  — committed interim goldens; the live diff-gate (ParseSuccessReportTests)
  asserts byte-equality against these and was confirmed green in this
  session before the new files were added.

### What the verifier may flag normally

- The narrative's claim that `member-not-found-at-segment-<N>` falls through
  to the GenericSuggestedNextSteps list (the closed lookup keys on stable
  reason codes only; the segment-suffixed code is dynamic — see the
  `DescribeFormReferenceReason` switch in `UnresolvedItemAggregator.cs`
  for the prefix match).
- Anything else relating to the four in-scope files, the spec's Session 2
  plan, or the diff-gate contract.
"""

# ── Prompt assembly ───────────────────────────────────────────────────
prompt = f"""\
You are the cross-provider session-verifier for Session 2 of the session set
`harvester-finish-canonical-parse-success-report` in the dabbler-access-harvester
repo. Your job is to review the Session 2 deliverables against the spec's
Session 2 plan and surface any Critical / Major / Minor issues the orchestrator
should fix before commit.

**Your response MUST begin with a detailed prose analysis covering each
section below BEFORE the JSON block at the end.** Do not skip the prose
analysis — the JSON block alone is not sufficient.

Analyse the following concerns in order:

1. **Spec adherence.** Does each Session 2 step listed in the spec correspond
   to a concrete deliverable in the in-scope files? (Reconfirm diff gate;
   per-reason narrative; sentinel diff-message test; build+test.) Any
   missing step?
2. **Narrative correctness.** Does `unresolved-reason-narrative.md` cover
   every reason code emitted by `UnresolvedItemAggregator.SuggestedNextStepsLookup`?
   Are the verbatim suggested-next-steps lists accurate copies of the
   aggregator's lookup values? Is the construct-description per reason
   consistent with the producer's contract?
3. **Sentinel test integrity.** Does the sentinel test (`ParseSuccessReportDiffTests`)
   actually exercise the production diff-message builder? The Session 2 plan
   requires it to test the message a human would see at gate-fail time — not
   a parallel reimplementation. Is the helper refactor (`ParseSuccessReportDiff`)
   byte-identical to the inline message Session 1 shipped?
4. **Diff-gate continuity.** Are there any behaviours of the live gate
   (`ParseSuccessReportTests`) that the refactor changed? The Session 2 plan
   explicitly inherits the diff contract unchanged.
5. **Determinism / convention compliance.** Reason codes must be stable
   strings, the narrative must not invent new reasons, and any code change
   must hold the lean-by-default principle.

---

## Spec Session 2 plan (excerpt from `spec.md`)

```
{spec_session2}
```

---

## In-scope files

{contents}

---

{out_of_scope_note}

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

# Persist the prompt before calling — Persist-Before-Display Convention.
out_dir = Path("docs/session-sets/harvester-finish-canonical-parse-success-report/session-reviews")
out_dir.mkdir(parents=True, exist_ok=True)
prompt_path = out_dir / "session-002-prompt.md"
prompt_path.write_text(prompt, encoding="utf-8")
print(f"Wrote prompt ({len(prompt)} chars) to {prompt_path}")

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set=str(SESSION_SET),
    session_number=2,
)

# Persist response before any display side effect — Persist-Before-Display.
review_path = out_dir / "session-002.md"
review_path.write_text(result.content, encoding="utf-8")
log.save_session_review(session_number=2, review_text=result.content, round_number=1)

# Log the verify step.
log.log_step(
    session_number=2,
    step_number=5,
    step_key="session-2/verify",
    description="Cross-provider session-verification routed (round 1)",
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
