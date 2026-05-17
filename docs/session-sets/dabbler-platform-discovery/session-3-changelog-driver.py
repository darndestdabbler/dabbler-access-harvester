"""Generate change-log.md for the dabbler-platform-discovery session set."""

from __future__ import annotations

import sys
from pathlib import Path

from ai_router import route
from ai_router.session_log import SessionLog

REPO_ROOT = Path(__file__).resolve().parents[3]
SESSION_SET = REPO_ROOT / "docs" / "session-sets" / "dabbler-platform-discovery"
OUT = SESSION_SET / "change-log.md"


PROMPT = """Author a `change-log.md` for the `dabbler-platform-discovery`
session set. This is a narrative prose document that records what the set
delivered, the verification posture, the routed cost, and where its
outputs live. It is committed alongside the final session's code.

# Set summary

`dabbler-platform-discovery` ran 3 sessions on 2026-05-17, all orchestrated
by Claude Opus 4.7 at effort=high (the spec's original routed
recommendation was Sonnet 4.6 at medium across all three sessions; the
human picked Opus to keep the orchestrator consistent across the set).
Total routed cost was ~$2.16 across 23 calls.

Goal: produce a structured, machine-readable snapshot of the sibling
`../dabbler-platform` repo so downstream AI agents (in this repo and in
the planned Access-to-Blazor migration repo) have an authoritative
reference for migration targets. Read-only on the sibling repo; no
platform source code copied here; documentation only.

# Deliverables (committed under docs/cross-repo/, all pinned to platform commit aa77020e1ca0 from 2026-05-16)

  - `dabbler-platform-components.md` — categorized component
    inventory (form controls, layout, data-display, navigation, modal,
    security, reports) with per-component name + file path + purpose
    + required/optional parameters + common usage contexts. Marks
    stable contracts vs. deprecated compat-shell surface.
  - `dabbler-platform-report-rdf-schema.md` — full schema reference
    for `report.rdf.json`: every section (dataSources, parameters,
    grouping, sorting, bands, elements, expressions, styles) with
    purpose, field-by-field shape, and synthetic minimal examples
    (no copying of sample .rdf.json files from the platform repo —
    that decision came out of a Session-1 verifier adjudication via
    an Opus tiebreaker, verdict C).
  - `access-to-platform-migration-map.md` — cross-reference from
    each major Access construct kind (Form, Subform, Report,
    Subreport, Query, Macro, VBA event handler) to its natural
    dabbler-platform migration target. Includes a
    "platform-extension candidates" list flagging Access features
    with no clear platform counterpart. Session 3 amended the Form
    and Subform tables to close 6 gaps surfaced by a real-form
    smoke test (calculated controls, cross-form references, UDFs in
    ControlSource, ComboBox row-source-type split, button intent
    split, StatusBarText handling, subform-container events).
  - `dabbler-pdf-overview.md` — boundary documentation for the
    `dabbler-pdf` dependency: what it does for `dabbler-platform`,
    its public surface, and consuming-repo setup notes. Per Session
    2's verifier-Codex adjudication, the file uses the standard
    `../dabbler-platform` currency header plus an inline body note
    recording the dabbler-pdf SHA, date, and NuGet pin.

# Session-by-session

  - Session 1 of 3 (Inventory + Schema): authored components.md and
    report-rdf-schema.md. Routed cost ~$1.28 across 12 calls; VERIFIED
    on round 2 after a 3-round verification arc that included one
    spec-interpretation adjudication (sample .rdf.json copying — Opus
    tiebreaker picked verdict C: synthetic minimal examples + see-also
    pointers, which is a stronger Feature-2 contract than verbatim
    quotes and was adopted in the round-2 redo).

  - Session 2 of 3 (Migration Map + PDF Boundary): authored migration
    map and PDF overview. Routed cost ~$0.74 across 5 calls; VERIFIED
    on round 2 after a currency-header convention adjudication
    (verifier flagged a per-artifact dabbler-pdf header pin; Codex
    GPT-5 tiebreaker sided with verifier on spec-text-controlling
    grounds; hybrid fix applied: standard `../dabbler-platform`
    header + body note for the dabbler-pdf metadata).

  - Session 3 of 3 (Smoke-Test + Buffer): walked
    `frmOrderDetails`+`sfrmOrderLineItems` from Northwind Developer
    Edition through the migration map. Routed cost ~$0.13 across 2
    calls; VERIFIED on round 1 with zero issues — a notable contrast
    to Sessions 1 and 2 because the changes were purely additive
    table rows against an existing structure rather than new
    interpretive claims. Six gaps surfaced (3 Major, 3 Minor); all
    closed in-session by 8 added/refined rows in the migration map.

# Verification posture

All three sessions ended in cross-provider session-verification via
GPT-5.4 (the configured cross-provider verifier for a Claude
orchestrator). Two sessions also exercised the verifier-disagreement
protocol with human-routed tiebreakers (Opus for Session 1, Codex
GPT-5 for Session 2). Both adjudications resolved by accepting the
verifier finding, which was then logged via `record_adjudication`.

# Verifier-disagreement adjudications

  - Session 1 — Sample `.rdf.json` copying. Verifier flagged that
    Feature 2 needed example documents; orchestrator initially
    pushed back citing the spec's "no copying" Boundaries clause.
    Tiebreaker (Opus) picked verdict C: synthetic minimal examples
    + see-also citations to platform paths. Adopted in round 2.
  - Session 2 — Currency-header convention. Verifier flagged that
    `dabbler-pdf-overview.md` should use a standard
    `../dabbler-platform` header; orchestrator preferred per-artifact
    pinning. Tiebreaker (Codex GPT-5) sided with the verifier on
    spec-text-controlling grounds. Hybrid fix applied: standard
    header + inline body note for the dabbler-pdf metadata.

# Next session set

See `next-session-set-recommendation.md` — `blazor-update-workflows`
(Phase 3a per the post-DAG strategy.md trajectory). The
`generalization-validation-on-non-northwind` set previously named
this as next; this set inherits that recommendation since this
cross-repo workstream produced no follow-up obligation.

# Open follow-ups for the human

None inside this repo. The migration map and component inventory
are now ready inputs for the planned Access-to-Blazor migration
repo. Regenerate this set when the platform's component library
changes meaningfully or when migration-planning work begins in the
third repo.

# Output format

Write a Markdown change log with:

```
# Change Log — dabbler-platform-discovery

> Session set status: complete | Sessions: 3 of 3 | Final verdict: VERIFIED | Total routed cost: ~$2.16
> Completed: 2026-05-17 | Orchestrator: Claude Opus 4.7 (all sessions)

## What this set delivered
...

## How it was verified
...

## Notable adjudications
...

## Where the outputs live
...

## Next session set
...

## Open follow-ups
...
```

Keep it prose-y but punchy. The audience is anyone picking the project
up two weeks from now: they should be able to read this change log
plus the four `docs/cross-repo/` files and understand what was built
and why. Do not invent details beyond what is provided above.
"""


def main() -> int:
    log = SessionLog(SESSION_SET)

    result = route(
        content=PROMPT,
        task_type="summarization",
        max_tier=1,
        session_set=str(SESSION_SET),
        session_number=3,
    )

    # Persist BEFORE printing (cp1252 print-crash defense).
    OUT.write_text(result.content, encoding="utf-8")

    log.log_step(
        session_number=3,
        step_number=6,
        step_key="session-3/change-log",
        description="Generate change-log.md via route(summarization, max_tier=1)",
        status="complete",
        api_calls=[
            {
                "model": result.model_name,
                "taskType": "summarization",
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
