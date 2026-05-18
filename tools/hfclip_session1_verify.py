"""Cross-provider session-verification for
harvester-finish-cli-packaging Session 1.

Routes via route(task_type='session-verification') — same-provider-as-orchestrator
exception does NOT apply here per Rule #2.
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

SPEC = (SESSION_SET / "spec.md").read_text(encoding="utf-8")
CSPROJ = Path("AccessHarvester.Cli/AccessHarvester.Cli.csproj").read_text(encoding="utf-8")
PUBLISH_PS1 = Path("tools/publish/publish.ps1").read_text(encoding="utf-8")
GITIGNORE = Path(".gitignore").read_text(encoding="utf-8")
AI_ASSIGNMENT = (SESSION_SET / "ai-assignment.md").read_text(encoding="utf-8")

PROMPT = f"""# Session 1 Verification — harvester-finish-cli-packaging

You are an independent reviewer verifying the work of Session 1 of 2 in
the `harvester-finish-cli-packaging` session set. This is the final
Phase 4 gate: ship the harvester CLI as a self-contained Windows binary
release that a downstream AI tooling agent can install without cloning
or building from source. Session 1 is "Publish Wiring + Reproducible
Build." Session 2 (not in scope here) adds the smoke tests + install
docs.

## Spec excerpt (authoritative for scope)

```
{SPEC[:9000]}
```

## Session 1 deliverables (changes under review)

### 1. AccessHarvester.Cli/AccessHarvester.Cli.csproj

```xml
{CSPROJ}
```

### 2. tools/publish/publish.ps1 (new)

```powershell
{PUBLISH_PS1}
```

### 3. .gitignore (only the new tail entry shown — full file unchanged elsewhere)

```
{GITIGNORE}
```

### 4. docs/session-sets/harvester-finish-cli-packaging/ai-assignment.md

```markdown
{AI_ASSIGNMENT}
```

## Validation results (claimed by the orchestrator)

- `pwsh tools/publish/publish.ps1` produced `publish/win-x64/AccessHarvester.exe`
  at 36.9 MB self-contained single-file (well under the 500MB stop-rule).
- Manual `./publish/win-x64/AccessHarvester.exe --help` printed the full
  usage banner for extract / translate / run / parse / generate-docs;
  CLI launches standalone with no .NET runtime preinstalled needed.
- Two consecutive publishes produced byte-identical artifacts: SHA256
  `0CC72857AC2678E809404A57D7FFA90F09E8FC5066CBFADE8265BD50FC3624DA`
  on both runs. Reproducibility verified without timestamp normalization —
  .NET 10 SDK's `Deterministic=true` default plus compressed single-file
  packaging is already deterministic.
- `dotnet build AccessHarvester.sln` succeeded with 0 errors (323
  pre-existing CA1416 warnings about Windows-only platform).
- `dotnet test` has 6 pre-existing failures unrelated to this session:
  2 ParseSuccessReportTests, 2 SchemaConformanceTests, 1 CliGenerateDocsTests,
  1 CliDeterminismTests. **Confirmed pre-existing by stashing all session
  changes and re-running on pristine `main` HEAD — same 6 failures
  reproduce.** Root cause is line-ending mismatch: golden / example
  fixtures are committed with LF (`i/lf` per `git ls-files --eol`) but
  Windows checkout normalizes them to CRLF (`w/crlf`) because the repo
  has no `.gitattributes` file. The generators emit LF; the tests compare
  byte-for-byte. This affects only fixture-comparison tests, not the CLI
  packaging being verified here. A follow-up tree-amendment to add a
  `.gitattributes` file (or to make the fixture-comparison tests
  line-ending-tolerant) is the natural remediation but is **out of scope
  for this session** (Phase 4 CLI packaging).

## Files explicitly out of scope but referenced by spec

- `Session 2 of 2` (smoke tests, install docs, README addendum,
  `--version` flag, closeout) — NOT in scope for this review.
- The "packaged binary" smoke test under
  `tests/AccessHarvester.Integration/Cli/` — Session 2.
- `docs/harvester/install.md` (or `docs/install.md`) — Session 2.
- README CLI section addendum — Session 2.

## What to verify

1. **Spec adherence (Session 1 only)**: are the four Session 1 steps
   (csproj publish properties, pack/publish script, validation,
   build/test) completed correctly? The spec lists six steps; steps 1
   and 2 (elaborate stub, author ai-assignment) and step 7
   (verification + commit + notify) are the bracketing workflow steps —
   focus on steps 3-6 (csproj, publish script, validate, build/test).
2. **Publish flag soundness**: are `PublishSingleFile=true`,
   `PublishTrimmed=false` (with rationale: ANTLR runtime reflection +
   DAO COM interop + Microsoft.Data.Sqlite native deps),
   `IncludeNativeLibrariesForSelfExtract=true`, and
   `EnableCompressionInSingleFile=true` the right choices for a
   self-contained Windows distribution that a downstream AI tooling
   agent will install and invoke as a single binary? Are there flags
   missing that materially affect the artifact's correctness or
   installability? `RuntimeIdentifier` and `SelfContained` are passed
   from the publish script rather than baked into the csproj — does
   that design choice introduce any failure mode?
3. **Reproducibility**: is the demonstrated byte-identical-SHA256 result
   sufficient evidence of reproducibility per the spec's
   "Reproducibility fails" stop rule, or are there concerns the
   single-machine, single-day, single-commit reproducibility check
   doesn't cover that are within Session 1's scope?
4. **Script correctness**: does `tools/publish/publish.ps1` produce the
   artifact reproducibly and handle obvious edge cases (re-run after
   prior run, repo-root resolution from `$PSScriptRoot`, exit-code
   propagation from `dotnet publish`)? Are there `-ErrorAction` or
   process-exit pitfalls?
5. **.gitignore correctness**: does `/publish/` correctly exclude the
   self-contained artifact without accidentally excluding committed
   files?
6. **ai-assignment.md correctness**: the file was authored via
   `route(task_type='analysis')` per Rule #17 (the orchestrator must
   not self-opine on routing). Does it conform to the schema in
   `docs/ai-led-session-workflow.md` Step 3.5 (Recommended orchestrator
   / Rationale / Estimated routed cost / per-step table / Actuals
   filled after / Next-session orchestrator recommendation)? Note: the
   router's Session 2 recommendation names `GPT-4o` which is not in
   this repo's model pool — flag as a Minor or note, not Critical.
7. **Stop rules**: do any of the spec's four stop rules apply (trim
   breaks reflection — no, trim is off; reproducibility fails — no,
   identical SHA256; consumer requests dotnet tool channel — no, no
   consumer surfaced; binary >500MB — no, 36.9MB)? If any apply, flag
   Critical.

If something is missing context from a related file the verifier should
have seen, prefer to call it out as a context-gap finding rather than
silently flag a Major issue.

After your analysis, output a JSON block in this exact format:

```json
{{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    {{
      "severity": "Critical" | "Major" | "Minor",
      "location": "<file or symbol>",
      "summary": "<one-sentence description>",
      "fix": "<recommended fix>"
    }}
  ]
}}
```

If verdict is VERIFIED, `issues` must be an empty array `[]`.
Do not omit this block.
"""


def main() -> int:
    out_path = SESSION_SET / "session-reviews" / "session-001.md"
    out_path.parent.mkdir(parents=True, exist_ok=True)

    result = route(
        content=PROMPT,
        task_type="session-verification",
        complexity_hint=70,
        context="harvester-finish-cli-packaging Session 1 — publish wiring + reproducible build",
        session_set=str(SESSION_SET),
        session_number=SESSION_NUMBER,
    )

    out_path.write_text(result.content, encoding="utf-8")

    print(f"[wrote {out_path}] model={result.model_name} "
          f"tokens={result.input_tokens}+{result.output_tokens} "
          f"cost=${result.cost_usd:.4f}")

    log = SessionLog(str(SESSION_SET), total_sessions=2)
    log.log_step(
        session_number=SESSION_NUMBER,
        step_number=8,
        step_key="session-1/cross-provider-verification",
        description=f"Cross-provider verification via route(task_type='session-verification'). Verifier: {result.model_name}.",
        status="complete",
        api_calls=[{
            "model": result.model_name,
            "taskType": "session-verification",
            "inputTokens": result.input_tokens,
            "outputTokens": result.output_tokens,
            "costUsd": result.cost_usd,
        }],
    )
    log.save_session_review(
        session_number=SESSION_NUMBER,
        review_text=result.content,
        round_number=1,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
