# Scrub Allowlist Rationale — 003 Northwind Proprietary-Data Remediation

> **Companion to** [`scrub-allowlist.txt`](scrub-allowlist.txt), the exact
> `git filter-repo --invert-paths --paths-from-file` input for Session 3.
> **Authored:** Session 2 (2026-06-05). **Paths + rationale only — no
> proprietary content.**

## Method

A case-insensitive search for `northwind` across **all** git history
(`git log --all --pretty=format: --name-only | grep -i northwind | sort -u`)
returned **444 unique paths**. Every path was bucketed; the buckets sum exactly
to 444 with an empty catch-all, so the universe is fully accounted for:

| Bucket | Count | Disposition |
|---|---:|---|
| `samples/GoldenOutput/NorthwindDevEdition/**` | 406 | **SCRUB** (harvester golden output of the proprietary DB) |
| `samples/` other (`.accdb`, `.laccdb`, FormsAndReportsYaml `.yaml`, parse-success-report `.json`) | 4 | **SCRUB** (binary + lock + derived mirrors) |
| `examples/northwind-output.json` | 1 | **SCRUB** (judgment call — see below) |
| `tools/symbol-db/*-northwind.md` | 2 | **SCRUB** (judgment call — see below) |
| `docs/test-cases/comprehension/NorthwindDevEdition/**` | 2 | **SCRUB** (judgment call — see below) |
| `docs/session-sets/generalization-validation-on-non-northwind/**` | 18 | **KEEP** (incidental slug match) |
| `docs/session-sets/003-northwind-proprietary-data-remediation/**` | 9 | **KEEP** (this remediation set itself) |
| `docs/homehealthcare-northwind-comparison.md` | 1 | **KEEP** (fixture-selection rationale doc) |
| `tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs` | 1 | **KEEP** (test source referencing the fixture by name) |
| **Total** | **444** | |

The scrub set is therefore **415 paths** (406 + 4 + 1 + 2 + 2) collapsed to
**9 allowlist lines** (the 406-file golden tree and the 2-file comprehension dir
are each one directory prefix).

> Note on path-counting method: the 444/415/29 figures come from the
> **path-complete** historical pathname universe — the union of
> `git ls-tree -r --name-only <commit>` over every reachable commit, `sort -u`.
> This is the enumeration the dry-run proof below uses, and it agrees exactly
> with the bucket math: 444 northwind paths in history, 415 removed by the
> allowlist, 29 kept. (An earlier draft used `git rev-list --all --objects` as a
> surrogate; that enumerates *objects*, annotating each blob with only one
> pathname, so content-identical files at different paths collapse — it
> under-reported 444→346 and 415→312. The round-1/round-2 verifier correctly
> flagged this; the proof below is re-anchored on the `ls-tree` path-complete
> universe.)

## Scrub entries — per-path rationale

### Seed set (unambiguous)

| Allowlist line | Why scrub |
|---|---|
| `samples/NorthwindDevEdition.accdb` | The proprietary database binary itself (tracked across ~19 commits before being gitignored). |
| `samples/NorthwindDevEdition.laccdb` | Stray Access lock file for the proprietary DB. |
| `samples/GoldenOutput/NorthwindDevEdition/` | The harvester's full structural + enriched output (`ir/`, `kb/`, reports-baseline, usage, cleanup, manifest) for the proprietary DB — 406 files. |
| `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml` | SaveAsText-derived forms/reports YAML dump of the proprietary DB. |
| `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json` | Parse-success report enumerating the proprietary DB's modules/procedures. |

### Judgment calls (contents inspected in Session 2)

All four judgment-call path-classes were classified **SCRUB**. An independent
cross-provider reasoning pass (`task_type=analysis`, gemini-pro, see
`router-metrics.jsonl` for this session) agreed on all five paths with no
uncertainty. The borderline case is `examples/northwind-output.json`.

Proprietary identifiers and expressions are described **by category only** below
— they are deliberately not reproduced verbatim, since this rationale doc is a
keep-file that survives the Session 3 rewrite.

| Allowlist line | Contents found (described by category) | Decision |
|---|---|---|
| `examples/northwind-output.json` | A **DatabaseManifest** skeleton whose `Database.Name` is the proprietary `.accdb` filename. All counts are `0` and all arrays are empty (forms/reports/queries/modules/tables). **Borderline:** it carries no proprietary object names or values — only the proprietary DB *name* and the manifest schema shape. | **SCRUB.** It is a (degenerate) harvester manifest *of* the proprietary DB and names the proprietary file. The maintainer's standing policy is that proprietary DB artifacts — including derived metadata — do not belong in the repo at all. A schema-shape example should instead be cut from the synthetic HomeHealthCare fixture. |
| `tools/symbol-db/interpretation-summary-northwind.md` | A symbol-DB interpretation metrics report that discloses real proprietary form/module names, procedure and member identifiers, and structural counts (symbol / table / query / enum-value totals) for the proprietary DB. | **SCRUB.** Reveals the proprietary DB's internal structure and object names — derived metadata, not an incidental name-match. |
| `tools/symbol-db/measurement-northwind.md` | A symbol-DB measurement report with per-module structural breakdowns of the proprietary DB (real module names with their symbol / procedure / module-var / enum-value counts). | **SCRUB.** Same class as above — discloses proprietary module names and per-module structure counts. |
| `docs/test-cases/comprehension/NorthwindDevEdition/` (dir: `questions.draft.raw.txt` + `questions.draft.yaml`) | 20 comprehension Q&A items that disclose the proprietary DB's real form / report / query / table / module / control names, control-source formula expressions, validation rules, and startup / ribbon / event-handler logic. | **SCRUB.** The fullest structural disclosure of the proprietary DB in the repo. Directory prefix is safe (see boundary check). |

## Keep list — incidental name-matches explicitly NOT scrubbed

These contain **no** proprietary-derived metadata and are preserved verbatim:

- `docs/session-sets/generalization-validation-on-non-northwind/**` (18 paths) —
  a session-set whose slug merely contains "non-northwind"; predominantly
  workflow/process docs. The packaged scenario inside it
  (`packages/add-a-client.md`) is built on a *different* (non-Northwind) fixture.
  It is KEPT as an incidental slug-match. **Caveat:** one file in this subtree —
  `inventory.md` — incidentally names two Northwind forms as *comparison
  analogues* for the HHC fixture, so this subtree is not 100% free of Northwind
  identifiers. That single incidental reference is captured in
  `embedded-residue-findings.md` (LOW bucket) and is out of scope to rewrite here;
  it does not change the keep decision.
- `docs/session-sets/003-northwind-proprietary-data-remediation/**` (9 paths) —
  this remediation set itself (spec, state, verification, and this allowlist).
  Scrubbing it would erase the record of the remediation.
- `docs/homehealthcare-northwind-comparison.md` — a fixture-selection rationale
  doc comparing the two fixtures; references Northwind by name only.
- `tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs` — C# test
  source that references the fixture by name (the fixture is local-only; the test
  is `[SkippableFact]`-gated, hardened in Session 1).

## Boundary checks (why directory prefixes are safe / why exact paths elsewhere)

Verified against full history in Session 2:

- **`docs/test-cases/comprehension/NorthwindDevEdition/` — dir prefix OK.** The
  `comprehension/` parent also holds `HomeHealthCare/` and `scenarios.md`; the
  `NorthwindDevEdition/` subdir holds **only** the two proprietary files, so the
  prefix removes nothing else.
- **`examples/northwind-output.json` — exact path required.** `examples/` also
  holds `complex-form.json`, `minimal-form.json`, and
  `validate-invalid-manifest.json` (non-Northwind keepers), so a directory prefix
  would over-scrub. The exact file path is used.
- **`tools/symbol-db/*-northwind.md` — exact paths.** Only these two files under
  `tools/symbol-db/` match `northwind`; listed individually.
- **`samples/DatabaseYaml/NorthwindDevEdition.accdb/` — omitted.** Although
  present in `.gitignore` (forward-prevention), it was **never tracked** in
  history, so it is intentionally absent from the allowlist.
- **`samples/NorthwindDevEdition.accdb` vs `.laccdb`** are distinct exact paths
  with different prefixes; neither prefix-matches the other or the
  FormsAndReportsYaml `.accdb.yaml` (different parent directory).

## Dry-run exhaustiveness proof ("removes exactly the intended paths, nothing else")

The Session 2 dry-run ran the real rewrite command on a throwaway `--mirror` clone
and then proved set-equality over the **path-complete** historical pathname
universe (not sentinel spot-checks, and not the object-enumeration surrogate):

1. `BEFORE` = union of `git ls-tree -r --name-only <commit>` over every reachable
   commit of an unmodified mirror, `sort -u`: **2,626 unique paths** (444 of them
   northwind-matching).
2. `AFTER` = the same enumeration over a sibling mirror that had
   `git filter-repo --invert-paths --paths-from-file scrub-allowlist.txt --force`
   applied: **2,211 unique paths** (29 northwind-matching).
3. `removed = BEFORE − AFTER` = **415 paths**, and **every one** of those 415 is
   matched by an allowlist entry (exact file, or directory-prefix incl. the bare
   directory tree-entry). `matched_by_allowlist == 415 == removed`. This 415
   equals the curated scrub-path count exactly.
4. Set-equality check: `expected_after = {p in BEFORE : not matched_by_allowlist}`
   equals `AFTER` **exactly** (`expected_after == AFTER → True`):
   - KEEP paths unexpectedly lost: **0**
   - allowlisted paths that survived: **0**
5. All **29** northwind-matching paths surviving in `AFTER` are exactly the kept
   set: the `003-…` remediation set, the `generalization-validation-on-non-northwind`
   set, `docs/homehealthcare-northwind-comparison.md`, and
   `tests/.../NorthwindFixtureSmokeTests.cs`.
6. Independent negative controls in the same run: tags `v1.0.0/v1.1.0/v1.2.0`
   still resolve, `main` commit count is preserved (423), and the deferred-residue
   file `tools/symbol-db/symbol-db.sqlite` is still present (it is intentionally
   not on the allowlist).

The throwaway mirrors and path-list scratch files were deleted after the proof
(they held proprietary path names as unreachable pre-gc objects).

## Embedded-residue scan

See [`embedded-residue-findings.md`](embedded-residue-findings.md) for the scan
of proprietary content quoted **inside keep-files** (out of scope to rewrite in
this set; recorded as maintainer findings if any).
