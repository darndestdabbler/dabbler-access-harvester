# Northwind Proprietary-Data Remediation Spec

> **Purpose:** Remove the proprietary **NorthwindDevEdition** database and all of its
> harvester-derived artifacts from the repository — first from the working tree at `HEAD`
> (the only *currently-tracked* violation is the 406-file
> `samples/GoldenOutput/NorthwindDevEdition/` golden tree), then from **all git history**
> via a reviewed `git filter-repo` rewrite + force-push. The committed metadata is an
> upstream-feedback finding surfaced during set `002`: NorthwindDevEdition is proprietary, so
> neither its `.accdb` nor any derived metadata (IR/KB/goldens/YAML/parse-reports) should live
> in the repo — those artifacts are kept on the local filesystem only. HomeHealthCare (the
> synthetic primary fixture) is unaffected and remains fully committed.
> **Created:** 2026-06-05
> **Session Set:** `docs/session-sets/003-northwind-proprietary-data-remediation/`
> **Prerequisite:** `002-harvester-cleanup-golden-refresh-and-release` (complete) — the
> `v1.2.0` release should be cut on stable history *before* the Session 3 rewrite force-pushes
> and rewrites every commit hash.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full tier; no UI, so
> no UAT/E2E. **Session 3 is destructive and outward-facing** (history rewrite + force-push)
> and runs only on explicit maintainer go-ahead at the moment of the rewrite.
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
  - slug: 002-harvester-cleanup-golden-refresh-and-release
    condition: complete
totalSessions: 3
```

> Rationale: the set removes tracked files, rewrites infrastructure (`.gitignore`, test
> skip-guards), and performs a destructive git-history rewrite. It touches no UI and no live
> classifier behavior, so the quality gate is build + hermetic tests + cross-provider
> verification with no UAT/E2E layer. The `002` prerequisite keeps the `v1.2.0` release on a
> stable, un-rewritten history; cutting the release after a force-push would orphan/relocate
> the tagged commit.

---

## Why This Set, Why Now

Set `002` Session 1 surfaced (and the maintainer confirmed) that **NorthwindDevEdition is
proprietary** and must not be committed to the repo, yet a large derived footprint is present:

- **Tracked at `HEAD` (the active violation):** `samples/GoldenOutput/NorthwindDevEdition/` —
  406 files (`ir/` 198, `kb/` 197, plus `reports-baseline/`, `usage/`, `cleanup/`, `manifest.json`,
  `unresolved-items.json`, `READ-ME-FIRST.md`). This is the harvester's full structural +
  enriched output for the proprietary DB.
- **In history only (already gitignored at `HEAD`):** the `samples/NorthwindDevEdition.accdb`
  binary (tracked across ~19 commits before being gitignored at `0595016`), a stray
  `samples/NorthwindDevEdition.laccdb` lock file, `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml`,
  and `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`.

Because the repo is **private**, the data is not publicly exposed — but the maintainer's
standing policy is that proprietary DB artifacts do not belong in the repo at all, and the
chosen remediation depth (confirmed 2026-06-05) is **forward removal _and_ a full history
scrub**.

### Curation is mandatory — this is not a blind grep

A case-insensitive search for `northwind` over history matches ~430 paths, but many are
**incidental name-matches that MUST be preserved**:

- `docs/session-sets/generalization-validation-on-non-northwind/` — a session-set whose slug
  merely contains "non-northwind"; pure workflow/process docs, no DB data.
- `docs/homehealthcare-northwind-comparison.md` — a fixture-selection rationale doc.
- `tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs` and other **test/source
  code** that reference the fixture *by name*.

And a few are **judgment calls** whose contents must be inspected before classifying as
proprietary-derived (scrub) vs. structural/incidental (keep):

- `examples/northwind-output.json`
- `tools/symbol-db/interpretation-summary-northwind.md`, `tools/symbol-db/measurement-northwind.md`
- `docs/test-cases/comprehension/NorthwindDevEdition/` (`questions.draft.*`)

The scrub target is therefore a **reviewed allowlist**, authored and verified in Session 2,
never a mechanical name match.

## Done Looks Like

- No NorthwindDevEdition-derived artifact is tracked at `HEAD`; the paths are gitignored so a
  fresh run cannot re-commit them; local copies are preserved for the developer's own use.
- The build is green and every hermetic test that consumed the committed Northwind golden now
  **skips cleanly when the local golden is absent** (so a fresh clone builds + tests green).
- A reviewed allowlist of proprietary Northwind paths exists (paths only, no proprietary
  content), with a documented keep/scrub rationale for every judgment-call path, plus a full
  local backup bundle of the pre-rewrite repository.
- After the gated rewrite: `git log --all -- <path>` is empty and `git rev-list --all --objects`
  contains no allowlisted path, for every allowlisted path; all refs/tags are force-pushed to
  the private remote; the bare-repo + worktree layout is re-established against the rewritten
  history; and the procedure is recorded (including the GitHub-side unreachable-object caveat).

## In Scope

1. Forward removal of the tracked `samples/GoldenOutput/NorthwindDevEdition/` tree + gitignore.
2. Converting Northwind-golden-dependent hermetic tests to skip-when-absent.
3. Authoring + verifying the curated history-scrub allowlist and taking a full backup.
4. The gated, destructive `git filter-repo` history rewrite, force-push, and worktree
   re-establishment.

## Out Of Scope

- HomeHealthCare (synthetic; stays fully committed) and any non-Northwind fixture.
- Removing incidental `northwind` name-matches (the `non-northwind` session-set, comparison
  doc, test/source code that reference the fixture by name).
- Any change to harvester classifier/extractor logic.
- Scrubbing proprietary content *embedded inside otherwise-keep files* (e.g. a Northwind data
  value quoted inside a session-review). If any such residue is found during Session 2 it is
  recorded as a finding for the maintainer, not silently rewritten.

---

## Sessions

### Session 1 of 3: Forward Removal + Test Skip-Hardening (non-destructive)

**Steps:**

1. Inventory the **tracked-at-HEAD** Northwind footprint (`git ls-files samples/GoldenOutput/NorthwindDevEdition/`)
   and record the count. Confirm `samples/NorthwindDevEdition.accdb` + the YAML/parse-report
   mirrors are already gitignored (history-only; handled in Sessions 2–3).
2. Enumerate every test that reads `samples/GoldenOutput/NorthwindDevEdition/`. Known
   consumers to verify: `ManifestCompletenessTests` and `MigrationFixtureCoverageTests` (both
   `[InlineData("NorthwindDevEdition")]`), `MigrationGoldenScriptTests`, and
   `CanonicalGoldenRegressionTests.NorthwindDevEdition_ReproducesGoldenOutput` (already
   `Skip.IfNot` on the gitignored `.accdb`). For each that would fail when the golden dir is
   absent, add a `Skip.IfNot(Directory.Exists(goldenDir), "...local-only per 003...")` guard
   mirroring the existing pattern in `CanonicalGoldenRegressionTests` — do **not** weaken any
   assertion that still runs when the golden IS present.
3. `git rm` the tracked `samples/GoldenOutput/NorthwindDevEdition/` tree **while keeping the
   local files on disk** (`git rm --cached -r`), so the developer retains the local copy.
4. Add `.gitignore` entries for `samples/GoldenOutput/NorthwindDevEdition/` alongside the
   existing Northwind block, with a comment pointing at this set.
5. Update docs: the `project-guidance.md` Canonical Test Fixtures table (note Northwind golden
   output is local-only, regenerated locally), remove the now-untracked
   `samples/GoldenOutput/NorthwindDevEdition/READ-ME-FIRST.md` reference if any doc links it,
   and add a short note to `samples/GoldenOutput/HomeHealthCare/READ-ME-FIRST.md` or a
   samples-level README that the Northwind sibling is intentionally local-only.
6. Build (`dotnet build AccessHarvester.sln`) then run the affected hermetic test projects
   sequentially **twice**: once with the local golden present (assertions still run green) and
   once with it temporarily moved aside (the new skips fire; build + tests stay green). Restore
   the local golden afterward. Sweep spurious `samples/*.accdb` churn before staging.
7. Cross-provider verification (removal completeness, skip-guard correctness, no weakened
   assertions, doc accuracy, scope discipline).
8. Closeout: `disposition.json`, update `session-state.json`, append actuals to `ai-assignment.md`.

**Creates:** (none — removes tracked files, adds gitignore + test skip-guards)

**Touches:**

- `.gitignore`
- `tests/AccessHarvester.Pipeline.Tests/Manifest/ManifestCompletenessTests.cs`
- `tests/AccessHarvester.Pipeline.Tests/Migration/MigrationFixtureCoverageTests.cs`
- `tests/AccessHarvester.Pipeline.Tests/Migration/MigrationGoldenScriptTests.cs`
- `tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs` (only if its
  golden-dir guard needs the same skip treatment)
- `docs/planning/project-guidance.md` (Canonical Test Fixtures table)
- `samples/GoldenOutput/NorthwindDevEdition/**` (removed from tracking; kept locally)

**Ends with:** the Northwind golden tree is untracked + gitignored, local copy preserved, the
dependent hermetic tests skip cleanly when it is absent, and `dotnet build` + the affected
hermetic tests are green both with and without the local golden present.

**Progress keys:** `session-1/inventory-tracked`, `session-1/skip-harden-tests`,
`session-1/untrack-and-gitignore`, `session-1/docs`, `session-1/build-test`, `session-1/verify`,
`session-1/closeout`.

---

### Session 2 of 3: Curate Scrub Allowlist + Tooling + Backup (analysis/prep, no rewrite)

**Steps:**

1. Install `git-filter-repo` into the workspace `.venv` (`pip install git-filter-repo`) or
   fetch the standalone script; verify it runs (`git filter-repo --version`). Record the
   version. (It is not installed as of 2026-06-05.)
2. Author the **curated scrub allowlist** — the proprietary Northwind-DB-derived paths to purge
   from all history. Seed set (confirm each is present in history before listing):
   `samples/NorthwindDevEdition.accdb`, `samples/NorthwindDevEdition.laccdb`,
   `samples/GoldenOutput/NorthwindDevEdition/`,
   `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml`,
   `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`.
   For each **judgment-call** path — `examples/northwind-output.json`,
   `tools/symbol-db/interpretation-summary-northwind.md`,
   `tools/symbol-db/measurement-northwind.md`,
   `docs/test-cases/comprehension/NorthwindDevEdition/` — inspect the **contents** and classify
   scrub vs keep with a one-line reason. Explicitly EXCLUDE the incidental name-matches
   (`docs/session-sets/generalization-validation-on-non-northwind/`,
   `docs/homehealthcare-northwind-comparison.md`, test/source `*.cs`).
3. Write the allowlist to a committed manifest
   (`scrub-allowlist.txt` + `scrub-allowlist-rationale.md`, **paths + rationale only, no
   proprietary content**) in the session-set folder. This is the exact input
   `git filter-repo --invert-paths --paths-from-file` will consume in Session 3.
4. Scan for proprietary **content embedded inside keep-files** (e.g. Northwind data quoted in a
   session-review or example). If found, record it as a maintainer finding in
   `embedded-residue-findings.md` (do not rewrite those files in this set).
5. Take a **full local backup**: `git bundle create <external-path>/dabbler-access-harvester-pre003.bundle --all`
   stored OUTSIDE the repo tree, plus note the current `HEAD` SHA of every ref/worktree.
6. **Dry-run** the rewrite on a throwaway mirror clone (`git clone --mirror` → run filter-repo
   there) and confirm: every allowlisted path disappears from `git rev-list --all --objects`,
   no kept path is affected, and the repo size drops. Do NOT touch the real repo or remote.
7. Cross-provider verification (allowlist correctness — nothing proprietary missed, nothing
   incidental wrongly included; dry-run evidence; backup integrity).
8. Closeout: `disposition.json`, update `session-state.json`, append actuals.

**Creates:**

- `docs/session-sets/003-northwind-proprietary-data-remediation/scrub-allowlist.txt`
- `docs/session-sets/003-northwind-proprietary-data-remediation/scrub-allowlist-rationale.md`
- `docs/session-sets/003-northwind-proprietary-data-remediation/embedded-residue-findings.md`
  (only if residue is found)
- a local backup bundle (stored OUTSIDE the repo, not committed)

**Touches:**

- `requirements.txt` (if `git-filter-repo` is pinned there) — optional

**Ends with:** a reviewed, cross-provider-verified scrub allowlist + rationale committed, a
verified full backup bundle, and a successful throwaway-clone dry-run proving the rewrite
removes exactly the intended paths and nothing else. **No real history rewritten.**

**Progress keys:** `session-2/install-tooling`, `session-2/curate-allowlist`,
`session-2/write-manifest`, `session-2/embedded-residue-scan`, `session-2/backup`,
`session-2/dry-run`, `session-2/verify`, `session-2/closeout`.

---

### Session 3 of 3: Execute History Rewrite + Force-Push (DESTRUCTIVE — gated)

**Steps:**

1. **Pre-flight gate.** Confirm: (a) explicit maintainer go-ahead AT THIS MOMENT; (b) Session 2's
   backup bundle exists and is restorable; (c) the `v1.2.0` release (`002` Session 2) is already
   published; (d) every worktree under the container is clean and pushed; (e) no other session
   set is in-flight. If any check fails, STOP and surface it.
2. Operate on the **bare repository** (the container's `.bare/`) or a fresh `--mirror` clone —
   `git filter-repo` refuses to run against a non-fresh clone by default. Run
   `git filter-repo --invert-paths --paths-from-file scrub-allowlist.txt` (use `--force` only
   after confirming the backup). Apply to all refs + tags.
3. Verify the rewrite locally: for every allowlisted path, `git log --all -- <path>` is empty
   and `git rev-list --all --objects | grep <path>` is empty; confirm kept paths (e.g.
   `samples/GoldenOutput/HomeHealthCare/`, the `non-northwind` session-set) are intact and the
   `v1.2.0` tag still resolves; confirm repo object count/size dropped.
4. **Force-push** all rewritten refs and tags to the private `origin`
   (`git push --force --all` + `git push --force --tags`). This is the irreversible,
   outward-facing step — performed only under the Step 1 go-ahead.
5. Re-establish the bare-repo + flat-worktree layout against the rewritten history: remove and
   recreate each worktree (`main/` and any session-set worktrees) so none points at an orphaned
   pre-rewrite commit. Run `cleanup-dev-orphans` first if Windows file locks block worktree
   removal.
6. Record the GitHub-side caveat: force-push leaves unreachable objects on the remote until
   GitHub garbage-collects them; old commit SHAs may remain fetchable by exact hash for a window.
   Document whether contacting GitHub support / triggering GC is warranted for this private repo.
7. Cross-provider verification — against a **description-of-work** (a history rewrite cannot be
   unit-tested): supply the allowlist, the before/after `rev-list` evidence, the force-push
   confirmation, and the worktree re-establishment log; ask the verifier to flag any path that
   should/should not have been purged and any procedural gap.
8. Closeout: `disposition.json`, `change-log.md`, `next-session-set-recommendation.md`, update
   `session-state.json` (set flips to complete), notify the maintainer. Step 9 reorganization
   review.

**Creates:**

- `docs/session-sets/003-northwind-proprietary-data-remediation/rewrite-record.md` (allowlist
  used, before/after object counts, force-push confirmation, worktree re-establishment, GitHub
  caveat)
- `docs/session-sets/003-northwind-proprietary-data-remediation/change-log.md`
- `docs/session-sets/003-northwind-proprietary-data-remediation/next-session-set-recommendation.md`

**Touches:** the entire git history (all commit hashes change); `.bare/` and every worktree.

**Ends with:** every allowlisted proprietary Northwind path is absent from all history, the
rewritten history is force-pushed to the private remote, the worktree layout is re-established,
and the procedure (with the GitHub unreachable-object caveat) is recorded.

**Progress keys:** `session-3/preflight-gate`, `session-3/filter-repo`, `session-3/verify-local`,
`session-3/force-push`, `session-3/reestablish-worktrees`, `session-3/github-caveat`,
`session-3/verify`, `session-3/closeout`.

---

## Stop Rules

- **Do not run the Session 3 rewrite or force-push without explicit maintainer go-ahead at the
  moment of the rewrite**, a verified restorable backup bundle, and the `v1.2.0` release already
  published. This is destructive and outward-facing.
- **Do not scrub any path not on the Session-2-reviewed allowlist.** Incidental `northwind`
  name-matches (the `non-northwind` session-set, the comparison doc, test/source code) are
  explicitly preserved. If the allowlist looks wrong during Session 3, STOP and re-curate.
- **Do not weaken a test assertion to make it pass when the golden is absent** — add a
  skip-when-absent guard so the assertion still runs when the local golden IS present.
- **Do not delete the developer's local copy** of the Northwind golden tree; it is untracked,
  not erased.
- If proprietary content is found embedded inside a keep-file, **surface it to the maintainer**
  as a separate finding rather than rewriting that file's history in this set.
- Do not change harvester classifier/extractor logic.
