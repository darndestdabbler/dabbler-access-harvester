# Harvester Cleanup-Golden Refresh + Release Spec

> **Purpose:** Make the harvester's *already-shipped* improvements visible to the
> downstream consumer. The cleanup classifier was narrowed to positive-stale-like
> signals only (landed as set `001`'s baseline commit `6da3b29`) and the usage layer
> gained launcher / out-of-band / parameter recognition (set `001`), but three
> consumer-facing surfaces still show the old behavior: the committed cleanup golden
> fixtures (stale — they still list removed `CrossReferenceAbsence` findings), the
> upstream-feedback planning doc (never updated), and the published release
> (`v1.1.0`, which predates all of set `001`). This set regenerates the stale cleanup
> goldens, prunes the now-dead cross-reference detector, refreshes the feedback doc,
> and cuts a `v1.2.0` release bundling the usage + cleanup fixes.
> **Created:** 2026-06-04
> **Session Set:** `docs/session-sets/002-harvester-cleanup-golden-refresh-and-release/`
> **Prerequisite:** `001-harvester-upstream-feedback-followups` (complete)
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full tier;
> no UI, so no UAT/E2E.
> **Status:** Draft

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatStyle: ad-hoc
uatScope: none
prerequisites:
  - slug: 001-harvester-upstream-feedback-followups
    condition: complete
totalSessions: 2
```

> Rationale: this set changes committed golden artifacts, removes dead code, updates
> planning docs, and produces a release. It touches no UI and no live behavior the
> cleanup/usage classifiers don't already exhibit, so the quality gate is build +
> hermetic tests + cross-provider verification with no UAT/E2E layer. The `001`
> prerequisite is real: Session 2's release bundles set `001`'s usage fixes, and the
> regenerated goldens should reflect the post-`001` repository state.

---

## Why This Set, Why Now

A downstream consumer re-raised the original five-item upstream-feedback list and
reported that it "still stands." Grounding each item against current output (not the
planning doc) showed that the harvester code already addresses them:

- **Usage side (shipped in set `001`, cross-provider VERIFIED 2026-06-02):**
  switchboard/menu/startup launcher recognition (`LauncherSurface` / `StartupEntry`,
  `NavigationShell`), out-of-band entry uncertainty (`OutOfBandEntryPossible`),
  parameterized-query / parameter-dialog signals (`ParameterizedInput`,
  `ParameterDialogFlow`, `ParameterizedQuery`), and canonical PascalCase
  `object-use-checklist.json`.
- **Cleanup side (shipped as set `001`'s baseline commit `6da3b29`):**
  `CleanupAnalyser` now emits **positive stale-like signals only** (name-pattern +
  stale-date). `CrossReferenceDetector` is no longer wired into the pipeline; lack of
  inbound references is no longer treated as cleanup evidence. This is test-pinned by
  `CleanupAnalyserTests.Run_Does_Not_Emit_CrossReferenceOnly_Findings` (a
  `frmSwitchboard` yields zero findings).

What is *not* current is the consumer-facing evidence:

1. **Stale cleanup goldens.** A fresh `identify-cleanup --no-com` against the committed
   IR flags **0 objects** for both HomeHealthCare and NorthwindDevEdition, but the
   committed `object-cleanup.json` goldens still contain **30** (HHC) and **37**
   (Northwind) `Low` `CrossReferenceAbsence` findings — artifacts of the removed code,
   including `frmSwitchboard`, `mcrSwitchboardActions`, `tblSwitchboardItems`, and
   `qryProviderPlansByClient_Param`. Anyone reading the committed golden sees behavior
   the binary no longer produces.
2. **Stale feedback doc.** `docs/planning/harvester-upstream-feedback.md` is the
   original five-item list that drove set `001`; it was never annotated with what
   shipped, so a reader concludes the list "still stands."
3. **Stale release.** The published release is `v1.1.0` at commit `85e95b6`, which
   predates all of set `001`. A consumer on the released binary has none of the usage
   or cleanup improvements.

This set closes those three gaps. It deliberately makes **no change to cleanup or
usage classification logic** — that work is already done and verified.

## Done Looks Like

- The committed `object-cleanup.json` / `object-cleanup-checklist.json` goldens for
  both fixtures reflect the current narrowed classifier (no `CrossReferenceAbsence`
  findings; switchboards and parameterized objects are not flagged).
- The dead `CrossReferenceDetector` (and its now-orphaned tests) are removed, or — if
  the maintainer wants it retained for a future re-scoping — explicitly documented as
  intentionally-unwired with a short note; the build and remaining tests stay green.
- `docs/planning/harvester-upstream-feedback.md` records, per item, that the feedback
  is addressed, with the shipping set and a one-line evidence pointer.
- A `v1.2.0` release exists on GitHub Releases (zip + `.sha256`, tag `v1.2.0`) built
  from the post-refresh `main`, with release notes summarizing the usage + cleanup
  improvements, and `docs/harvester/install.md` reflects the new version.

## In Scope

1. Regeneration of the committed cleanup golden fixtures for HomeHealthCare and
   NorthwindDevEdition from the current classifier.
2. Removal (or explicit documented retention) of the unwired `CrossReferenceDetector`
   and its orphaned tests.
3. A refresh of the upstream-feedback planning doc marking each item addressed.
4. A `v1.2.0` release: build, package (zip + `.sha256`), tag, GitHub release, release
   notes, and `install.md` version bump.

## Out Of Scope

- Any change to cleanup or usage **classification logic** (already shipped + verified
  in set `001`). This set only refreshes artifacts, removes dead code, updates docs,
  and releases.
- The deferred richer stale-like cleanup signals (duplicate-table-shape detection,
  obsolete-module-header detection). These remain a future set unless the maintainer
  explicitly pulls them in.
- Regenerating the usage goldens (already current as of set `001` Session 3).

---

## Sessions

### Session 1 of 2: Stale-Artifact Refresh (Cleanup Goldens + Dead Code + Feedback Doc)

**Steps:**

1. Re-confirm the current cleanup behavior on a fresh run: build the solution, then
   run `identify-cleanup` against the committed HomeHealthCare and NorthwindDevEdition
   IR. Record the fresh finding counts vs the committed goldens. Decide the
   regeneration mode deliberately: prefer COM date sampling on a Windows + Access host
   so genuinely stale-date tables are captured faithfully; fall back to `--no-com`
   (DAO `LastUpdated`) — the established procedure from
   `harvester-object-cleanup-identifier` Session 3 — when COM is unavailable, and note
   which mode was used and why.
2. Regenerate the committed `object-cleanup.json` and `object-cleanup-checklist.json`
   goldens for both fixtures from the fresh run. LF-normalize per `.gitattributes`.
   Sweep any spurious `samples/*.accdb` timestamp churn before staging (per the repo
   convention). Confirm the regenerated goldens contain no `CrossReferenceAbsence`
   findings and do not flag launcher/parameter objects.
3. Confirm `CrossReferenceDetector` has no production caller (it does not as of
   2026-06-04). Remove `CrossReferenceDetector.cs` and `CrossReferenceDetectorTests.cs`
   (and any now-unused `CrossReferenceAbsenceSignal` type / schema enum entry **only**
   if nothing else references it — verify the cleanup schema and `CleanupChecklistWriter`
   first). If the maintainer prefers retention, instead add a one-line doc comment
   marking it intentionally unwired and skip the deletion. Keep the build green either
   way; do not touch `CleanupAnalyser`'s logic.
4. Refresh `docs/planning/harvester-upstream-feedback.md`: add a dated "Resolution"
   note per item (or a status table) recording the shipping set and a one-line
   evidence pointer (e.g. "#2 switchboard detection — addressed in set `001` S1;
   `frmSwitchboard` -> `NavigationShell` @ 0.85"). Do not delete the original
   feedback content; annotate it.
5. Run the narrowest relevant tests first (`AccessHarvester.Pipeline.Tests`, Cleanup
   slice), then `dotnet build` and the affected hermetic test project(s)
   sequentially.
6. Cross-provider verification (golden faithfulness, dead-code-removal safety, doc
   accuracy, scope discipline — no classifier-logic change).
7. Closeout: update `session-state.json`, append actuals to `ai-assignment.md`.

**Creates:**

- (none — regenerates committed fixtures and edits existing docs/code)

**Touches:**

- `samples/GoldenOutput/HomeHealthCare/cleanup/object-cleanup.json`
- `samples/GoldenOutput/HomeHealthCare/cleanup/object-cleanup-checklist.json`
- `samples/GoldenOutput/NorthwindDevEdition/cleanup/object-cleanup.json`
- `samples/GoldenOutput/NorthwindDevEdition/cleanup/object-cleanup-checklist.json`
- `AccessHarvester.Pipeline/Cleanup/CrossReferenceDetector.cs` (remove or annotate)
- `tests/AccessHarvester.Pipeline.Tests/Cleanup/CrossReferenceDetectorTests.cs`
  (remove if the detector is removed)
- `AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json` (only if the
  `CrossReferenceAbsence` enum entry becomes genuinely unused)
- `docs/planning/harvester-upstream-feedback.md`

**Ends with:** the committed cleanup goldens match a fresh classifier run (no
`CrossReferenceAbsence`, no launcher/parameter false positives), dead cross-reference
code is removed (or documented as retained), the feedback doc records each item as
addressed, and the build + hermetic tests are green.

**Progress keys:** `session-1/confirm-fresh-behavior`, `session-1/regen-goldens`,
`session-1/prune-dead-code`, `session-1/refresh-feedback-doc`, `session-1/build-test`,
`session-1/verify`, `session-1/closeout`.

---

### Session 2 of 2: Release v1.2.0

**Steps:**

1. Confirm `main` is at the post-Session-1 state with a clean working tree and all
   tests green. Read `docs/harvester/install.md` for the canonical
   download + checksum-verify procedure and mirror its packaging shape.
2. Determine the version: `v1.2.0` (minor bump from `v1.1.0` — new
   consumer-visible classification behavior since the last release, no breaking CLI
   change). Confirm with the maintainer before tagging if there is any doubt about the
   semver level.
3. Build the release configuration of `AccessHarvester.Cli`, package the binary as the
   release zip, and compute its `.sha256` exactly as prior releases did (see the
   `harvester-finish-cli-packaging` set and `install.md` for the established shape).
4. Author release notes summarizing the consumer-visible improvements since `v1.1.0`:
   launcher/switchboard recognition, out-of-band entry uncertainty, parameterized-query
   / parameter-dialog signals, canonical PascalCase usage checklist, and the
   positive-stale-like-only cleanup output.
5. Tag `v1.2.0` and publish the GitHub release (zip + `.sha256`). This is an
   outward-facing, hard-to-reverse action — confirm with the maintainer immediately
   before publishing.
6. Update `docs/harvester/install.md` (and any version references) to the new version.
7. Cross-provider verification (release-notes accuracy, checksum/process correctness,
   install-doc consistency).
8. Closeout: `disposition.json`, `change-log.md`,
   `next-session-set-recommendation.md`, update `session-state.json`, notify the
   maintainer. Step 9 reorganization review.

**Creates:**

- release artifacts (zip + `.sha256`, attached to the GitHub release — not committed
  to the repo tree)
- `docs/session-sets/002-harvester-cleanup-golden-refresh-and-release/change-log.md`
- `docs/session-sets/002-harvester-cleanup-golden-refresh-and-release/next-session-set-recommendation.md`

**Touches:**

- `docs/harvester/install.md`

**Ends with:** a published `v1.2.0` GitHub release (zip + `.sha256`, tag `v1.2.0`)
built from the refreshed `main`, with accurate release notes and an updated
`install.md`, so the consumer can download a binary that contains every addressed
feedback item.

**Progress keys:** `session-2/confirm-clean-main`, `session-2/build-package`,
`session-2/release-notes`, `session-2/tag-and-publish`, `session-2/update-install-doc`,
`session-2/verify`, `session-2/closeout`.

---

## Stop Rules

- Do **not** change cleanup or usage classification logic. This set refreshes
  artifacts, removes dead code, updates docs, and releases. If a fresh cleanup run
  surfaces a behavior that looks wrong (not merely stale-vs-fresh), stop and surface it
  to the maintainer rather than "fixing" it here — that is a separate, scoped set.
- Do **not** fold the deferred richer stale-like signals (duplicate-table-shape,
  obsolete-module-header) into this set unless the maintainer explicitly expands scope.
- Do **not** publish the `v1.2.0` release without an explicit maintainer go-ahead at
  the moment of tagging (outward-facing, hard-to-reverse).
- Do **not** delete `CrossReferenceDetector` if a production caller is found during
  Session 1 — if so, the "dead code" premise is wrong; stop and reassess.
