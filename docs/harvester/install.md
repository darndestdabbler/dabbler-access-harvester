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
`generate-docs`, and `validate` subcommands are pure file-to-file and
run anywhere Windows can launch the binary.

---

## Where to get it

The artifact is **not** committed to the repository. The repo's
`.gitignore` excludes the publish output (`/publish/`); the binary is
produced on demand from a known source revision and is published to the
distribution channel a consumer pulls from.

The current convention (subject to change as a real downstream consumer
materialises):

- **From a local clone:** run the publish script
  (`tools/publish/publish.ps1`) — produces
  `publish/win-x64/AccessHarvester.exe` at the repo root. This is the
  reference path the integration tests check against.
- **From a release drop:** when a versioned release is cut, the
  artifact lands as an attachment on a tagged GitHub release; a
  downstream consumer downloads the exe directly.

If you are a downstream consumer and no release channel is documented
here, ask the harvester maintainer for the artifact rather than building
from source. Building from source defeats the point of the
self-contained distribution.

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
AccessHarvester generate-docs --in <manifest.json> --out <output-dir>
AccessHarvester validate      --in <output-root-dir> [--json-status]
AccessHarvester validate      --manifest <manifest.json> [--json-status]
AccessHarvester parse         <source-file>
AccessHarvester parse         --in <source-file>
AccessHarvester               <source-file>              # bare-file mode; equivalent to 'parse'
```

`run` is `extract` + `translate` + `enrich` in one invocation.
`generate-docs` reads a `DatabaseManifest` JSON and writes a markdown
schema summary to `<output-dir>/<database-slug>/schema-summary.md`.
`validate` checks a `DatabaseManifest` JSON against the harvester-schema
v1.0 that is embedded in the binary. `--in` locates `manifest.json`
recursively under the output root produced by `run`; `--manifest`
validates a single specified file directly.

Layout under `<output-root-dir>`:
`AccessMetaData/<dbName>/{raw,kb,ir}/<Category>/...`.

Exit codes per subcommand group:

- `extract` / `translate` / `run`: `0` (all OK), `1` (usage error),
  `2` (unexpected error), `3` (completed with per-object failures).
- `parse` / bare-file: `0` (parsed), `1` (failed / not found / usage
  error).
- `generate-docs`: `0` (summary written), `1` (usage error),
  `2` (unexpected error).
- `validate`: `0` (manifest conforms to schema v1.0), `1` (usage error /
  file not found / not valid JSON), `2` (unexpected error),
  `3` (schema-validation errors found).
- `--version` / `--help`: `0` (printed).

For the per-subcommand option list, JSON-status envelope shapes, and
the parse output schema, run `AccessHarvester --help`.

---

## Versioning convention

The CLI surfaces both its own semver and the schema version of the
manifests it produces:

```
> AccessHarvester --version
AccessHarvester 1.0.0 (schema v1.0)
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
`AccessHarvester-1.0.0-schema-v1.0-win-x64.exe`. The CLI's `--version`
output is the runtime authority — if the filename and the runtime
output disagree, trust the runtime output and re-cut the release.

---

## Verifying an install

Smoke-check the installed binary with four zero-cost invocations:

```bash
AccessHarvester --version
# AccessHarvester 1.0.0 (schema v1.0)

AccessHarvester --help
# (full usage text on stderr; exit 0)

AccessHarvester parse path\to\a\sample.bas
# {"status":"parsed", "errors":[], "tree_stats": {...}}  on stdout

AccessHarvester validate --manifest path\to\a\manifest.json
# Validation passed (1 manifest(s) checked against schema v1.0).  on stderr; exit 0
```

These four calls exercise the dispatcher, the help text, the parse
pipeline, the embedded-schema validator, and the JSON-emit path without
touching Microsoft Access. If any of them fail, the install is broken;
do not proceed to `extract` or `run`.

For the `validate` smoke check, pass any `manifest.json` produced by a
prior `run` invocation, or use the `examples/northwind-output.json` file
from the repository. The embedded schema is the same as
`docs/test-cases/harvester-schema.v1.0.json` at build time — no schema
file on disk is required at runtime.

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
- [Session set: `harvester-finish-cli-packaging`](../session-sets/harvester-finish-cli-packaging/spec.md)
  — the planning and acceptance criteria behind this distribution.
