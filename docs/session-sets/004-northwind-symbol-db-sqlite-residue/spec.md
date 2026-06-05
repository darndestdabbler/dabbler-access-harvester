# Northwind Symbol-DB / SQLite Residue Remediation Spec

> **Purpose:** Remove the proprietary **NorthwindDevEdition**-derived data that set
> `003` deliberately left in place: the symbol-DB **SQLite binaries and their derived
> consistency data artifacts** (and any committed proprietary module-source fixtures
> the symbol-DB tests build from), first from the working tree at `HEAD`, then from
> **all git history** via a reviewed `git filter-repo` rewrite. Set `003` scrubbed the
> harvester's *dedicated* Northwind artifacts (the `.accdb`, golden tree, YAML/parse
> mirrors, symbol-db measurement docs, comprehension drafts), but its Session-2 scan
> found a **123-file embedded-residue footprint** it could not touch within its reviewed
> allowlist — the highest-density items being `tools/symbol-db/symbol-db.sqlite`
> (1.2 MB) and `tools/symbol-db/consistency/grades.sqlite` (672 KB), which embed full
> proprietary VBA source bodies and AI interpretations. This set remediates that
> SQLite/symbol-db cluster. The remaining HIGH/MEDIUM/LOW residue buckets (probe/
> translation/workflow docs and characterization tests) are **out of scope** here and
> remain a candidate for a later set.
> **Created:** 2026-06-05
> **Session Set:** `docs/session-sets/004-northwind-symbol-db-sqlite-residue/`
> **Prerequisite:** None to begin curation. **Session 3 (destructive) must be
> coordinated with `003` Session 3** — see Stop Rules: if `003`'s history rewrite has
> not yet run, the preferred path is to MERGE this set's allowlist into `003` Session 3
> so the private remote takes **one** force-push, not two.
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification. Full tier; no
> UI, so no UAT/E2E. **Session 3 is destructive and outward-facing** (history rewrite +
> force-push) and runs only on explicit maintainer go-ahead at the moment of the rewrite.
> **Status:** Draft

---

## Session Set Configuration

```yaml
tier: full
requiresUAT: false
requiresE2E: false
uatStyle: ad-hoc
uatScope: none
prerequisites: []
totalSessions: 3
```

> Rationale: the set untracks committed data artifacts, converts symbol-DB tests to
> skip-when-absent, and performs a destructive git-history rewrite. It touches no UI and
> no live classifier/extractor behavior, so the quality gate is build + hermetic tests +
> cross-provider verification with no UAT/E2E layer. No hard prerequisite is declared so
> curation/backup/dry-run can proceed independently; the **destructive** force-push is
> gated and coordinated with `003` Session 3 via the Stop Rules rather than a structured
> prerequisite (which would needlessly block the non-destructive curation work).

---

## Why This Set, Why Now

`003` Session 2's `embedded-residue-findings.md` recorded (and the maintainer confirmed
2026-06-05) that the narrow `003` allowlist leaves a large secondary footprint, with the
**CRITICAL** items being the two committed SQLite databases. Unlike the prose/test
residue, these binaries carry the proprietary DB's **full module source bodies and
symbol interpretations** serialized into committed `.sqlite` files — the densest
proprietary disclosure remaining in the repo. The maintainer asked for a dedicated
follow-up set for this SQLite remediation.

### Curation is mandatory — separate proprietary DATA from generic TOOLING

`tools/symbol-db/` mixes proprietary-derived **data** (scrub) with generic **tooling
code/config** (keep). Confirmed in `003` Session 2 / `004` scoping:

- **Scrub candidates (proprietary NorthwindDevEdition-derived data):**
  `tools/symbol-db/symbol-db.sqlite`, `tools/symbol-db/consistency/grades.sqlite`,
  and the derived consistency data — `consistency/correlation-summary.json`,
  `consistency/sample.json`, `consistency/sample-report.md`,
  `consistency/correlation-report.md`, `consistency/correlation-scatter.png` (confirm
  each is NW-derived before listing). Plus any committed **raw proprietary
  module-source fixture** that the symbol-DB tests build from (the `rawModules` source
  behind `CrossModuleResolutionTests.BuildNorthwind()` — locate and classify it).
- **Keep (generic tooling — NOT scrubbed):** all Python under
  `tools/symbol-db/**/*.py` (graders, samplers, correlation, interpret, probe, their
  unit tests), `tools/symbol-db/interpretation-config.json` (generic thresholds/policy,
  inspected: no proprietary data), and the C# source/test code (which is its own
  embedded-residue question deferred to a later set, not part of this SQLite set).

The scrub target is a **reviewed allowlist**, authored and verified in Session 2, never
a mechanical name match.

## Done Looks Like

- No proprietary symbol-DB SQLite/data artifact is tracked at `HEAD`; the paths are
  gitignored so a fresh run cannot re-commit them; local copies are preserved for the
  developer; the generic tooling can regenerate them locally.
- The build is green and every hermetic test that consumed a committed symbol-DB data
  artifact **skips cleanly when it is absent** (fresh clone builds + tests green).
- A reviewed allowlist of proprietary symbol-DB data paths exists (paths + rationale
  only, no proprietary content), plus a full local backup bundle of the pre-rewrite repo.
- After the gated rewrite (coordinated with `003`): `git log --all -- <path>` is empty
  and a path-complete `ls-tree` before/after proof shows the rewrite removes exactly the
  allowlisted paths and nothing else; all refs/tags are force-pushed; the procedure is
  recorded (incl. the GitHub unreachable-object caveat).

## In Scope

1. Forward removal of the tracked proprietary symbol-DB SQLite + derived consistency
   data artifacts (+ any committed proprietary module-source fixture) + gitignore.
2. Converting symbol-DB-data-dependent hermetic tests to skip-when-absent (mirror the
   `003` Session 1 / `CanonicalGoldenRegressionTests` skip pattern; never weaken a live
   assertion).
3. Authoring + verifying the curated scrub allowlist + rationale and taking a full backup.
4. The gated, destructive `git filter-repo` history rewrite + force-push, coordinated
   with `003` Session 3, and worktree/layout reconciliation.

## Out Of Scope

- The HIGH/MEDIUM/LOW embedded-residue buckets from `003`'s findings (probe/translation/
  workflow docs, characterization tests, name-only references) — a later set.
- Generic symbol-DB Python tooling/config and C# source/test code (kept; the C# residue
  question is deferred).
- HomeHealthCare (synthetic) and any non-Northwind fixture/data.
- Any change to harvester or symbol-DB *behavior* — this is data/artifact removal +
  test skip-hardening only.

---

## Sessions

### Session 1 of 3: Forward Removal + Test/Tooling Skip-Hardening (non-destructive)

**Steps:**

1. Inventory the tracked-at-HEAD proprietary symbol-DB data footprint
   (`git ls-files tools/symbol-db/ | grep -iE '\.(sqlite|json|png|md)$'`), inspect each
   candidate's contents, and classify scrub (proprietary-derived data) vs keep (generic
   tooling/config). Locate the proprietary module-source fixture behind
   `CrossModuleResolutionTests.BuildNorthwind()` and classify it.
2. Enumerate every test/tool that reads a committed symbol-DB data artifact (known:
   `tests/AccessHarvester.SymbolDb.Tests/GenerateArtifacts.cs` regenerates
   `symbol-db.sqlite`; `CrossModuleResolutionTests.cs`; `AccessHarvester.SymbolDb/Cli/BuildArtifacts.cs`;
   `Md5Deduper.cs`; the Python consistency runners). For each that would fail when the
   artifact is absent, add a skip-when-absent guard mirroring the `003` pattern — do
   **not** weaken any assertion that still runs when the artifact IS present.
3. `git rm --cached` the proprietary data artifacts (keep local copies on disk).
4. Add `.gitignore` entries with a comment pointing at this set.
5. Update docs: note in `project-guidance.md` / a `tools/symbol-db/README` that the
   symbol-DB SQLite + consistency data are local-only (regenerated locally), proprietary.
6. Build (`dotnet build`) then run the affected hermetic test projects sequentially
   **twice**: with the local artifact present (assertions run green) and with it moved
   aside (the new skips fire; build + tests stay green). Restore afterward.
7. Cross-provider verification (removal completeness, skip-guard correctness, no weakened
   assertions, proprietary-vs-generic split correctness, scope discipline).
8. Closeout.

**Creates:** (none — removes tracked files, adds gitignore + skip-guards)

**Touches:** `.gitignore`; the symbol-DB test/tool files needing skip-guards;
`docs/planning/project-guidance.md` (or a tools README); the proprietary data artifacts
(untracked, kept locally).

**Ends with:** the proprietary symbol-DB data artifacts are untracked + gitignored, local
copies preserved, dependent tests skip cleanly when absent, build + affected tests green
both with and without the local artifacts.

**Progress keys:** `session-1/inventory`, `session-1/skip-harden`, `session-1/untrack-gitignore`,
`session-1/docs`, `session-1/build-test`, `session-1/verify`, `session-1/closeout`.

---

### Session 2 of 3: Curate Scrub Allowlist + Backup + Dry-Run (analysis/prep, no rewrite)

**Steps:**

1. Confirm `git-filter-repo` is available (installed by `003`; re-verify
   `git filter-repo --version`, recalling the host `safe.bareRepository=explicit` needs a
   `GIT_CONFIG_*` env override and that filter-repo runs via `.venv/Scripts` on PATH).
2. Author the curated scrub allowlist — the proprietary symbol-DB data paths to purge
   from all history (confirm each is present in history). Inspect judgment-call contents;
   EXCLUDE generic tooling/config/code.
3. Write `scrub-allowlist.txt` + `scrub-allowlist-rationale.md` (paths + rationale only,
   no proprietary content). Describe proprietary identifiers BY CATEGORY (these are
   keep-files that survive the rewrite).
4. Take a full local backup bundle (`git bundle create <external>/...pre004.bundle --all`)
   OUTSIDE the repo; verify + restore-test it; record ref SHAs.
5. **Dry-run** on throwaway `--mirror` clones and prove exactness over the
   **path-complete** historical pathname universe (union of
   `git ls-tree -r --name-only <commit>`, not `rev-list --objects` which blob-dedups
   pathnames — lesson from `003` Session 2): BEFORE − allowlisted == AFTER exactly.
6. Cross-provider verification (allowlist correctness, dry-run exactness, backup integrity).
7. Closeout.

**Creates:** `scrub-allowlist.txt`, `scrub-allowlist-rationale.md`, a local backup bundle
(outside the repo, not committed).

**Ends with:** a reviewed, cross-provider-verified allowlist + rationale committed, a
verified backup, and a path-complete dry-run proving the rewrite removes exactly the
intended paths and nothing else. **No real history rewritten.**

**Progress keys:** `session-2/verify-tooling`, `session-2/curate-allowlist`,
`session-2/write-manifest`, `session-2/backup`, `session-2/dry-run`, `session-2/verify`,
`session-2/closeout`.

---

### Session 3 of 3: Execute History Rewrite + Force-Push (DESTRUCTIVE — gated)

**Steps:**

1. **Pre-flight gate.** Confirm: (a) explicit maintainer go-ahead AT THIS MOMENT;
   (b) Session 2's backup bundle exists and is restorable; (c) the force-push
   **coordination decision** with `003` (combined single rewrite vs. sequential second
   rewrite) is settled; (d) every worktree is clean and pushed; (e) no other set is
   mid-rewrite. If any check fails, STOP and surface it.
2. Apply `git filter-repo --invert-paths --paths-from-file scrub-allowlist.txt` to the
   bare repo / fresh `--mirror` (with the `GIT_CONFIG_*` `safe.bareRepository=all`
   override). If coordinating with `003`, run the combined allowlist in one pass.
3. Verify locally with the path-complete `ls-tree` before/after proof; confirm kept paths
   intact and `v1.2.0` resolves; confirm object count/size dropped.
4. **Force-push** all rewritten refs + tags to the private `origin` (irreversible).
5. Re-establish the worktree/layout against the rewritten history.
6. Record the GitHub unreachable-object caveat.
7. Cross-provider verification against a description-of-work (a rewrite can't be
   unit-tested): allowlist, before/after proof, force-push confirmation, layout log.
8. Closeout: `change-log.md`, `next-session-set-recommendation.md`, state flip to
   complete, notify, Step 9 reorganization review.

**Creates:** `rewrite-record.md`, `change-log.md`, `next-session-set-recommendation.md`.

**Touches:** the entire git history (all commit hashes change); the worktree/layout.

**Ends with:** every allowlisted proprietary symbol-DB data path is absent from all
history, the rewritten history is force-pushed, the layout is re-established, and the
procedure (with the GitHub caveat) is recorded.

**Progress keys:** `session-3/preflight-gate`, `session-3/filter-repo`,
`session-3/verify-local`, `session-3/force-push`, `session-3/reestablish-layout`,
`session-3/github-caveat`, `session-3/verify`, `session-3/closeout`.

---

## Stop Rules

- **Coordinate the force-push with `003` Session 3.** If `003`'s history rewrite has NOT
  yet run when this set reaches Session 3, the **preferred** path is to merge this set's
  allowlist into `003` Session 3 so the private remote takes **one** force-push. Two
  separate full-history rewrites/force-pushes are disruptive — only do that if `003` has
  already rewritten history. Surface the decision to the maintainer at the Session 3 gate.
- **Do not run the Session 3 rewrite or force-push without explicit maintainer go-ahead
  at the moment of the rewrite**, a verified restorable backup, and the `v1.2.0` release
  already published. Destructive and outward-facing.
- **Do not scrub any path not on the Session-2-reviewed allowlist.** Generic symbol-DB
  Python tooling, `interpretation-config.json`, and C# source/test code are explicitly
  preserved. If the allowlist looks wrong during Session 3, STOP and re-curate.
- **Do not weaken a test assertion to make it pass when the artifact is absent** — add a
  skip-when-absent guard so the assertion still runs when the local artifact IS present.
- **Do not delete the developer's local copy** of the symbol-DB data; it is untracked,
  not erased; the generic tooling regenerates it locally.
- If proprietary content is found embedded inside a keep-file, **surface it to the
  maintainer** as a finding rather than rewriting that file's history in this set.
- Do not change harvester or symbol-DB behavior.
