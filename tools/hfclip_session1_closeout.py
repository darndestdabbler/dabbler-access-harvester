"""Session 1 closeout for harvester-finish-cli-packaging.

Non-last session — no change-log.md, no next-session-set-recommendation
(those land at Session 2 closeout). Routes a fresh next-orchestrator
recommendation for Session 2 per Step 8 (and Rule #17), then writes the
revised ai-assignment.md with Session 1 actuals filled in and the
Session 2 recommendation replacing the Step-3.5 placeholder (which the
verifier flagged Minor because it named GPT-4o, a model not in this
repo's pool).

Output is persisted to disk BEFORE display side effects per the
'Persist Routed Output To Disk Before Any Display Or Logging Side
Effects' Convention.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import route
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
SESSION_NUMBER = 1


def read(path) -> str:
    return Path(path).read_text(encoding="utf-8")


# ── Next-orchestrator recommendation for Session 2 ─────────────────────
spec = read(SESSION_SET / "spec.md")

session1_actuals = """\
Session 1 of 2 actuals:
- Orchestrator: claude-code / claude-opus-4-7 @ effort=high.
- Routed cost (this session before this script): $0.0135 (ai-assignment
  analysis on gemini-pro) + $0.0104 (session-verification on gemini-pro
  via the GPT-5.4 verifier-failure fallback, escalation-ladder step 2
  after three GPT-5.4 429s). Total so far ~$0.024 — well inside the
  spec's $0.05-$0.20 band.
- Deliverables: AccessHarvester.Cli.csproj publish properties
  (PublishSingleFile=true, PublishTrimmed=false documented,
  IncludeNativeLibrariesForSelfExtract=true,
  EnableCompressionInSingleFile=true); tools/publish/publish.ps1
  wrapper that passes --self-contained --runtime win-x64; .gitignore
  excludes /publish/.
- Validation: pwsh tools/publish/publish.ps1 produces
  publish/win-x64/AccessHarvester.exe at 36.9 MB; --help launches
  standalone; two consecutive publishes hash to byte-identical
  SHA256 0CC72857... (Deterministic=true is .NET 10 default, no
  timestamp normalization needed).
- Build/test: dotnet build clean (0 errors); dotnet test has 6
  pre-existing failures on pristine HEAD (line-ending mismatch
  i/lf vs w/crlf because the repo has no .gitattributes) — NOT
  caused by this session; flagged for a follow-up tree-amendment.
- Verifier flagged one Minor: the original ai-assignment.md (authored
  by gemini-pro at Step 3.5) named 'GPT-4o' for Session 2 — not in
  this repo's model pool. That placeholder is being superseded by the
  Step-8 routed recommendation right now.

Model pool actually available in this repo's router-config.yaml:
- Tier 1: gemini-flash
- Tier 2: gemini-pro, sonnet, gpt-5-4-mini
- Tier 3: opus, gpt-5-4
"""

session2_steps = """\
Session 2 of 2: Black-box Smoke Tests + Install Docs + Closeout

Steps:
1. Path-resolution helper that finds the published artifact (Session
   1's publish writes to publish/win-x64/AccessHarvester.exe).
2. Small [SkippableFact]-gated black-box smoke test under
   tests/AccessHarvester.Integration/Cli/ that invokes the published
   exe (bootstrap, parse, generate-docs subset) — skipped when the
   artifact is absent.
3. Install / invoke documentation: standalone install doc and a
   README CLI addendum linking to it. Includes schema-version
   alignment convention (semver tag + schema version).
4. --version flag (or PrintUsage augmentation) so the CLI surfaces
   the schema version it produces.
5. dotnet build + dotnet test sequentially.
6. Cross-provider session-verification.
7. Closeout: disposition.json, change-log.md,
   next-session-set-recommendation.md, commit + push +
   mark_session_complete() + notify.
8. Step 9 reorganization review AFTER notify.

Workload character: mechanical (path helper, smoke-test scaffolding,
README addendum) plus synthesis (install doc narrative, change-log,
next-set recommendation). Roughly comparable in size and reasoning
mix to Session 1; small synthesis layer for the docs.
"""

next_orc_prompt = f"""\
You are authoring the next-orchestrator recommendation for Session 2 of
2 in the session set `harvester-finish-cli-packaging` (the final gate
for Phase 4 of the dabbler-access-harvester repo). Per workflow Step 8
and Rule #17, this recommendation must be routed, not orchestrator
self-opinion. The recommendation supersedes a Step-3.5 placeholder
that named a non-existent model.

Required output: a concise Markdown block per the schema in
`docs/ai-led-session-workflow.md` Step 3.5, structured as:

```
## Session 2: Black-box Smoke Tests + Install Docs + Closeout

### Recommended orchestrator
<engine> <model> @ effort=<low|medium|high|max>

### Rationale
<2 sentences — why this engine for this session's mix of work>

### Estimated routed cost
<qualitative — "low" / "moderate" / "high">

| Step | Action | Routing Decision |
|------|--------|------------------|
| ...  | ...    | ...              |
```

Choose only from this repo's actual model pool (the prior gemini-pro
hallucinated 'GPT-4o' which does not exist here):
- Tier 1: gemini-flash
- Tier 2: gemini-pro, sonnet, gpt-5-4-mini
- Tier 3: opus, gpt-5-4

Engines that can act as orchestrator in this workflow: claude-code
(Anthropic), codex (OpenAI), gemini-cli (Google). Match the chosen
model to a plausible orchestrator engine (e.g., sonnet/opus -> claude-code,
gpt-5-4 -> codex, gemini-pro -> gemini-cli).

Spec for this set:

```
{spec[:5500]}
```

Session 1 actuals (already complete):

{session1_actuals}

Session 2 plan:

{session2_steps}

Write the Markdown block directly (no preamble like "Here is the
recommendation:"). Keep rationale to 1-2 sentences. The routing table
should reflect that session-verification ALWAYS routes (mandatory
cross-provider), but the per-task-type defaults in router-config.yaml
already pick cheapest capable — the orchestrator's only job is to pick
the right task_type.
"""

print("Routing next-orchestrator recommendation for Session 2...")
result = route(
    content=next_orc_prompt,
    task_type="analysis",
    context="Next-orchestrator recommendation for Session 2 per Step 8 / Rule #17",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

# Persist to disk BEFORE printing or logging (Convention)
out_path = SESSION_SET / "next-orchestrator-recommendation-session-2.md"
out_path.write_text(result.content, encoding="utf-8")

print(f"[wrote {out_path}] model={result.model_name} "
      f"tokens={result.input_tokens}+{result.output_tokens} "
      f"cost=${result.cost_usd:.4f}")

log = SessionLog(str(SESSION_SET), total_sessions=2)
log.log_step(
    session_number=SESSION_NUMBER,
    step_number=9,
    step_key="session-1/next-orchestrator-recommendation",
    description=(
        "Routed analysis: next-orchestrator recommendation for Session 2 "
        "per Step 8 / Rule #17. Supersedes the Step-3.5 placeholder that "
        "named a non-existent model (verifier Round 1 Minor)."
    ),
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "analysis",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)
