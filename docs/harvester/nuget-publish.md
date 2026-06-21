# Publishing `Dabbler.AccessHarvester.Tool` to nuget.org

This runbook covers the `dotnet tool` publish channel — the
tag-triggered, idempotent, guarded pipeline that pushes
`Dabbler.AccessHarvester.Tool` (command `access-harvester`) to nuget.org.
It is **additive** to the self-contained GitHub Releases zip; that channel
(`release.yml`, triggered on plain `v<semver>` tags) is unchanged. See
[`install.md`](install.md) for the consumer side of both channels.

The pipeline lives in
[`.github/workflows/nuget-publish.yml`](../../.github/workflows/nuget-publish.yml)
and mirrors the `dabbler-platform` NuGet precedent (trusted publishing,
build-once / promote-artifacts, `--skip-duplicate`).

---

## One-time maintainer setup (cannot be automated)

These must exist **before the first `nuget-v…` tag push**. They are
account/owner actions on nuget.org and GitHub, not code:

1. **Create a nuget.org account/org** under the `darndestdabbler` profile
   (the `user:` value in the publish job's `NuGet/login` step).
2. **Create a trusted-publishing policy** on nuget.org for this repo and
   the `nuget-publish.yml` workflow, scoped to the `Dabbler.AccessHarvester.Tool`
   package ID (or the `Dabbler.` prefix once reserved). Trusted publishing
   exchanges a GitHub OIDC token for a short-lived (1 h) API key at push
   time, so **no long-lived `NUGET_API_KEY` secret is stored in the repo.**
3. **Create the `nuget-publish` GitHub Environment** (name overridable via
   the `NUGET_PUBLISH_ENVIRONMENT` repo variable). If the repo's plan
   supports environment protection rules, add **required reviewers** so the
   first push of a permanent public ID is a one-click human gate. (On a
   private repo without protection rules, GitHub rejects the required-reviewers
   rule — the environment still carries the OIDC claim but cannot gate, so a
   tag push auto-publishes. This matched the platform's experience.)
4. **(Post-first-publish)** apply for the **`Dabbler` ID-prefix
   reservation** on nuget.org. nuget.org grants prefix reservation to an
   account that already owns matching published IDs, so this is a
   *post*-first-push action, not a pre-push blocker.

---

## Cutting a release

The version is **single-sourced** from `Directory.Build.props`
(`<Version>`), the same value the runtime `--version` output and the
GitHub Release bundle name use. The publish pipeline does **not** invent a
version — it verifies the tag matches the props value and packs from it.

1. **Bump `Directory.Build.props` `<Version>`** to the new semver (e.g.
   `1.3.0`) on `main` and merge it. This is the source of truth.
2. **Tag and push** a `nuget-v<semver>` tag whose semver **equals** that
   `<Version>`:
   ```bash
   git tag nuget-v1.3.0
   git push origin nuget-v1.3.0
   ```
   The `nuget-v` prefix is deliberately namespaced away from the plain
   `v<semver>` tags `release.yml` consumes, so the GitHub-Releases zip and
   the nuget.org tool publish never double-fire on one push. A normal
   release cuts **both** tags for the same semver (`v1.3.0` for the zip,
   `nuget-v1.3.0` for the tool).
3. The workflow then:
   - **`resolve-context`** — parses the tag, and **fails the run if the
     tag's semver ≠ `Directory.Build.props` `<Version>`** (drift guard).
   - **`validate`** — packs the CLI project once (`dotnet pack`, RID-neutral
     so no single-file props leak in), **asserts the packed package ID is
     exactly `Dabbler.AccessHarvester.Tool.<version>.nupkg`** (typo guard for
     the permanent public ID), **smoke-installs the packed tool from a local
     feed and runs `access-harvester --help`** (proves the package's managed
     + ANTLR assets resolve from the tool install location, not just the
     self-extracting exe), then uploads the exact `.nupkg` + `.snupkg`.
   - **`publish-nuget-org`** — downloads that exact artifact, performs the
     OIDC→temporary-key login, and runs `dotnet nuget push --skip-duplicate`
     (which also pushes the sibling `.snupkg`). `--skip-duplicate` makes a
     re-run of the same tag idempotent: same bits → no error, no duplicate.

## Dry run (no nuget.org ID burned)

Use the manual `workflow_dispatch` trigger with a `version` input equal to
`Directory.Build.props` `<Version>`. It runs the identical pack→validate
mechanic but pushes to **GitHub Packages** instead of nuget.org, so the
whole pipeline is exercised without claiming a permanent nuget.org ID.
Recommended before the very first public push.

---

## Irreversibility guardrails (why the extra jobs exist)

The package ID `Dabbler.AccessHarvester.Tool` and any pushed version are
**permanent** on nuget.org (unlist ≠ delete). The pipeline front-loads the
checks that a permanent public artifact warrants:

| Guardrail | Where | Protects against |
|---|---|---|
| Tag ↔ `<Version>` equality | `resolve-context` | A tool version that doesn't map to a known harvester build (risk #3) |
| Package-ID/version assertion | `validate` | A typo'd or unexpected `PackageId` becoming a permanent public claim (risk #4) |
| Smoke install + run from local feed | `validate` | Shipping a package whose assets don't resolve from `~/.dotnet/tools` (risk #2) |
| Build-once / promote-artifacts | `validate` → `publish` | A re-run pushing *different bits* under the same version |
| `--skip-duplicate` | `publish` | Non-idempotent partial-failure recovery |
| GitHub Packages dry run | `workflow_dispatch` | A first-time pipeline bug burning a real nuget.org ID |
| Environment gate (where the plan allows) | `publish-nuget-org` | An unreviewed push of a permanent ID |
| License completeness | `AccessHarvester.Cli.csproj` + `THIRD-PARTY-NOTICES.txt` | A wrong/permanent first-push license expression (MIT) and missing attribution for the dependency DLLs the tool package redistributes (ANTLR BSD-3-Clause + the MIT json-everything / Humanizer / `System.*` assemblies — all reproduced in `THIRD-PARTY-NOTICES.txt`) |

---

## Related documents

- [`install.md`](install.md) — consumer acquisition (both channels).
- [`.github/workflows/nuget-publish.yml`](../../.github/workflows/nuget-publish.yml) — the pipeline.
- [`.github/workflows/release.yml`](../../.github/workflows/release.yml) — the self-contained zip channel (unchanged).
- `LICENSE` / `THIRD-PARTY-NOTICES.txt` — the package's MIT license and the bundled ANTLR runtime's BSD-3-Clause notice.
