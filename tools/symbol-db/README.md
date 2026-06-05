# `tools/symbol-db/` — VBA symbol-DB tooling

This directory holds the **generic tooling** that builds and analyses the VBA
symbol database. The tooling is committed and supported. The **data** it
produces from the proprietary `NorthwindDevEdition` fixture is **not** — it is
local-only and regenerated on demand.

## Local-only, proprietary data artifacts (NOT committed)

The symbol DB and its derived consistency-grading outputs serialize full
`NorthwindDevEdition` VBA source bodies and AI interpretations. `NorthwindDevEdition`
is proprietary (see `docs/planning/project-guidance.md` → *Canonical Test
Fixtures*), so these artifacts are gitignored and live only on the developer's
filesystem:

| Path | What it is |
|---|---|
| `symbol-db.sqlite` | The symbol database — modules, symbols, references, interpretations |
| `measurement-report.md` | Build measurement report emitted alongside the DB |
| `consistency/grades.sqlite` | Cross-provider consistency-grading rows |
| `consistency/sample.json` | Stratified sample manifest |
| `consistency/sample-report.md` | Sample report |
| `consistency/correlation-summary.json` | Correlation summary |
| `consistency/correlation-report.md` | Correlation + calibration report |
| `consistency/correlation-scatter.png` | Correlation scatter plot |

These were untracked from `HEAD` in session set
`004-northwind-symbol-db-sqlite-residue` (forward removal); the full git-history
scrub is handled in that set's Sessions 2–3. The `.gitignore` entries carry the
same note.

## Generic tooling (committed, kept)

Everything else here is generic and committed: the Python packages under
`interpret/`, `consistency/`, and `probe/` (graders, samplers, correlation,
interpretation, probe builders, and their unit tests), `interpretation-config.json`
(generic thresholds/policy — no proprietary data), and the `probe-builder/` C#
utility. The symbol-DB C# pipeline lives in `AccessHarvester.SymbolDb/` and its
tests in `tests/AccessHarvester.SymbolDb.Tests/`.

## Regenerating the data locally

The data is reproducible from the local `NorthwindDevEdition` KB fixture
(`tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/kb/`, itself local-only):

- **`symbol-db.sqlite` (structural rows) + `measurement-report.md`** —
  `AccessHarvester.SymbolDb.Cli.BuildArtifacts.RunForFixture(repoRoot, fixture, ...)`,
  where `fixture` (a `FixtureObjectSet`) names the local kb-tier directory and the
  form/module to ingest. The `probe-builder/` utility drives it from the command
  line (`--fixture <name> --form-json … --form-module … --module-json … --module-name …`),
  so no fixture-specific identifiers live in source — point it at whatever local
  Access fixture you are measuring.
- **AI interpretation columns** — `python -m interpret --db symbol-db.sqlite ...`.
- **Consistency outputs** (`grades.sqlite`, `sample*.json/.md`, `correlation-*`) —
  the `consistency/` runners (sampler → grader → correlation → report).

When the fixture is absent (a fresh clone, where `NorthwindDevEdition` is not
available), the symbol-DB-data-dependent hermetic tests skip cleanly via
`FixtureLoader.RequireNorthwindFixture()` rather than failing.
