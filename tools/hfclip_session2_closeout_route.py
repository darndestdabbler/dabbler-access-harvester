"""Route change-log.md and next-session-set-recommendation.md for Session 2 closeout.

Persist BEFORE display side-effects per the convention.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
SESSION_NUMBER = 2


def read(path) -> str:
    return Path(path).read_text(encoding="utf-8")


spec = read(SESSION_SET / "spec.md")
ai_assignment = read(SESSION_SET / "ai-assignment.md")
session2_review = read(SESSION_SET / "session-reviews" / "session-002.md")
session1_disposition = read(SESSION_SET / "disposition.json")

# ── change-log.md prompt (last session) ──────────────────────────────────
change_log_prompt = f"""\
Author the end-of-set change log for `harvester-finish-cli-packaging`
(2 sessions). This set CLOSES Phase 4 of the dabbler-access-harvester
program-of-work — Criterion #7 ("Packaging"). It is the FINAL Phase 4
session set per `strategy.md`.

Write the markdown directly. Structure to match existing sibling
change-logs in this repo (see `harvester-finish-cli-e2e-test-audit/
change-log.md` for shape). Required sections (in order):

# Change Log — Harvester Finish: CLI Packaging
**Status:** COMPLETE (today's date in YYYY-MM-DD form: 2026-05-18)
**Sessions:** 2 of 2

## Summary
One paragraph: what the set delivered, why it matters (Phase 4
Criterion #7 — packaging gate), and the headline outcome (the
harvester CLI ships as a self-contained Windows binary with smoke
tests, install docs, and a version surface that names the schema
version).

## Per-session work

### Session 1: Publish Wiring + Reproducible Build
Bullet list. Cite Session 1's actual deliverables from the inputs
below (csproj publish properties, publish.ps1, .gitignore, the
36.9 MB artifact + byte-identical SHA256 reproducibility result,
the cross-provider verification fallback to gemini-pro).

### Session 2: Black-box Smoke Tests + Install Docs + Closeout
Bullet list. Cite Session 2's deliverables: PublishedArtifact path
helper (+ RepoPaths.LocateDirectoryContaining addition), 4 new
[SkippableFact] PackagedBinarySmokeTests, --version flag with
schema-version surface (+ bootstrap theory test), install docs at
docs/harvester/install.md, README CLI addendum link. Build/test
green (31 Integration tests including new ones), VERIFIED Round 1
(gemini-pro fallback after another GPT-5.4 429 — three-for-three on
the recent harvester closeout-grade rounds; same pattern as Session 1
and `harvester-finish-cli-e2e-test-audit` Sessions 1+2).

## Files created or modified
Flat list of paths (one per line, bullet-prefixed). Include both
sessions' files. Include the disposition.json, change-log.md,
next-session-set-recommendation.md, both session-reviews.

## Verification
Two bullets: (1) Per-session cross-provider verifier verdicts
(Session 1: VERIFIED after one Minor fixed; Session 2: VERIFIED
Round 1). (2) Build/test outcomes (Session 1: 0 errors, pre-existing
line-ending failures cleared by separate tree-amendment; Session 2:
all green, 31 integration tests pass).

## Phase 4 status
**Phase 4 CLOSES with this set.** Criterion #7 (Packaging) is now
green; combined with #1 (canonical parse-success report), #3 + #5
(golden output + UnresolvedItems), #4 (CLI E2E tests), #6 (operations
and interpretation docs) shipped earlier in Phase 4, and #2 (schema
finality) shipped before Phase 4, all seven Phase 4 criteria are
closed. Per `strategy.md` and `ROADMAP.md` → "What happens after
Phase 4 closes": the harvester repo moves to maintenance; the
Access-to-Blazor application migration work moves to a future
migration repo not yet created.

## Cost rollup
A short factual bullet: total routed cost across both sessions.
Session 1: ~$0.030 (three gemini-pro calls — ai-assignment analysis,
fallback session-verification, Session 2 next-orc recommendation).
Session 2: include the cost of this set's routed calls (verification
fallback + this change-log + the next-session-set recommendation —
the values land in router-metrics.jsonl; quote approximate totals).
Final rollup should be inside the spec's $0.10–$0.40 band.

Keep the document under ~120 lines. Be specific — cite filenames and
mention the gemini-pro / GPT-5.4-429 pattern explicitly (the pattern
recurs three-for-three across recent closeout-grade verifications and
is worth surfacing for any future Step 9 reorganization review).

## Inputs

### Spec excerpt (`spec.md`)

```
{spec[:4500]}
```

### Existing ai-assignment.md (Session 1 actuals + Session 2 plan)

```
{ai_assignment}
```

### Session 2 verifier review (verbatim — Round 1 VERIFIED)

```
{session2_review}
```

### Session 1 disposition.json (in-progress — will be overwritten at closeout)

```
{session1_disposition}
```
"""

print("Routing change-log.md (summarization, max_tier=1)...")
cl_result = route(
    content=change_log_prompt,
    task_type="summarization",
    context="End-of-set change log for harvester-finish-cli-packaging (Phase 4 closer).",
    max_tier=1,
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

cl_path = SESSION_SET / "change-log.md"
cl_path.write_text(cl_result.content, encoding="utf-8")
print(f"[wrote {cl_path}] model={cl_result.model_name} "
      f"tokens={cl_result.input_tokens}+{cl_result.output_tokens} "
      f"cost=${cl_result.cost_usd:.4f}")

log = SessionLog(str(SESSION_SET), total_sessions=2)
log.log_step(
    session_number=SESSION_NUMBER,
    step_number=8,
    step_key="session-2/change-log",
    description=(
        f"Routed change-log.md via route(task_type='summarization', max_tier=1) "
        f"-> {cl_result.model_name}. Phase 4 closer end-of-set change log."
    ),
    status="complete",
    api_calls=[{
        "model": cl_result.model_name,
        "taskType": "summarization",
        "inputTokens": cl_result.input_tokens,
        "outputTokens": cl_result.output_tokens,
        "costUsd": cl_result.cost_usd,
    }],
)

# ── next-session-set-recommendation.md prompt ────────────────────────────
strategy = read(Path("docs/planning/strategy.md"))[:6000]
roadmap_section = read(Path("docs/session-sets/ROADMAP.md"))[:4500]

next_set_prompt = f"""\
Author the next-session-set recommendation for the dabbler-access-harvester
repo, written from inside the closing set
`harvester-finish-cli-packaging`. This is a plan-driven repo: a master
plan (`docs/planning/strategy.md`) and a ROADMAP exist. Per
`docs/ai-led-session-workflow.md` Step 8, the recommendation must
(1) read the plan, (2) identify the next set per the plan, (3) either
confirm the planned next set or propose a deviation with explicit
justification.

**Critical context: this set CLOSES Phase 4.** Per `strategy.md`
Phase 4 (excerpt below), Phase 4 is the mechanical harvester finish
line. F5 (`harvester-finish-cli-packaging`) is "the final Phase 4 gate."
After F5 closes, the plan says the harvester repo moves to maintenance,
and the Access-to-Blazor application migration work moves to a future
migration repo not yet created. ROADMAP.md → "What happens after
Phase 4 closes" elaborates.

Write the recommendation as a single markdown document. Required
structure:

# Next session set — Post-Phase-4 (harvester-finish-cli-packaging closes Phase 4)

## What the plan says

Two-paragraph summary: the planned post-Phase-4 posture per
`strategy.md` and `ROADMAP.md`. Cite the relevant paragraphs.
Specifically: the harvester repo moves to maintenance; the future
migration repo will inherit the workflow-package shape, the Option D
pattern, the Phase 4 golden output as migration ground truth, and the
recommendations in `strategy.md` → Recommendations for the future
migration repo.

## Recommended next session set

**Slug:** (one of)
- `(none — Phase 4 complete; harvester repo enters maintenance)`
- a specific maintenance-class slug if a clear deferred item from this
  set's spec or stop rules warrants one.

**Rationale:** 2-3 sentences. Be specific about whether the plan is
**confirmed** (no deviation — Phase 4 closes, no next set) or
**deviated from** (a specific maintenance set is needed).

**What the human should do next:**
- Three bullets max. Concrete actions: e.g., "decide when to bootstrap
  the future migration repo", "schedule a maintenance pass against
  Phase 4's golden output if a downstream consumer surfaces requests",
  "decide whether `MiniComplex.accdb` returns to coverage at some
  point (deferred by `remaining-work-planner` Open Question 1)".

## Carry-overs and known follow-ups

Short list of follow-up items this set surfaced (if any). Examples
worth checking: the GPT-5.4 verifier 429 pattern (three sessions in a
row fell back to gemini-pro — should the configured
`session-verification` target swap to gemini-pro?), the
`dotnet tool` channel deferred per Open Question 3 (out of scope but
optional secondary if a real downstream consumer asks), code signing
(deferred per spec stop rules / out-of-scope), release-channel
publication (the spec leaves this as "Session 1 decides" but Session 1
documented the local-clone publish path only — a real release channel
hasn't been provisioned because no consumer has surfaced yet).

## Inputs

### strategy.md (excerpt)

```
{strategy}
```

### ROADMAP.md (excerpt — Phase 4 + post-Phase-4 sections)

```
{roadmap_section}
```

Keep the recommendation under ~80 lines. The point is to give the
human a crisp readout of where the program-of-work stands and what
they need to decide next; not to design future sets in this set's
closeout.
"""

print("Routing next-session-set-recommendation.md (analysis)...")
ns_result = route(
    content=next_set_prompt,
    task_type="analysis",
    context="Next-session-set recommendation: plan-driven, Phase 4 closer.",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

ns_path = SESSION_SET / "next-session-set-recommendation.md"
ns_path.write_text(ns_result.content, encoding="utf-8")
print(f"[wrote {ns_path}] model={ns_result.model_name} "
      f"tokens={ns_result.input_tokens}+{ns_result.output_tokens} "
      f"cost=${ns_result.cost_usd:.4f}")

log.log_step(
    session_number=SESSION_NUMBER,
    step_number=9,
    step_key="session-2/next-session-set-recommendation",
    description=(
        f"Routed next-session-set-recommendation.md via "
        f"route(task_type='analysis') -> {ns_result.model_name}. "
        f"Plan-driven: confirms Phase 4 closes with this set, recommends "
        f"maintenance posture; surfaces carry-overs for human decision."
    ),
    status="complete",
    api_calls=[{
        "model": ns_result.model_name,
        "taskType": "analysis",
        "inputTokens": ns_result.input_tokens,
        "outputTokens": ns_result.output_tokens,
        "costUsd": ns_result.cost_usd,
    }],
)

print("\nDone with routed closeout content.")
print(f"  change-log:   {cl_path}")
print(f"  next-set rec: {ns_path}")
