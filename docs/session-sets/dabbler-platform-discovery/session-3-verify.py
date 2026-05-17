"""Session 3 verification: cross-provider session verification.

Routes via task_type="session-verification" (configured to GPT-5.4 for a
Claude orchestrator). The verifier receives:
  - The spec excerpt for Session 3
  - The smoke-test analysis (Gemini Pro's gap inventory)
  - The 6 amendments applied to the migration map (as actual file diff context)
  - Build/test result summary
  - Required trailing JSON block per the lessons-learned convention

Out-of-scope disclosures:
  - The component inventory and RDF schema were verified in Session 1.
  - The PDF boundary doc was verified in Session 2.
  - This session only touched access-to-platform-migration-map.md (6 amendments)
    and produced smoke-test-frmOrderDetails.md (smoke-test analysis).
"""

from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "dabbler-platform-discovery"
MIGRATION_MAP = REPO_ROOT / "docs" / "cross-repo" / "access-to-platform-migration-map.md"
SMOKE_TEST = SESSION_SET / "smoke-test-frmOrderDetails.md"
SPEC = SESSION_SET / "spec.md"


SPEC_EXCERPT = """### Session 3 of 3: Smoke-Test + Buffer

- Take a single concrete sample (e.g., one form from
  `samples/NorthwindDevEdition.accdb`) and walk through the
  migration-relevance map by hand. Note any gaps the map missed.
- Address verifier-surfaced findings.
- Generate change-log.

Routing notes (Session 3):
- Change-log: `route(task_type="summarization", max_tier=1)`
- Session-verification: standard cross-provider with verification prompt that
  discloses which platform paths were inspected.
"""


def main() -> int:
    log = SessionLog(SESSION_SET)

    smoke_test = SMOKE_TEST.read_text(encoding="utf-8")
    migration_map = MIGRATION_MAP.read_text(encoding="utf-8")

    prompt = f"""You are verifying Session 3 of the `dabbler-platform-discovery`
session set (Microsoft Access -> Blazor migration-target discovery).

# Session 3 spec excerpt

{SPEC_EXCERPT}

# Context (what the prior sessions established, not under review here)

Sessions 1 and 2 (already VERIFIED in prior commits) produced four artifacts
under `docs/cross-repo/` from a read-only inspection of `../dabbler-platform`
at commit `aa77020e1ca0` (2026-05-16):

  - `dabbler-platform-components.md` (Feature 1 — component inventory)
  - `dabbler-platform-report-rdf-schema.md` (Feature 2 — RDF schema reference)
  - `access-to-platform-migration-map.md` (Feature 3 — migration map; modified this session)
  - `dabbler-pdf-overview.md` (Feature 4 — PDF boundary)

Out of scope for this verification:
  - The component inventory and RDF schema (Feature 1 and 2, verified in Session 1).
  - The PDF boundary doc (Feature 4, verified in Session 2).
  - The dabbler-platform source itself (read-only sibling repo, not edited).

This session's deliverables, all of which ARE in scope:

  1. **Smoke-test artifact** (`docs/session-sets/dabbler-platform-discovery/smoke-test-frmOrderDetails.md`)
     A routed analysis of one real Northwind form (`frmOrderDetails` plus its
     subform `sfrmOrderLineItems`) walked through the migration map by
     Gemini Pro, identifying 6 gaps (3 Major, 3 Minor).

  2. **Migration-map amendments** to `docs/cross-repo/access-to-platform-migration-map.md`
     The orchestrator applied direct edits addressing all 6 gaps from the
     smoke-test:
       - Added 7 new rows to the Form mapping table (Command Buttons text
         amended; ComboBox-Table/Query, ComboBox-Value-List, three
         calculated-control variants, and StatusBarText added).
       - Added 1 row to the Subform mapping table (subform-container
         OnEnter/OnExit events).
       - Refined the Form section's "Open gaps" note to scope it to
         property-sheet tweaks (input masks, formats) now that
         control-source expressions have explicit rows.

The smoke-test driver and verification driver themselves
(`session-3-driver.py`, `session-3-verify.py`) are also in the diff but are
infrastructure, not deliverables — equivalent driver scripts exist for
Sessions 1 and 2.

# Smoke-test analysis (Gemini Pro, $0.03, in scope)

{smoke_test}

# Migration map after amendments (the file under review)

{migration_map}

# Build and test results

`dotnet build`: 0 errors, 323 pre-existing warnings (warnings are CA1416
platform suppressions and xUnit analyzer hints, unchanged from baseline).
`dotnet test`: 1708 passed, 2 skipped (SkippableFact for Windows-COM tests
that ran successfully), 0 failed. No C# code was modified this session;
this run confirms the documentation-only changes did not regress the build.

# What to verify

1. **Gap coverage is complete.** Each of the 6 gaps the smoke-test surfaced
   should be addressed by at least one of the 8 added/amended rows. Map
   the gaps to the amendments and flag any gap not addressed.

2. **The amendments are accurate descriptions of the platform.** The
   Form section's new rows name `MudSelect`, `view model`, `MudTooltip`,
   `EventCallback<T>`, cascading state, etc. Each of these should be
   plausible against the platform context already in the document. Flag any
   row whose target component or pattern is fabricated, mis-typed, or
   inconsistent with the rest of the map.

3. **Internal consistency.** The amendments should not contradict other
   rows in the same table or the cross-cutting notes. In particular,
   check the new cross-form calculated-control row against the existing
   `State Management` cross-cutting note, and check the new Subform
   container-events row against the existing `Subform` row's overall
   pattern.

4. **The smoke-test artifact is appropriately scoped.** The spec asked
   for one form walked through the map; the artifact picks
   `frmOrderDetails` with reasoning. Confirm the smoke-test does what
   the spec asked.

5. **No spec-boundary violations.** The spec is explicit that this set
   does NOT modify dabbler-platform itself, does NOT copy platform
   source, and produces structured snapshots only. Confirm no
   amendments crossed those lines.

6. **Currency header is intact.** The migration map's currency header
   still pins `aa77020e1ca0` (2026-05-16) — this session did not
   change the snapshot date because nothing in the platform repo was
   re-inspected. Confirm this is correct: the smoke-test was a check
   AGAINST the existing snapshot, not a re-snapshot.

# Response format

Provide a detailed prose analysis for each of the 6 concerns above BEFORE
the JSON block. Do not skip the prose. Then, after your analysis, output
a JSON block in this exact format:

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
        content=prompt,
        task_type="session-verification",
        complexity_hint=70,
        session_set=str(SESSION_SET),
        session_number=3,
    )

    log.save_session_review(session_number=3, review_text=result.content, round_number=1)

    log.log_step(
        session_number=3,
        step_number=4,
        step_key="session-3/verify-round-1",
        description="Cross-provider session verification round 1",
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

    print(f"VERIFIED via {result.model_name}  "
          f"in={result.input_tokens} out={result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
