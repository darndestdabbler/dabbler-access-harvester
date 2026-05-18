"""Log Session 2 Step 5: dotnet build + dotnet test."""
import os
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from pathlib import Path
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
log = SessionLog(str(SESSION_SET), total_sessions=2)

log.log_step(
    session_number=2,
    step_number=6,
    step_key="session-2/build-test",
    description=(
        "dotnet build PASSED (0 errors, 323 warnings — all pre-existing CA1416 "
        "warnings on Windows-only APIs in test files). dotnet test PASSED across "
        "10 test assemblies: TranslationProbe(9), Characterization(131), "
        "Vba.Parser(69), Vba.Grammar(294), Sql.Grammar(85), Vba.Resolver(400), "
        "Pipeline(375), SymbolDb(70), AccessHarvester.Tests(436+2 skipped synthetic), "
        "Integration(31 — includes new VersionFlag bootstrap theory and 4 "
        "PackagedBinarySmokeTests). No failures. Re-published "
        "publish/win-x64/AccessHarvester.exe (36.9 MB) so smoke tests exercise "
        "the post-version-flag build; --version on the packaged binary prints "
        "'AccessHarvester 1.0.0 (schema v1.0)' correctly."
    ),
    status="complete",
    api_calls=[],
)
print("Logged Step 5 (build+test).")
