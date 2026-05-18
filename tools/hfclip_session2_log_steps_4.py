"""Log Session 2 steps 4.1 through 4.4."""
import os
os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from pathlib import Path
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
log = SessionLog(str(SESSION_SET), total_sessions=2)

log.log_step(
    session_number=2,
    step_number=2,
    step_key="session-2/path-resolution-helper",
    description=(
        "Authored tests/AccessHarvester.Integration/TestSupport/PublishedArtifact.cs. "
        "Resolves repo root via new RepoPaths.LocateDirectoryContaining(\"AccessHarvester.sln\") "
        "and probes publish/<rid>/AccessHarvester.exe. TryLocate returns null on absence so "
        "[SkippableFact] tests can skip cleanly."
    ),
    status="complete",
    api_calls=[],
)

log.log_step(
    session_number=2,
    step_number=3,
    step_key="session-2/packaged-binary-smoke-tests",
    description=(
        "Authored tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs — "
        "4 [SkippableFact] tests: no-args/usage, --help, parse against 01-simple-validation.bas, "
        "generate-docs against examples/northwind-output.json. Process invoked directly so the "
        "packaged exe is exercised (not the test-bin per-project copy). 90s timeout covers "
        "single-file cold-start native-lib extraction."
    ),
    status="complete",
    api_calls=[],
)

log.log_step(
    session_number=2,
    step_number=4,
    step_key="session-2/install-docs",
    description=(
        "Authored docs/harvester/install.md (install + invoke guide, versioning convention, "
        "verify-an-install smoke commands, constraints). Updated README.md CLI section with "
        "a link to the install doc and dropped the now-inaccurate 'three subcommands' lead. "
        "Synthesis prose authored in-conversation per the 'orchestrator's model matches "
        "routing target' Convention; Opus-as-orchestrator does not route synthesis to itself."
    ),
    status="complete",
    api_calls=[],
)

log.log_step(
    session_number=2,
    step_number=5,
    step_key="session-2/version-flag",
    description=(
        "Added --version / version flag to AccessHarvester.Cli.Program. Prints "
        "'AccessHarvester <semver> (schema v1.0)'. Harvester semver pulled from "
        "AssemblyInformationalVersionAttribute on the Pipeline assembly with the "
        "'+commit-sha' SourceLink suffix stripped. Schema-version literal '1.0' "
        "pinned (must match DatabaseManifest.SchemaVersion). PrintUsage mentions "
        "--version. Bootstrap test theory covers both --version and 'version' forms."
    ),
    status="complete",
    api_calls=[],
)
print("Logged session 2 steps 4.1-4.4.")
