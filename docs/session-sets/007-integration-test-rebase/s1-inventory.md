# Session 1 Inventory — `northwind-output.json` Dependency Surface

> Authored 2026-06-12 (Session 1, step 2). Grep-grounded against the working
> tree at session start (`grep -rn northwind-output tests/`). Every claim
> below names its evidence; nothing is carried forward from the spec without
> re-verification.

## Tests whose body resolves `examples/northwind-output.json`

**8 tests** across the five files resolve the path (the spec anticipated the
*failure* count, 7 — reconciliation below).

| # | Test | File:Line | What it asserts |
|---|------|-----------|-----------------|
| 1 | `V1_0_Shaped_SmokeFixture_ValidatesAgainstSchema` (`[Fact]`) | `Pipeline/ManifestSchemaConformanceTests.cs:149` | A **v1.0-shaped** document (no `Documentation`, no `FileMap`) still validates against the embedded v1.1 schema — the additive backward-compat guarantee. Hard-fails on missing fixture (`Assert.True(File.Exists…)`). |
| 2 | `PackagedBinary_GenerateDocs_AgainstCommittedManifest_WritesSchemaSummary` (`[SkippableFact]`) | `Cli/PackagedBinarySmokeTests.cs:69` | Packaged self-contained exe `generate-docs`: exit 0, `<out>/<slug>/schema-summary.md` exists. Skips when `publish/` artifact absent; hard-fails on missing fixture when artifact present. |
| 3 | `PackagedBinary_Validate_ConformantManifest_ReturnsZero` (`[SkippableFact]`) | `Cli/PackagedBinarySmokeTests.cs:95` | Packaged exe `validate --manifest`: exit 0 + `"Validation passed"` on stderr. Same skip/fail gating as #2. |
| 4 | `Validate_Manifest_ValidDocument_ReturnsZero` (`[Fact]`) | `Cli/CliValidateTests.cs:18` | `validate --manifest` happy path: exit 0 + `"Validation passed"`. Hard-fails on missing fixture. |
| 5 | `Validate_BothInAndManifest_ReturnsOne_UsageError` (`[Fact]`) | `Cli/CliValidateTests.cs:253` | `validate --in X --manifest Y` → exit 1 + `"Usage error"` (mutual exclusion). **No `File.Exists` assert** — see reconciliation. |
| 6 | `Validate_JsonStatus_ValidDocument_EmitsEnvelopeWithExitZero` (`[Fact]`) | `Cli/CliValidateTests.cs:265` | `--json-status` envelope on a valid doc: exit 0; `Command == "validate"`, `ExitCode == 0`, empty `Errors[]`. Hard-fails on missing fixture. |
| 7 | `GenerateDocs_AgainstCommittedManifest_WritesSchemaSummary` (`[Fact]`) | `Cli/CliGenerateDocsTests.cs:18` | Source-build `generate-docs`: exit 0; summary at `<out>/<db-slug>/schema-summary.md`; content contains the `Database.Name`. Hard-fails on missing fixture. |
| 8 | `GenerateDocs_TwoInvocations_ProduceIdenticalSchemaSummary` (`[Fact]`) | `Cli/CliDeterminismTests.cs:37` | Byte-identical `schema-summary.md` across two `generate-docs` invocations (determinism gate). Hard-fails on missing fixture. |

## Failure-count reconciliation (expected 7 → confirmed consistent)

- **5 tests fail unconditionally** on a clean checkout (#1, #4, #6, #7, #8 —
  each opens or `File.Exists`-asserts the fixture).
- **2 tests (#2, #3) fail only when a published artifact is present**
  (`PublishedArtifact.TryLocate()`; otherwise `Skip.If` fires). The 005
  measurement host had one, giving 5 + 2 = **7** — matching the 005
  change-log's "7 depend on `examples/northwind-output.json`".
- **1 test (#5) passes even without the fixture**: `RunValidate` throws the
  `--in`/`--manifest` mutual-exclusion `ArgumentException` (→ `"Usage error"`,
  exit 1) at `Program.cs:331-332` **before** the `File.Exists` probe at
  `Program.cs:337-338`. Verified by reading the source, not inferred from the
  test alone. It still references the path and is re-based with the rest.

## Fixture-strategy probes (step-3 evidence)

- `samples/GoldenOutput/HomeHealthCare/manifest.json` is **v1.1-shaped**:
  `SchemaVersion == "1.1"`, `Documentation` and `FileMap` both present
  (probed via JSON load, 2026-06-12). So the v1.0-shape conformance case
  (#1) **requires a hand-trimmed committed variant** per the spec constraint
  — pointing it at the golden would be a silent intent downgrade.
- The golden carries normalization placeholders
  (`HarvesterVersion == "<NORMALIZED:HarvesterVersion>"`,
  `Database.FilePath/ExportedAt` likewise) — it is a regression-pin artifact,
  not a consumer-facing example.
- Schema constraints (both v1.0 and v1.1): `DatabaseManifest` requires
  `SchemaType`, `SchemaVersion` (const per version), `HarvesterVersion`
  (free-form string), `Database`; `DatabaseInfo` requires only `Name`.
- `generate-docs` slug derivation (`Program.cs:293-294`):
  `Regex.Replace(GetFileNameWithoutExtension(dbName), @"[^\w-]", "-").ToLowerInvariant()`
  → `"HomeHealthCare.accdb"` → **`homehealthcare`**.

## Decision (step 3): one committed v1.0-shaped fixture for all 8 tests

**Create `examples/homehealthcare-output.json`** — a hand-trimmed,
v1.0-shaped manifest example derived exclusively from committed
HomeHealthCare artifacts (`Database.Name` and `HarvesterVersion` are the
golden manifest's literal values — the latter is the golden's
`<NORMALIZED:HarvesterVersion>` placeholder, schema-valid and asserted on
by no test; the six per-category arrays present and empty; no
`Documentation`/`FileMap`) — and point all 8 tests at it.

> R1 verification note: the first draft set `HarvesterVersion` to the
> `Directory.Build.props` version `1.2.0`; the cross-provider verifier
> flagged that as outside the "derive only from committed HomeHealthCare
> artifacts" constraint, and the fixture now carries the golden's literal
> value instead.

**`SchemaVersion` detail (caught during implementation):** the conformance
test validates via `SchemaValidator(RepoPaths.SchemaFile())` — strictly
against `harvester-schema.v1.1.json`, whose `SchemaVersion` is
`const "1.1"`. The fixture therefore declares `SchemaVersion: "1.1"` while
remaining v1.0-*shaped* — exactly what the test comment's "modulo the
SchemaVersion const bump" records about the original fixture. An initial
draft declaring `"1.0"` (and named `homehealthcare-output.v1_0.json`)
would have failed the const; it was corrected, and the file renamed to
`homehealthcare-output.json`, before the build/test step. Per-document
dispatch of declared-`"1.0"` manifests is separately covered by
`Validate_V1_0_Manifest_DispatchesToV1_0_Schema_AndReturnsZero` over an
inline temp document, unaffected by this set.

Rationale, against the spec's "leanest option that preserves each test's
intent and keeps `examples/` consumer-meaningful":

1. **The v1.0-shape variant is mandatory anyway** (golden is v1.1-shaped, see
   probe above). Reusing that one file for the 7 CLI-side tests mirrors the
   original arrangement exactly — `northwind-output.json` was itself the
   v1.0-shape smoke fixture all eight tests shared (per the conformance
   test's own comment) — so each test's contract is checked over the same
   *kind* of input as before. No assertion is weakened or strengthened.
2. **Leanest**: one small new committed file; the alternative (CLI tests →
   golden directly, conformance → separate variant) still commits a file
   *and* couples seven tool-behavior tests to golden-regeneration churn
   (the golden regenerates with every schema set and embeds normalization
   placeholders).
3. **`examples/` stays consumer-meaningful**: it gains the manifest-shaped
   example the `CliGenerateDocsTests` fixture-choice comment describes
   (`minimal-form.json` / `complex-form.json` are per-form IR records, not
   manifests). The deliberate v1.0 shape is made explicit in
   `examples/README.md` (provenance + refresh rule) and in the consuming
   tests' comments, avoiding the silent-stale-example hazard the
   "Regenerate Committed Example Files Alongside The Schema Set That
   Produces Them" lesson warns about.
4. **Provenance** is recorded in a new `examples/README.md` (where the file
   came from, what shape it deliberately is, and when it must be refreshed),
   satisfying the spec's Fixture Strategy provenance constraint.

Stop-rule compliance: the fixture derives only from committed HomeHealthCare
artifacts; nothing NorthwindDevEdition-derived is recreated; only test code
and committed example fixtures change.
