"""Round-3 verification: discloses round-2 fixes and the human's
spec-interpretation rulings so the verifier judges the current state
rather than re-finding the same issues.
"""
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/dabbler-platform-discovery")
OUT_DIR = Path("docs/cross-repo")

spec_text = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
ai_assignment = (SESSION_SET / "ai-assignment.md").read_text(encoding="utf-8")
inventory = (OUT_DIR / "dabbler-platform-components.md").read_text(encoding="utf-8")
schema = (OUT_DIR / "dabbler-platform-report-rdf-schema.md").read_text(encoding="utf-8")

PROMPT = f"""You are the independent cross-provider verifier for Session 1
of the `dabbler-platform-discovery` session set in the
`dabbler-access-harvester` repo. This is round 3.

Your response MUST begin with a detailed prose analysis covering each
of the concerns below BEFORE the trailing JSON block. Be specific about
file paths and section names when you identify issues.

# Background — what changed since round 2

Round 1 returned ISSUES_FOUND (4 Major + 1 Minor). Round 2 returned
ISSUES_FOUND (2 Major + 1 Minor). Round 3 is verifying after:

1. **Round 2 Major #1 (inventory grounding):** The human ruled that
   per-component file-path citations satisfy the spec's literal text
   ("each public component's name, file path, purpose, required
   parameters, optional parameters, common usage contexts"). The
   per-component file path is the citation. The verifier prompt's
   stricter reading ("citations are required for every claim") was an
   over-reading of the spec — the spec does not require per-claim
   citations beyond the file path. **For this round, judge the
   inventory against the spec text, not against the round-2 verifier
   prompt's over-reading.**

2. **Round 2 Major #2 (schema doc):** The schema doc was directly
   edited to fix the concrete gaps the verifier identified:
     - Styles section: added `borderTop`, `borderBottom`, `borderLeft`,
       `borderRight` rows alongside `border` shorthand (matches
       `StyleDefinition.cs` lines 42-55).
     - Parameters section: added a "Nested object: validation"
       subsection documenting `ParameterValidation` with `minValue`,
       `maxValue`, `pattern` (matches `ParameterDefinition.cs` line 73).
     - Elements section (was already in round-2 output) covers all
       type-specific fields from `ElementDefinition.cs` including
       `imageSource`, `imageScaleMode`, `subreportPath`,
       `subreportParameters`, `subreportDataSourceName`,
       `lineDirection`, `canGrow`, `canShrink`, `runningTotal`,
       `hideDuplicates`, `formatString`.

3. **Round 2 Minor (inventory structure):** The human ruled that the
   current category-with-inline-per-component-entries structure is a
   reasonable read of the spec's "single-screen-per-category index
   with deeper per-component sections" phrasing and accepts the doc
   as-is on this point. **Do NOT re-flag the inventory's structural
   shape in round 3.**

# Files you should evaluate

## 1. Session-set spec (the contract)

---SPEC START---
{spec_text}
---SPEC END---

## 2. ai-assignment.md (Session 1 has actuals annotation per Issue 5 fix)

---AI ASSIGNMENT START---
{ai_assignment}
---AI ASSIGNMENT END---

## 3. Feature 1 deliverable

---INVENTORY START---
{inventory}
---INVENTORY END---

## 4. Feature 2 deliverable

---SCHEMA DOC START---
{schema}
---SCHEMA DOC END---

# Out-of-scope disclosures

Per the workflow Convention "Send Related Files To The Cross-Provider
Verifier Or Disclaim What Is Out Of Scope":

- Harvester source code under `AccessHarvester.*` is untouched by this
  session.
- `docs/planning/*`, `docs/ai-led-session-workflow.md` are workflow
  assets, not touched.
- The sibling repos (`../dabbler-platform`, `../dabbler-pdf`) are
  read-only for this set — citations into them are intentional. Treat
  any complaint about "the platform repo should also be updated" as
  out of scope.
- `session-1-driver.py`, `session-1-fixup.py`, `verify-round-3.py`,
  `third-opinion.py`, and `third-opinion-issue-4.md` in the session-set
  folder are orchestration artifacts, not Feature deliverables.
- Sessions 2 and 3 deliverables (migration map, PDF boundary, smoke
  test, change log) are deferred per the spec.

# What to check (round 3 scope)

1. **Schema doc field-completeness for the round-2 specific gaps.** Is
   the Styles table now field-complete against `StyleDefinition.cs`
   (Border, BorderTop, BorderBottom, BorderLeft, BorderRight, plus
   the other style fields)? Is the new "Nested object: validation"
   subsection accurate and complete? Are the Elements type-specific
   fields documented?

2. **Schema doc internal consistency.** Every field shown in a synthetic
   example must appear in the corresponding property table. Are there
   any remaining example-only fields not in the tables?

3. **Inventory spec compliance ON ITS OWN TERMS.** The spec asks for
   each public component's name, file path, purpose, required
   parameters, optional parameters, common usage contexts, and a
   stability assessment. Does the inventory provide those for the
   components it covers? (Do NOT re-flag the structure or per-claim
   citation issues from round 2 — those are settled.)

4. **Boundary discipline.** No copying of platform source code. The
   schema doc's JSON snippets are synthetic minimal examples with
   "See also" pointers (verified in round 2).

5. **Currency headers.** Both docs carry the snapshot-commit-date +
   regeneration-trigger block at the top.

6. **ai-assignment.md.** Session 1's "Actuals" section now annotates
   the human-override-to-Opus situation. Does the structure satisfy
   the Step 3.5 schema?

# Required response format

After your prose analysis, output a JSON block in this EXACT format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or section name>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""

result = route(
    content=PROMPT,
    task_type="session-verification",
    complexity_hint=70,
    session_set=str(SESSION_SET),
    session_number=1,
)

log = SessionLog(SESSION_SET)
log.save_session_review(session_number=1, review_text=result.content, round_number=3)
log.log_step(
    session_number=1,
    step_number=11,
    step_key="session-1/verify-round-3",
    description="Cross-provider session verification round 3 (after direct gap fixes + human spec-interpretation rulings)",
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)
print(f"Round 3 saved.  ({result.model_name}, ${result.cost_usd:.4f})")
print("--- first 600 chars ---")
print(result.content[:600])
print("\n--- last 800 chars ---")
print(result.content[-800:])
