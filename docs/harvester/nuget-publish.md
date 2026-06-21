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
account/owner actions on nuget.org and GitHub, not code.

> **This is the same OIDC trusted-publishing mechanism that published all
> of the `Dabbler.*` `dabbler-platform` packages to nuget.org on
> 2026-05-30** (every push returned `Created`). The fields below are
> filled in with the proven values from that setup. The platform's only
> real snag was a **workflow-filename mismatch** between its policy and its
> workflow file — see step 2; getting that field right up front avoids it.

1. **nuget.org account/profile `darndestdabbler`** (email
   `darndestdabbler@gmail.com`). The publish job's `NuGet/login` step uses
   `user: darndestdabbler` — that is the nuget.org **profile name, NOT the
   email**. They already match in the committed workflow.
2. **Create a trusted-publishing policy** on nuget.org
   (Account → *Trusted Publishing*) with these exact fields:
   - **Package owner / Subject:** `darndestdabbler`
   - **Package:** `Dabbler.AccessHarvester.Tool` (a *glob* / the `Dabbler.`
     prefix is fine once the prefix is reserved; before first publish the
     policy can name the literal ID for a not-yet-existent package).
   - **Repository owner / Repository:** `darndestdabbler` /
     `dabbler-access-harvester`.
   - **Workflow file:** `nuget-publish.yml` — ⚠️ **must match this repo's
     workflow filename EXACTLY.** This is the field the platform tripped on:
     its policy named `build.yml`, so it had to *rename* its workflow file to
     match. Here the file is already `nuget-publish.yml`; enter that string
     verbatim (no path, no leading `.github/workflows/`).
   - **Environment:** `nuget-publish` — must match the `environment:` in the
     publish job (its default is `nuget-publish`; only override if you also
     set the `NUGET_PUBLISH_ENVIRONMENT` repo variable to the same value).

   Trusted publishing then exchanges the GitHub OIDC token for a short-lived
   (1 h) API key at push time, so **no long-lived `NUGET_API_KEY` secret is
   stored in the repo.**
3. **Create the `nuget-publish` GitHub Environment** (Settings →
   Environments; name overridable via the `NUGET_PUBLISH_ENVIRONMENT` repo
   variable — keep it `nuget-publish` to match the platform precedent and
   the policy's Environment field above). If the repo's plan supports
   environment protection rules, add **required reviewers** so the first
   push of a permanent public ID is a one-click human gate. (On a private
   repo without protection rules, GitHub rejects the required-reviewers rule
   — the environment still carries the OIDC claim but cannot gate, so a tag
   push auto-publishes. This is exactly what the platform observed.)
4. **(Post-first-publish)** apply for the **`Dabbler` ID-prefix
   reservation** on nuget.org. nuget.org grants prefix reservation to an
   account that already owns matching published IDs, so this is a
   *post*-first-push action, not a pre-push blocker.

Once these exist, follow *Cutting a release* below. Strongly recommended
for the very first push: run the **GitHub Packages dry run** (the
`workflow_dispatch` path) first — it exercises the whole pack→validate→push
mechanic without claiming a permanent nuget.org ID.

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
