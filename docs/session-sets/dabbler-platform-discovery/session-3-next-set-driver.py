"""Session 3 (last) — route the next-session-set recommendation."""

from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "dabbler-platform-discovery"
OUT = SESSION_SET / "next-session-set-recommendation.md"


PROMPT = """You are recommending the next session set to run in the
`dabbler-access-harvester` repo, after the `dabbler-platform-discovery`
session set just closed out.

# This session set's actuals

`dabbler-platform-discovery` (3 sessions, 2026-05-17, total routed cost ~$2.16):
  - Session 1 of 3: Inventory + Schema — landed Feature 1 (component
    inventory) and Feature 2 (RDF schema reference) under `docs/cross-repo/`.
    VERIFIED on round 2 after a fixup pass.
  - Session 2 of 3: Migration Map + PDF Boundary — landed Feature 3
    (access-to-platform migration map) and Feature 4 (dabbler-pdf
    boundary doc). VERIFIED on round 2 after a header-convention
    adjudication.
  - Session 3 of 3: Smoke-Test + Buffer — walked
    `frmOrderDetails`+`sfrmOrderLineItems` through the migration map,
    surfaced 6 gaps (3 Major, 3 Minor), amended the map with 8
    added/refined rows, VERIFIED round 1 with zero issues.

All four cross-repo deliverables are pinned to the `../dabbler-platform`
snapshot `aa77020e1ca0` from 2026-05-16.

This set was a cross-repo support workstream (read-only on the sibling
platform repo) — explicitly NOT part of the harvester's main DAG
(`docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`).
The set was authored ad-hoc on 2026-04-27 to give the planned third
(Access-to-Blazor migration) repo and the harvester's downstream AI
agents a structured snapshot of the platform's component library and
report.rdf.json schema.

# State of nearby session sets (read this before recommending)

The most-recently-completed sibling sets and their explicit
next-session-set recommendations:

  - `generalization-validation-on-non-northwind` (closed 2026-05-15)
    Verdict: `generalizes-cleanly` for the workflow-package template on
    HHC's `Add a Client`. Recommended next: `blazor-update-workflows`
    (Phase 3a per `strategy.md`: implement Update an Order / Update a
    Product / Update an Employee against the Blazor host established
    by `workflow-package-pilot` Phase 2). Open Validation #2 for HHC
    is resolved by this set, which removed the structural-risk
    argument for sequencing 3c before 3a.

  - `form-report-code-and-grouping` (closed mid-May 2026, prior to
    generalization-validation set). Form/report code+grouping
    extraction landed.

  - `blazor-add-an-order` (already complete, with change-log).

  - `blazor-update-workflows` (NOT started; only `spec.md` and a
    `not-started` session-state.json exist). This is the set
    `generalization-validation-on-non-northwind` recommended as next.

The main DAG (Phase 1-7 of the VBA-Parser Migration Plan) is largely
complete — 30 session sets have `change-log.md` files. Post-DAG work
has been about generalization (workflow packages, characterization
tests on non-Northwind DBs), Blazor host harness, and IR foundations
(`lowdefy-ir-foundation` recently closed).

# Two facts that constrain the recommendation

1. **This set produced no follow-up obligation.** The 6 smoke-test gaps
   were all closed in-session. The currency header is intact; no
   re-snapshot is owed unless the platform's component library changes
   meaningfully or migration planning begins in earnest. No checklist,
   no UAT, no deferred fixups.

2. **The natural next set was already named by the prior completion.**
   `generalization-validation-on-non-northwind` recommended
   `blazor-update-workflows` as next — and that set is sitting in
   not-started state with a spec ready. Picking the same next set
   keeps the project on the recommended trajectory.

# Your task

Recommend the next session set to run. Format the response as a
Markdown document with the following structure:

```
# Next Session Set Recommendation

> Authored 2026-05-17 by route(task_type="analysis") after dabbler-platform-discovery Session 3 close.

**Recommended next session set:** `<slug>`

**Rationale:** <2-4 sentences. Cite the prior set's recommendation if
you are accepting it, or explicitly justify a deviation.>

**Optional parallel work:** <If the recommended set's first session
does not consume the full window, name one or two low-risk
parallel-able items. Otherwise write "None" — do not invent work.>

**Plan-driven posture:** This repo has a master plan
(`docs/VBA-Parser-Migration-Harvester-Plan.md`) plus a generated DAG
(`docs/session-sets/vba-parser-session-set-generator/session-set-tree.md`).
Post-DAG generalization work is plan-driven via `strategy.md`-style
documents from the `workflow-package-pilot` lineage. State whether
your recommendation is consistent with that posture or is an explicit
deviation.

**Pre-flight check:** Before starting `<slug>`, the orchestrator
should verify: <2-3 specific things to confirm — e.g., specific files
exist, build is clean, no stray uncommitted work from this set>.
```

Keep it focused. The audience is the next session's orchestrator and the
human starting it. The headline is the slug; everything else is
context.
"""


def main() -> int:
    log = SessionLog(SESSION_SET)

    result = route(
        content=PROMPT,
        task_type="analysis",
        complexity_hint=40,
        session_set=str(SESSION_SET),
        session_number=3,
    )

    OUT.write_text(result.content, encoding="utf-8")

    log.log_step(
        session_number=3,
        step_number=5,
        step_key="session-3/next-session-set-recommendation",
        description="Route next-session-set recommendation (last session of set)",
        status="complete",
        api_calls=[
            {
                "model": result.model_name,
                "taskType": "analysis",
                "inputTokens": result.input_tokens,
                "outputTokens": result.output_tokens,
                "costUsd": result.cost_usd,
            }
        ],
    )

    print(f"WROTE {OUT}  model={result.model_name} in={result.input_tokens} "
          f"out={result.output_tokens} cost=${result.cost_usd:.4f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
