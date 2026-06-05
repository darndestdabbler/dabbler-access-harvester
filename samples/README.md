# Sample Access Databases

This directory holds committed `.accdb` / `.mdb` fixtures used by the
harvester's integration tests, the symbol-DB pipeline (see
`docs/session-sets/vba-symbol-db/`), and ad-hoc developer exploration.

## Inventory

| File | Purpose | Status |
|---|---|---|
| `NorthwindDevEdition.accdb` | The reference large-fixture corpus the resolver / classifier / enrichment / symbol-DB pipelines target. **Proprietary — gitignored and local-only** (see `.gitignore`); not present on a fresh clone. Maintain a working copy locally. The binary was first gitignored on 2026-04-27 (Access mutates internal timestamps on every open); session set `003-northwind-proprietary-data-remediation` then established that **all** Northwind-derived metadata is proprietary and local-only — see the note below. | Local-only; gitignored. |
| `HomeHealthCare.accdb` | **Primary committed fixture** — the synthetic, real-world-shaped baseline a fresh clone builds and tests against (Northwind is local-only). Drives the committed golden under `samples/GoldenOutput/HomeHealthCare/`. | Stable; committed. |
| `MiniComplex.accdb` | Smaller fixture covering complex-but-narrow constructs (`With` blocks, nested subforms, etc.) used for fast iteration loops. | Stable. |
| `MasterDbPropertyExtractor.accdb` | Fixture for the property-extractor tooling under `tools/`. | Stable. |
| `FormAndReportDefaults.accdb` | Captures the default property values Access applies to forms / reports / controls; used by the property-defaults extractor. | Stable. |
| `ProjectBoard.accdb` | Mid-sized real-world-shaped fixture. | Stable. |
| `VbaParserTestCoverage.accdb` | **Initial work-in-progress.** A 3-form synthetic DB built to materialize targeted test cases for the VBA parser. May be expanded into a full synthetic-coverage corpus later, or abandoned if the existing committed `.accdb` fixtures prove sufficient. **Do not write tests that depend on this file without confirming with the human first** — its contents may change or it may be removed. | WIP / experimental. |
| `FormsAndReportsYaml/` | YAML extractions used by parts of the pipeline. The `HomeHealthCare` extract is committed; the `NorthwindDevEdition.accdb.yaml` mirror is gitignored and local-only (proprietary). | Stable. |

## NorthwindDevEdition is proprietary — local-only

`NorthwindDevEdition` is a proprietary database. Neither its `.accdb` nor any
harvester-derived metadata belongs in the repo, so all of the following are
gitignored and kept on the developer's filesystem only (regenerate locally):

- `samples/NorthwindDevEdition.accdb` (and its `.laccdb` lock file)
- `samples/DatabaseYaml/NorthwindDevEdition.accdb/`
- `samples/FormsAndReportsYaml/NorthwindDevEdition.accdb.yaml`
- `samples/parse-success-reports/NorthwindDevEdition.accdb.parse-success-report.json`
- `samples/GoldenOutput/NorthwindDevEdition/` (full structural + enriched golden tree)

These were untracked from `HEAD` in session set
`003-northwind-proprietary-data-remediation` (forward removal); that set's later
sessions scrub them from git history. The Northwind-dependent hermetic tests
(`ManifestCompletenessTests`, `MigrationFixtureCoverageTests`) skip cleanly when
the local golden is absent, so a fresh clone builds and tests green on the
**HomeHealthCare** golden alone. HomeHealthCare is synthetic and fully committed.

## Lock files (`.laccdb` / `.ldb`)

Microsoft Access creates a `.laccdb` (Access 2007+) or `.ldb` (older
formats) lock file alongside an open database. **These are transient
and must not be committed.** They are gitignored (or should be — if
one shows up in `git status`, the gitignore needs an entry).

If a `.laccdb` is present after Access has been closed, the lock was
not released cleanly. Delete it manually before continuing.

## Drift after opening files in Access

Opening an `.accdb` in Access produces two kinds of drift in `git status`:

1. **Mtime-only drift.** Access touched the modification time but did
   not change any bytes. Git's status fast-path treats this as
   "modified" until it actually reads the bytes; `git diff` shows zero
   bytes and `git hash-object <file>` matches HEAD.
2. **Real byte drift.** Access wrote a small number of bytes —
   internal metadata such as last-opened timestamps embedded inside
   the binary, statistics rebuilds, auto-format upgrades. The file
   size is identical but content hashes differ. Confirmed empirically:
   a casual open-and-close of `HomeHealthCare.accdb` produced ~454
   different bytes; `MiniComplex.accdb` produced ~27.

The two cases need different fixes:

- **For mtime-only drift,** run the timestamp-restore script. It
  rewinds the working-tree mtime to the last commit's committer date
  *only if* the file's content matches what's staged. Files with real
  byte differences are skipped explicitly so a real change is never
  silently masked.

  ```bash
  python tools/refresh-sample-timestamps.py            # actually restore
  python tools/refresh-sample-timestamps.py --dry-run  # report only
  ```

- **For real byte drift** (Access wrote internal metadata), discard
  the working-tree changes:

  ```bash
  git restore samples/<File>.accdb
  ```

  The internal metadata Access wrote is noise that will get overwritten
  the next time the file is opened anyway, so discarding is safe.

**Recommended cleanup sequence after closing Access:**

```bash
# 1. See what's actually different.
python tools/refresh-sample-timestamps.py --dry-run

# 2. Files that the script would skip with "content differs" are real
#    byte drift. Discard them with git restore.
git restore samples/<files-with-real-drift>

# 3. Run the script for real to fix any remaining mtime-only drift.
python tools/refresh-sample-timestamps.py
```

If you have intentional changes you want to keep, stage them first
(`git add`) before running this sequence.
