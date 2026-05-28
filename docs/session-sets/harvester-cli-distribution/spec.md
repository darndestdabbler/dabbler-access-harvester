# Harvester CLI Distribution Spec

> **Purpose:** Establish a documented release channel for the
> `AccessHarvester.exe` self-contained binary so downstream consumers
> can install it without cloning the repo or asking the maintainer.
> Today `docs/harvester/install.md` is candid that the binary "is not
> committed" and instructs consumers to "ask the harvester maintainer
> for the artifact" — that's fine for the first consumer (where the
> maintainer is one keystroke away) but won't scale across the
> hundreds of consumer projects this harvester is intended to serve.
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-cli-distribution/`
> **Prerequisite:** `harvester-finish-cli-packaging` — that set already
> delivered the reproducible publish + the self-contained Windows
> binary at `publish/win-x64/AccessHarvester.exe`. This set picks up
> where it left off and ships the distribution path.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: Distribution is infrastructure work. Verified by a
> consumer-style fresh-install transcript captured at the end of
> Session 2 (download → smoke-check the install per existing
> `install.md` verification steps).

---

## Project Overview

The harvester ships as a self-contained Windows binary today, but the
binary's distribution surface stops at the local clone:

- It's not committed (per `.gitignore`).
- It's not published anywhere consumers can pull from.
- `install.md` is honest about this and tells consumers to ask the
  maintainer.

This set chooses a release channel, wires the publish flow to write a
release-ready artifact, documents the install path against that
channel, and updates `install.md` plus the AI-instruction files so
future orchestrators (in consumer projects) know where to look.

### In scope

1. **Pick a release channel.** Recommended default: GitHub Releases
   on this repo. The publish script writes a release zip + checksum
   to a known path; a release is cut by tagging the repo + uploading
   the artifact. Other channels (private blob store, NuGet release
   feed, internal CDN) are acceptable substitutes — Session 1 picks
   one and documents the choice.
2. **Release-artifact shape.** Per release: one
   `AccessHarvester-<semver>-schema-v<schema>-win-x64.zip`
   (zip rather than bare exe so checksums travel with the artifact)
   containing `AccessHarvester.exe` + `RELEASE-NOTES.md` + a
   `LICENSE` reference. Accompanied by `*.sha256`.
3. **CI publish workflow (optional but recommended).** A GitHub
   Actions workflow triggered on tag pushes that runs
   `tools/publish/publish.ps1`, computes the SHA-256, and creates a
   draft GitHub Release with the zip attached. Manual approval gates
   the draft → published transition. If GitHub Actions is not the
   chosen channel, the publish-and-upload steps run as a documented
   manual sequence instead.
4. **Versioning convention pinned in `install.md`.** Filename encodes
   harvester semver + schema version (e.g.,
   `AccessHarvester-1.0.0-schema-v1.0-win-x64.zip`). The CLI's
   `--version` output is the runtime authority — `install.md` already
   says this; the new release artifact must conform.
5. **`install.md` rewrite for the new channel.** Replace the "ask the
   maintainer" paragraph with: where releases live, how to download
   the latest, how to verify the checksum, and how to consume the
   exe out of the unzipped folder. Keep the existing verification
   procedure (`--version`, `--help`, `parse <sample.bas>`) — that
   stays as the smoke check.
6. **Instruction-file updates across the repo.** The new install path
   must land in every place an AI orchestrator (or a human via an
   orchestrator) would look:
   - `docs/harvester/install.md` (primary — see #5).
   - Top-level `README.md` install section.
   - `CLAUDE.md`, `AGENTS.md`, `GEMINI.md` — anywhere they reference
     the harvester install path.
   - `docs/cross-repo/*.md` cross-repo docs that point downstream
     consumers at the harvester install.
   - `docs/planning/project-status.md` "where durable content lives"
     section if the install path moves.

### Out of scope

- Code signing the binary. The existing constraint in `install.md`
  ("Distribution-grade code signing is a separate concern; the
  consumer organisation supplies it if their policy requires a signed
  binary") stands. If signing becomes required, that's a separate
  spec.
- `dotnet tool` channel. The packaging set deliberately deferred this
  (Open Question 3 resolution); the deferral still stands.
- Autoupdate, MSI installer, Chocolatey, winget. Also deferred per
  the packaging set.
- Cross-platform builds (Linux, macOS). Harvester is Windows-only by
  COM constraint.
- An in-repo binary commit. The `.gitignore` exclusion of the publish
  output stands; releases are the distribution channel.
- A migration tool to upgrade older `harvester-schema.v0` outputs to
  v1.0. That's part of `harvester-schema-v1-adr-followups`.

---

## Sessions

### Session 1 of 2: Channel Decision + Publish-Flow Wiring

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and
   `docs/harvester/install.md` (the current state).
2. Route a planning task that picks the release channel and the
   artifact-bundle shape. The default recommendation is GitHub
   Releases on this repo with zip+checksum bundles; the routed
   decision either confirms or proposes an alternative.
3. Update `tools/publish/publish.ps1` (or the equivalent) to produce
   the chosen artifact bundle. Output goes to a known path under
   `publish/` so the CI workflow (Session 2) and a manual maintainer
   can both pick it up.
4. Add a SHA-256 hashing step to the publish script. Verify
   determinism survives the zip step — two consecutive publishes
   produce byte-identical zip + identical SHA-256 (after timestamp
   normalization documented in the existing publish flow).
5. Smoke-validate: run the publish, unzip into a temp folder, run
   `AccessHarvester.exe --version` from the unzipped path.
6. `dotnet build && dotnet test` sequentially.
7. End-of-session verification + commit + push + notify.

**Creates:**
- (Optional) `docs/session-sets/harvester-cli-distribution/ai-assignment.md`
  if the channel decision was routed.
- `docs/session-sets/harvester-cli-distribution/session-reviews/session-001.md`

**Touches:**
- `tools/publish/publish.ps1` (or equivalent — bundle + hash).
- `.gitignore` if new publish output paths need exclusion.

**Ends with:** Running the publish script produces the release-ready
zip + checksum at a documented path; the unzipped binary is
functionally identical to the pre-bundling `publish/win-x64/AccessHarvester.exe`.

---

### Session 2 of 2: CI Workflow + Docs + First Release

**Steps:**

1. Read the Session 1 review and the chosen-channel decision.
2. (If GitHub Releases is the channel) Author the GitHub Actions
   workflow under `.github/workflows/release.yml` triggered on tag
   pushes matching `v*.*.*`. Workflow runs the publish script,
   uploads the zip + checksum as draft-release assets, and posts the
   release notes. If a non-GH-Actions channel was chosen, document
   the equivalent manual sequence in a runbook under
   `docs/harvester/release-runbook.md` instead.
3. Rewrite `docs/harvester/install.md` per #5 in scope. The
   verification section ( `--version` / `--help` / `parse`) stays
   intact.
4. Update the AI-instruction files per #6 in scope: top-level
   `README.md`, `CLAUDE.md`, `AGENTS.md`, `GEMINI.md`,
   `docs/cross-repo/*.md`. Use `route(task_type="documentation")`
   for each refresh pass.
5. Cut a first real release: tag `v<semver>` at the current `HEAD`
   on `master`, push the tag, watch the workflow (or run the manual
   sequence), publish the draft. Record the release URL in the
   session review.
6. Validate from a "consumer's perspective": from a clean directory,
   download the release zip, verify the checksum, unzip, run the
   `install.md` verification steps. Capture the transcript in the
   session review as the cross-provider-verifiable proof.
7. `dotnet build && dotnet test` sequentially.
8. End-of-session verification.
9. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, commit + push +
   `mark_session_complete()` + notify.

**Creates:**
- `.github/workflows/release.yml` (or `docs/harvester/release-runbook.md`
  if non-GH-Actions channel chosen).
- `docs/session-sets/harvester-cli-distribution/session-reviews/session-002.md`
- `docs/session-sets/harvester-cli-distribution/change-log.md`
- `docs/session-sets/harvester-cli-distribution/disposition.json`

**Touches:**
- `docs/harvester/install.md` (rewrite of distribution section).
- Top-level `README.md`.
- `CLAUDE.md`, `AGENTS.md`, `GEMINI.md`.
- `docs/cross-repo/*.md` install-pointer paragraphs.
- `docs/planning/project-status.md` if the install path moves.

**Ends with:** A documented release URL exists; a consumer can
install per the new `install.md` without contacting the maintainer;
the consumer-perspective transcript in the session review
demonstrates the path works.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **GitHub Releases policy issue on the repo** | The repo is private / org policy blocks releases / token scopes don't allow tagging | Stop. Surface to the human. Pivot to a documented manual-runbook channel rather than forcing the GH-Releases path. |
| **Bundle determinism fails** | Two consecutive publishes produce different zip checksums after timestamp normalization | Stop. Investigate (likely zip metadata — directory timestamps, central-directory ordering, embedded build IDs). Determinism is the project-guidance Principle's distribution analog; do not ship a non-deterministic artifact. |
| **Checksum verification doesn't add value for this consumer profile** | The chosen channel already provides cryptographic integrity (signed release notes, internal artifact store with hash-on-upload) | Document the channel's native integrity guarantee in `install.md` instead of duplicating the SHA-256 step; do not skip the integrity story silently. |
| **First-release tag conflicts with an existing tag scheme** | The repo already has tags under a different scheme that the new `v*.*.*` pattern would clash with | Stop. Surface to the human. Decide between scheme migration and a different release-tag pattern before cutting the first release. |

---

## Why now

The first downstream consumer (`Dabbler.NorthwindToBlazor`) has its
own local clone of this repo and can copy
`publish/win-x64/AccessHarvester.exe` directly — so this set is not
blocking that consumer's Set 001 Session 2. However, the harvester is
positioned to serve "hundreds of Access databases" worth of consumer
projects. The first one can hand-copy the binary; the hundredth
cannot. Closing the distribution gap now means every future consumer
project follows a single documented install path rather than
re-inventing one each time.
