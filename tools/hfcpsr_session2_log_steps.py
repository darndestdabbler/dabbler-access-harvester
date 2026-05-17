"""Log Session 2 pre-verification steps to activity-log.json.

The orchestrator performed Steps 1-4 of the Session 2 plan in
conversation; log them mechanically here before the cross-provider
verification call (Step 5).
"""
import os
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from pathlib import Path
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-canonical-parse-success-report")
log = SessionLog(SESSION_SET)

log.log_step(
    session_number=2,
    step_number=1,
    step_key="session-2/diff-gate-reconfirmed",
    description="Re-ran dotnet test ParseSuccessReportTests (default mode, no UPDATE_PARSE_SUCCESS_REPORTS); HomeHealthCare + Northwind both pass byte-equality against committed golden.",
    status="complete",
    api_calls=[],
)

log.log_step(
    session_number=2,
    step_number=2,
    step_key="session-2/per-reason-narrative",
    description="Authored unresolved-reason-narrative.md in-conversation (orchestrator-model-matches-routing-target Convention). Covers all reasons emitted by UnresolvedItemAggregator.SuggestedNextStepsLookup: 5 Resolver, 6 stable + 1 dynamic SqlFormReference family, 3 DynamicSqlSite, 2 NavigationTarget, plus FunctionClassification (same reason codes as Resolver).",
    status="complete",
    api_calls=[],
)

log.log_step(
    session_number=2,
    step_number=3,
    step_key="session-2/sentinel-test",
    description="Extracted ParseSuccessReportDiff helper from ParseSuccessReportTests; refactored AssertOrUpdate to delegate (diff message byte-identical). Added ParseSuccessReportDiffTests with 3 assertions: byte-equal returns null, mutated-count produces actionable message (fixture name, line number, both snippets, regeneration hint), Snippet escapes embedded newlines.",
    status="complete",
    api_calls=[],
)

log.log_step(
    session_number=2,
    step_number=4,
    step_key="session-2/build-test",
    description="Sequential dotnet build && dotnet test on AccessHarvester.sln. Build: 0 errors, 323 pre-existing warnings (no new). Test suite: Pipeline.Tests 360 passed (357 prior + 3 new sentinel), full suite 1853 passed + 2 skipped (COM-gated). No regressions.",
    status="complete",
    api_calls=[],
)

print("Logged Session 2 steps 1-4.")
