# Harvester Schema v1 ADR Follow-ups Spec

> **Purpose:** Close the two "Action required" items documented in
> `docs/schema/v1-adr.md` "Known Issues" section: (a) apply the
> `[JsonPolymorphic]` / `[JsonDerivedType]` attributes (or a custom
> converter) to the `PredicateNode` hierarchy so the v1.0 schema can
> validate `PredicateNode`-containing fields, and (b) migrate the 13
> `docs/test-cases/*.expected.json` fixtures from camelCase to
> PascalCase so they validate against the v1.0 schema.
> **Created:** 2026-05-28
> **Session Set:** `docs/session-sets/harvester-schema-v1-adr-followups/`
> **Prerequisite:** None hard-blocking. The original
> `schema-finalization-and-docs` set authored the v1.0 schema +
> recorded these as "Session 2 or a subsequent resolver session must
> add these"; this set is that subsequent session.
> **Workflow:** Orchestrator → AI Router → Cross-provider verification

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
```

> Rationale: Schema-conformance work verified by JSON Schema validation
> + the existing parser test suites running against the migrated
> fixtures.

---

## Project Overview

`docs/schema/v1-adr.md` § "Known Issues" calls out two items that
block v1.0 from being the operative emitted schema:

1. **PredicateNode polymorphism requires `[JsonPolymorphic]`.**
   Without discriminator attributes (or a custom JsonConverter),
   `System.Text.Json` serializes `AndPredicate` and `OrPredicate`
   to identical JSON shapes — the schema's `NodeKind` discriminator
   field is never written, and validation against any
   `PredicateNode`-containing field fails. Affected pipeline output:
   any `QueryAbstraction.PredicateNode` tree, which means any saved
   Access query with non-trivial WHERE-clause structure.

2. **Test fixture casing migration.** All 13
   `docs/test-cases/*.expected.json` files were authored against
   v0's camelCase convention. v1.0 normalized to PascalCase
   (D20 decision). The fixtures will fail v1.0 schema validation
   until migrated. The ADR specifically calls for a migration script
   (`tools/migrate_v0_to_v1.py`) that "renames camelCase keys to
   PascalCase and wraps bare parser-output files in a
   `DatabaseManifest` envelope" — and notes that downstream consumers
   maintaining their own v0 files should be able to run the same
   script.

Closing both items lets the harvester's `run` output be the operative
v1.0 surface end-to-end: emitted output conforms, test fixtures
conform, downstream consumers (and the future
`AccessHarvester validate` subcommand) get a clean pass.

### In scope

1. **`PredicateNode` polymorphism fix.**
   - Apply `[JsonPolymorphic(TypeDiscriminatorPropertyName = "NodeKind")]`
     to the `PredicateNode` base.
   - `[JsonDerivedType(typeof(AndPredicate), "And")]`,
     `[JsonDerivedType(typeof(OrPredicate), "Or")]`, and equivalents
     for every concrete `PredicateNode` subtype in
     `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs`.
   - Confirm via round-trip test: serialize → validate against v1.0
     → deserialize → re-serialize is byte-identical.
   - Alternative: if `[JsonPolymorphic]` attributes don't cover a
     subtype shape cleanly (e.g., sealed records with positional
     constructors), implement a focused
     `JsonConverter<PredicateNode>` instead. Pick whichever is
     simpler given the actual record hierarchy.
2. **Migration script.** `tools/migrate_v0_to_v1.py` (per the ADR's
   naming) that:
   - Recursively renames camelCase JSON keys to PascalCase.
   - Wraps bare parser-output files (`{source, items, schemaVersion}`)
     in a `DatabaseManifest` envelope per the D21 mapping rule.
   - Idempotent — running it twice produces no further changes.
   - Driven by a key-mapping table the script logs at start so
     consumers can audit / extend it.
3. **Apply the migration to the 13 test fixtures.** Each
   `docs/test-cases/*.expected.json` is rewritten in-place by the
   script. Diffs are committed alongside the migration. After
   migration, every fixture validates against v1.0.
4. **Schema validation in the test suite.** Add a test (or extend
   the existing v0 validator wiring) that asserts every
   `*.expected.json` fixture validates against
   `harvester-schema.v1.0.json`. This becomes the standing regression
   guard so future fixture edits can't silently drift back to v0.
5. **ADR status update.** Edit
   `docs/schema/v1-adr.md` "Known Issues" — both items move from
   "Action required" to "Resolved" with a brief pointer to this
   set's change-log.
6. **Documentation refresh.** `docs/schema/v1-adr.md` and any
   `docs/test-cases/README*.md` (if present) updated to reflect the
   completed migration. AI-instruction files (`CLAUDE.md`,
   `AGENTS.md`, `GEMINI.md`) refreshed if they reference v0 fixture
   conventions or the v1.0 known issues.

### Out of scope

- The other ADR items listed under "Gaps Deferred to Post-v1.0"
  (D5, D6, D7, D12, D14, D15, D17, D18, D19). Those are explicitly
  deferred by the original ADR; this set does not reopen them.
- Schema v2 work. v1.0 stays the operative schema.
- A C# wrapper around the Python migration script. The ADR specifies
  Python; consumers running .NET-only environments invoke Python
  out-of-band.
- Re-running `extract` / `translate` against any Access source to
  regenerate fixtures from scratch. The migration is mechanical key
  renaming; no source re-extraction needed.
- Adding `validate` as a CLI subcommand. That's
  `harvester-cli-validate-subcommand`.

---

## Sessions

### Session 1 of 1: Fix Polymorphism, Migrate Fixtures, Close ADR

**Steps:**

1. Read `docs/planning/project-guidance.md`,
   `docs/planning/lessons-learned.md`, and
   `docs/schema/v1-adr.md` "Known Issues" section in full.
2. Inspect `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs`
   to enumerate the full `PredicateNode` subtype hierarchy. Confirm
   whether `[JsonPolymorphic]` attributes cleanly cover the record
   shapes or whether a `JsonConverter<PredicateNode>` is the better
   fit. Document the choice in the session review.
3. Apply the chosen fix to `PredicateNode` and subtypes. Add round-
   trip tests: serialize → validate → deserialize → re-serialize
   produces byte-identical output across at least three
   representative predicate trees (single `AndPredicate`, nested
   `OrPredicate(AndPredicate, ...)`, and a leaf comparison).
4. Author `tools/migrate_v0_to_v1.py`. Drive it from a published
   key-mapping table (idempotent, audited). Document the script's
   usage in a `tools/migrate_v0_to_v1.md` (or extend an existing
   tools README).
5. Run the script against `docs/test-cases/*.expected.json`. Commit
   the renamed fixtures.
6. Add the schema-conformance regression test (each fixture
   validates against `harvester-schema.v1.0.json`).
7. `dotnet build && dotnet test` sequentially. All tests pass,
   including the new schema-conformance test and the
   `PredicateNode` round-trip tests.
8. Update `docs/schema/v1-adr.md`:
   - Move both "Known Issues" items to a new "Resolved" subsection
     with a pointer to this set's `change-log.md`.
   - Update the "Back-Compatibility Plan" table if the migration
     script's existence changes the back-compat story.
9. Update AI-instruction files (`CLAUDE.md`, `AGENTS.md`,
   `GEMINI.md`) where they reference v0 fixtures or the v1.0 known
   issues. Use `route(task_type="documentation")`.
10. End-of-session verification + commit + push + notify.
11. Closeout: `disposition.json`, `change-log.md`,
    `next-session-set-recommendation.md`,
    `mark_session_complete()` + notify.

**Creates:**
- `tools/migrate_v0_to_v1.py` (per ADR naming).
- `tools/migrate_v0_to_v1.md` (or extended `tools/README.md`).
- Schema-conformance regression test (location follows existing test
  conventions — likely under `tests/AccessHarvester.*`).
- `PredicateNode` round-trip tests (same location).
- `docs/session-sets/harvester-schema-v1-adr-followups/session-reviews/session-001.md`
- `docs/session-sets/harvester-schema-v1-adr-followups/change-log.md`
- `docs/session-sets/harvester-schema-v1-adr-followups/disposition.json`

**Touches:**
- `AccessHarvester.Pipeline/Enrichment/QueryAbstraction.cs`
  (`PredicateNode` + subtypes — polymorphism attributes or converter).
- `docs/test-cases/*.expected.json` (all 13 files — migration script
  output).
- `docs/schema/v1-adr.md` (Known Issues → Resolved).
- `CLAUDE.md`, `AGENTS.md`, `GEMINI.md` if they reference v0
  conventions.

**Ends with:** Every `*.expected.json` fixture validates against
`harvester-schema.v1.0.json`; `PredicateNode` trees round-trip
through v1.0 validation cleanly; the ADR's Known Issues section
shows both items as Resolved.

---

## Stop Rules

| Stop rule | Signal | Pivot |
|---|---|---|
| **`[JsonPolymorphic]` doesn't work on the `PredicateNode` record hierarchy** | `System.Text.Json` rejects the attribute combination at runtime (e.g., because the records are sealed with positional constructors and the polymorphic-type-handling can't find a deserialization path) | Stop. Pivot to a custom `JsonConverter<PredicateNode>`. Do not change the record hierarchy shape to accommodate the serializer — the schema's discriminator should adapt to the domain types, not vice versa. |
| **Migration script produces a non-idempotent diff** | A second run of `tools/migrate_v0_to_v1.py` against already-migrated fixtures produces additional changes | Stop. The script must be idempotent — that's the consumer guarantee. Investigate the key-mapping table for ordering effects or unhandled nested-shape edge cases. |
| **A fixture that passes v0 validation cannot be cleanly migrated to v1.0** | The structural envelope changes (D21 `DatabaseManifest` wrapping) don't fit a particular legacy fixture — for example a fixture targeting only a sub-document | Document the affected fixture as a v0-only fixture pinned to the v0 schema, and exclude it from the v1.0 conformance regression. Do not force a malformed v1.0 envelope. |

---

## Why now

The v1.0 schema went out as Accepted on 2026-04-28. Six weeks later
(today, 2026-05-28) the Known Issues are still open, which means v1.0
is nominally the operative schema but two of its load-bearing
guarantees (round-trip-validatable predicate trees; conformant test
fixtures) don't hold yet. Closing both items unblocks
`harvester-cli-validate-subcommand` from shipping against a clean
v1.0 surface, and it removes the "if these are still unresolved,
prepare for noise" caveat that downstream consumer specs (including
`Dabbler.NorthwindToBlazor` Set 001 Session 2) have to carry forward.
