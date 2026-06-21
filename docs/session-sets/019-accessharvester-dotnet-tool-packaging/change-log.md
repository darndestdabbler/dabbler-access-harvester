# Change Log — Set 019: AccessHarvester `dotnet tool` Packaging

**Tier:** Full (2/2) · **Verification mode:** out-of-band-or-none · **Date:** 2026-06-20
**Orchestrator:** Claude Opus 4.8 (claude-code), both sessions · **Verdict:** VERIFIED (both sessions)

## What shipped

A second, developer-friendly acquisition channel for the harvester CLI:
`AccessHarvester.Cli` now packs as the public `dotnet tool`
**`Dabbler.AccessHarvester.Tool`** (command `access-harvester`), with a
tag-triggered, guarded nuget.org publish pipeline. This is **additive** — the
self-contained single-file Windows publish (GitHub Releases zip,
`tools/publish/publish.ps1`, `v*.*.*` tags) is unchanged and remains the artifact
for hosts without a .NET 10 runtime.

### Session 1 — make the CLI packable and prove it runs
- `AccessHarvester.Cli.csproj`: `PackAsTool` / `ToolCommandName=access-harvester` /
  `PackageId=Dabbler.AccessHarvester.Tool` + package metadata. The self-contained
  single-file publish props were **RID-gated** (`Condition="'$(RuntimeIdentifier)' != ''"`)
  so they never leak into `dotnet build` / `dotnet pack` (spec risk #1).
- Version is single-sourced from `Directory.Build.props` `<Version>` (no second scheme).
- A tool-specific `docs/harvester/nuget-readme.md` (uses the `access-harvester` command
  name, not the source-build `AccessHarvester` exe name) is the packed README.
- `install.md` documents both channels (SDK-to-install vs runtime-to-run).
- **Evidence correction:** the CLI has **no** `Microsoft.Data.Sqlite` dependency
  (SymbolDb-only), so spec risk #2's SQLite-native premise does not apply.
- Proven: pack → local-feed install → 4 hermetic smoke checks; self-contained publish 5/5.
- Cross-provider verification VERIFIED at R2 (R1: 2 Major — SDK-vs-runtime docs, packed
  README command name — both fixed).

### Session 2 — publish pipeline + first-push guardrails + license
- **`.github/workflows/nuget-publish.yml`** — tag-triggered (`nuget-v*`, namespaced
  away from the existing `v*.*.*` Release workflow so the two never double-fire).
  nuget.org **OIDC trusted publishing** (`NuGet/login@v1`, `id-token: write`,
  environment gate) — no long-lived secret. **Build-once / promote-artifacts**
  (pack in `validate`, push the exact uploaded `.nupkg`), `--skip-duplicate`,
  `.snupkg` symbols, actions pinned to commit SHAs.
- **Irreversibility guardrails** (the public ID is permanent):
  - version-drift guard — `resolve-context` fails unless the `nuget-v<semver>` tag
    equals `Directory.Build.props` `<Version>` (risk #3);
  - package-ID/typo assertion — `validate` fails unless the packed file is exactly
    `Dabbler.AccessHarvester.Tool.<version>.nupkg` (risk #4);
  - smoke install + run from a local feed proves the packed tool's managed/ANTLR
    assets resolve from the tool install location (risk #2);
  - a `workflow_dispatch` GitHub-Packages **dry run** exercises the whole
    pack→push mechanic without burning a nuget.org ID.
- **Package license (deferred from S1 by design — a first-push expression is
  permanent):** **MIT** (owner choice, consistent with the rest of the Dabbler
  ecosystem). `PackageLicenseExpression=MIT` + root `LICENSE` + `THIRD-PARTY-NOTICES.txt`.
  Reproducible builds: SourceLink + `.snupkg` symbols (pack/publish-scoped).
- **`docs/harvester/nuget-publish.md`** runbook (one-time maintainer nuget.org setup,
  the tag-cut flow, and the guardrail table); `install.md` availability note updated.
- **Cross-repo pin (`dabbler-access-migration-orchestrator`):** the playbook Step 00
  `<CONFIRM>` placeholder and the harvester workaround-ledger elimination path now
  record that the packaging + publish pipeline landed upstream — kept honest: the
  package is **not yet live on nuget.org** until the maintainer's one-time setup +
  first `nuget-v<semver>` tag push.

## Verification (Session 2)
- **Greenfield matrix (Set-075, packaging-small):** `push:anthropic` + `pull:openai`
  + `pull:google` (skipped=0 — no DeterministicServantViolation this run). **Adjudicated
  union TP = 0.** All 5 consolidated findings were push-only FP/artifacts: the push
  surface, fed a tracked-only diff that **elided the session's new untracked files**
  (`THIRD-PARTY-NOTICES.txt`, `nuget-publish.md`), manufactured false "missing file"
  findings; both pull arms read the real repo and returned clean. A **second** Set-019
  data point running counter to the "packaging-small ⇒ favors-push" prior. Telemetry
  shipped to canonical `075/.../dabbler-access-harvester/019-s2/`.
- **Routed cross-provider `session-verification` (gpt-5-4):**
  - **R1 — ISSUES_FOUND, 1 Major:** `THIRD-PARTY-NOTICES.txt` reproduced ANTLR's
    BSD-3-Clause text but only **named** the MIT components. Evidence-checked the packed
    `.nupkg` (a `dotnet tool` declares no `<dependencies>` and **bundles** its dependency
    DLLs) → the MIT assemblies (json-everything family, Humanizer, `System.*`) really are
    redistributed → **fixed** by reproducing the full MIT license text + copyright holders
    (.NET Foundation and Contributors; Greg Dennis).
  - **R2 — Minor only** (`LICENSE` referenced by the notices but not packed in the
    `.nupkg`): per the Set-071 materiality discipline a Minor-only round is **effectively
    VERIFIED**; the Minor was fixed anyway (LICENSE now packs at the package root, MIT SPDX
    expression unchanged).
  - **Final: VERIFIED** — no blocking findings remain.

## Validation
- `dotnet build AccessHarvester.Cli` (Release): green (pre-existing warnings only).
- `dotnet test AccessHarvester.Tests` (hermetic, CLI-bearing): **703 passed / 2 skipped / 0 failed.**
- `dotnet pack` → `Dabbler.AccessHarvester.Tool.1.2.0.nupkg` + `.snupkg`; nuspec
  `<license type="expression">MIT</license>`, `DotnetTool` type; package root bundles
  `LICENSE`, `THIRD-PARTY-NOTICES.txt`, `nuget-readme.md`.
- Local-feed `dotnet tool install` + run: `access-harvester --version` →
  `AccessHarvester 1.2.0 (schema v1.1)`; `--help` exit 0.

## Net deliverables
- `Dabbler.AccessHarvester.Tool` packs + installs + runs (verified from a feed).
- A tag-triggered, idempotent, guarded nuget.org publish path (awaiting the maintainer's
  one-time nuget.org setup + first `nuget-v<semver>` tag for the first public push).
- `install.md` documents both channels; `nuget-publish.md` is the publish runbook.
- The self-contained single-file Windows publish remains intact and green.

## Follow-ups (not blockers)
- **Maintainer one-time, manual:** create the nuget.org account + trusted-publishing
  policy + `nuget-publish` environment, then push the first `nuget-v<semver>` tag; apply
  for the `Dabbler` ID-prefix reservation post-first-publish.
- The cross-repo `<CONFIRM>` placeholder clears once that first publish makes
  `dotnet tool install --global Dabbler.AccessHarvester.Tool` resolve from nuget.org.
- Guidance-file token ceilings: `lessons-learned.md` (~46k) and `project-guidance.md`
  (~15.6k) are over their advisory ceilings — a pruning/archival sweep is due (non-blocking).
