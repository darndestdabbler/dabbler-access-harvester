# Harvester CLI Validate Subcommand Spec

> **Purpose:** Add an `AccessHarvester validate` subcommand that
> validates a `run` output (or a standalone `DatabaseManifest`
> document) against the embedded `harvester-schema.v1.0.json`. Today
> downstream consumers wanting to "smoke-validate the IR JSON" have
> to wire up a JSON Schema validator themselves and locate the schema
> file — the harvester ships everything needed but exposes no
> consumer-facing surface for the check.
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-cli-validate-subcommand/`
> **Prerequisite:** None hard-blocking. Soft-prefers
> `harvester-schema-v1-adr-followups` to be complete first so the
> validator runs against a schema with all known polymorphism issues
> resolved — but the validator can ship without that and consumers
> simply see the existing schema issues surface explicitly, which is
> arguably what they want.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: New CLI subcommand verified by unit + integration tests
> in the existing `tests/AccessHarvester.*` suites and a packaged-
> binary smoke test (same pattern as `parse`, `generate-docs`, etc.).

---

## Project Overview

`docs/harvester/install.md` advertises five subcommands today:
`extract`, `translate`, `run`, `generate-docs`, and `parse`. Behind
the curtain, the harvester also produces and maintains a JSON Schema
(`docs/test-cases/harvester-schema.v1.0.json`) describing the
`DatabaseManifest` envelope that `run` emits. Consumers — including
the `Dabbler.NorthwindToBlazor` consumer authoring Set 001 Session 2 —
need to confirm that an IR JSON produced by `run` actually conforms to
the schema before downstream pipelines depend on it.

Today they can do this only by:

- Cloning the harvester repo to get the schema file, or
- Asking the maintainer to send the schema, or
- Reading it out of the test-cases folder if they happen to be
  co-located.

A `validate` subcommand closes that gap: the schema ships embedded
inside the self-contained exe, and consumers run
`AccessHarvester validate --in <run-output-root>` to get a pass/fail
verdict with structured error reporting on failure.

### In scope

1. **New subcommand: `AccessHarvester validate`.**
   - `validate --in <output-root-dir>` — finds the
     `DatabaseManifest.json` (or equivalent root document) under the
     `AccessMetaData/<dbName>/` layout `run` produces and validates
     against the embedded v1.0 schema.
   - `validate --manifest <path>` — validates a single specified
     manifest file. Convenience for pipelines that move the manifest
     around.
   - `--json-status` — same envelope convention the other
     subcommands use; consumers parse the structured response.
2. **Embedded schema resource.** `harvester-schema.v1.0.json` is
   compiled into the CLI binary as an embedded resource so consumers
   don't need to download or locate it separately. The schema source
   of truth stays at `docs/test-cases/harvester-schema.v1.0.json`;
   the build copies / embeds it into `AccessHarvester.Cli`.
3. **Exit-code contract.** Follows the existing convention:
   - `0` — validation passed.
   - `1` — usage error (bad args, file not found, manifest unparseable
     as JSON before schema validation begins).
   - `2` — unexpected error (I/O failure, embedded-schema load
     failure).
   - `3` — schema-validation errors found. Error list emitted on
     stderr (human-readable) and in the `--json-status` envelope
     (structured).
4. **Error reporting shape.** Each validation error includes the
   JSON Pointer to the offending location, the schema rule that
   failed, a one-line human-readable description, and a short
   suggestion when the rule is one of the well-known v1.0 gotchas
   (e.g., `PredicateNode` polymorphism, casing).
5. **Tests.** Unit tests against representative valid + invalid
   fixtures. Integration test against the existing
   `docs/test-cases/*.expected.json` test corpus (after casing
   migration — see Prerequisite note). Packaged-binary smoke test
   that runs `validate` against a fixture and asserts exit code +
   stderr shape, matching the pattern in
   `tests/AccessHarvester.Integration/Cli/PackagedBinarySmokeTests.cs`.
6. **Documentation.** `install.md` "How to invoke" section grows a
   `validate` row, the "Verifying an install" section grows a
   `validate` step against a bundled tiny fixture (so smoke-check
   doesn't require a real Access run), and the README's CLI section
   reflects the new subcommand.
7. **Instruction-file updates.** Per house convention, surface the
   new subcommand wherever AI orchestrators look:
   `docs/harvester/install.md` (primary), top-level `README.md`,
   `CLAUDE.md` / `AGENTS.md` / `GEMINI.md` where they enumerate the
   CLI surface.

### Out of scope

- Validation against schema versions other than v1.0. Backward
  compatibility for `v0` is handled by
  `harvester-schema-v1-adr-followups`' migration script; the
  `validate` subcommand targets the current operative schema only.
- Schema authoring / editing. The validate subcommand consumes the
  schema; it doesn't shape it.
- Automatic remediation (fix-on-fail). Validation reports; consumer
  decides what to do.
- A `validate` mode that re-extracts from the source `.accdb`. The
  subcommand operates on harvester output, not Access input.
- Pretty-printing or HTML-formatting validation reports. Stderr +
  JSON-status envelope is the surface; downstream consumers format
  for their own UIs.

---

## Sessions

### Session 1 of 1: Implement, Test, Document

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and
   `docs/schema/v1-adr.md` (to internalize the current v1.0 known
   issues so the error-suggestion table in #4 hits the real ones).
2. Pick a JSON Schema validator library that runs against
   `harvester-schema.v1.0.json` cleanly. Document the choice
   (likely `JsonSchema.Net` or `NJsonSchema` — Session 1 picks based
   on .NET 10 compatibility and how cleanly it surfaces JSON Pointer
   paths).
3. Wire the schema into `AccessHarvester.Cli` as an embedded
   resource. Build-time copy from `docs/test-cases/` so the source
   of truth doesn't fork.
4. Implement the `validate` subcommand under
   `AccessHarvester.Cli/Commands/` (or wherever existing
   subcommands live). Mirror the option-parsing patterns of
   `parse` / `generate-docs`.
5. Implement error reporting with JSON Pointers + the well-known-
   gotcha suggestion table.
6. Unit tests for: valid manifest → exit 0; invalid manifest (one
   each: missing required field, type mismatch, unknown enum value,
   `PredicateNode` polymorphism — if still unresolved); usage errors
   (file not found, unparseable JSON before schema check).
7. Integration test: walk the `docs/test-cases/` fixtures (post
   casing-migration if that work has landed); each one validates.
8. Packaged-binary smoke test: runs `validate` from the published
   exe against a tiny embedded fixture.
9. Update `docs/harvester/install.md` per #6 in scope.
10. Update README + AI-instruction files per #7 in scope. Use
    `route(task_type="documentation")` for the refresh passes.
11. `dotnet build && dotnet test` sequentially.
12. End-of-session verification + commit + push + notify.
13. Closeout: `disposition.json`, `change-log.md`,
    `next-session-set-recommendation.md`,
    `mark_session_complete()` + notify.

**Creates:**
- `AccessHarvester.Cli/Commands/ValidateCommand.cs` (or equivalent
  location).
- Unit + integration + packaged-binary tests for `validate`.
- `docs/session-sets/harvester-cli-validate-subcommand/session-reviews/session-001.md`
- `docs/session-sets/harvester-cli-validate-subcommand/change-log.md`
- `docs/session-sets/harvester-cli-validate-subcommand/disposition.json`

**Touches:**
- `AccessHarvester.Cli/AccessHarvester.Cli.csproj` (embedded resource
  + new package reference for the JSON Schema validator).
- `AccessHarvester.Cli/Program.cs` (dispatcher gets a new subcommand
  entry).
- `docs/harvester/install.md` (new subcommand + verification step).
- Top-level `README.md`.
- `CLAUDE.md`, `AGENTS.md`, `GEMINI.md`.

**Ends with:** `AccessHarvester validate --in <output>` returns
0 on a schema-conformant `run` output and 3 with structured errors
on a non-conformant one. The packaged-binary smoke test confirms
the exe ships with the validator wired up.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **No .NET 10-compatible JSON Schema validator handles the v1.0 schema features** | Library evaluation reveals `oneOf` / `$defs` / discriminator features the v1.0 schema relies on are unsupported or buggy in candidate libs | Stop. Surface to the human. Decide between (a) downgrading schema features, (b) hand-rolling a focused validator, (c) deferring the subcommand. Do not ship a validator that silently passes invalid documents. |
| **Embedded schema balloons binary size** | The embedded JSON Schema + validator library push the exe well past its current ~36 MB footprint | Investigate compression / lazy-load. The packaging set's 500 MB stop rule still applies; well below that the user judges acceptability. |
| **PredicateNode polymorphism makes most real outputs fail validation** | The ADR's known issue, if still unresolved, makes `validate` against a real Northwind `run` output fail noisily on `PredicateNode` types | Document the known issue prominently in the validation-error suggestion table; do not silently pass it. Sequence with `harvester-schema-v1-adr-followups` ideally completing first. |

---

## Why now

`Dabbler.NorthwindToBlazor` Set 001 Session 2 says "Smoke-validate the
IR JSON (no schema-validator errors)" and that consumer currently has
no first-class way to do it short of pulling the schema file out of
this repo and wiring up its own validator. Across hundreds of future
consumer projects, "wire your own validator" is N copies of the same
thing that the harvester ships everything for. A first-class subcommand
makes the validation step a single CLI call per project.
