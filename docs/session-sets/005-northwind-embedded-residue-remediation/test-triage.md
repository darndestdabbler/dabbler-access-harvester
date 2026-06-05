# Test Triage — 005 Northwind Embedded-Residue Remediation (Session 2)

> **Authored:** Session 2 (2026-06-05). **Paths + decisions only — no proprietary content.**
> Per-file triage of the 26 corpus-pinned test paths the Session-1
> [`disposition-manifest.md`](disposition-manifest.md) marked `re-base`. Session 1
> blanket-classified all 26 as "re-base onto the synthetic HomeHealthCare (HHC)
> fixture, or delete; never weaken a live assertion." Session 2 refines that into a
> reviewed per-file decision, grounded in a fresh read of every file plus verified
> ground-truth facts (below). Companion: the re-based/edited test sources themselves.

## Ground-truth facts that drove the decisions (verified this session)

1. **A committed HHC KB fixture exists in the exact loader format.**
   `samples/GoldenOutput/HomeHealthCare/kb/Forms/*.json` and
   `kb/ModulesAndClasses/*.json` are tracked and consumed verbatim by
   `KbVbaSourceLoader.FromKbJson` — the same path the SymbolDb `FixtureLoader`
   used for the proprietary fixture. So re-basing SymbolDb tests onto HHC is
   feasible **and turns today's skipped tests into live CI coverage** (the
   NorthwindDevEdition KB is gitignored/local-only, so those `[SkippableFact]`s
   skip on a fresh clone today).
2. **The `AccessHarvester.SymbolDb` engine is covered ONLY by
   `AccessHarvester.SymbolDb.Tests`** (`git grep` for `SymbolDbBuilder` /
   `AccessHarvester.SymbolDb` across `tests/` returns no other project). Deleting
   its fixture-coupled tests would drop **unique** engine coverage → re-base, not
   delete.
3. **6 of the 26 files carry zero NorthwindDevEdition structural tokens**
   (`git grep` for the distinctive identifier set returns no matches): they were
   over-included by the manifest's broad bucket and need no change.
4. **`AccessHarvester.TranslationProbe.Tests` is a leaf project** (referenced only
   by itself + the `.sln`) whose code is a hand-written C# reimplementation of a
   proprietary form's business logic — a spent translation PoC, not engine coverage.
5. The two Integration golden tests run an **HHC arm** (committed golden, live in
   CI) and a **NorthwindDevEdition arm** (golden gitignored → already skipped in
   CI). `SyntheticDbIntegrationTests` has synthetic-DB tests (geometry/labels, no
   Northwind) + a few that open the gitignored `samples/NorthwindDevEdition.accdb`
   via COM (skipped in CI).

## Review trail

Cross-provider consensus on this triage strategy (route `analysis` → **gemini-pro**:
APPROVE; `query` → **gpt-5-4**: APPROVE-WITH-CHANGES). GPT's three execution
constraints were adopted: (a) rename **every** surviving Northwind-named symbol
incl. test class/file/display names, not just the fixture pointer; (b) on re-base,
preserve the **same semantic claim** — never downgrade to vague "non-empty"/"contains"
assertions; if HHC lacks a construct, extend a committed fixture rather than weaken;
(c) keep Access object-kind prefix semantics (`frm*`/`rpt*`/`qry*`/`mod*`) in literal
redactions. The two decisions that deviate from the reviewed S1 manifest
(`re-base` → `delete` for the TranslationProbe project and `GenerateArtifacts`) were
**confirmed by the maintainer** (2026-06-05), consistent with the S1 precedent of
maintainer sign-off on delete-scope decisions for spent proprietary-logic residue.

## Summary

| Decision | Count | Meaning |
|---|---:|---|
| **keep (clean)** | 6 | Already hermetic / synthetic-HHC; zero NorthwindDevEdition tokens. Manifest over-included. No change. |
| **redact-literals** | 5 | Hermetic (no fixture); rename distinctive NorthwindDevEdition `rpt*`/`qry*`/`frm*`/`mod*` identifiers to neutral/HHC names, keep generic SQL vectors, preserve every assertion. |
| **re-base** | 6 | SymbolDb engine tests + the shared `FixtureLoader`; re-pointed onto committed HHC KB, assertions re-derived from real HHC content, behavior-under-test preserved; become live CI tests. |
| **edit (trim arm)** | 3 | Remove the NorthwindDevEdition arm/methods; keep the HHC/synthetic arm at full strength. |
| **delete (file)** | 1 | `GenerateArtifacts` — weak dev regenerator coupled to `RunForNorthwind` (S3 removes it). |
| **delete (project)** | 4 | The entire `TranslationProbe.Tests` project (3 code files + csproj; removed from `.sln`). |

(26 listed test paths + `FixtureLoader.cs` infra; the `.sln` edit is shared with the
delete.)

## keep (clean) — 6 paths

Verified zero NorthwindDevEdition structural tokens; coverage is hermetic inline VBA
or synthetic HHC `docs/test-cases/*.bas` fixtures. No change.

- `tests/AccessHarvester.SymbolDb.Tests/BodyHasherTests.cs` — hermetic inline VBA (`Foo()`, `Bob's database`); `[Fact]`.
- `tests/AccessHarvester.SymbolDb.Tests/IntraProcedureDeadCodeDetectorTests.cs` — hermetic inline VBA; `[Fact]`.
- `tests/AccessHarvester.Vba.Resolver.Tests/CrossReference/CrossReferenceFixtureTests.cs` — synthetic HHC `.bas` fixtures (`rptSummary`, `frmCaseManagement`, `modStartup`).
- `tests/AccessHarvester.Vba.Resolver.Tests/References/ResolverFixtureAnnotationTests.cs` — synthetic HHC `.bas` fixtures.
- `tests/AccessHarvester.Vba.Resolver.Tests/ScopeBuildingFixtureTests.cs` — synthetic HHC `.bas`/`.cls` fixtures.
- `tests/AccessHarvester.Vba.Resolver.Tests/Types/TypeInferenceFixtureTests.cs` — synthetic HHC fixtures + inline `modSmoke`.

## redact-literals — 5 paths

Hermetic; the NorthwindDevEdition names are opaque labels (report/query names passed
to the aggregate computer; one set of module names in a filter test). Rename the
distinctive `rpt*`/`qry*`/`frm*`/`mod*` proprietary identifiers to neutral
placeholders that **keep the object-kind prefix** (e.g. a `rpt*` report name →
`rptSummary`, a `qry*` query name → `qrySummary`), keep generic SQL vectors
(`Orders`/`Customers`/`Order Details`/`ShipCountry` — Microsoft public-Northwind
textbook names, already classified `keep-accepted (generic)` by S1), and preserve
every assertion unchanged.

- `tests/AccessHarvester.SymbolDb.Tests/DeferralPreFilterTests.cs` — rename the two incidental proprietary `frm*`/`mod*` module-name literals → generic; redact the comment that named the proprietary fixture.
- `tests/AccessHarvester.Tests/ReportBaseline/RecordSourceResolverTests.cs` — redact the distinctive proprietary `qry*` names; keep generic SQL vectors.
- `tests/AccessHarvester.Tests/ReportBaseline/ReportAggregateComputerTests.cs` — redact the proprietary `rpt*`/`qry*` report/query label literals → neutral.
- `tests/AccessHarvester.Tests/ReportBaseline/ReportAggregatesWriterTests.cs` — redact the proprietary `rpt*`/`qry*` label literals → neutral.
- `tests/AccessHarvester.Tests/ReportBaseline/ReportBaselineServiceTests.cs` — redact the proprietary `rpt*`/`qry*` label literals, the proprietary-DB temp-path segment, and a method-name token → neutral/HHC.
- (also genericized one incidental proprietary `rpt*` reference in the S1 keep-accepted `SumAggregateEvaluatorTests.cs` — same family, kept consistent.)

## re-base — 6 paths

Re-point onto the committed HHC KB; re-derive expected values from real HHC content
(form: `frmProviderSearch`; standard modules: `basListHelpers` [has a `ParamArray`
function], `basUtilities`; plus the HHC `kb/{Forms,Macros,Queries,Tables}` trees for
the Session-3 extractors). Preserve the behavior-under-test and assertion strength;
rename `Northwind*` symbols/files. These become **live, non-skipped** CI tests.

- `tests/AccessHarvester.SymbolDb.Tests/FixtureLoader.cs` — re-point KB root to `samples/GoldenOutput/HomeHealthCare/kb`; rename `Northwind*` helpers → `Hhc*`; load HHC modules. (HHC KB is committed, so the skip guard becomes a belt-and-suspenders fallback.)
- `tests/AccessHarvester.SymbolDb.Tests/NorthwindFixtureSmokeTests.cs` → `HhcFixtureSmokeTests.cs` — builder shape on HHC form + standard module.
- `tests/AccessHarvester.SymbolDb.Tests/CrossModuleResolutionTests.cs` — cross-module + same-module + out-of-scope resolution on HHC modules.
- `tests/AccessHarvester.SymbolDb.Tests/ReferencesExtractorTests.cs` — references/member-access rows, caller kinds, control/enum/variable reference kinds, dot vs bang on HHC.
- `tests/AccessHarvester.SymbolDb.Tests/Session3ExtractorTests.cs` — form-controls / macro / query-schema / table-schema extractors on the HHC KB trees; keep the 2 already-synthetic `[Fact]`s.
- `tests/AccessHarvester.SymbolDb.Tests/PersistenceAndDedupTests.cs` — re-base the fixture-coupled tests onto HHC; redact the already-synthetic tests' incidental names.

## edit (trim Northwind arm) — 3 paths

- `tests/AccessHarvester.Tests/SyntheticDbIntegrationTests.cs` — delete the NorthwindDevEdition COM test methods + the `ResolveSampleDb` helper; keep the synthetic geometry/label tests (the file's real purpose) at full strength.
- `tests/AccessHarvester.Integration/Pipeline/CanonicalGoldenRegressionTests.cs` — drop the `NorthwindDevEdition.accdb` dictionary entry + the `NorthwindDevEdition_ReproducesGoldenOutput` test; keep the HomeHealthCare regression gate untouched.
- `tests/AccessHarvester.Integration/Pipeline/GoldenCorpusRegenerator.cs` — drop NorthwindDevEdition from the regen fixture list; keep the HHC regen path.

## delete (file) — 1 path

- `tests/AccessHarvester.SymbolDb.Tests/GenerateArtifacts.cs` — `[SkippableFact]` dev regenerator that only asserts `exit == 0`, calls `BuildArtifacts.RunForNorthwind` (S3 genericizes/removes it), and rebuilds a local-only SQLite artifact. Builder behavior is covered by the re-based `PersistenceAndDedupTests` round-trip. Maintainer-confirmed delete.

## delete (project) — 4 paths (+ shared `.sln` edit)

The entire `AccessHarvester.TranslationProbe.Tests` project — a spent C#
reimplementation of a proprietary form's validation logic (denser than
structural names) and tests of that reimplementation. Leaf project; not harvester-engine
coverage; cannot be "re-based" (would mean authoring a new probe for an HHC form).
Maintainer-confirmed delete.

- `tests/AccessHarvester.TranslationProbe.Tests/AccessHarvester.TranslationProbe.Tests.csproj`
- `tests/AccessHarvester.TranslationProbe.Tests/<frm*>TranslationProbe.cs` — the proprietary-form translation probe source.
- `tests/AccessHarvester.TranslationProbe.Tests/<frm*>TranslationProbeTests.cs` — its tests.
- `tests/AccessHarvester.TranslationProbe.Tests/Session3RegressionTests.cs`
- `AccessHarvester.sln` — remove the `AccessHarvester.TranslationProbe.Tests` project entry (via `dotnet sln remove`).

(The exact deleted paths, incl. the proprietary-form-derived filenames, remain on the
S1 `disposition-manifest.md` DELETE record and in git history per the spec's
accepted-process-record carve-out; they are genericized here to keep this doc free of
proprietary structural identifiers.)

## Stop-rule compliance

- **Never weaken an assertion:** re-based tests re-derive expected values from real
  HHC content and keep the same semantic claim; no assertion downgraded to
  "non-empty"/"contains". Confirmed by `dotnet test` running the re-based tests live.
- **No file with ongoing value deleted to satisfy the scan:** the only deletes are a
  spent dev regenerator and a spent proprietary-logic PoC project — both
  maintainer-confirmed, neither carrying unique engine coverage.
- **No engine behavior changed:** only the test harness + the `FixtureLoader` fixture
  pointer move; the code under test is untouched.
- **Local-only fixture untouched:** the gitignored `NorthwindDevEdition` trees are
  not referenced by any re-based test and are left in place.
