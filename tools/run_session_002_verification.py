"""One-shot Session 2 cross-provider verification.

Persist-then-display per the Lessons Learned convention.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog


SESSION_SET = Path("docs/session-sets/generalization-validation-on-non-northwind")


def read_file(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def build_prompt() -> str:
    spec_excerpt = read_file(str(SESSION_SET / "spec.md"))
    package = read_file(str(SESSION_SET / "packages" / "add-a-client.md"))
    findings = read_file(str(SESSION_SET / "generalization-findings.md"))
    change_log = read_file(str(SESSION_SET / "change-log.md"))
    next_rec = read_file(str(SESSION_SET / "next-session-set-recommendation.md"))
    s2_tests = read_file(
        "tests/AccessHarvester.CharacterizationTests/AddAClient/Session2FalsificationTests.cs"
    )
    session_1_review = read_file(str(SESSION_SET / "session-reviews" / "session-001.md"))

    prompt = f"""You are performing the end-of-session cross-provider verification for
Session 2 of the `generalization-validation-on-non-northwind` session set.

Your response MUST begin with a detailed prose analysis covering each numbered
concern below, BEFORE the trailing JSON block. Do not return a JSON-only or
bare-verdict response.

# Context

Session 1 (Codex GPT-5) authored an inventory, the seven-section workflow package
`Add a Client` for HomeHealthCare, three characterization-test files, and was
verified VERIFIED by gemini-pro (the Session 1 review is included below).

Session 2 (this work, Claude Opus 4.7) performed an independent peer review of
Session 1's package and tests, added falsification tests for missing invariants,
refined the package, and authored the headline `generalization-findings.md`
with verdict `generalizes-cleanly` plus the closeout artifacts (change-log,
next-session-set recommendation, disposition.json).

# What I want you to verify

1. **Generalization-findings verdict integrity.** Is the verdict
   `generalizes-cleanly` well-supported by the rest of the document? Are the
   section-by-section fit claims consistent with the package content? Are the
   pattern-divergence claims accurate against the listbox-cascade / OpenArgs /
   no-data-macro / mixed-DAO+ADODB / Hungarian-naming patterns the spec calls
   out? Flag any over-claim or under-claim.

2. **Falsification-test quality.** Do the nine tests in
   Session2FalsificationTests.cs actually pin invariants that Session 1 missed?
   Are they anti-coupled to UI mechanics (the package's evidence + anti-coupling
   rule)? Are they genuinely falsifiable, or are they stub-tautology tests that
   only verify their own stub implementations?

3. **Package refinements.** Do the new business rules added to Section 3
   (phone normalization, state-city auto-fill, deactivation-confirm-rollback),
   the five new invariant entries in Section 4, and the two new ambiguity-log
   entries in Section 7 trace cleanly to the source evidence? The package
   confidence tags should be `confirmed` for source-grounded rules and
   `inferred` for anything that requires reading a procedure body we haven't
   read (e.g., the exact FormatPhone mask).

4. **Recommended-next-move alignment.** The next-session-set recommendation
   claims this is a plan-driven repo with the recommendation aligning to
   strategy.md Phase 3a, no deviations. Is that consistent with what the
   strategy doc actually says? (The strategy doc is part of the repo but not
   included in this prompt — flag if you'd need to read it; otherwise treat
   the no-deviation claim as a non-verified-by-you assertion.)

5. **Spec compliance.** Does Session 2 deliver everything the spec's
   Session 2 plan named? See the spec (included below) — its Session 2 plan
   lists package-review, test-review, falsification-tests, package-refinement,
   generalization-findings, change-log, next-session-set-recommendation,
   disposition, build/test, verification, and Step 9 reorganization. Step 9
   happens AFTER you (the verifier) return, so its absence here is correct.

6. **Stop rules check.** The spec defines five stop rules. None should have
   fired. Confirm.

# Files in scope

## spec.md (session set spec)

{spec_excerpt}

## packages/add-a-client.md (post-Session-2 refinement)

{package}

## generalization-findings.md (headline deliverable)

{findings}

## Session2FalsificationTests.cs (new tests)

```csharp
{s2_tests}
```

## change-log.md

{change_log}

## next-session-set-recommendation.md

{next_rec}

## Session 1's verification review (for context — already VERIFIED)

{session_1_review}

# Files OUT of scope but referenced

These exist in the repo but are not included here. Do NOT flag them as
"missing" — they live in the repo and Session 1 already verified them. They
are noted so you can interpret references in the package and findings:

- `tests/AccessHarvester.CharacterizationTests/AddAClient/AddAClientWorkflowModels.cs`
  (the `ClientDraft` DTO; shared between Session 1 and Session 2 tests).
- `tests/AccessHarvester.CharacterizationTests/AddAClient/ClientCreationBehaviorTests.cs`,
  `ClientValidationBehaviorTests.cs`, `ClientLifecyclePolicyTests.cs`
  (Session 1's three test files; verified by gemini-pro Round 1).
- `docs/session-sets/generalization-validation-on-non-northwind/inventory.md`
  (Session 1's inventory; verified by gemini-pro Round 1).
- `docs/planning/strategy.md` (the strategy doc with Phase 3a-3g candidates;
  the next-session-set recommendation aligns with it).
- `docs/session-sets/workflow-package-pilot/packages/add-an-order.md`
  (the Northwind baseline package the HHC package is compared against).
- `docs/homehealthcare-northwind-comparison.md` (the comparison doc).
- `samples/DatabaseYaml/HomeHealthCare.accdb/` (the YAML mirror used as the
  source-of-truth reading surface for the peer review).

# Build and test status

`dotnet build` succeeded (0 errors, 323 warnings — all pre-existing
platform-attribute and xunit-analyzer warnings).

`dotnet test --no-build` passed:
- AccessHarvester.CharacterizationTests: 131 passed (was 122 in Session 1 + 9
  new Session 2 falsification tests).
- AccessHarvester.Tests: 436 passed, 2 skipped (Access-COM gated).
- All other test projects: green.
- Total: 1848 passed, 2 skipped, 0 failed across the solution.

# Required response shape

Begin with a prose analysis for each concern (1) through (6) above. After your
prose analysis, emit a single trailing JSON block in this exact format:

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
    print(f"Prompt size: {len(prompt)} chars ({len(prompt) // 4} approx tokens)")

    # Route cross-provider. The router-config.yaml pins session-verification to
    # gpt-5-4 for the Claude orchestrator, which is correct here.
    result = route(
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=2,
    )

    # PERSIST FIRST, print second (Lessons-Learned convention).
    output_path = SESSION_SET / "session-reviews" / "session-002.md"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(result.content, encoding="utf-8")

    # Also save via SessionLog for the canonical save path.
    log = SessionLog(SESSION_SET)
    log.save_session_review(
        session_number=2,
        review_text=result.content,
        round_number=1,
    )

    # Log the verification step.
    log.log_step(
        session_number=2,
        step_number=12,
        step_key="session-2/verification",
        description=(
            f"Cross-provider session-verification routed via "
            f"route(task_type='session-verification', complexity_hint=70). "
            f"Model: {result.model_name}. Saved raw output to "
            f"session-reviews/session-002.md."
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
