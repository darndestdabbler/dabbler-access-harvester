# Batch Operations Guide

This document tells a downstream consumer (AI tooling agent,
migration orchestrator, scripted harvest runner) how to drive
`AccessHarvester run` across many `.accdb` files in one pass — the
"hundreds of databases" scale at which the surface-level CLI usage
documented in [`install.md`](install.md) needs care.

The harvester is a **single-DB-per-process** tool with **multi-DB
output layout baked into the directory shape**. There is no
`--db-list` flag and no multi-DB pipeline; batching is the
consumer's responsibility, and the harvester's contract is that as
long as the consumer keeps using the same `--out` root and drives
each DB sequentially, the resulting tree (and the cross-DB roll-up
index inside it) describes the full corpus.

If you only need a quick reference, the **five things to know** are:

1. Use the same `--out <root>` for every DB. The harvester writes
   each DB under `<root>/AccessMetaData/<dbName>/` so the per-DB
   trees coexist without colliding.
2. **One DB at a time.** Never run two `AccessHarvester` processes
   against the same `.accdb` (or even against different `.accdb`
   files in the same `--out` root) in parallel. Access COM is
   single-host.
3. Default is **clean re-harvest**: the per-tier directory is
   wiped before write, so deleted source objects do not leave
   orphan IR files. Override with `--no-clean` for in-place merge.
4. `<root>/AccessMetaData/index.json` is the **cross-DB roll-up**.
   It is rewritten on every successful `run` from a re-scan of the
   sibling per-DB `manifest.json` files; consumers can poll the
   roll-up to discover which DBs are in the corpus without
   filesystem scanning.
5. Add `--json-status` to every invocation. The status envelope
   carries `HarvesterVersion`, `StartedAt`, `DurationMs`, and
   `Clean` so a batch runner can record per-DB provenance and
   reconcile failures.

The rest of this document expands each point.

---

## Output layout for many databases

Every successful `AccessHarvester run --db <X.accdb> --out <root>`
writes its three-tier output under:

```
<root>/AccessMetaData/<dbName>/
    raw/          # Access-native artifacts (SaveAsText, DAO dumps)
    kb/           # clean parsed JSON
    ir/           # consumer-facing tier (raw/kb + mechanical enrichers)
    manifest.json
    unresolved-items.json
    READ-ME-FIRST.md
```

`<dbName>` is the source `.accdb` filename without extension —
`HomeHealthCare.accdb` lands under
`<root>/AccessMetaData/HomeHealthCare/`. Two databases with the
same filename (e.g., the same database checked into two source
locations) would collide; the batch driver is responsible for
renaming or routing them to separate `--out` roots.

The `<root>/AccessMetaData/` interlock is fixed. The harvester
constructs `BaseDir = <root>/AccessMetaData/<dbName>` and writes
all per-DB content under that — even when only one database is
being harvested. A consumer that expects per-DB content directly
under `<root>` will not find it.

After running the harvester against N databases under the same
root, the consumer's `<root>` looks like:

```
<root>/
└── AccessMetaData/
    ├── index.json                          # cross-DB roll-up (Pipeline.EmitRootIndex)
    ├── HomeHealthCare/
    │   ├── manifest.json
    │   ├── READ-ME-FIRST.md
    │   ├── unresolved-items.json
    │   ├── raw/  kb/  ir/
    │   └── ...
    ├── NorthwindDevEdition/
    │   └── ...
    └── <other DBs>/
```

A binary-only consumer that opens `<root>` cold should start at
`<root>/AccessMetaData/index.json` to discover which DBs are
present, then traverse each per-DB tree by opening that DB's
`manifest.json`. The per-DB `READ-ME-FIRST.md` is a one-paragraph
human hand-off pointing at the same entry point.

---

## Sequential-only operation

Access COM is single-host. Two `AccessHarvester` processes running
concurrently against the *same* `.accdb` will race on ACE OLE DB
file locks and one will fail or time out. Two processes against
*different* `.accdb` files in the *same* `--out` root will not
race on file locks but will both try to spawn `MSACCESS.EXE`
instances and to attach to overlapping COM handles — undefined
behaviour, and observed to wedge the host.

The contract is:

- **One harvester process at a time per host.** A batch driver
  runs each DB to completion (or failure) before starting the
  next.
- **No threading inside the CLI.** Even if a future flag invited
  it, the COM pipeline is built around per-process isolation and
  is not safe to share across threads.
- **Quiesce other Access usage on the host.** Close any open
  Access GUI session before starting a batch; an Access instance
  the user opened manually is a sibling of the harvester's
  spawned `MSACCESS.EXE` and competes for the same COM machinery.

A typical batch driver (PowerShell):

```powershell
$root = "C:\harvest-out"
$dbs  = Get-ChildItem -Path "C:\dbs" -Filter "*.accdb"

foreach ($db in $dbs) {
    Write-Host "Harvesting: $($db.Name)"
    & "AccessHarvester.exe" run `
        --db $db.FullName `
        --out $root `
        --json-status |
      Tee-Object -FilePath "$root\runlog\$($db.BaseName).json"

    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3) {
        Write-Warning "Run failed for $($db.Name) with exit $LASTEXITCODE"
        # Decide: stop the batch, skip, or retry. The harvester does
        # not retry on its own; the batch driver owns the policy.
    }
}
```

Exit codes are documented in [`install.md`](install.md): `0` =
all OK, `1` = usage error, `2` = unexpected error, `3` = completed
with per-object failures. A `3` is not a process-level failure —
the per-DB output is still on disk and the per-object failure list
is in the `--json-status` envelope (and in the manifest's
`Diagnostics` array); a batch driver typically treats `3` as
"continue" and only stops on `1` / `2`.

---

## MSACCESS and RCW hygiene across runs

The harvester instantiates Access via DAO and ACE COM, hands those
objects to the extractor, and releases them at end-of-run by
disposing the per-DB `AccessComSession`. On a clean exit, the
spawned `MSACCESS.EXE` and the `conhost.exe` it borrows are torn
down by the runtime callable wrapper's release path.

On an **unclean exit** — a process crash, a SIGKILL, or a host
power loss — the spawned `MSACCESS.EXE` and any orphan `conhost.exe`
can persist. On Windows they hold file handles on the target
`.accdb`, and a subsequent run sees ACE OLE DB "file in use"
errors. A batch driver running over many DBs is the most likely
context for this to surface.

**Pre-batch and inter-batch hygiene:**

```powershell
# Confirm no stale Access process is holding a target .accdb.
Get-Process MSACCESS -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process conhost  -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -eq "" } |
    Stop-Process -Force
```

The harvester itself does not run this — the batch driver does. If
a run starts with stale orphans in place, the symptom is
ACE OLE DB `Could not lock file` errors on the first extractor call;
the fix is to terminate the orphans and re-run.

The Python utility shipped under
[`ai_router.utils`](../../ai_router/) exposes
`cleanup-dev-orphans` for the same purpose with finer-grained
control (it can also shut down `dotnet build-server` instances and
match by working-directory path); see
`docs/planning/project-guidance.md` → *Bare-repo + flat-worktree
layout* for the recipe.

If a batch is hung on a single DB and the host is otherwise
healthy, the safe response is to kill that DB's spawned
`AccessHarvester` + `MSACCESS` pair, advance the batch driver to
the next DB, and quarantine the failing `.accdb` for a manual
investigation rather than retrying — a `.accdb` whose harvest
deadlocks is almost always corrupt or holding an unbroken
session lock from another process.

---

## `--clean` / `--no-clean` semantics

The `run`, `extract`, and `translate` subcommands accept
`--no-clean` (default is **clean**). The behaviour is per-tier:

| Subcommand   | Tier(s) wiped on default clean        | What `--no-clean` does                                |
|--------------|---------------------------------------|-------------------------------------------------------|
| `extract`    | `raw/` for this DB                    | Skips the wipe; merges over the prior tier in place.  |
| `translate`  | `kb/` for this DB                     | Skips the wipe; merges over the prior tier in place.  |
| `run`        | `raw/`, `kb/`, and `ir/` for this DB  | Skips the wipe; merges over the prior tiers in place. |

The clean is *target-directory level*, not file-by-file. The wipe
runs **before** the harvester writes anything new, so a re-run of
the same DB whose source objects have changed produces an output
tree that reflects the **current** source — deleted objects do
not leave orphan IR files behind. This was Finding #4 from the
original consumer's harvest of `NorthwindDevEdition`, where the
prior file-by-file overwrite let stale objects accumulate across
re-runs.

The clean only wipes **this DB's** tier directories. Sibling DBs
under the same `<root>/AccessMetaData/` (other databases harvested
to the same root) are untouched — including their per-DB
`manifest.json` and `READ-ME-FIRST.md`. The cross-DB
`index.json` is also untouched by the wipe; it is rebuilt from
scratch at the end of the run (see *Cross-DB roll-up index*
below).

### `--types` + default clean interact destructively

`--types <comma-list>` filters the categories the harvester
processes. Combined with the default clean, the per-tier
directory is wiped **wholesale** before the selected categories
are repopulated. The consequence: `run --types Forms` against an
existing output tree clears `raw/`, `kb/`, and `ir/` for the DB
and then repopulates only Forms — `Reports/`, `Queries/`,
`Tables/`, etc. from prior runs are **gone**.

To do an incremental partial re-harvest that preserves prior
categories, pass `--no-clean` alongside `--types`:

```
AccessHarvester run --db X.accdb --out C:\out --types Forms --no-clean
```

The CLI's `--help` text spells out this hazard verbatim; this
section restates it because batch drivers reach for `--types`
exactly when re-harvesting a subset.

### When to use `--no-clean`

Use `--no-clean` when:

- A targeted re-harvest needs to land alongside prior content for
  the same DB without losing the unselected categories
  (the `--types` case above).
- A consumer pre-staged hand-curated content in `raw/` or `kb/`
  and needs the harvester to merge on top without clobbering.

Otherwise prefer the default clean. The cost of a fresh tier
wipe is one `Directory.Delete(recursive: true)` plus a re-write;
the cost of *not* wiping is orphan IR files that downstream
consumers cannot distinguish from current ones.

---

## Cross-DB roll-up index

After every successful `run`, the harvester re-scans
`<root>/AccessMetaData/<*>/manifest.json` and writes a roll-up
to `<root>/AccessMetaData/index.json`:

```json
{
  "SchemaType": "AccessMetaDataRootIndex",
  "SchemaVersion": "1.0",
  "Databases": [
    {
      "Name": "HomeHealthCare.accdb",
      "Manifest": "HomeHealthCare/manifest.json",
      "SchemaVersion": "1.1"
    },
    {
      "Name": "NorthwindDevEdition.accdb",
      "Manifest": "NorthwindDevEdition/manifest.json",
      "SchemaVersion": "1.1"
    }
  ]
}
```

**Properties** a consumer can rely on:

- **`SchemaType: "AccessMetaDataRootIndex"`** lets a consumer that
  loads JSON without a path discriminate the file from a
  per-DB manifest.
- **`SchemaVersion`** on the index itself is independent of the
  per-DB manifest schema version; each entry's per-DB
  `SchemaVersion` reports the schema the corresponding
  `manifest.json` declares.
- **`Databases[].Manifest`** is a forward-slash relative path
  from the index file, valid on any OS that can read paths case-
  sensitively. The index is portable to a tool that consumes it
  as JSON without filesystem assumptions.
- **Entries are sorted by `Name` (ordinal).** Consumers can rely
  on byte-stable ordering across runs.
- **Manifests that fail to parse are skipped silently.** A
  half-written or corrupt per-DB manifest does not poison the
  index for the surviving DBs; the failure surfaces through the
  failing run's own `--json-status` envelope and through the
  per-DB manifest's `Diagnostics` array.
- **The index does not carry timestamps or durations.** The roll-up
  is determinism-safe: re-running on the same set of per-DB
  manifests produces byte-identical `index.json`.

**Updating the index without re-running a harvest.** When a DB is
removed from disk (manually deleted under
`<root>/AccessMetaData/<dbName>/`) the surviving index still
lists it until the next `run`. To refresh without a full
re-harvest, a batch driver can call the public
`Pipeline.EmitRootIndex(outputRoot)` entry point in
`AccessHarvester.Core`. The CLI does not currently surface a
"just refresh the index" subcommand; the next refresh happens on
the next `run` against any DB under the same root.

---

## Status envelope provenance

Pass `--json-status` on every batched invocation. The envelope
goes to **stdout** (human messages stay on stderr), so a batch
driver can pipe stdout directly to a per-DB log file.

The envelope for `run` / `extract` / `translate` carries:

| Field              | Determinism            | Meaning                                                       |
|--------------------|------------------------|---------------------------------------------------------------|
| `Command`          | deterministic          | `"run"`, `"extract"`, `"translate"`, etc.                     |
| `HarvesterVersion` | deterministic          | Semver of the binary; matches `manifest.HarvesterVersion`.    |
| `StartedAt`        | **non-deterministic**  | UTC ISO-8601 timestamp at process start.                      |
| `DurationMs`       | **non-deterministic**  | Wall-clock duration of the run.                               |
| `Database`         | deterministic          | Absolute path to the source `.accdb` (for `run` / `extract`). |
| `OutputRoot`       | deterministic          | The `--out` value resolved to an absolute path.               |
| `BaseDir`          | deterministic          | `<OutputRoot>/AccessMetaData/<dbName>`.                       |
| `RawDir`, `KbDir`, `IrDir` | deterministic  | Per-tier paths under `BaseDir`.                               |
| `Clean`            | deterministic          | The effective per-tier clean mode (true = default, false = `--no-clean`). |
| `ExitCode`         | deterministic          | The process exit code (`0` / `3` for successful runs).        |
| `Extract`, `Translate`, `Enrich` | deterministic | Per-category counts and failure lists.                     |

**`HarvesterVersion` consistency.** The version surfaced in the
`--json-status` envelope is **identical** to the
`HarvesterVersion` written into `manifest.json` for the same run.
Both are resolved through `DatabaseManifest.ResolveHarvesterVersion()`
— a single canonical helper that strips the SourceLink `+<commit-sha>`
suffix from the assembly version. A batch driver can use the
envelope's `HarvesterVersion` as the per-DB provenance stamp
without re-opening the manifest.

**Determinism exclusions.** The two non-deterministic fields
(`StartedAt`, `DurationMs`) are intentionally not part of the
manifest's determinism gate — `manifest.json` itself excludes
`ExportedAt`, the cross-DB `index.json` carries no timestamps,
and the determinism gate compares manifests byte-for-byte after
normalising those fields out. The status envelope is the
*right* place to record provenance per-run; the manifest is the
*right* place to keep canonical schema content. A batch driver
that wants a full audit trail records both the envelope and the
manifest, then diffs across runs on the manifest only.

---

## Recipes

### Batch-harvest a directory of `.accdb` files

```powershell
$root  = "C:\harvest-out"
$logs  = Join-Path $root "runlog"
New-Item -ItemType Directory -Force -Path $logs | Out-Null

Get-ChildItem -Path "C:\dbs" -Filter "*.accdb" | ForEach-Object {
    $db  = $_.FullName
    $log = Join-Path $logs ($_.BaseName + ".json")
    Write-Host "[$([DateTime]::Now.ToString('s'))] $($_.Name)"

    & "AccessHarvester.exe" run --db $db --out $root --json-status > $log
    if ($LASTEXITCODE -notin 0,3) {
        Write-Warning "Exit $LASTEXITCODE for $($_.Name); see $log"
    }
}

# index.json was rewritten by the last successful run; verify.
Get-Content (Join-Path $root "AccessMetaData\index.json") | ConvertFrom-Json |
    Select-Object -ExpandProperty Databases | Format-Table Name, SchemaVersion
```

### Re-harvest one DB without disturbing siblings

```powershell
# Default clean — wipes only HomeHealthCare/{raw,kb,ir}; siblings untouched.
AccessHarvester.exe run --db C:\dbs\HomeHealthCare.accdb --out C:\harvest-out --json-status

# index.json is also rebuilt; the sibling NorthwindDevEdition entry remains.
```

### Add one category to an existing DB tree

```powershell
# Default clean would wipe the whole DB tree; use --no-clean to merge.
AccessHarvester.exe run `
  --db C:\dbs\HomeHealthCare.accdb `
  --out C:\harvest-out `
  --types Reports `
  --no-clean `
  --json-status
```

### Validate every manifest in a batch

```powershell
AccessHarvester.exe validate --in C:\harvest-out --json-status
```

`validate --in` recursively finds every `manifest.json` under
`<root>` and dispatches each to the matching embedded schema
version (`v1.0` or `v1.1`). A batch driver can run this once at
end-of-batch as the final go / no-go.

---

## Related documents

- [`install.md`](install.md) — single-DB CLI surface, install,
  smoke check.
- [`interpretation-guide.md`](interpretation-guide.md) — how a
  downstream AI consumer reads the per-DB output.
- [Project guidance](../planning/project-guidance.md) →
  *Bare-repo + flat-worktree layout* — host-level recipe for the
  Python `cleanup-dev-orphans` utility referenced above.
