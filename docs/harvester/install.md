# Installing the Dabbler Access Harvester CLI

This document tells a downstream consumer (AI tooling agent, future
migration-repo orchestrator, human evaluator) how to install and invoke
the harvester CLI **without** cloning the repository or building from
source.

The harvester ships as a **self-contained Windows binary**: one
`AccessHarvester.exe` that carries its own .NET runtime, native
SQLite dependencies, and managed payload inside a single
self-extracting executable.

---

## What you get

| Property | Value |
|---|---|
| Distribution shape | Self-contained, single-file Windows binary |
| Runtime identifier | `win-x64` |
| Approximate size | ~36 MB (compressed single-file) |
| .NET runtime | Bundled — no separate install required |
| Microsoft Access | **Required at runtime** for `extract` / `run` (COM-based) |
| Determinism | Two publishes of the same source produce a byte-identical exe |

The exe contains everything except Microsoft Access itself. `extract`
and `run` subcommands talk to Access via COM and therefore require
Windows + an installed Microsoft Access (per the `AccessHarvester.Core`
`[SupportedOSPlatform("windows")]` gating). The `parse`, `translate`,
`generate-docs`, `validate`, and `generate-migration` subcommands are
pure file-to-file and run anywhere Windows can launch the binary.

---

## Where to get it

The recommended way to get the harvester is from the official GitHub
Releases page for the `dabbler-access-harvester` repository:

- **<https://github.com/darndestdabbler/dabbler-access-harvester/releases>**

Each release is created by a CI workflow on a tag push (`v<semver>`,
e.g., `v1.2.0`) on `main`. The workflow runs the deterministic publish
script and creates the release as a draft; the maintainer reviews and
publishes. Consumers only see published releases.

Each published release ships two assets:

1. The bundle: `AccessHarvester-<semver>-schema-v<schema>-win-x64.zip`
   (e.g., `AccessHarvester-1.2.0-schema-v1.1-win-x64.zip`), containing
   `AccessHarvester.exe`, `RELEASE-NOTES.md`, and `LICENSE.txt` at the
   archive root.
2. A `.sha256` sidecar (`<bundle-filename>.zip.sha256`) for integrity
   verification. The sidecar is a single line in the standard
   `<hash>  <filename>` format with two spaces between the hash and
   the filename, LF-terminated.

### Download and verify

1. From the releases URL above, open the latest published release and
   download both the `.zip` bundle and the matching `.zip.sha256`
   sidecar into the same directory.

2. Verify the integrity of the bundle against the sidecar. Substitute
   the version you downloaded for `1.2.0` / `v1.1` below.

   **PowerShell (native Windows):**
   ```powershell
   $zip = "AccessHarvester-1.2.0-schema-v1.1-win-x64.zip"
   $expected = ((Get-Content "$zip.sha256" -Raw) -split '\s+')[0].ToLower()
   $actual   = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
   if ($actual -ne $expected) { throw "Checksum mismatch: $actual != $expected" }
   "OK: $zip"
   ```

   **Git Bash / WSL:**
   ```bash
   sha256sum -c AccessHarvester-1.2.0-schema-v1.1-win-x64.zip.sha256
   # AccessHarvester-1.2.0-schema-v1.1-win-x64.zip: OK
   ```

3. Extract the `.zip`. The exe is at the archive root alongside
   `RELEASE-NOTES.md` and `LICENSE.txt`; there is no leading folder.

After extraction, run the "Verifying an install" smoke check (below)
to confirm the binary loads and the parse pipeline works.

### Maintainer-only: from a local clone

The publish output is gitignored (`/publish/`). Maintainers can produce
the same artifacts locally by running `tools/publish/publish.ps1` from
the repo root; outputs land at `publish/win-x64/AccessHarvester.exe`
(bare exe — the reference path the integration tests check against)
and `publish/release/AccessHarvester-<semver>-schema-v<schema>-win-x64.zip`
plus the matching `.sha256` sidecar. This path is **not** the
recommended install path for downstream consumers — pull from the
release channel instead so the integrity-verified, deterministic
artifact reaches you.

---

## How to install

1. Place `AccessHarvester.exe` anywhere on disk the consuming process
   can read (a tool directory, a per-project `bin/` folder, a
   shared agent workspace — all are fine).
2. Optionally add the containing directory to `PATH` so the exe is
   invocable by bare name; otherwise call it by absolute path.
3. The first invocation extracts the bundled native dependencies to a
   per-user temp directory (`%TEMP%\.net\AccessHarvester\<hash>\`).
   This is the standard .NET single-file behaviour and may add a
   one-time second or two of latency to the first run.

There is no installer, no MSI, no service registration, no autoupdate.
The exe is the install.

---

## How to invoke

The CLI is the same surface whether you run the source build or the
packaged binary. The subcommands are:

```bash
AccessHarvester --version                                # version + schema-version, exit 0
AccessHarvester --help                                   # full usage text
AccessHarvester extract       --db <file.accdb> --out <output-root-dir> [--types ...] [--json-status]
AccessHarvester translate     --in <raw-dir>    --out <kb-dir>          [--types ...] [--json-status]
AccessHarvester run           --db <file.accdb> --out <output-root-dir> [--types ...] [--json-status]
AccessHarvester report-baseline --db <file.accdb> --out <output-root-dir> [--reports name,name] [--params <params.json>] [--include-pdf] [--no-clean] [--json-status]
AccessHarvester generate-docs --in <manifest.json> --out <output-dir>
AccessHarvester validate      --in <output-root-dir> [--json-status]
AccessHarvester validate      --manifest <manifest.json> [--json-status]
AccessHarvester generate-migration --in <harvested-output-root> --out <output-dir> [--classification <classification.json>] [--data <sanitized-csv-dir>] [--json-status]
AccessHarvester parse         <source-file>
AccessHarvester parse         --in <source-file>
AccessHarvester               <source-file>              # bare-file mode; equivalent to 'parse'
```

`run` is `extract` + `translate` + `enrich` in one invocation.
`generate-docs` reads a `DatabaseManifest` JSON and writes a markdown
schema summary to `<output-dir>/<database-slug>/schema-summary.md`.
`validate` checks a `DatabaseManifest` JSON against the harvester-schema
(v1.0 or v1.1) embedded in the binary, matching the manifest's declared
`schemaVersion`. `--in` locates `manifest.json`
recursively under the output root produced by `run`; `--manifest`
validates a single specified file directly.

Layout under `<output-root-dir>`:
`AccessMetaData/<dbName>/{raw,kb,ir}/<Category>/...`.

Exit codes per subcommand group:

- `extract` / `translate` / `run`: `0` (all OK), `1` (usage error),
  `2` (unexpected error), `3` (completed with per-object failures).
- `report-baseline`: `0` (all considered reports captured or harmlessly
  skipped), `1` (usage error), `2` (unexpected error), `3` (completed
  with per-report failures). COM-based — requires Windows + Access, like
  `extract` / `run`.
- `parse` / bare-file: `0` (parsed), `1` (failed / not found / usage
  error).
- `generate-docs`: `0` (summary written), `1` (usage error),
  `2` (unexpected error).
- `validate`: `0` (manifest conforms to its declared schema version — v1.0
  or v1.1), `1` (usage error /
  file not found / not valid JSON), `2` (unexpected error),
  `3` (schema-validation errors found).
- `generate-migration`: `0` (clean migration, no degradations), `1` (usage
  error / input not found / invalid classification), `2` (unexpected
  error), `3` (completed with flagged degradations — review
  `migration-manifest.json`). File-to-file, no Access required.
- `--version` / `--help`: `0` (printed).

For the per-subcommand option list, JSON-status envelope shapes, and
the parse output schema, run `AccessHarvester --help`.

---

## `report-baseline` — report parity baselines

`report-baseline` captures a per-report **parity baseline** so a
downstream migration can verify a ported report against the original
Access report's ground truth. It is COM-based (Windows + Microsoft
Access required), like `extract` / `run`.

```
AccessHarvester report-baseline --db <file.accdb> --out <output-root-dir>
    [--reports <name,name>] [--params <params.json>] [--include-pdf]
    [--no-clean] [--json-status]
```

| Option | Required | Meaning |
|---|---|---|
| `--db` | yes | The `.accdb` / `.mdb` to capture against |
| `--out` | yes | Output root; artifacts land under `<out>/AccessMetaData/<dbName>/reports-baseline/` |
| `--reports` | no | Comma-separated allowlist of report names. Default: **all** reports in the database |
| `--params` | no | Path to a JSON file of per-report parameter values (see below) |
| `--include-pdf` | no | Also render `output.pdf` for reports whose aggregates captured successfully. **Default: off** — aggregates only |
| `--no-clean` | no | Do not wipe the `reports-baseline` root first (default is a clean capture) |
| `--json-status` | no | Emit a machine-readable JSON status summary to stdout |

**What it writes**, under
`<out>/AccessMetaData/<dbName>/reports-baseline/`:

- `<report>/aggregates.json` — **always**. The report's `RecordSource`
  query result aggregates (row count, group subtotals, grand total),
  plus provenance (parameters used, harvester version). These reflect
  the RecordSource **result set** — Access's own query engine on the
  data — *not* the rendered footer-control expressions. This is the
  deterministic parity ground truth.
- `<report>/output.pdf` — **only with `--include-pdf`** and only for
  reports whose aggregate capture succeeded. A `DoCmd.OutputTo` render
  for human visual review. Non-deterministic binary; excluded from any
  byte-comparison gate.
- `baseline-manifest.json` — top-level index of every report processed:
  `{name, status, paths, reason?}`. `status` is `Captured`, `Skipped`
  (e.g. an unbound report), or `Failed` (with a `reason` such as
  `AggregateFailed` / `RenderFailed`) — failed/skipped reports are
  explicit, never silently omitted.

**Default capture is aggregates-only and modal-free.** Because the
default path never renders a PDF, a required-parameter report that
cannot run unattended surfaces as an explicit `Failed`
(`AggregateFailed`) manifest entry rather than blocking on a modal
prompt or entering `DoCmd.OutputTo` — the batch continues.

**Parameters (`--params`).** Supply values for reports whose
`RecordSource` requires them. The JSON is keyed by report, then by
parameter:

```json
{
  "rptInvoice":       { "OrderID": 10250 },
  "rptTotalsByPeriod": { "StartDate": "2026-01-01", "EndDate": "2026-03-31" }
}
```

Parameters bind **positionally, in the order declared** for each report,
to the SQL's `?` placeholders (Access OLE DB ignores parameter names for
text commands); the values used are recorded verbatim in
`aggregates.json` provenance so two runs reproduce exactly.

> **Status (`harvester-report-baseline-capture` S3):** the subcommand,
> aggregates engine, PDF render, and parameter handling are shipped. A
> committed **golden reports-baseline corpus + regression gate** (spec
> decision #7) is **deferred** to the follow-up set
> `harvester-report-baseline-golden-corpus` — capturing it currently
> trips an Access design-view / ACE-OLEDB exclusive-lock contention, and
> the follow-up will make aggregate capture lock-free.

## `generate-migration` — SQL Server migration

`generate-migration` is the final product of the classify → sanitize →
**migrate** chain: a deterministic SQL Server migration emitted from
already-harvested artefacts. It is **file-to-file (no Access required)** —
it reads the harvested `ir/`, the reviewed `classification.json`, and the
`sanitize` CSV output; it never opens the `.accdb`.

```
AccessHarvester generate-migration --in <harvested-output-root> --out <output-dir>
    [--classification <classification.json>] [--data <sanitized-csv-dir>] [--json-status]
```

| Option | Required | Meaning |
|---|---|---|
| `--in` | yes | Harvested output root (the directory containing `ir/`, or `ir/` itself). `ir/Tables` + `ir/Relationships` are the schema source |
| `--out` | yes | Output dir; `schema.sql`, `data.sql`, and `migration-manifest.json` land under `<out>/<database-slug>/` |
| `--classification` | no | Reviewed `classification.json` (multi-value / preserve decisions). Recommended |
| `--data` | no | The `sanitize` CSV-per-table directory (`<sanitize-out>/<database-slug>/`). When omitted, `schema.sql` still emits and every table is flagged missing data |
| `--json-status` | no | Emit a machine-readable JSON status summary to stdout |

**What it writes**, under `<out>/<database-slug>/`:

- `schema.sql` — `CREATE TABLE` (type map, nullability, defaults, PK)
  + `ALTER TABLE … ADD … FOREIGN KEY`. Autonumber → `IDENTITY(1,1)`;
  identifiers bracket-quoted; each batch ends with `GO`.
- `data.sql` — the data-load, in **FK-safe order** (parents before
  children), with `SET IDENTITY_INSERT [T] ON … OFF` where an identity
  column's preserved keys are loaded. **Run `schema.sql` first, then
  `data.sql`.**
- `migration-manifest.json` — `{ SchemaType, Database, GeneratedFiles,
  TableLoadOrder, DegradationCount, Degradations[] }`. Every column or
  table that could not migrate cleanly (OLE / multi-value / linked-table
  / untranslated-default / assumed-precision / dropped-FK / FK-cycle /
  missing-data / omitted-column) is flagged here — **never silently
  dropped or fabricated**.

Because there is no SQL Server in the harvester's test environment, the
emitted SQL is validated **hermetically** (golden-script comparison +
dialect well-formedness checks); an actual run against a live SQL Server
is an optional, non-gated spot-check. See the interpretation guide §5.5
for the degradation semantics a downstream consumer should read.

## Versioning convention

The CLI surfaces both its own semver and the schema version of the
manifests it produces:

```
> AccessHarvester --version
AccessHarvester 1.2.0 (schema v1.1)
```

- **`AccessHarvester <semver>`** — the harvester tool's own version.
  Bumps follow standard semver. A patch bump means bug fixes with no
  schema or surface change; a minor bump means additive surface
  changes; a major bump means a breaking surface or schema change.
- **`schema v<version>`** — the version of
  `harvester-schema.v<version>.json` the produced manifests conform
  to. Current value: `1.0`. Downstream consumers should pin their
  integration to a specific schema version; the harvester maintains
  backwards-compatible schema bumps within a major version line.

A release artifact filename should encode both the harvester semver and
the schema version it produces, e.g.,
`AccessHarvester-1.2.0-schema-v1.1-win-x64.zip` (the published release
bundle; the extracted executable inside is simply `AccessHarvester.exe`).
The CLI's `--version`
output is the runtime authority — if the filename and the runtime
output disagree, trust the runtime output and re-cut the release.

---

## Verifying an install

Smoke-check the installed binary with four zero-cost invocations:

```bash
AccessHarvester --version
# AccessHarvester 1.2.0 (schema v1.1)

AccessHarvester --help
# (full usage text on stderr; exit 0)

AccessHarvester parse path\to\a\sample.bas
# {"status":"parsed", "errors":[], "tree_stats": {...}}  on stdout

AccessHarvester validate --manifest path\to\a\manifest.json
# Validation passed (1 manifest(s) checked against schema v1.1).  on stderr; exit 0
```

These four calls exercise the dispatcher, the help text, the parse
pipeline, the embedded-schema validator, and the JSON-emit path without
touching Microsoft Access. If any of them fail, the install is broken;
do not proceed to `extract` or `run`.

For the `validate` smoke check, pass any `manifest.json` produced by a
prior `run` invocation (it declares its `schemaVersion`). The embedded
schemas (v1.0 and v1.1) are the same as the
`docs/test-cases/harvester-schema.v1.0.json` /
`harvester-schema.v1.1.json` files at build time — no schema file on
disk is required at runtime.

The repository's `tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs`
runs the same checks plus a `generate-docs` round-trip against the
published artifact at `publish/win-x64/AccessHarvester.exe`. These
tests are `[SkippableFact]`-gated: they skip when the publish step has
not been run, and they exercise the packaged binary (not the source
build) so the install-time contract has explicit test coverage.

---

## Constraints and stop rules

- **Trimming is disabled.** `PublishTrimmed=false` is intentional —
  ANTLR4's generated runtime and the DAO COM interop both rely on
  reflection the IL trimmer cannot follow, and `Microsoft.Data.Sqlite`
  carries native dependencies the extraction host needs. Do not
  re-enable trimming without re-running the full integration suite
  against the trimmed artifact.
- **Code signing is not applied.** Distribution-grade code signing is
  a separate concern; the consumer organisation supplies it if their
  policy requires a signed binary.
- **Cross-platform builds are out of scope.** The harvester is
  Windows-only by COM constraint; a cross-platform RID would be
  misleading.
- **No autoupdate.** A new release means a new download. There is no
  in-process update channel.

If a build-time or publish-time anomaly fires one of the spec's stop
rules (publish output > 500 MB, two consecutive publishes producing
different binaries, etc.), the harvester maintainer surfaces the issue
rather than shipping a degraded artifact.

---

## Related documents

- [README](../../README.md) — repo overview and source-build path.
- [Interpretation Guide](interpretation-guide.md) — how a downstream
  AI consumer reads a harvester run's output.
- [Batch Operations Guide](batch-operations.md) — driving
  `AccessHarvester run` across many databases (multi-DB layout,
  `--clean` / `--no-clean`, cross-DB roll-up index, status-envelope
  provenance, MSACCESS hygiene).
- [Session set: `harvester-finish-cli-packaging`](../session-sets/harvester-finish-cli-packaging/spec.md)
  — the planning and acceptance criteria behind this distribution.
