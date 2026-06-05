# Change Log — 003 Northwind Proprietary-Data Remediation

> **Status: COMPLETE** (3 of 3 sessions, cross-provider VERIFIED). Full tier; no
> UI, so no UAT/E2E. **Session 3 performed a destructive, irreversible,
> outward-facing git history rewrite + force-push** under explicit maintainer
> go-ahead.

## What this set did

Removed the proprietary **NorthwindDevEdition** Access database and its
**dedicated** harvester-derived artifacts from the repository — first from the
working tree at `HEAD`, then from **all git history** — while preserving the
developer's local (untracked) copies and leaving the synthetic HomeHealthCare
fixture fully committed. The scrub was driven by a reviewed, cross-provider-
verified 9-line allowlist (415 proprietary paths), never a blind name match.

## Session 1 — Forward Removal + Test Skip-Hardening (non-destructive)

- `git rm --cached -r` untracked the 406-file `samples/GoldenOutput/NorthwindDevEdition/`
  golden tree (kept on disk); added `.gitignore` entries.
- **Scope expansion (maintainer-approved):** the spec's premise that the
  `NorthwindDevEdition.accdb` YAML + parse-success-report mirrors were "already
  gitignored" was factually wrong — both were still tracked at `HEAD` and were
  also untracked. Total 408 deletions (406 golden + 2 mirrors).
- Added `Skip.IfNot(Directory.Exists(goldenDir), …)` guards to the Northwind-
  golden-dependent hermetic tests so a fresh clone builds + tests green without
  the local golden; no assertion weakened.
- Build + affected hermetic tests green both with and without the local golden.
  Cross-provider VERIFIED (gpt-5-4, R2 after one Minor doc-consistency fix).

## Session 2 — Curate Scrub Allowlist + Tooling + Backup (analysis/prep, no rewrite)

- Installed `git-filter-repo` 2.47.0 into the workspace `.venv`.
- Inventoried the **full** git history for `northwind` paths via path-complete
  `ls-tree` enumeration (the authoritative method; the `rev-list --objects`
  surrogate blob-dedups pathnames and under-counts — the R1/R2 verifier caught
  this). Bucketed all 444 historical northwind paths: **415 SCRUB** (collapsed to
  a 9-line allowlist) + **29 KEEP** (incidental name-matches).
- Inspected all 5 judgment-call paths and classified each **SCRUB** (example
  manifest naming the proprietary DB; two symbol-db structural disclosure docs;
  the comprehension Q&A dir = fullest structural disclosure). Independent
  cross-provider pass (gemini-pro) agreed.
- Wrote `scrub-allowlist.txt` + `scrub-allowlist-rationale.md` (paths + rationale
  only — keep-files, so proprietary identifiers described by category).
- **Embedded-residue finding (recorded, deferred):** a scan surfaced **123
  KEEP-files** (incl. two binary SQLite DBs) that embed proprietary *structure*
  but are out of scope for `003`'s narrow allowlist. Maintainer decision:
  **defer to follow-up set `004`** rather than expand `003`. See
  `embedded-residue-findings.md`.
- Took a verified, restorable full backup bundle; dry-ran the rewrite on
  throwaway `--mirror` clones and proved set-equality. Cross-provider VERIFIED
  (gpt-5-4, R3 — R1/R2 caught a count error and the rev-list-vs-ls-tree proof
  flaw, both fixed).

## Session 3 — Execute History Rewrite + Force-Push (DESTRUCTIVE — gated)

- **Pre-flight gate (all satisfied):** explicit maintainer go-ahead at the moment
  of the rewrite; verified restorable backup (Session-2 bundle + a **fresh**
  bundle at HEAD `897d851`); `v1.2.0` published to remote + GitHub Release live;
  working tree clean and in sync; no other set in-flight (`004` not-started).
- **Layout reconciliation:** the spec assumed the bare-repo + flat-worktree
  layout, but the actual repo is a **plain single-worktree checkout** (one branch).
  Reconciled: rewrite on a throwaway `--mirror` clone; re-establishment = reset +
  gc of the single checkout.
- Ran `git filter-repo --invert-paths --paths-from-file scrub-allowlist.txt`
  (filter-repo 2.47.0, `safe.bareRepository=all` override) in the mirror — 426
  commits reprocessed.
- **Verified exactness** over the path-complete pathname universe: BEFORE 2,636
  unique paths → AFTER 2,221; `removed = 415`, all 415 allowlist-matched;
  `expected_after == after` (0 keep lost, 0 scrub survived); per-path
  `git log --all` and `rev-list --objects` = 0 for every allowlist entry; tags
  resolve; commit count preserved (426); keep-set intact.
- **Force-pushed** rewritten `main` + tags to the private remote:
  `main 897d851→87844e3`, `v1.0.0 81fb14a→842ff1b`, `v1.1.0 0ccdd72→991260f`,
  `v1.2.0 e8ec779→800fe15`. (Auth: the fresh-clone GCM helper hung under the
  non-interactive shell; pushed via gh as the sole credential helper.)
- **Re-established** the live checkout: `fetch --force --tags --prune` →
  `reset --hard origin/main` → `reflog expire --all` → `gc --prune=now`. Old
  commit `897d851` pruned; **0 proprietary objects** remain in local all-history;
  developer's local untracked copies preserved (406 golden files + 12.2 MB
  `.accdb`). Throwaway mirror + scratch path-lists deleted.
- **GitHub caveat recorded** (`rewrite-record.md`): force-push leaves old objects
  as server-side unreachable until GitHub GC; exact-SHA fetch window; private repo
  / no forks / Release survives tag re-point; Support can expedite GC.
- Cross-provider VERIFIED (gpt-5-4): R1 ISSUES_FOUND (one Major — a transcription
  typo in the evidence table, AFTER total written as 2,211 instead of the measured
  2,221; underlying rewrite correct) → corrected + embedded verbatim measured
  results → **R2 VERIFIED**.

## Done Looks Like — final state

- ✅ No NorthwindDevEdition-derived **dedicated** artifact is tracked at `HEAD`;
  paths gitignored; local copies preserved.
- ✅ Build + hermetic tests green; Northwind-golden-dependent tests skip cleanly
  when the local golden is absent.
- ✅ Reviewed allowlist + rationale committed; full verified backup taken.
- ✅ After the rewrite: every allowlisted path is absent from all history; refs +
  tags force-pushed to the private remote; the single checkout re-established on
  the rewritten history; procedure (incl. GitHub caveat) recorded.

## Known remaining work (out of scope, tracked)

The ~123-file embedded-residue footprint (incl. the two binary SQLite DBs) still
discloses proprietary Northwind structure and is **deferred to set `004`** by
maintainer decision. Until `004` lands, `main` retains structural disclosure — so
**no new release should be cut until `004` completes** (see
`next-session-set-recommendation.md`).

## Artifacts created this set

`scrub-allowlist.txt`, `scrub-allowlist-rationale.md`, `embedded-residue-findings.md`,
`rewrite-record.md`, `change-log.md`, `next-session-set-recommendation.md`,
`disposition.json`, the `s1`/`s2`/`s3` verification + issue files, and the two
external backup bundles under `C:/temp/dabbler-access-harvester-backups/`
(not committed).
