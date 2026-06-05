# History-Rewrite Record — 004 Northwind Symbol-DB / SQLite Residue (Session 3)

> **Destructive, outward-facing operation executed 2026-06-05** under explicit
> maintainer go-ahead. This document records the allowlist used, before/after
> evidence, the force-push confirmation, the checkout re-establishment, and the
> GitHub-side unreachable-object caveat. Paths + SHAs only — no proprietary
> content.

## Coordination with `003` (Stop-Rule resolution)

The spec's Stop Rule preferred merging this set's allowlist into `003` Session 3
so the private remote would take **one** force-push. At execution time `003` was
already **fully complete** — its Session 3 history rewrite + force-push ran
earlier on 2026-06-05 (all three `003` sessions `complete`). The combined-single-
pass option was therefore no longer available, so `004` ran its own
**sequential second rewrite**, which the Stop Rule explicitly permits once `003`
has already rewritten history. This is the unavoidable second of two force-pushes.

## Pre-flight gate (all satisfied before any destructive step)

| Gate | Result |
|---|---|
| (a) Explicit maintainer go-ahead **at the moment of the rewrite** | ✅ given ("authorized to go ahead with the rewrite and force-push") |
| (b) Verified restorable backup bundle | ✅ Session-2 bundle (`pre004-session2-9d07baa4b3cf.bundle`, HEAD `9d07baa`) + a **fresh** bundle taken this session at HEAD `9bba62f` (`pre004-session3-9bba62fc5e4d.bundle`), sha256-recorded, `git bundle verify`-clean, and restore-tested (clone resolved to `9bba62f`, v1.2.0 + an allowlist path present in restored history) |
| (c) `v1.2.0` release published on stable history | ✅ tag on remote (`800fe15`, local == remote) prior to the rewrite |
| (d) Working tree clean & pushed | ✅ clean; local `main` == `origin/main` (`9bba62f`) |
| (e) No other session set in-flight | ✅ `003` complete; single-worktree checkout, no parallel sets |

**Layout reconciliation:** the spec's Step 2/Step 5 assume the bare-repo +
flat-worktree layout (`.bare/` + multiple worktrees). The actual repo is a
**plain single-worktree checkout** with one branch (`main`)
(`git rev-parse --is-bare-repository` → `false`; `git worktree list` → one
entry at the container root). So "operate on `.bare/`" → operate on a throwaway
`--mirror` clone; "re-establish each worktree" → re-sync the single checkout.
This matches the layout-drift lesson carried forward from `003` Session 3.

## Tooling

- `git-filter-repo` **2.47.0** (pip, in workspace `.venv`; `pip show` + module
  import confirm the version. `git filter-repo --version` prints the tool's
  internal source id `a40bce548d2c`, which is expected, not a version string).
- Host sets `safe.bareRepository=explicit` (confirmed active this session — a
  bare read failed without the override), so all operations against the bare
  mirror used the env override
  `GIT_CONFIG_COUNT=1 / GIT_CONFIG_KEY_0=safe.bareRepository / GIT_CONFIG_VALUE_0=all`.

## Allowlist used

`scrub-allowlist.txt` — **7 exact file paths**, no directory prefixes (a subtree
prefix would over-scrub the generic tooling, tests, and config that share
`tools/symbol-db/`). Unchanged from the Session-2 cross-provider-VERIFIED file.
See `scrub-allowlist-rationale.md` for per-path rationale and the 40-path keep
list.

```
tools/symbol-db/symbol-db.sqlite
tools/symbol-db/consistency/grades.sqlite
tools/symbol-db/consistency/correlation-summary.json
tools/symbol-db/consistency/sample.json
tools/symbol-db/consistency/sample-report.md
tools/symbol-db/consistency/correlation-report.md
tools/symbol-db/consistency/correlation-scatter.png
```

## Procedure (mirror-clone approach)

1. Fresh `git bundle create … --all` backup at HEAD `9bba62f` (sha256
   `d5b4b80030210ee55d64f0c46c8d49d89c50275273d5afc3fdc916d06ba20818`,
   `git bundle verify` clean, restore-tested).
2. `git clone --mirror .` of the live repo → throwaway bare mirror
   (`C:\tmp\004-rewrite.git`).
3. `git filter-repo --invert-paths --paths-from-file scrub-allowlist.txt --force`
   in the mirror. **434 commits** reprocessed (`Parsed 434 commits`).
4. Local verification in the mirror (below).
5. Force-push rewritten `main` + tags to private `origin`.
6. Re-sync the live checkout; expire reflog; prune old objects.
7. (Closeout) delete the throwaway mirror + scratch path-lists.

## Before / after evidence (path-complete pathname universe)

Enumeration = union of `git ls-tree -r --name-only <commit>` over **every
reachable commit**, `sort -u` (not the `rev-list --objects` surrogate, which
blob-dedups pathnames — the failure mode the `003` Session-2 verifier caught).

| Metric | BEFORE | AFTER |
|---|---:|---:|
| Total unique historical paths | 2,238 | 2,231 |
| Reachable commit count (`rev-list --count --all`) | 434 | 434 |
| `v1.2.0` resolves | `800fe15` | `a712e87` (expected hash change; tag still live) |
| `main` | `9bba62f` | `1ee4102` |

Exactness proof (Python set algebra over the two universes):

- removed = BEFORE − AFTER = **exactly the 7 allowlist paths**, nothing else.
- keep paths unexpectedly lost: **0**
- allowlist paths that survived: **0**
- paths added: **0**
- `expected_after (BEFORE − allowlist) == AFTER`: **True**
- `removed set == allowlist set`: **True**

Per-path `git log --all -- <path>` is **empty (0 commits)** for all 7 entries in
both the rewritten mirror and the re-synced live checkout. Keep-paths confirmed
intact at HEAD (`tools/symbol-db/interpretation-config.json`,
`tools/symbol-db/README.md`, `tools/symbol-db/consistency/correlation.py`).

The post-rewrite `v1.2.0` hash `a712e87` matches the Session-2 dry-run's
post-rewrite hash, an independent consistency check that the same allowlist
produced the same rewrite.

## Force-push confirmation

Pushed from the rewritten mirror to private `origin` using **gh as the sole
credential helper** (`-c credential.helper= -c credential.helper='!gh auth
git-credential'`) to avoid the headless GCM-GUI hang documented in `003`
Session 3. Result (all forced updates, no hang):

```
 + 9bba62f...1ee4102 main   -> main   (forced update)
 + 842ff1b...3fe60bd v1.0.0 -> v1.0.0 (forced update)
 + 991260f...2b0c717 v1.1.0 -> v1.1.0 (forced update)
 + 800fe15...a712e87 v1.2.0 -> v1.2.0 (forced update)
```

Post-push `git ls-remote` confirms `origin` `main` = `1ee4102`, `v1.2.0` =
`a712e87`.

## Checkout re-establishment

`git fetch origin --force --tags --prune` → `git reset --hard origin/main` →
`git reflog expire --expire=now --all` → `git gc --prune=now`. The live checkout
now sits on `1ee4102` with a 2,231-path universe.

Per the `003` lesson, `reset --hard` reverted the tracked session-state files
(they live under `docs/session-sets/.../`), silently undoing the Step-0
`register_session_start`. `register_session_start` was therefore **re-run after
the reset** (session 3 → `in-progress`), and the closeout commit/push precedes
`mark_session_complete`. The developer's local symbol-db copies are gitignored,
so the reset preserved them on disk (untracked, not erased).

## GitHub unreachable-object caveat

Force-pushing rewritten refs does **not** immediately purge the old (pre-rewrite)
commits and blobs from GitHub's server-side storage. The old objects become
**unreachable** but can linger:

- They are no longer reachable from any ref, so a fresh `git clone` will not
  fetch them and they will not appear in history.
- GitHub may still serve a specific old object by its exact 40-char SHA via the
  web UI / API for a window after the rewrite, until GitHub's own gc runs (no
  guaranteed SLA; can be days).
- A pull request, cached view, or fork can also retain a reference to old
  objects independently of the default branch.

Because this is a **private** repository with a single maintainer and no forks
or open PRs, the residual-exposure risk is minimal. If hard server-side
expungement of the old objects is ever required within a bounded window,
the path is a GitHub Support request citing the rewritten refs (the only way to
force server-side gc of unreachable objects on github.com). The reusable backup
bundles are retained outside the repo so the pre-rewrite state remains
recoverable if needed.
