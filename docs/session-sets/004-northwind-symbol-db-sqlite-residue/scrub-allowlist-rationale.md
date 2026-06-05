# Scrub Allowlist Rationale - 004 Northwind Symbol-DB SQLite Residue

> **Companion to** [scrub-allowlist.txt](scrub-allowlist.txt), the exact
> `git filter-repo --invert-paths --paths-from-file` input for Session 3.
> **Authored:** Session 2 (2026-06-05). **Paths + rationale only - no
> proprietary content.**

## Method

The allowlist is based on the exact historical pathname universe for the
`tools/symbol-db/` subtree, not on a name-match heuristic.

- Historical universe: union of `git ls-tree -r --name-only <commit>` over every
  reachable commit, filtered to `tools/symbol-db/`, then `sort -u`.
- Result: **47 unique historical paths** under `tools/symbol-db/`.
- Bucketed disposition:
  - **SCRUB:** 7 paths
  - **KEEP:** 40 paths

The 7 scrub paths are all proprietary NorthwindDevEdition-derived data
artifacts. The 40 keep paths are generic tooling, tests, docs, config, or code.

Negative checks, run against reachable history before authoring the allowlist:

- `tools/symbol-db/measurement-northwind.md` is **not** in history.
- The raw `BuildNorthwind()` fixture sources under
  `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/kb/` are **not** in
  history.

Those local-only paths are therefore excluded from the history rewrite input.

## Scrub entries - per-path rationale

All seven allowlist entries are exact file paths. No directory prefix is used,
because `tools/symbol-db/` also contains generic committed tooling that must
survive the rewrite.

| Allowlist line | Why scrub |
|---|---|
| `tools/symbol-db/symbol-db.sqlite` | The proprietary symbol database. It serializes NorthwindDevEdition module/source structure and downstream interpretations. |
| `tools/symbol-db/consistency/grades.sqlite` | The proprietary consistency-grading database derived from the same Northwind-backed symbol corpus. |
| `tools/symbol-db/consistency/correlation-summary.json` | Derived analysis output computed from proprietary Northwind-backed grading rows. |
| `tools/symbol-db/consistency/sample.json` | Derived sample manifest pointing at proprietary Northwind-backed symbol/interpretation rows. |
| `tools/symbol-db/consistency/sample-report.md` | Human-readable report emitted from the proprietary Northwind-backed sample set. |
| `tools/symbol-db/consistency/correlation-report.md` | Human-readable correlation/calibration report derived from proprietary Northwind-backed grading data. |
| `tools/symbol-db/consistency/correlation-scatter.png` | Visualization derived from proprietary Northwind-backed grading data. |

## Keep list - explicitly preserved categories

The rewrite must preserve the generic symbol-db tooling. The 40 kept historical
paths fall into these categories:

- `tools/symbol-db/consistency/**/*.py` and `tools/symbol-db/consistency/tests/**/*.py`
  - generic samplers, graders, correlation logic, and unit tests.
- `tools/symbol-db/interpret/**/*.py`, prompt templates, and tests
  - generic interpretation pipeline code and prompts.
- `tools/symbol-db/probe/**/*.py`
  - generic probe-building and probe-runner code.
- `tools/symbol-db/probe-builder/*`
  - committed C# probe-builder utility.
- `tools/symbol-db/interpretation-config.json`
  - generic thresholds/policy configuration; not proprietary data.
- `tools/symbol-db/README.md`
  - repository documentation describing the local-only data policy.

## Explicit exclusions from the allowlist

These paths are intentionally **not** in `scrub-allowlist.txt`:

- `tools/symbol-db/measurement-northwind.md`
  - local-only on disk but never tracked in reachable history, so there is
    nothing for `git filter-repo` to purge.
- `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/kb/Forms/frmCompanyDetail.json`
  - local-only fixture source; not in reachable history.
- `tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/kb/ModulesAndClasses/modStrings.json`
  - local-only fixture source; not in reachable history.

## Boundary checks

- **Exact-path only allowlist.** A subtree prefix such as `tools/symbol-db/` or
  `tools/symbol-db/consistency/` would over-scrub generic tooling, tests, and
  config. Every allowlist line is therefore an exact file path.
- **No fixture-subtree rewrite in this set.** The `BuildNorthwind()` raw fixture
  sources remain out of scope for history rewrite because the reachable-history
  checks were empty. They are local-only current-filesystem inputs, not tracked
  git history.
- **No `measurement-northwind.md` rewrite.** Forward-ignore is correct, but a
  history rewrite entry would be a false-positive because the file never landed
  in reachable history.

## Dry-run proof

Session 2 dry-ran the real rewrite command on a throwaway `--mirror` clone using
the authored allowlist and then proved exactness over the **full historical
pathname universe** (whole repo, not just `tools/symbol-db/`).

- `git-filter-repo` package version: **2.47.0** (workspace `.venv`).
- The dry-run used the `safe.bareRepository=all` env override carried forward
  from set `003`. The current direct `git config --get safe.bareRepository`
  probe in this worktree returned empty, so the override is recorded here as a
  conservative compatibility shim, not as a freshly re-proven host requirement.

Measured results from the throwaway mirrors:

| Metric | Value |
|---|---:|
| BEFORE unique historical paths | 2235 |
| AFTER unique historical paths | 2228 |
| Removed paths (`BEFORE - AFTER`) | 7 |
| Allowlist lines | 7 |
| Keep paths unexpectedly lost | 0 |
| Allowlist paths that survived | 0 |
| `expected_after == after` | True |
| BEFORE reachable commit count | 432 |
| AFTER reachable commit count | 432 |

Additional controls:

- Tag `v1.2.0` still resolves after the rewrite rehearsal (pre-rewrite
  `800fe15abe1c`, post-rewrite `a712e8769077`; expected hash change, tag still
  live).
- The removed-path set is exactly the seven allowlist entries and nothing else.

Removed paths in the dry-run:

- `tools/symbol-db/symbol-db.sqlite`
- `tools/symbol-db/consistency/grades.sqlite`
- `tools/symbol-db/consistency/correlation-summary.json`
- `tools/symbol-db/consistency/sample.json`
- `tools/symbol-db/consistency/sample-report.md`
- `tools/symbol-db/consistency/correlation-report.md`
- `tools/symbol-db/consistency/correlation-scatter.png`

## Verified Session 3 command sequence

Session 3 should reuse the measured command shape below, swapping in a fresh
throwaway mirror path and a fresh pre-rewrite backup bundle at the live rewrite
HEAD.

```powershell
$worktree = (Get-Location).Path
$allowlist = Join-Path $worktree 'docs\session-sets\004-northwind-symbol-db-sqlite-residue\scrub-allowlist.txt'
$mirror = 'C:\path\to\throwaway\004-rewrite.git'

git clone --mirror . $mirror

$env:PATH = "$worktree\.venv\Scripts;$env:PATH"
$env:GIT_CONFIG_COUNT = '1'
$env:GIT_CONFIG_KEY_0 = 'safe.bareRepository'
$env:GIT_CONFIG_VALUE_0 = 'all'

git -C $mirror filter-repo --invert-paths --paths-from-file $allowlist --force
```

Measured exactness-check shape from Session 2:

```powershell
function Get-UniquePaths([string]$repo) {
    $paths = @()
    $commits = git -C $repo rev-list --all
    foreach ($commit in $commits) {
        $paths += git -C $repo ls-tree -r --name-only $commit
    }

    $paths | Where-Object { $_ } | Sort-Object -Unique
}

$beforePaths = @(Get-UniquePaths $beforeMirror)
$afterPaths = @(Get-UniquePaths $afterMirror)
$allowlistPaths = @(Get-Content $allowlist | Where-Object { $_ -and -not $_.StartsWith('#') })

$removed = @($beforePaths | Where-Object { $_ -notin $afterPaths })
$keepLost = @($removed | Where-Object { $_ -notin $allowlistPaths })
$allowlistSurvived = @($allowlistPaths | Where-Object { $_ -in $afterPaths })
$expectedAfter = @($beforePaths | Where-Object { $_ -notin $allowlistPaths })

# Session-2 success criteria:
# - $removed.Count == 7
# - $keepLost.Count == 0
# - $allowlistSurvived.Count == 0
# - $expectedAfter and $afterPaths are set-equal
```

Session 3 should also re-check:

- `git -C <mirror> rev-list --count --all` before vs after
- `git -C <mirror> rev-parse refs/tags/v1.2.0`
- `git log --all -- <allowlist-path>` is empty for every allowlist entry