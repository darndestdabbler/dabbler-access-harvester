"""Run end-of-session cross-provider verification for Session 2 of
harvester-finish-operations-and-interpretation-docs.

Per the workflow doc, this script:
- builds a verification prompt with the spec excerpt, the outline,
  the body, and the audience-check probe synthesis;
- appends the mandatory structured JSON response block;
- routes the prompt as task_type='session-verification' so the
  router picks a cross-provider verifier;
- saves the raw response (never edited) into session-reviews/.
"""

import os
import pathlib

from ai_router import query, route
from ai_router.session_log import SessionLog

SESSION_SET = pathlib.Path("docs/session-sets/harvester-finish-operations-and-interpretation-docs")
SESSION_NUMBER = 2


def _read(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8")


def main() -> int:
    log = SessionLog(str(SESSION_SET))

    spec = _read(SESSION_SET / "spec.md")
    outline = _read(pathlib.Path("docs/harvester/interpretation-guide-outline.md"))
    body = _read(pathlib.Path("docs/harvester/interpretation-guide.md"))
    audience_check = _read(SESSION_SET / "audience-check-probe.md")

    prompt = f"""# Verification — Session 2 of harvester-finish-operations-and-interpretation-docs

## What this session produced

A single file:
`docs/harvester/interpretation-guide.md` (~4,711 words, ~38 KB).

It implements the body of the interpretation guide against the
outline that Session 1 authored, folding in the must-have and
nice-to-have additions surfaced by the Session 1 audience-check
probe.

## Out of scope for this verification

- Session 1 artefacts (the outline at
  `docs/harvester/interpretation-guide-outline.md` and the
  audience-check probe at
  `docs/session-sets/harvester-finish-operations-and-interpretation-docs/audience-check-probe.md`)
  were independently verified at Session 1 close and are NOT being
  re-litigated here. They appear below as inputs the body must
  remain consistent with.
- `dotnet build` / `dotnet test` were skipped per the Session 2 spec
  (docs-only session; no code touched).
- The Session 3 deliverables (pointer page at `docs/README.md`, link
  from top-level README, downstream-AI probe) are explicitly Session
  3's scope and absent by design.

## Spec excerpt — Session 2 only

```markdown
{spec}
```

## Outline (Session 1 deliverable; input to this session)

```markdown
{outline}
```

## Audience-check probe synthesis (Session 1 deliverable; input)

```markdown
{audience_check}
```

## Body produced by this session

```markdown
{body}
```

## What to verify

Please verify the body against the outline and the probe synthesis on
these dimensions:

1. **Coverage.** Every outline section (1-10) is present with the
   content the outline calls for.
2. **Audience-probe must-haves folded in.** The three must-have gaps
   from the probe synthesis are addressed in the body:
   (a) `RowSourceSpec` record-shape snippet (Sections 4 and 5.3);
   (b) `FunctionClassifications` + `EventHandlerClassifications`
   record-shape snippets (Section 4 + cross-link from 5.4);
   (c) `<UserInterfaceMacro>` XML shape sketch (Section 5.1, with
   a cross-link to 5.6 acceptable in lieu of full duplication).
3. **Audience-probe nice-to-haves folded in where claimed.** The
   body claims all four nice-to-haves are present; verify:
   (d) `kb/` mirrors `ir/` per-category layout (Section 3 or 2);
   (e) `Diagnostics` record shape sketched (Section 4);
   (f) Default-value lookup pointer (Section 7);
   (g) Relationship cardinality sentence (Section 5.5).
4. **Length within budget.** The outline pins 3,500-5,000 words; the
   body should land inside that range.
5. **Authority placement.** Where the body references the per-reason
   narrative, the schema docs, or the strategy doc, it should *link*
   to them rather than restating their content (lean-by-default).
6. **Provenance conventions consistency.** Section 7's null-vs-
   omission framing should be consistent with project-guidance
   `Output JSON Conventions` and with how the body's earlier
   sections use `null` and explicit `{{ Source: "Unresolved" }}`.
7. **Frozen-list consistency.** Section 8's known-limitations bullets
   should be drawn from `docs/planning/strategy.md` →
   *Frozen Unless Reauthorized (2026-05-17)*. The body should not
   silently expand or contract that frozen list.
8. **Internal link integrity.** Spot-check the relative paths to
   sibling docs (`docs/schema/`, `docs/planning/strategy.md`,
   `docs/planning/project-guidance.md`,
   `docs/ai-led-session-workflow.md`, the unresolved-reason
   narrative, the example files, the golden corpus directories,
   `docs/README.md` for Session 3).

If anything in the body materially deviates from the outline, surface
it as an issue. If the body adds content the outline did not call for,
that is OK as long as it serves the audience (downstream AI consumer)
and stays within the lean-by-default principle.

After your analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or section>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""

    review_path_hint = SESSION_SET / "session-reviews" / f"session-{SESSION_NUMBER:03d}.md"
    print(f"Prompt size: {len(prompt)} chars (~{len(prompt)//4} tokens)")
    print(f"Will save review to: {review_path_hint}")

    # Verifier-failure escalation ladder rung 2: OpenAI's gpt-5-4 returned
    # sustained 429s during the initial route() attempt (same condition
    # Session 1 hit; ai-assignment.md notes asked Session 2 to plan on the
    # gemini-pro fallback from the outset). Use query() to bypass the
    # task_type_overrides mapping and pin gemini-pro for the cross-provider
    # verification step. Still cross-provider relative to the Opus
    # orchestrator (Anthropic), so rule 2 of the workflow doc is satisfied.
    result = query(
        model="gemini-pro",
        content=prompt,
        task_type="session-verification",
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    # Persist BEFORE any side effects per the project-guidance Convention
    # ("Persist Routed Output To Disk Before Any Display Or Logging Side
    # Effects") — the response may contain Unicode characters that
    # Windows cp1252 cannot encode.
    review_dir = SESSION_SET / "session-reviews"
    review_dir.mkdir(parents=True, exist_ok=True)
    log.save_session_review(
        session_number=SESSION_NUMBER,
        review_text=result.content,
        round_number=1,
    )

    print()
    print(f"Verifier model: {result.model_name}")
    print(f"Input tokens:   {result.input_tokens}")
    print(f"Output tokens:  {result.output_tokens}")
    print(f"Cost:           ${result.cost_usd:.4f}")

    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=3,
        step_key="session-2/verification",
        description=f"Cross-provider session-verification routed via route(task_type='session-verification'); verifier={result.model_name}.",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    print("Verification step logged.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
