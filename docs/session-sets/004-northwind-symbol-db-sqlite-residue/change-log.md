# Change Log — 004 Northwind Symbol-DB / SQLite Residue Remediation

> Session set complete (3 of 3 sessions). This set removed the proprietary
> NorthwindDevEdition-derived symbol-DB SQLite binaries and their derived
> consistency artifacts that `003` deliberately left in place — first from the
> working tree, then from all git history via a reviewed `git filter-repo`
> rewrite. Paths + SHAs only; no proprietary content.

## Outcome

The 7 proprietary symbol-DB data artifacts are gone from `HEAD` and from **all
git history**, gitignored so a fresh run cannot re-commit them, with the
developer's local copies preserved. The generic `tools/symbol-db/` tooling,
config, and C# code survive untouched. The rewritten history is force-pushed to
the private remote; the local checkout is re-established on it. Every session was
cross-provider VERIFIED.

## Session-by-session

### Session 1 — Forward Removal + Test/Tooling Skip-Hardening (non-destructive) — VERIFIED (Opus 4.8)
- Inventoried the tracked-at-HEAD symbol-DB footprint; classified scrub (7
  proprietary-derived data files) vs keep (40 generic tooling/test/config paths).
- `git rm --cached` the 7 artifacts (local copies retained on disk); added
  `.gitignore` entries pointing at this set.
- Skip-hardened the symbol-DB-data-dependent hermetic tests so they skip cleanly
  when the artifact is absent, without weakening any assertion that still runs
  when it is present.
- Build green; affected tests green both with the local artifacts present
  (assertions run) and moved aside (skips fire).

### Session 2 — Curate Allowlist + Backup + Dry-Run (analysis/prep, no rewrite) — VERIFIED (gpt-5-4 via opus override)
- Authored the exact **7-path** scrub allowlist (`scrub-allowlist.txt`) +
  `scrub-allowlist-rationale.md` (exact-path only — no directory prefix — to avoid
  over-scrubbing the shared `tools/symbol-db/` subtree); confirmed each path in
  reachable history and documented the local-only exclusions.
- Took an out-of-repo backup bundle at HEAD `9d07baa`, sha256-recorded,
  `git bundle verify`-clean, restore-tested.
- Dry-ran the real rewrite on throwaway `--mirror` clones and proved exactness over
  the path-complete pathname universe (BEFORE 2235 → AFTER 2228, removed = the 7
  allowlist paths, 0 keep lost, 0 survivors, commits preserved 432 → 432).

### Session 3 — Execute History Rewrite + Force-Push (DESTRUCTIVE, gated) — VERIFIED (gpt-5-4)
- **Pre-flight gate** all satisfied: explicit maintainer go-ahead at the moment of
  the rewrite; fresh restore-tested backup at live HEAD `9bba62f`
  (`pre004-session3-9bba62fc5e4d.bundle`, sha256
  `d5b4b800…ba20818`); `v1.2.0` published; tree clean & pushed; no other set in
  flight.
- **003 coordination:** `003` was already fully complete (its history rewrite ran
  earlier 2026-06-05), so the combined-single-pass option was gone and `004` ran a
  **sequential second rewrite**, which the Stop Rule permits.
- **Layout:** plain single-worktree checkout (doc drift carried from `003`) →
  rewrote on a throwaway `--mirror` clone; re-established via
  `fetch --force` + `reset --hard` + `gc --prune=now`.
- Ran `git filter-repo 2.47.0 --invert-paths --paths-from-file scrub-allowlist.txt`
  (`safe.bareRepository=all` override). **434 commits** reprocessed.
- **Exactness proof** over the path-complete `ls-tree` universe: BEFORE **2238** →
  AFTER **2231**; removed == exactly the 7 allowlist paths; 0 keep lost; 0
  survivors; 0 added; `expected_after == after`; commits preserved 434 → 434;
  `git log --all -- <path>` empty for all 7. Post-rewrite `v1.2.0` = `a712e87`
  (matches the S2 dry-run hash — independent consistency check).
- **Force-pushed** rewritten `main` (`9bba62f` → `1ee4102`) + all 3 tags to the
  private remote, via `gh` as the sole credential helper (avoids the headless GCM
  hang from `003`). Re-established the live checkout; re-registered session state
  after `reset --hard` reverted the tracked state files (documented `003` lesson).
- Recorded the GitHub server-side **unreachable-object caveat** in
  [`rewrite-record.md`](rewrite-record.md).

## Artifacts produced

- [`rewrite-record.md`](rewrite-record.md) — full procedure, before/after evidence,
  force-push confirmation, layout + re-establishment log, GitHub caveat.
- [`scrub-allowlist.txt`](scrub-allowlist.txt) / [`scrub-allowlist-rationale.md`](scrub-allowlist-rationale.md) — the reviewed 7-path allowlist + per-path rationale + keep list.
- `s1/s2/s3-verification.md` — raw cross-provider verifier output (never edited).
- [`next-session-set-recommendation.md`](next-session-set-recommendation.md) —
  routed recommendation for `005-embedded-residue-redaction` (the remaining
  HIGH/MEDIUM/LOW prose/test/name-only residue + C# residue question).
- Out-of-repo backup bundles (not committed): `pre004-session2-9d07baa4b3cf.bundle`,
  `pre004-session3-9bba62fc5e4d.bundle` (+ `.sha256`).

## What remains (deferred to a follow-on set)

The HIGH/MEDIUM/LOW embedded-residue buckets (probe/translation/workflow docs,
characterization tests, name-only references that disclose proprietary *structure*)
and the C# symbol-DB source/test residue question are **out of scope** for this set
and are the subject of the routed `005-embedded-residue-redaction` recommendation.
Until they are remediated, the proprietary-data remediation is not fully complete
and new releases should remain blocked.
