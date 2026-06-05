# History-Rewrite Record — 003 Northwind Proprietary-Data Remediation (Session 3)

> **Destructive, outward-facing operation executed 2026-06-05** under explicit
> maintainer go-ahead. This document records the allowlist used, before/after
> evidence, the force-push confirmation, the checkout re-establishment, and the
> GitHub-side unreachable-object caveat. Paths + SHAs only — no proprietary
> content.

## Pre-flight gate (all satisfied before any destructive step)

| Gate | Result |
|---|---|
| (a) Explicit maintainer go-ahead **at the moment of the rewrite** | ✅ given (mirror-clone procedure) |
| (b) Verified restorable backup bundle | ✅ Session-2 bundle (`pre003.bundle`, HEAD `44122f7`) + a **fresh** bundle taken this session at HEAD `897d851` (`pre003-rewrite.bundle`), both sha256-recorded and `git bundle verify`-clean |
| (c) `v1.2.0` release published on stable history | ✅ tag on remote + GitHub Release live (not draft, assets attached, published 2026-06-05) |
| (d) Working tree clean & pushed | ✅ clean; local `main` == `origin/main` (`897d851`) |
| (e) No other session set in-flight | ✅ set `004` was `not-started` |

**Layout reconciliation:** the spec's Step 2/Step 5 assume the bare-repo +
flat-worktree layout (`.bare/` + multiple worktrees). The actual repo is a
**plain single-worktree checkout** with one branch (`main`). So "operate on
`.bare/`" → operate on a throwaway `--mirror` clone; "re-establish each
worktree" → re-sync the single checkout. This was flagged in Session 2's
actuals and confirmed at preflight.

## Tooling

- `git-filter-repo` **2.47.0** (pip, in workspace `.venv`; `--version` prints
  the tool's internal source id `a40bce548d2c`, which is expected).
- Host sets `safe.bareRepository=explicit`, so all operations against the bare
  mirror used the env override `GIT_CONFIG_COUNT=1 / GIT_CONFIG_KEY_0=safe.bareRepository / GIT_CONFIG_VALUE_0=all`.

## Allowlist used

`scrub-allowlist.txt` — 9 lines (7 exact file paths + 2 directory prefixes),
expanding to the curated **415-path** proprietary scrub set. Unchanged from the
Session-2 cross-provider-VERIFIED file. See `scrub-allowlist-rationale.md` for
per-path rationale and the keep list.

## Procedure (mirror-clone approach)

1. Fresh `git bundle create … --all` backup at HEAD `897d851` (sha256 recorded,
   `git bundle verify` clean).
2. `git clone --mirror` of the live repo → throwaway bare mirror.
3. `git filter-repo --invert-paths --paths-from-file scrub-allowlist.txt --force`
   in the mirror. **426 commits** reprocessed.
4. Local verification in the mirror (below).
5. Force-push rewritten `main` + tags to private `origin`.
6. Re-sync the live checkout; prune old objects.
7. Delete the throwaway mirror + scratch path-lists.

## Before / after evidence (path-complete pathname universe)

Enumeration = union of `git ls-tree -r --name-only <commit>` over **every
reachable commit**, `sort -u` (not the `rev-list --objects` surrogate, which
blob-dedups pathnames — the failure mode the Session-2 verifier caught).

| Metric | BEFORE | AFTER |
|---|---:|---:|
| Total unique historical paths | 2,636 | 2,221¹ |
| Northwind-matching paths | 454 | 39 |
| Commits on `main` | 426 | 426 (preserved) |
| Pack size (`size-pack`) | 8.83 MiB | 8.25 MiB² |

¹ `removed = BEFORE − AFTER = 2,636 − 2,221 = 415`, and **every one** of those
415 is matched by an allowlist entry (`removed NOT matched by allowlist = 0`).
Set-equality `expected_after == after` held exactly: **0 keep paths lost, 0
allowlisted paths survived**. Verbatim measured results from the in-mirror check
(Python over the two `ls-tree`-union path lists):

```
BEFORE total: 2636 | AFTER total: 2221 | removed: 415
removed NOT matched by allowlist (must be 0): 0
expected_after == after : True
  keep paths lost (must be 0): 0
  allowlisted survived (must be 0): 0
matched_by_allowlist total in removed: 415
```

² Net pack delta is small because filter-repo rewrites all 426 commits,
creating many new tree/commit objects that partially offset the removed
proprietary blobs (object count 3,186 → 7,041); the proprietary blobs are
nonetheless fully gone (per-path `rev-list --objects = 0`).

> The +10 northwind paths vs. the Session-2 dry-run (which saw 444 / 415 / 29)
> are the Session-2 closeout files committed into
> `docs/session-sets/003-northwind-proprietary-data-remediation/` and the new
> `docs/session-sets/004-northwind-symbol-db-sqlite-residue/` folder — **all in
> KEEP directories**, none on the allowlist. Surviving 39 = the 29 original
> keeps + these 10. Removed stayed exactly 415.

Per-allowlist-path emptiness (each of the 9 entries): `git log --all -- <path>`
= 0 and `git rev-list --all --objects | grep <path>` = 0.

Keep-set spot checks still present after rewrite: the `003`/`004` session-set
folders, `generalization-validation-on-non-northwind/`,
`docs/homehealthcare-northwind-comparison.md`,
`tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs`,
`samples/GoldenOutput/HomeHealthCare/`, and the deferred-residue file
`tools/symbol-db/symbol-db.sqlite` (intentionally **not** on this set's
allowlist — handled by set `004`).

## Force-push confirmation

Pushed to `https://github.com/darndestdabbler/dabbler-access-harvester.git`
(private). Old → new ref SHAs:

| Ref | Before | After |
|---|---|---|
| `refs/heads/main` | `897d851` | `87844e3` |
| `refs/tags/v1.0.0` | `81fb14a` | `842ff1b` |
| `refs/tags/v1.1.0` | `0ccdd72` | `991260f` |
| `refs/tags/v1.2.0` | `e8ec779` | `800fe15` |

Independent `git ls-remote` after the push confirms all four refs at the
rewritten SHAs.

**Auth note (operational, for future destructive sessions):** a fresh
`--mirror` clone inherits the global Git Credential Manager helper, which under
this non-interactive shell **hung** (it tries to pop a GUI dialog;
`GIT_TERMINAL_PROMPT=0` suppresses only git's built-in terminal prompt, not
GCM). The reliable push was via the gh-CLI token as the *sole* credential
helper, resetting the inherited list first:
`git -c credential.helper= -c credential.helper='!gh auth git-credential' push …`
(gh is authenticated with a `repo`+`workflow` token).

## Checkout re-establishment (plain single-worktree)

`git fetch --force --tags --prune origin` → `git reset --hard origin/main`
(`87844e3`) → `git reflog expire --expire=now --all` → `git gc --prune=now`.

Post-state of the live checkout:
- `HEAD` = `87844e3`; working tree **clean**.
- Old pre-rewrite commit `897d851` is **gone** (`git cat-file -e` fails).
- **0** proprietary objects in `git rev-list --all --objects`.
- **0** tracked proprietary paths at `HEAD`; 23 keep-paths still tracked.
- Developer's **local untracked** copies preserved on disk: the 406-file
  `samples/GoldenOutput/NorthwindDevEdition/` golden tree and the 12.2 MB
  `samples/NorthwindDevEdition.accdb` (both gitignored — `reset --hard` does
  not touch untracked/ignored files).
- Throwaway mirror + scratch path-list files deleted (they held proprietary
  path names as unreachable pre-gc objects).

## GitHub-side unreachable-object caveat

A force-push does **not** immediately remove the old objects from GitHub's
server-side storage. Until GitHub garbage-collects the repository:

- The pre-rewrite commits (and the proprietary blobs they reference) remain on
  GitHub's servers as **unreachable** objects. They are not listed in the UI,
  not included in a normal `clone`/`fetch`, and not reachable from any branch
  or tag — but a party who already knows an **exact old commit SHA** may be
  able to fetch it by hash for a window.
- GitHub runs repository GC on its own schedule (and prunes unreachable objects
  after it does). There is no public command to trigger it; to force immediate
  server-side GC, **contact GitHub Support** and request a `git gc` /
  repository-maintenance run on this repo.
- Things that can *keep* old objects reachable on the server: open pull
  requests (`refs/pull/*`), forks, and releases pinned to an old commit. This
  repo is **private with no forks**; the `v1.0.0/v1.1.0/v1.2.0` releases were
  re-pointed by the tag force-push (the Release objects survive — they are
  keyed by tag *name*, and their zip assets are build output independent of the
  repo tree). No open PRs were referencing the scrubbed paths.

**Risk posture:** the repo is private and the data was never publicly exposed;
the maintainer's standing policy is simply that proprietary DB artifacts do not
belong in the repo. The forward removal (set `002`/`003` Session 1) + this
history scrub satisfy that policy for the working tree and for all *reachable*
history immediately; the server-side unreachable window is the only residual,
and is closed by GitHub's GC (optionally expedited via Support).

## Backups (retained, outside the repo)

`C:/temp/dabbler-access-harvester-backups/`:
- `dabbler-access-harvester-pre003-rewrite.bundle` (+ `.sha256`) — fresh, HEAD
  `897d851`, the authoritative pre-rewrite restore point.
- `dabbler-access-harvester-pre003.bundle` (+ `.sha256`) — Session-2 bundle,
  HEAD `44122f7` (kept as the earlier checkpoint).

Restore (if ever needed): `git clone <bundle> <dir>` reproduces the full
pre-rewrite history including the scrubbed paths.
