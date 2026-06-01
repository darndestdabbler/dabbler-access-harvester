# Harvester SQL Migration Generator Spec (Capstone)

> **Purpose:** Add a `generate-migration` CLI subcommand that emits a
> runnable **SQL Server** migration script — `CREATE TABLE` (with types,
> nullability, defaults, primary keys, foreign keys, and identity handling)
> plus data-load (`INSERT` / bulk) — from the harvester's already-captured
> artifacts: the harvested table metadata (`ir/Tables` + `ir/Relationships`),
> the reviewed `classification.json`, and the sanitized CSV-per-table output
> of the `sanitize` subcommand. This is **Phase 4 of
> [`docs/planning/data-classification-plan.md`](../../planning/data-classification-plan.md)**
> and the **feature capstone** of the mechanical harvester: it turns the
> classify -> sanitize chain's intermediate artifacts into a final,
> high-value product, making the classify -> sanitize -> migrate pipeline
> end-to-end useful.
> **Created:** 2026-06-01
> **Session Set:** `docs/session-sets/harvester-sql-migration-generator/`
> **Workflow:** Orchestrator -> AI Router -> Cross-provider verification.
> Full tier; no UI, so no UAT/E2E.
> **Status:** Draft (authored 2026-06-01 from the
> [`harvester-cli-distribution` next-set recommendation](../harvester-cli-distribution/next-session-set-recommendation.md),
> after the two pre-flight gates were resolved — see "Pre-flight" below).

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites: []
totalSessions: 3
```

> Rationale: a CLI/data-artifact feature — deterministic SQL generation +
> hermetic dialect/round-trip tests + a live end-to-end emit against a
> committed fixture + cross-provider verification. No UI, so no human UAT or
> browser E2E. There is no SQL Server in the test environment, so the emitted
> script is validated by hermetic parsing/dialect checks and a golden-script
> comparison rather than by executing it against a live SQL Server (an
> optional manual run is a non-blocking spot-check, mirroring the
> report-baseline PDF spot-check).

---

## Pre-flight (resolved before authoring, per the next-set recommendation)

The recommendation carried a **stop-rule** ("stop if the sanitized output +
`classification.json` are insufficient to generate a FK-consistent SQL Server
migration") and a **belongs-here-vs-future-migration-repo** scope question,
both to be surfaced before the spec. Both were resolved 2026-06-01:

- **Metadata sufficiency — SUFFICIENT (stop-rule does not fire).** Each
  `ir/Tables/<t>.ir.json` carries the verbatim Access `Ddl` (column types,
  sizes, `NOT NULL`, `DEFAULT` including `Date()`/`True`/literals,
  `PRIMARY KEY`, and `FOREIGN KEY ... REFERENCES ... ON UPDATE CASCADE`) plus
  structured per-field metadata (`TypeCode`/`TypeName`/`Size`/`Required`/
  `DefaultValue`/`ValidationRule`/`Attributes`/`OrdinalPosition`/`ForeignName`),
  with a separate `ir/Relationships` tier as a second FK source. Autonumber
  is captured (`COUNTER` in the DDL / `isAutoNumber` in `classification.json`),
  and the plan's `preserveAutonumber` flag + `IDENTITY_INSERT` handling
  (plan §5) cover identity. The known gaps are **flaggable degradations, not
  blockers**: custom identity seed/increment (Access does not expose it — map
  to `IDENTITY(1,1)` and note), calculated/computed columns (rare; flag),
  explicit collation (use the target DB default), and OLE Object / multi-value
  fields (plan §8.2 already says these do not migrate cleanly — flag, do not
  fabricate).
- **Scope — belongs in the harvester (owner-directed 2026-06-01).** This is a
  deterministic, mechanical transform of already-harvested metadata + sanitized
  data into a text artifact — the same character as `generate-docs` and
  `report-baseline`. Runtime/application migration (the Blazor host, data-load
  orchestration against a live server) stays deferred to the future migration
  repo.

---

## Inputs and Output

**Inputs (all already produced by existing subcommands):**
1. **Schema** — `ir/Tables/<t>.ir.json` (DDL + fields) and `ir/Relationships`
   for FK constraints. The DDL string is the primary, structured fields are
   the cross-check.
2. **Decisions** — reviewed `classification.json` (per-column category,
   `humanOverride.preserveAutonumber`, `maskingStrategy`, FK preserve/remap).
3. **Data** — the `sanitize` subcommand's CSV-per-table output (the masked /
   preserved row values). Session 1 confirms the exact on-disk shape/location
   of the sanitize output as its first input-contract step.

**Output:** a SQL Server migration script (or ordered set of scripts) under a
new output location, plus a manifest indexing what was emitted and any
degradations (OLE/multi-value/calculated columns flagged, not fabricated).

## Scope

1. **Access -> SQL Server type mapping + DDL generation.** Map Access types to
   SQL Server (`COUNTER`->`INT IDENTITY(1,1)`, `TEXT(n)`->`NVARCHAR(n)`,
   `LONGTEXT`/memo->`NVARCHAR(MAX)`, `DATETIME`->`DATETIME2`, `BIT`->`BIT`,
   `CURRENCY`->`MONEY`/`DECIMAL`, `DOUBLE`/`SINGLE`/`LONG`/`INTEGER`->the SQL
   numeric, etc.), preserving nullability, defaults, and `PRIMARY KEY`. Emit
   `FOREIGN KEY` constraints from `ir/Relationships` (cross-checked against the
   DDL), FK-consistent.
2. **Data-load generation.** Emit `INSERT` (or a bulk-load form) from the
   sanitized CSV-per-table data, honoring `classification.json` preserve/remap/
   mask decisions and **FK-safe table ordering** (topological sort: parents
   before children). Handle identity with `SET IDENTITY_INSERT ON/OFF` per the
   `preserveAutonumber` flag (plan §5).
3. **Honest degradation reporting.** Columns/tables that cannot migrate cleanly
   (OLE Object, multi-value, calculated, unmappable type) are emitted as
   flagged degradations in the output manifest — never silently dropped, never
   fabricated.
4. **CLI subcommand + docs.** Wire `generate-migration` into the CLI (mirroring
   the existing subcommand contract: exit codes, `--json-status`), and document
   it in the interpretation guide + install guide.

## Out of Scope

- Executing the script against a live SQL Server, or shipping a SQL Server
  instance in the test env (validate hermetically; an actual run is an optional
  manual spot-check).
- `.mdb`/`.accdb` target generation (plan open-question 8.2 — SQL Server only).
- Application/runtime migration (Blazor host, ORM models) — future migration repo.
- Re-deriving classification or sanitization (those are upstream, already shipped).
- Synthetic fresh-row generation (the sanitize output masks rows in place; full
  FK-graph synthesis is explicitly deferred — plan §9.1).

## Stop Rule

Session 1 re-confirms type-mapping coverage against the **actual committed
fixture columns** (HomeHealthCare + Northwind subsets). If a fixture column
carries an Access type with **no safe SQL Server mapping** that also cannot be
honestly represented as a flagged degradation, stop and surface to the human
before proceeding — do not fabricate a lossy mapping silently.

---

## Sessions

### Session 1 of 3: Input contract + Access->SQL Server DDL generation

**Steps:**
1. Confirm the `sanitize` output on-disk contract (CSV-per-table shape/location)
   and assemble the generator's inputs (`ir/Tables`, `ir/Relationships`,
   `classification.json`, sanitize CSVs) behind a small input model.
2. Implement the Access->SQL Server **type map** and **`CREATE TABLE`
   generation** (types, nullability, defaults, PK), with unmappable types
   routed to flagged degradations.
3. Emit `FOREIGN KEY` constraints from `ir/Relationships` (DDL cross-checked).
4. Hermetic tests against a synthetic schema covering each type-map branch,
   nullability/default, PK, FK, and the degradation path. Re-confirm the
   stop-rule against the committed fixtures.

**Creates:** `docs/session-sets/harvester-sql-migration-generator/ai-assignment.md`

**Ends with:** Deterministic SQL Server DDL (CREATE TABLE + PK + FK) generated
from harvested metadata, with unmappable columns flagged, proven by hermetic
tests; type-map coverage confirmed against the real fixtures.

**Progress keys:** `session-1/input-contract`,
`session-1/type-map-and-ddl`, `session-1/foreign-keys`

---

### Session 2 of 3: Data-load + identity + FK-safe ordering

**Steps:**
1. Emit data-load (`INSERT`/bulk) from the sanitized CSV-per-table data,
   honoring `classification.json` preserve/remap/mask decisions.
2. Implement FK-safe **topological table ordering** (parents before children;
   detect and surface cycles) and `SET IDENTITY_INSERT ON/OFF` per
   `preserveAutonumber`.
3. Hermetic tests: ordering correctness (including a cycle case),
   identity-insert framing, value escaping/typing in `INSERT`, and
   preserve/remap/mask honoring.

**Ends with:** A complete, FK-consistent, identity-correct migration script
(DDL + data-load) generated deterministically from the committed artifacts.

**Progress keys:** `session-2/data-load`, `session-2/fk-safe-ordering`,
`session-2/identity-insert`

---

### Session 3 of 3: CLI subcommand + end-to-end emit + docs + closeout

**Steps:**
1. Wire the `generate-migration` CLI subcommand (exit codes, `--json-status`,
   output layout + a generated migration manifest with degradations).
2. End-to-end: emit the migration script for a committed fixture subset
   (HomeHealthCare and/or Northwind); validate the emitted SQL hermetically
   (dialect/parse check + golden-script comparison). Optional non-blocking
   manual run against a real SQL Server is documented, never gated.
3. Update the interpretation guide + install guide; update ROADMAP/change-log;
   run focused validation, closeout, and the Step 9 reorganization review.

**Ends with:** `generate-migration` ships; the classify -> sanitize -> migrate
pipeline is end-to-end useful; docs describe the new artifact and its
degradation semantics.

**Progress keys:** `session-3/cli-subcommand`,
`session-3/end-to-end-emit`, `session-3/docs-and-closeout`

---

## Deliverables

- A `generate-migration` CLI subcommand emitting a runnable SQL Server
  migration script (DDL + data-load) from the harvested metadata + sanitized
  data + reviewed classification.
- FK-consistent, identity-correct output with honest degradation flagging.
- Hermetic dialect/round-trip + golden-script tests; an end-to-end emit against
  a committed fixture.
- Interpretation-guide + install-guide coverage of the new artifact.

## Provenance

Phase 4 of the data-classification plan and the long-standing feature capstone
(recommended by `harvester-cli-distribution`; deferred "until CI is reliable",
now satisfied). This is the **second and last** of the two remaining harvester
sets — after it closes, the repo is **consumer-driven-only** (no self-initiated
work; new sets only on a concrete downstream-consumer request).
