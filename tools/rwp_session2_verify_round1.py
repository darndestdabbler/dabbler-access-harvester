"""
Cross-provider session-verification for `remaining-work-planner` Session 2.
Per workflow Step 6: routes to a different provider (gpt-5-4 cross-provider
to the Claude orchestrator); persist routed output to disk BEFORE any
display side effect per the project-guidance Convention.
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

# Inputs (gap-audit + planner spec) -------------------------------------
gap_audit = read("docs/session-sets/remaining-work-planner/gap-audit.md")
planner_spec = read("docs/session-sets/remaining-work-planner/spec.md")
session1_disposition = read("docs/session-sets/remaining-work-planner/disposition.json")

# Deliverables ----------------------------------------------------------
stubs = {
    "F1 harvester-finish-canonical-parse-success-report": read(
        "docs/session-sets/harvester-finish-canonical-parse-success-report/spec.md"
    ),
    "F2 harvester-finish-canonical-golden-output": read(
        "docs/session-sets/harvester-finish-canonical-golden-output/spec.md"
    ),
    "F3 harvester-finish-cli-e2e-test-audit": read(
        "docs/session-sets/harvester-finish-cli-e2e-test-audit/spec.md"
    ),
    "F4 harvester-finish-operations-and-interpretation-docs": read(
        "docs/session-sets/harvester-finish-operations-and-interpretation-docs/spec.md"
    ),
    "F5 harvester-finish-cli-packaging": read(
        "docs/session-sets/harvester-finish-cli-packaging/spec.md"
    ),
}
tree = read("docs/session-sets/remaining-work-planner/remaining-work-tree.md")

# Verification prompt ----------------------------------------------------
prompt = f"""You are a cross-provider verifier for `remaining-work-planner`
Session 2 of 3. This session is a META-PLANNING set — its deliverables are
session-set stub specs and an index, not code changes. The harvester
codebase was not modified.

# Your task

Verify that Session 2's deliverables (five stub specs + index) correctly
close every in-scope GAP from Session 1's gap-audit. Specifically check:

1. **Gap coverage.** Every in-scope GAP from gap-audit.md (#1, #3, #4,
   #5, #6, #7) has a corresponding stub spec OR a documented fold-in.
   (GAP #2 was COVERED in the audit and explicitly does not need a
   new set.)

2. **Stub-spec contract discipline.** Each stub fixes acceptance
   criteria but does NOT prescribe implementation details (per the
   Session-1-vs-Session-2 boundary discipline in
   `remaining-work-planner/ai-assignment.md` Session 1 actuals, calibration
   note #2: "anti-coupling discipline for Session 2 stub-spec authoring.
   R1's Minor #3 (packaging-implementation prescription) is a
   Session-1-vs-Session-2 boundary reminder: gap audit recommends scope
   and acceptance criteria; stub-spec authoring picks the implementation
   shape.").

3. **Configuration block.** Each stub has the Session Set Configuration
   block with `requiresUAT: false, requiresE2E: false, uatScope: none`
   plus a rationale.

4. **Authority pointers.** Each stub cites gap-audit.md GAP #N, the
   strategy.md Phase 4 criterion, and (for #1 + #7) the resolved Open
   Questions from the post-close resolutions block.

5. **Prereq chains.** The tree's per-set Prereqs column and the
   per-feature prereq table are consistent. No feature inside a set has
   a materially different prereq depth than its siblings (per
   `lessons-learned.md` → "Split Session Sets Whose Features Span
   Independent Prereq Chains").

6. **GAP #5 fold-in rationale.** F2 (canonical-golden-output) absorbs
   GAP #5 (UnresolvedItems wiring) because both require the same
   insertion point in `Pipeline.Run()`'s emission stage. Confirm this
   rationale is documented in F2's spec AND in remaining-work-tree.md.

7. **No phantom amendments / routing changes.** The planner spec
   conditionally produces `tree-amendments.md` (only if amendments) and
   `routing-playbook-delta.md` (only if new task categories). Both were
   evaluated and skipped — the decision should be reflected in the tree's
   "Out-of-tree items" section. Confirm the skips are justified.

8. **Stop rules.** Each stub has stop-rule entries appropriate to its
   risk surface.

9. **No scope drift outside Phase 4.** Each stub's "Out of scope"
   section explicitly bars items the 2026-05-17 strategic review froze
   (Blazor, IR-codegen, platform-integration, etc.).

10. **Cross-set citations are accurate.** Each stub cites the prior
    set's coverage-audit Gap A finding where relevant (specifically F2,
    which cites `integration-testing-and-acceptance/coverage-audit.md`
    Gap A as evidence the manifest aggregator does not yet exist).

# Out-of-scope for verification

These files were NOT changed in Session 2 and need not be re-verified:

- The harvester codebase itself (all C# projects).
- The 30+ existing session-set spec.md and change-log.md files.
- `docs/planning/strategy.md`, `project-guidance.md`,
  `lessons-learned.md`, `session-set-authoring-guide.md`,
  `ai-led-session-workflow.md` (read for context but not changed).
- Session 1's `gap-audit.md` and `disposition.json` (input only).
- `examples/*.json`, `samples/*.accdb`, etc.

The verifier sees the prompt's inputs only. If something is missing
that you would want to check, flag it as a request rather than as a
defect.

# Inputs (Session 1 outputs — read for context)

## Planner spec excerpt — Session 2 plan only

(Full spec is in this repo at docs/session-sets/remaining-work-planner/spec.md;
the Session 2 plan portion follows below.)

```
{planner_spec[planner_spec.find('### Session 2 of 3'):planner_spec.find('### Session 3 of 3')]}
```

## Session 1 disposition (resolved open questions)

```json
{session1_disposition}
```

## Gap audit (input authority for Session 2)

```markdown
{gap_audit}
```

# Deliverables (verify these)

## Stub 1 — F1 harvester-finish-canonical-parse-success-report

```markdown
{stubs["F1 harvester-finish-canonical-parse-success-report"]}
```

## Stub 2 — F2 harvester-finish-canonical-golden-output

```markdown
{stubs["F2 harvester-finish-canonical-golden-output"]}
```

## Stub 3 — F3 harvester-finish-cli-e2e-test-audit

```markdown
{stubs["F3 harvester-finish-cli-e2e-test-audit"]}
```

## Stub 4 — F4 harvester-finish-operations-and-interpretation-docs

```markdown
{stubs["F4 harvester-finish-operations-and-interpretation-docs"]}
```

## Stub 5 — F5 harvester-finish-cli-packaging

```markdown
{stubs["F5 harvester-finish-cli-packaging"]}
```

## remaining-work-tree.md (index)

```markdown
{tree}
```

# Response format

Your response MUST begin with a detailed prose analysis covering each
of the ten verification criteria above BEFORE the JSON block. For each
criterion, state the evidence you found and the verdict (per-criterion
PASS / ISSUES). Do not skip to the JSON.

After your analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or stub name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not
omit this block.
"""

print(f"Verification prompt size: {len(prompt)} chars", file=sys.stderr)

result = route(
    content=prompt,
    task_type="session-verification",
    complexity_hint=70,
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

# PERSIST FIRST, THEN PRINT (per lessons-learned)
out_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}.md"
out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(result.content, encoding="utf-8")

meta_path = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}-round1-meta.json"
meta_path.write_text(json.dumps({
    "model_name": result.model_name,
    "input_tokens": result.input_tokens,
    "output_tokens": result.output_tokens,
    "cost_usd": result.cost_usd,
    "task_type": "session-verification",
    "session_set": str(SESSION_SET),
    "session_number": SESSION_NUMBER,
    "round": 1,
}, indent=2), encoding="utf-8")

# Now safe to display
print(f"Verifier model: {result.model_name}")
print(f"Input tokens: {result.input_tokens}")
print(f"Output tokens: {result.output_tokens}")
print(f"Cost USD: ${result.cost_usd:.4f}")
print(f"Output written to: {out_path}")
print("---FIRST 800 CHARS OF OUTPUT---")
print(result.content[:800])
print("---LAST 600 CHARS OF OUTPUT---")
print(result.content[-600:])

# Log step
log = SessionLog(SESSION_SET)
log.log_step(
    session_number=SESSION_NUMBER,
    step_number=10,
    step_key="session-2/verify",
    description="Cross-provider session-verification (Round 1) via gpt-5-4",
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
