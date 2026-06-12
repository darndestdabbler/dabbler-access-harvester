# `examples/` — Committed Example Fixtures

Small, committed JSON examples consumed by documentation and by the
hermetic CLI integration tests (`tests/AccessHarvester.Integration/`).
These are examples of harvester *output shapes*, not golden regression
pins — the full pinned corpus lives at `samples/GoldenOutput/`.

| File | What it is |
|---|---|
| `minimal-form.json` | Minimal per-form IR record (`kb`/`ir` form shape). |
| `complex-form.json` | Richer per-form IR record exercising nested controls. |
| `validate-invalid-manifest.json` | Deliberately non-conformant manifest used by the `validate` subcommand tests' failure path. |
| `homehealthcare-output.json` | Manifest-shaped example (`SchemaType: "DatabaseManifest"`). Declares the current `SchemaVersion` (`"1.1"`) but is deliberately **v1.0-shaped**. See provenance below. |

## `homehealthcare-output.json` — provenance and refresh rule

- **Derived from:** the committed HomeHealthCare golden manifest
  (`samples/GoldenOutput/HomeHealthCare/manifest.json`), hand-trimmed on
  2026-06-12 (session set `007-integration-test-rebase`, Session 1):
  `Database.Name` and `HarvesterVersion` are the golden's literal values —
  the `<NORMALIZED:HarvesterVersion>` placeholder is what the golden
  itself carries (the regression corpus normalizes run-varying fields), it
  is schema-valid (the field is a free-form string), and no test asserts
  on it; the six per-category arrays are present and empty. It replaced
  the never-committed, NorthwindDevEdition-derived `northwind-output.json`
  in the same role.
- **Why it is v1.0-shaped on purpose:** the v1.0 → v1.1 schema bump was
  additive, and the guarantee under test is that a v1.0-shaped document —
  no `Documentation`, no `FileMap`, empty per-category arrays — still
  validates against the current schema, modulo the `SchemaVersion` const
  bump (which is why the file declares `"1.1"`).
  `ManifestSchemaConformanceTests.V1_0_Shaped_SmokeFixture_ValidatesAgainstSchema`
  pins exactly that guarantee against this file, and the `validate` /
  `generate-docs` / determinism / packaged-binary CLI tests drive the CLI
  against it as their committed manifest-shaped input. This is **not** a
  stale example awaiting regeneration — the old shape is its job.
- **Refresh rule:** when a future schema set bumps `SchemaVersion` again
  (additive policy: bump the const, keep the old shape valid), bump this
  file's `SchemaVersion` in that same set and nothing else. If a schema
  set ever retires the additive guarantee or the old shape, this file and
  the tests that consume it must be reworked **in that same set**.
- **Never** replace this file with anything derived from
  `NorthwindDevEdition.accdb` — that fixture is proprietary and local-only
  (see `docs/planning/project-guidance.md` → Canonical Test Fixtures).
