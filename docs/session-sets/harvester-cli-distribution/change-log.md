# Harvester CLI Distribution — Session Set Change Log

**Sessions:** 2 of 2 (complete)
**Started:** 2026-05-28
**Closed:** 2026-05-29
**Final verdict:** VERIFIED (Session 2 cross-provider, gpt-5-4 R1, $0.16; 2 Minor fixed inline, 1 Minor dismissed with adjudication evidence)

This session set established a documented release channel for the
self-contained `AccessHarvester.exe` binary. Session 1 wired the
deterministic publish flow to produce a release-ready zip + SHA-256
sidecar. Session 2 authored the CI workflow, rewrote
[`docs/harvester/install.md`](../../harvester/install.md) for the new
channel, added release pointers to README + the three agent-instruction
files, and cut + published the first real GitHub Release at
[v1.0.0](https://github.com/darndestdabbler/dabbler-access-harvester/releases/tag/v1.0.0).
End state: a downstream consumer can install the harvester binary
without contacting the maintainer.

---

## Session 1 of 2: Channel Decision + Deterministic Bundle

**Orchestrator:** claude-opus-4-7 @ effort=medium (matched the
recommended `architecture` / `planning` task type)
**Verification:** VERIFIED on R1 by gemini-pro
**Routed cost:** ≈$0.02

### Created
- `docs/session-sets/harvester-cli-distribution/ai-assignment.md` — the
  routed planning + per-session orchestrator recommendation table.
- `docs/session-sets/harvester-cli-distribution/session-reviews/session-001.md`
  — R1 verification (substantive 7-point analysis confirming spec
  adherence + deterministic-zip implementation correctness).

### Touched
- `tools/publish/publish.ps1` — extended from "produces the bare exe
  only" to "produces the bare exe + a deterministic release bundle":
  - Reads semver + schema from the binary's `--version` output (regex
    against `'AccessHarvester\s+(?<semver>\S+)\s+\(schema\s+v(?<schema>\S+)\)'`)
    so the filename matches the runtime authority.
  - Stages exe + `RELEASE-NOTES.md` placeholder + `LICENSE.txt`
    placeholder under `publish/release/_staging/`, then zips them via
    the .NET `System.IO.Compression.ZipFile` API (NOT PowerShell's
    `Compress-Archive`, which is non-deterministic).
  - Pins every entry's `LastWriteTime` to `2000-01-01T00:00:00Z`,
    sorts entries by name before adding, fixes `CompressionLevel` to
    `Optimal`, and avoids variable metadata.
  - Computes a SHA-256 against the produced zip and writes a
    `<zip>.sha256` sidecar in the standard `<hash>  <filename>`
    two-space format with an LF terminator.
  - Adds a `-SkipBundle` switch for fast local-iteration paths that
    only need the bare exe.

### Decisions (recorded in the session 1 review)

1. **Channel = GitHub Releases on `darndestdabbler/dabbler-access-harvester`.**
   The default spec recommendation was confirmed by the routed planning
   pass; no private-blob-store / NuGet-release-feed alternative was
   needed. The repo is public, has no conflicting tag scheme, and
   GitHub Actions is available.
2. **Bundle filename pattern: `AccessHarvester-<semver>-schema-v<schema>-win-x64.zip`.**
   Both versions encoded in the filename per the spec; the runtime
   `--version` output remains the authority.
3. **Determinism verified empirically.** Two consecutive `publish.ps1`
   runs produced byte-identical zips + identical SHA-256.

### Stop rules
- **Bundle determinism fails** — did NOT fire. Empirical
  byte-identical-zip + identical-SHA-256 evidence captured in the
  session 1 review.
- All other Session 1-applicable stop rules — did NOT fire.

---

## Session 2 of 2: CI Workflow + Docs + First Release

**Orchestrator:** claude-opus-4-7 @ effort=medium (matched the
recommended `architecture` task type for the workflow author pass)
**Verification:** VERIFIED on R1 by gpt-5-4 after two Minor inline
fixes + one Minor dismissed with adjudication evidence (see issue log)
**Routed cost:** ≈$1.36 (architecture $0.45 + documentation $0.01 +
verification $0.16 + next-set analysis $0.00 + one $0.73 architecture
call that was effectively wasted on a `UnicodeEncodeError` print
crash; the rerun produced the canonical YAML output)

### Created
- `.github/workflows/release.yml` (NEW) — GitHub Actions workflow
  triggered on `v*.*.*` tag push. Runs on `windows-latest`, sets up
  .NET 10, invokes `tools/publish/publish.ps1`, verifies that exactly
  one `AccessHarvester-*.zip` + one `.sha256` landed in
  `publish/release/`, extracts the schema version from the filename,
  prints the SHA-256 to the run log, and creates a DRAFT GitHub
  Release attached to the pushed tag with the zip + sha256 as assets.
  The release body links to the tag-pinned
  [`install.md`](https://github.com/darndestdabbler/dabbler-access-harvester/blob/v1.0.0/docs/harvester/install.md)
  and surfaces the SHA-256 for verify-before-download flows. Draft
  state is the manual-approval gate per spec.
- `docs/session-sets/harvester-cli-distribution/session-reviews/session-002.md`
  — R1 verification (3 issues: 1 Major sequencing observation
  resolved by closeout, 2 Minors — one fixed inline, one dismissed
  with empirical evidence + adjudication).
- `docs/session-sets/harvester-cli-distribution/issue-logs/session-002.json`
  — issue ledger with disposition + adjudication context for each.
- `docs/session-sets/harvester-cli-distribution/change-log.md` — this
  file.
- `docs/session-sets/harvester-cli-distribution/disposition.json` —
  closeout disposition.
- `docs/session-sets/harvester-cli-distribution/next-session-set-recommendation.md`
  — recommends `harvester-sql-migration-generator` as the next set
  (the final deferred phase of the data-classification plan).

### Touched
- `docs/harvester/install.md` — "Where to get it" rewritten for the
  GitHub Releases channel; download + checksum-verify recipes
  (PowerShell + sha256sum); maintainer-only local-clone escape hatch.
  Also fixed an unrelated `.exe` → `.zip` filename in the Versioning
  convention section (verifier finding R1, Minor).
- `README.md` — install paragraph leads with the GitHub Releases URL
  and links the install guide.
- `CLAUDE.md`, `AGENTS.md`, `GEMINI.md` — identical one-paragraph
  release-channel pointer added at the end of the Project Overview
  block in each. Surfaced at user request (scope expansion beyond the
  spec's "anywhere they reference" language) to prevent future
  "where does the binary live?" lookups.
- `docs/session-sets/harvester-cli-distribution/session-state.json`,
  `activity-log.json`, `session-events.jsonl` — session bookkeeping.

### Tag + release

- Tag: `v1.0.0` at commit `edccc8f` on `main` (annotated tag with
  short release message).
- Workflow run: `26629055594`, completed in 2m33s.
- Release: <https://github.com/darndestdabbler/dabbler-access-harvester/releases/tag/v1.0.0>
- SHA-256: `4c81f2426f56e2d8343241531c56bd74ed0903a367db4657bf24d97623b5e97e`
- Assets:
  - `AccessHarvester-1.0.0-schema-v1.0-win-x64.zip` (~33.7 MB)
  - `AccessHarvester-1.0.0-schema-v1.0-win-x64.zip.sha256`
- Consumer-perspective validation transcript: see the Session 2
  verification prompt embedded in
  [`session-reviews/session-002.md`](session-reviews/session-002.md);
  `gh release download` + `sha256sum -c` OK + `unzip -l` showing the
  deterministic 2000-01-01 epoch + four smoke checks (`--version`,
  `--help`, `parse <sample.bas>`, `validate --manifest`) all passed
  against the downloaded artifact.

### Decisions (recorded in this change log)

1. **First-release tag = `v1.0.0`.** Confirmed with the human at
   session start; matches the binary's `--version` output and the
   already-frozen schema v1.0.
2. **Draft → published in-session.** The maintainer (human) approved
   publishing the draft immediately after the consumer transcript
   verified, so v1.0.0 is publicly visible and the install-flow is
   closed end-to-end.
3. **CLAUDE/AGENTS/GEMINI scope expansion accepted.** The spec wording
   was "anywhere they reference the install path"; the human asked
   whether the agent-instruction files needed a pointer added even
   though they did not previously reference the path. Recorded the
   addition as a scope expansion in this change log.
4. **install.md `--help` stream documentation kept.** Verifier R1
   Minor said the doc misstated --help as stderr based on transcript
   piping; empirical test
   (`AccessHarvester.exe --help 2>/dev/null | head -3` produces
   empty stdout; `--help 2>&1 >/dev/null | head -3` produces the
   usage text) confirmed the doc is correct. Adjudication recorded
   via `record_adjudication(cause='orchestrator-error',
   resolution='accept-dismissal')`; human confirmed dismissal.

### Stop rules
- **GitHub Releases policy issue** — did NOT fire (repo is public,
  no org policy blocked the tag-push trigger).
- **Bundle determinism fails** — did NOT fire (Session 1 already
  verified; CI artifact's `unzip -l` listing shows the deterministic
  2000-01-01 epoch).
- **Checksum verification doesn't add value** — did NOT fire (.sha256
  sidecar shipped and verified cleanly with both `sha256sum -c` and
  the `Get-FileHash` recipe in install.md).
- **First-release tag conflicts with an existing tag scheme** — did
  NOT fire (repo had no prior tags).

### Workflow-run advisory (non-blocking)

The release workflow run emitted an actions/runner deprecation
notice: `actions/checkout@v4`, `actions/setup-dotnet@v4`, and
`softprops/action-gh-release@v2` currently run on Node.js 20, which
will be removed from runners on 2026-09-16. The workflow ran cleanly
today; the next release before September will need either an action
version bump (`@v5` for checkout once available) or the
`FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` opt-in. Recorded here as a
follow-up rather than fixed inline.

---

## Closing notes

- Total routed cost for the set: ≈$1.38.
- Phase 4 mechanical-finish-line + post-finish-line distribution + the
  data-classifier extension are now all complete. The next natural
  step is the explicitly-deferred Phase 4 of the
  data-classification-plan (SQL Server migration generator) — see
  [`next-session-set-recommendation.md`](next-session-set-recommendation.md).
- A consumer can now `gh release download v1.0.0 --repo
  darndestdabbler/dabbler-access-harvester`, verify the checksum, and
  invoke the binary without ever cloning this repository.
