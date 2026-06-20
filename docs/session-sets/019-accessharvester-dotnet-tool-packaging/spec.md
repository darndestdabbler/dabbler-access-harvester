# AccessHarvester `dotnet tool` Packaging Spec

> **Purpose:** Add a **`dotnet tool`** distribution channel for the harvester CLI
> so a downstream developer acquires it with a one-line
> `dotnet tool install --global Dabbler.AccessHarvester.Tool` (command
> `access-harvester`) instead of downloading a GitHub Release zip or building from
> source. This is **additive** — the existing self-contained single-file Windows
> publish (GitHub Releases, `tools/publish/publish.ps1`) remains the artifact for
> hosts without a .NET 10 runtime; a `dotnet tool` is framework-dependent and
> relies on the consumer's installed runtime.
> **Created:** 2026-06-20 (requested by the operator while wiring the
> `dabbler-access-migration-orchestrator` front-door playbook Step 00, which needs a
> simple, pinnable harvester acquisition command for the "hundreds of Access DBs"
> migration target).
> **Session Set:** `docs/session-sets/019-accessharvester-dotnet-tool-packaging/`
> **Prerequisite:** **P0 only** (a committed green build/test baseline). Independent
> of any other open set.
> **Source of truth (re-confirm every pointer before editing):**
> `AccessHarvester.Cli/AccessHarvester.Cli.csproj` (the `Exe` + the publish-time
> `PublishSingleFile`/`IncludeNativeLibrariesForSelfExtract` props that must stay
> publish-scoped, not leak into `pack`); `tools/publish/publish.ps1` (the existing
> self-contained channel this is additive to); `docs/harvester/install.md` (the
> consumer download/verify procedure to extend with the tool path);
> `CLAUDE.md` (GitHub Releases distribution + the `Dabbler.Security.DevIdp.Tool`
> precedent for a Dabbler `dotnet tool`); and, cross-repo, the agreed elimination
> path in
> `../dabbler-access-migration-orchestrator/docs/planning/harvester-integration-workarounds.md`
> and the acquisition note in that repo's `docs/playbooks/migration-playbook.md`
> (Step 00).
> **Workflow:** Orchestrator → AI Router → cross-provider verification. Full tier;
> no UI, so no UAT/E2E.

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatScope: none
totalSessions: 2
prerequisites: []
verificationMode: out-of-band-or-none
```

> Rationale: no UI surface; the quality gate is build + tests + a real
> `dotnet pack` → local-feed `dotnet tool install` → CLI smoke run, plus
> cross-provider verification. Two sessions: (1) make the CLI packable as a tool
> and prove it installs and runs; (2) the publish pipeline + first-push
> guardrails. `verificationMode: out-of-band-or-none` matches the inline-verify
> Full-tier pattern (007–018); register with
> `--verification-mode out-of-band-or-none`.

---

## Project Overview

**Scope.** Make `AccessHarvester.Cli` installable as a `dotnet tool` and publish it
to nuget.org as a new package `Dabbler.AccessHarvester.Tool` exposing the command
`access-harvester`, without disturbing the existing self-contained GitHub Releases
channel.

**Why now.** The `dabbler-access-migration-orchestrator` front door (playbook Step
00) needs a simple, pinnable harvester acquisition step. Today acquisition is
"download a Release zip and checksum-verify" or "build from source" — both heavier
than the one-line `dotnet tool install` the platform already uses for
`Dabbler.Security.DevIdp.Tool`. The migration target is hundreds of Access
databases, each harvested by a developer who should not have to chase a binary.

**Distribution model — additive, not a replacement.** A `dotnet tool` is a
framework-dependent managed package: it needs the .NET 10 runtime on the host and
does **not** bundle the runtime the way the self-contained single-file exe does.
The GitHub Releases self-contained zip therefore stays the right artifact for
runtime-less hosts; this set adds the developer-friendly channel alongside it.

**Host prerequisite is unchanged.** The harvester's extraction path uses DAO/COM
Access interop and `Microsoft.Data.Sqlite` native assets, so it remains
**Windows + Microsoft Access Database Engine** regardless of distribution channel.
The tool will install on any RID with the .NET runtime, but `run`/`classify`/etc.
only function on a properly provisioned Windows host. This is a documented runtime
caveat, not a packaging blocker; hermetic subcommands (`validate`, `parse`) work
anywhere.

**Non-goals.** No change to harvester behavior, subcommands, output, or schema. No
removal of the self-contained publish. No new feed infrastructure beyond what the
Dabbler NuGet publishing precedent already establishes. No relitigation of the
public-nuget.org decision the platform owner already locked.

## Known risks

1. **Publish-prop leakage.** `PublishSingleFile` / `EnableCompressionInSingleFile`
   / `IncludeNativeLibrariesForSelfExtract` are publish-time and must not bleed
   into `dotnet pack` (a tool package is not single-file). Keep them inside a
   publish-only `PropertyGroup`/condition; verify the packed tool restores its
   native + ANTLR/SQLite assets through the normal tool dependency graph.
2. **Native-asset bundling.** `Microsoft.Data.Sqlite` native binaries and DAO
   interop must resolve when the tool runs from `~/.dotnet/tools`, not only from a
   self-extracting single file. This is the main thing Session 1's local install
   smoke run must prove.
3. **Version drift.** The tool version must track the harvester `v<semver>` release
   line (the GitHub Releases tag) so a `dotnet tool` version maps to a known
   harvester build. Reuse the existing version source rather than inventing a
   second scheme.
4. **Permanent public ID.** `Dabbler.AccessHarvester.Tool` on nuget.org is a
   permanent ID claim (unlist ≠ delete). Gate the first push behind a dry-run /
   validation step (Session 2), mirroring the platform feed set's guardrails.

---

## Sessions

### Session 1 of 2: Make the CLI packable as a `dotnet tool` and prove it runs
**Goal:** `AccessHarvester.Cli` packs to a tool package that installs from a local
feed and runs.
**Steps:**
1. Add tool packaging to `AccessHarvester.Cli.csproj`: `PackAsTool=true`,
   `ToolCommandName=access-harvester`, `PackageId=Dabbler.AccessHarvester.Tool`,
   plus package metadata (authors, description, license, repo URL, README). Keep
   the self-contained publish props publish-scoped so `pack` and `build` are
   unaffected.
2. `dotnet pack` the CLI; install the resulting `.nupkg` from a local folder feed
   (`dotnet tool install --global --add-source <dir> Dabbler.AccessHarvester.Tool`
   or a local `dotnet-tools.json` manifest).
3. Smoke-run the installed `access-harvester` command (`--help` + one hermetic
   subcommand such as `validate`/`parse`) to confirm native/ANTLR/SQLite assets
   resolve from the tool install location, not just the single-file publish.
4. Extend `docs/harvester/install.md` with the `dotnet tool` acquisition path
   alongside the existing Release-zip procedure, and note the runtime/host caveats.
**Ends with:** a tool package that installs and runs locally; install docs cover
both channels; the self-contained publish is unchanged and still green.

### Session 2 of 2: Publish pipeline + first-push guardrails
**Goal:** A deterministic, idempotent publish of `Dabbler.AccessHarvester.Tool` to
nuget.org, consistent with the platform's NuGet publishing precedent.
**Steps:**
1. Add a tag-triggered publish step (mirroring the existing GitHub Releases
   pipeline and the platform `dabbler-nuget-feed-publication` mechanics):
   `dotnet pack` → `dotnet nuget push --skip-duplicate` (idempotent re-run),
   optional `.snupkg` symbols, API key from a CI secret with environment
   protection.
2. Add the irreversibility guardrails before the first public push: a dry-run /
   package-validation job and an ID/typo check (the public ID is permanent).
3. Pin the resolved install command back into the orchestrator playbook Step 00
   `<CONFIRM>` placeholder and the harvester workaround-ledger elimination path
   (cross-repo follow-up note).
**Ends with:** the tool is publishable on a version tag, idempotently and safely;
the orchestrator front door can cite a real `dotnet tool install` command.

## End-of-set deliverables

- `AccessHarvester.Cli` packs to `Dabbler.AccessHarvester.Tool` (command
  `access-harvester`); verified install + smoke run from a feed.
- `docs/harvester/install.md` documents both the `dotnet tool` and the
  self-contained Release-zip channels, with host/runtime caveats.
- A tag-triggered, idempotent, guarded nuget.org publish path.
- The self-contained single-file Windows publish remains intact and green.
