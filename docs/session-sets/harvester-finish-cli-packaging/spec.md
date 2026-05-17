# CLI Packaging Spec — **STUB**

> **Status:** STUB authored 2026-05-17 by `remaining-work-planner` Session 2.
> Acceptance criteria are pinned; the runtime identifier choice,
> publish-profile shape, and install documentation layout are left
> for the set's own Session 1.
>
> **Purpose:** Ship the harvester CLI as a self-contained Windows
> binary release a downstream consumer can install without
> cloning the repo or building from source. **Final gate for Phase 4.**
> **Created:** 2026-05-17
> **Session Set:** `docs/session-sets/harvester-finish-cli-packaging/`
> **Prerequisite:** `harvester-finish-cli-e2e-test-audit` — CLI is
> feature-stable with black-box coverage before we cut a release.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: No UI surface; packaging is an infrastructure change.
> Verified by re-running the existing CLI black-box tests against
> the packaged binary (rather than the source build).

---

## Origin and authority

- **Authority for scope:** the gap audit at
  [`docs/session-sets/remaining-work-planner/gap-audit.md`](../remaining-work-planner/gap-audit.md)
  → GAP #7.
- **Authority for "Done":**
  [`docs/planning/strategy.md`](../../planning/strategy.md) Phase 4
  Criterion #7.
- **Authority for shape (resolved 2026-05-17 via Open Question 3):**
  **self-contained Windows binary** (`dotnet publish --self-contained`).
  Both Gemini Pro and GPT-5.4 independently recommended this for the
  AI-tooling-agent consumer profile. `dotnet tool` channel may be
  added as optional secondary later; embedded NuGet deferred until
  an in-process host repo emerges. See gap-audit GAP #7 and
  `docs/session-sets/remaining-work-planner/q2-q3-recommendations/`
  for the recommendation rationale.

---

## Project Overview

The harvester CLI today is "clone the repo, `dotnet run`". A
downstream consumer (AI tooling agent, future migration-repo
orchestrator, human evaluator) cannot install it. This set ships:

1. A reproducible publish step that produces a self-contained
   Windows binary distribution.
2. A documented install-and-invoke path for downstream consumers.
3. A release-artifact location convention (GitHub Releases, repo
   `dist/`, or a designated channel — Session 1 decides per
   downstream-consumer access pattern).

### In scope

1. `AccessHarvester.Cli.csproj` updated with publish properties
   appropriate for `dotnet publish --self-contained` against a
   Windows RID (e.g., `win-x64`). PublishSingleFile / PublishTrimmed
   / IncludeNativeLibrariesForSelfExtract decisions documented.
2. A pack/publish script or `Directory.Build.props` snippet that
   produces the artifact reproducibly. Path: `tools/publish/` or
   similar (Session 1 decides).
3. Black-box "packaged binary" smoke tests: re-run the existing CLI
   black-box tests (bootstrap, parse, generate-docs, etc.) against
   the published binary rather than the source build. Lives under
   `tests/AccessHarvester.Integration/Cli/` with a path-resolution
   helper that finds the published artifact.
4. Install / invoke documentation for the downstream consumer:
   - Where the artifact lives.
   - How to extract it.
   - The single invocation entry point.
   - Versioning convention (semver tag aligned with the schema
     version + a build number).
5. The README's CLI section updated with the install path so a
   new consumer sees it.

### Out of scope

- A `dotnet tool` channel. Deferred per Open Question 3's resolution
  — may be added later as an **optional secondary**. If a real
  downstream consumer asks for tool-channel install before this set
  closes, the human authorizes a small additive scope; the
  orchestrator does not.
- Embedded NuGet for in-process library use. Explicitly deferred
  until an in-process host repo emerges that wants library reuse.
- Cross-platform builds (Linux, macOS). The harvester is Windows-only
  by COM constraint; a cross-platform RID would be misleading.
- A signed binary. Code signing is a separate distribution-grade
  concern that the human authorizes when the consumer requires it.
- An autoupdate / installer wrapper (MSI, Chocolatey, winget). Out
  of scope for Phase 4; downstream consumers download-and-extract.
- **Strategy.md "Frozen Unless Reauthorized" items remain frozen.**
  Specifically: Blazor application slices / workflows / pilots,
  production IR codegen, `dabbler-platform-discovery` refresh,
  additional Northwind workflows beyond the existing four pilot
  packages, CRUD-scaffolder expansion, and any item the 2026-05-17
  strategic review froze. The packaged binary ships the harvester
  CLI surface as it exists post-F3; it does NOT bundle Blazor
  artifacts, IR-codegen tooling, or platform-integration glue.

---

## Sessions

### Session 1 of 2: Publish Wiring + Reproducible Build

**Steps:**

1. Elaborate this stub.
2. Author `ai-assignment.md` via routed analysis.
3. Update `AccessHarvester.Cli.csproj` with publish properties.
   Decide and document: `PublishSingleFile`, `PublishTrimmed`,
   `IncludeNativeLibrariesForSelfExtract`, `RuntimeIdentifier`.
4. Author the pack/publish script that produces the artifact
   reproducibly. Two consecutive runs against the same source +
   `dotnet publish` invocation produce byte-identical output (after
   normalizing timestamps; document the normalization).
5. Validate: run the publish; manually invoke
   `<artifact>/AccessHarvester.exe --help` to confirm it works
   standalone.
6. `dotnet build && dotnet test` sequentially.
7. End-of-session verification + commit + push + notify.

**Creates:**
- The pack/publish script(s).
- `docs/session-sets/harvester-finish-cli-packaging/ai-assignment.md`
- `docs/session-sets/harvester-finish-cli-packaging/session-reviews/session-001.md`

**Touches:**
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj` (publish
  properties).
- `.gitignore` (likely add the publish output directory).

**Ends with:** a single command produces a self-contained binary
distribution; the binary launches and prints usage.

---

### Session 2 of 2: Black-box Smoke Tests + Install Docs + Closeout

**Steps:**

1. Add the path-resolution helper that finds the published
   artifact (Session 1's publish writes to a known path).
2. Author a small "packaged binary" black-box smoke test that
   re-runs a subset of the existing CLI tests against the
   published artifact rather than the source build. The smoke
   tests are `[SkippableFact]`-gated if the artifact is absent.
3. Author install + invoke documentation:
   - Standalone install doc at the chosen path (likely
     `docs/install.md` or `docs/harvester/install.md` if Session 1
     of `harvester-finish-operations-and-interpretation-docs` chose
     the latter structure).
   - README CLI section addendum with the install path link.
4. **Schema-version alignment.** Document the convention: the CLI
   ships with a version string that includes the schema version
   it produces (e.g., `harvester-1.0-schema-v1.0`). Lands as a
   `--version` flag if not present, or in the existing
   `PrintUsage()` text otherwise.
5. `dotnet build && dotnet test` sequentially.
6. End-of-session verification.
7. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.
8. Step 9 reorganization review AFTER notify.

**Creates:**
- The packaged-binary smoke test.
- Install / invoke docs.
- `docs/session-sets/harvester-finish-cli-packaging/change-log.md`
- `docs/session-sets/harvester-finish-cli-packaging/disposition.json`
- `docs/session-sets/harvester-finish-cli-packaging/session-reviews/session-002.md`

**Touches:**
- `AccessHarvester.Cli/Program.cs` if `--version` flag is added.
- `README.md` (install link).

**Ends with:** Phase 4 Criterion #7 closed: harvester CLI installable
without a from-source build; the install path is documented;
smoke tests verify the packaged artifact runs.

**🎯 Phase 4 closes with this set.**

---

## Cost Expectations

- **Routed cost target:** ~$0.05–$0.20 per session
  (cross-provider session-verification only).
  ~$0.10–$0.40 across the set.
- **In-conversation token spend:** Moderate. Publish wiring is
  well-trodden ground; the smoke-test author + docs are mostly
  mechanical against the patterns Session 1 of
  `harvester-finish-cli-e2e-test-audit` establishes.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **Trim breaks reflection-dependent code** | `PublishTrimmed=true` produces a binary that fails at runtime (DAO COM-interop reflection, ANTLR generated runtime reflection) | Stop. Surface to the human. Default to `PublishTrimmed=false` if trim is unsafe; document the trade. |
| **Reproducibility fails** | Two consecutive publishes produce different binaries even after timestamp normalization | Stop. Surface to the human. Investigate (likely a non-deterministic build input — embedded build timestamp, GUID, etc.). Reproducibility is the project-guidance determinism Principle's packaging analog. |
| **A consumer surfaces and asks for `dotnet tool` channel** | A downstream consumer (future migration repo, evaluator) materializes mid-set and asks for tool-channel install | Surface to the human. The Open Question 3 resolution explicitly allowed `dotnet tool` as an optional secondary; the human authorizes additive scope expansion or defers. |
| **Binary > 500MB or significantly bloated** | Self-contained publish output is unexpectedly large (a stock self-contained Windows publish is usually ~70-150MB) | Stop. Surface to the human. Likely a packaging misconfiguration; investigate before committing as the install target. |

---

## Why this is a stub

The stub fixes the contract: self-contained Windows binary,
reproducible publish, packaged-binary smoke tests, install docs.
RID choice, publish-flag combinations, and the smoke-test reuse
strategy are Session 1's call.
