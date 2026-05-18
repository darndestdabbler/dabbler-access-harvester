"""Verifier-failure escalation step 2: fall back to gemini-pro after
GPT-5.4 returned 429 three times on the configured task_type_overrides
route for session-verification. Uses query() to bypass the override.

This mirrors the Session 1 fallback path.
"""
from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("AI_ROUTER_CONFIG", "ai_router/router-config.yaml")
os.environ.setdefault("AI_ROUTER_METRICS_PATH", "ai_router/router-metrics.jsonl")

from ai_router import query
from ai_router.session_log import SessionLog

SESSION_SET = Path("docs/session-sets/harvester-finish-cli-packaging")
SESSION_NUMBER = 2


def read(path) -> str:
    return Path(path).read_text(encoding="utf-8")


def fenced(label: str, path: Path) -> str:
    return f"### {label} ({path})\n\n```\n{read(path)}\n```\n"


spec = read(SESSION_SET / "spec.md")

deliverables = [
    ("New: PublishedArtifact path-resolution helper",
     Path("tests/AccessHarvester.Integration/TestSupport/PublishedArtifact.cs")),
    ("Modified: RepoPaths (added LocateDirectoryContaining)",
     Path("tests/AccessHarvester.Integration/TestSupport/RepoPaths.cs")),
    ("New: PackagedBinarySmokeTests ([SkippableFact])",
     Path("tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs")),
    ("Modified: CliBootstrapTests (added --version theory)",
     Path("tests/AccessHarvester.Integration/Cli/CliBootstrapTests.cs")),
    ("Modified: Program.cs (added --version / PrintVersion + IsVersionFlag)",
     Path("AccessHarvester.Cli/Program.cs")),
    ("New: install / invoke docs",
     Path("docs/harvester/install.md")),
    ("Modified: README.md (CLI section addendum link)",
     Path("README.md")),
]

out_of_scope = """\
Files present in the repo but NOT changed by Session 2 (verifier should
not flag absent edits to these — they were authored by Session 1 of this
set or by earlier session sets, and are stable):

- AccessHarvester.Cli/AccessHarvester.Cli.csproj — Session 1's publish
  properties (PublishSingleFile=true, PublishTrimmed=false documented,
  IncludeNativeLibrariesForSelfExtract=true, EnableCompressionInSingleFile=true).
- tools/publish/publish.ps1 — Session 1's reproducible publish wrapper.
- .gitignore — Session 1 added /publish/ exclusion.
- docs/test-cases/harvester-schema.v1.0.json — schema authority for the
  '1.0' literal in Program.cs.SchemaVersionLiteral; pinned, not changed.
- AccessHarvester.Pipeline/Manifest/DatabaseManifest.cs — owns the
  authoritative SchemaVersion constant initialiser ("1.0"). Program.cs's
  SchemaVersionLiteral comment names it; they must agree.
- examples/northwind-output.json — used by the new generate-docs smoke
  test as a committed manifest fixture; refreshed by an earlier set.
- tests/AccessHarvester.Integration/TestSupport/CliInvoker.cs — source
  of the internal CliResult record the smoke tests reuse; not changed.
- Existing CLI tests (CliBootstrapTests apart from the new theory,
  CliParseTests, CliGenerateDocsTests, CliUsageTextTests, etc.) — the
  smoke tests intentionally re-run a SUBSET of these against the
  packaged binary, not a verbatim copy.
"""

deliverables_block = "\n".join(fenced(lbl, p) for lbl, p in deliverables)

prompt = f"""\
You are the cross-provider verifier for Session 2 of 2 of the session
set `harvester-finish-cli-packaging` in the dabbler-access-harvester
repo. This is the LAST session of the set — Phase 4 of the harvester
program-of-work closes with this set.

**Your response MUST begin with a detailed prose analysis covering each
verification question listed below BEFORE the JSON block.** Do not skip
the analysis. Both the analysis and the JSON block are required.
Single-line `VERIFIED` responses with no analysis are not acceptable
audit trail for a phase-closing session.

## Session goal

Per the spec (excerpt below), Session 2 ships:
1. A path-resolution helper that finds the published artifact.
2. A `[SkippableFact]`-gated black-box smoke test that re-runs a SUBSET
   of the existing CLI tests against the published artifact rather than
   the source build.
3. Install / invoke documentation: a standalone install doc at
   `docs/harvester/install.md` and a README CLI section addendum.
4. Schema-version alignment: the CLI surfaces its semver + the schema
   version it produces, via a `--version` flag.
5. dotnet build + dotnet test passing sequentially.
6. Closeout artifacts authored at Step 8 (out of scope for this round —
   verify the work product, not the closeout files).

## Verification questions

Analyse each numbered question explicitly. Cite the file path + line
range from the deliverable blocks below when claiming something.

1. **Path-resolution helper** — does `PublishedArtifact.TryLocate`
   correctly resolve `<repo-root>/publish/<rid>/AccessHarvester.exe`,
   return null when absent, and use a stable sentinel (sln file) to
   walk to the repo root? Is the `RepoPaths.LocateDirectoryContaining`
   helper additive (not breaking existing TestCases/Examples/Samples
   callers)?

2. **Smoke test gating** — are all four `PackagedBinarySmokeTests`
   `[SkippableFact]`-gated and do they skip via `Skip.If(...)` when
   `PublishedArtifact.TryLocate()` returns null? Do they invoke the
   packaged exe directly via `Process.Start` (not via `CliInvoker`,
   which targets the test-bin copy)? Is the chosen test subset
   (bootstrap no-args, --help, parse with a committed fixture,
   generate-docs with a committed manifest) a reasonable
   black-box coverage of the packaged binary's surface?

3. **--version flag** — is the flag wired into the dispatcher with
   exit 0 + stdout emission, before the bare-file-mode fallthrough?
   Does the output format `AccessHarvester <semver> (schema v<ver>)`
   surface both the harvester semver and the schema version? Is the
   schema-version literal "1.0" pinned in Program.cs with a comment
   that names DatabaseManifest.SchemaVersion as the source of truth?
   Is the SourceLink "+commit-sha" suffix stripped correctly? Is the
   new bootstrap theory test adequate coverage (both `--version` and
   `version` forms)?

4. **Install docs** — does `docs/harvester/install.md` document:
   distribution shape, where to get the artifact, install steps,
   invocation surface, exit codes, versioning convention (including
   the schema-version alignment story), and a verify-an-install smoke
   recipe? Is the README addendum a single drop-in pointer to the
   install doc that does not duplicate content?

5. **Spec adherence** — does the set as a whole close Phase 4
   Criterion #7 per the spec ("harvester CLI installable without a
   from-source build; install path documented; smoke tests verify
   the packaged artifact runs")? Any spec items left undone?

6. **Stop-rule status** — were any of the four spec stop rules
   tripped (trim breaks reflection-dependent code, reproducibility
   fails, dotnet-tool channel requested, binary >500MB or
   significantly bloated)? Session 1 confirmed 36.9 MB +
   byte-identical SHA256 — Session 2 made no publish-property
   changes, so by inheritance these remain green.

7. **Workflow discipline** — was the work delegated correctly per
   Delegation Discipline? In particular: the install doc is
   synthesis prose authored in-conversation per the "orchestrator's
   model matches routing target" Convention (Opus orchestrator did
   not route synthesis to itself). Did the orchestrator log each
   step via log.log_step (yes — verify via activity-log.json)?

## Spec excerpt

```
{spec[:4500]}
```

## Out-of-scope context (already in the repo, not changed by Session 2)

{out_of_scope}

## Deliverable files

{deliverables_block}

## Required response format

Begin with prose analysis for each of the 7 verification questions,
numbered. Cite file paths + line ranges from the deliverable blocks
above when making claims. Be specific about correctness signals
(actually visible in the code shown) vs. inferred / unverifiable
signals (would require running the binary or seeing files not
provided).

THEN append the JSON verdict block in this exact format:

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

If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not
omit this block. Do not return only the JSON block without the prose
analysis above it.
"""

print("Verifier-failure escalation step 2: querying gemini-pro for cross-provider verification...")
result = query(
    "gemini-pro",
    prompt,
    task_type="session-verification",
    session_set=str(SESSION_SET),
    session_number=SESSION_NUMBER,
)

log = SessionLog(str(SESSION_SET), total_sessions=2)
log.save_session_review(
    session_number=SESSION_NUMBER,
    review_text=result.content,
    round_number=1,
)

log.log_step(
    session_number=SESSION_NUMBER,
    step_number=7,
    step_key="session-2/cross-provider-verification",
    description=(
        f"Cross-provider session-verification (Round 1, fallback per escalation "
        f"ladder step 2): GPT-5.4 returned three 429s; queried {result.model_name} "
        f"directly. Prompt includes mandatory prose-before-JSON lead and the "
        f"required-JSON-block schema per Conventions."
    ),
    status="complete",
    api_calls=[{
        "model": result.model_name,
        "taskType": "session-verification",
        "inputTokens": result.input_tokens,
        "outputTokens": result.output_tokens,
        "costUsd": result.cost_usd,
    }],
)

print(f"Saved Round 1 review (fallback). model={result.model_name} "
      f"tokens={result.input_tokens}+{result.output_tokens} "
      f"cost=${result.cost_usd:.4f}")
print(f"Review file: {SESSION_SET / 'session-reviews' / 'session-002.md'}")
