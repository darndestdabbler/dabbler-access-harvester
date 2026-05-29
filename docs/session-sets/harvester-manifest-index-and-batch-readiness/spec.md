# Harvester Manifest-Index & Batch-Readiness Spec

> **Purpose:** Make `manifest.json` a **complete, self-describing entry
> point** that a downstream AI consumer reads first to learn what was
> harvested, where every artifact lives, and how to interpret it — and
> harden the CLI for **batch operation across many databases**. Closes the
> gaps the first real downstream consumer hit when harvesting
> `NorthwindDevEdition.accdb` (see *Why This Set, Why Now*).
> **Created:** 2026-05-29
> **Session Set:** `docs/session-sets/harvester-manifest-index-and-batch-readiness/`
> **Prerequisite:** None new. Operates on the shipped Phase 4 surface
> (`manifest.json`, the three-tier output, the v1.0 schema, the two golden
> fixtures). Golden regeneration in Sessions 1–2 requires **Windows +
> Microsoft Access** (COM).
> **Workflow:** Orchestrator → AI Router → Cross-provider verification.
> Full tier; per-session routed cost is cross-provider session-verification
> only, plus schema/architecture routes where the orchestrator model
> doesn't already match the task type.
> **Status of this document:** **DRAFT authored by the downstream consumer
> repo** (`Dabbler.NorthwindToBlazor`, Set 001 Session 2). The human
> finalizes it here — see *Finalization checklist (human)* at the end —
> before scheduling Session 1.

---

## Session Set Configuration

```yaml
requiresUAT: false
requiresE2E: false
uatScope: none
tier: full
prerequisites: []
totalSessions: 4   # provisional — see Finalization checklist
```

> Rationale: no UI surface (consistent with the whole repo). Quality is
> guarded by build + `dotnet test` (hermetic + `[SkippableFact]`
> integration), the **output-schema validator that fails the harvester on
> drift**, the **determinism gate** on every CLI subcommand (Phase 4 F3),
> the two committed golden fixtures, and per-session cross-provider AI
> verification. Sessions that change manifest shape MUST regenerate both
> goldens and keep the determinism gate green.

---

## Why This Set, Why Now

`harvester-cli-distribution` closed Phase 4 on 2026-05-29 and cut
[v1.0.0](https://github.com/darndestdabbler/dabbler-access-harvester/releases/tag/v1.0.0).
The **first real downstream consumer** — the `Dabbler.NorthwindToBlazor`
migration repo — ran `AccessHarvester run` against
`NorthwindDevEdition.accdb` the same day (197 objects, 0 failures, `validate`
passed) and surfaced two structural problems plus four batch-readiness gaps.

The ROADMAP already anticipates exactly this: *"The harvester repo itself
moves to maintenance — bug fixes against Phase 4's golden output, schema-tier
adjustments triggered by the future repo's consumption."* This set is that
consumption-triggered maintenance.

The headline problem is conceptual, and the consumer named it precisely:
**the harvester should generate an index/manifest and point AI agents to it
— and that file must be complete and self-describing.** The harvester already
*has* that file (`manifest.json`); it is (1) silently incomplete and (2) not
actually structured as a navigation index. A consuming agent that trusts it
concludes the database has **zero tables** — the worst possible field for a
migration tool to drop.

### Findings (evidence)

> Line numbers are from a 2026-05-29 source read and may have drifted;
> reconfirm during Session 1. Verdicts are the consumer's; the orchestrator
> should re-derive them, not take them on faith.

| # | Finding | Kind | Severity | Evidence |
|---|---|---|---|---|
| **1** | **Manifest omits Tables, Relationships, and Modules.** `Summary.TableCount`/`ModuleCount` are `0` and the `Tables`/`Relationships`/`Modules` arrays are empty, despite per-file IR existing (Northwind: 29 / 29 / 18). Only Forms/Reports/Queries are populated. Reproduces in **both** golden manifests. | code | **Critical** | `DatabaseManifest.cs:73-77` (typed `Tables`/`Relationships` props default to `Array.Empty`); `DatabaseManifestAggregator.cs:53-54` (`Build()` accepts `tables`/`relationships`) + `:82-90` (`TableCount = tablesList.Count`); **`Pipeline.cs:~344-352` builds the manifest WITHOUT passing `tables`/`relationships`**; `DatabaseManifestAggregatorTests.cs:322-339` proves the populate path works. Modules: call site appears to pass `modules: moduleExports` yet output is empty — **root-cause separately** (wrong collection? serialization filter? all 18 are class-modules counted elsewhere?). |
| **2** | **Manifest is not a navigation index.** It inlines Forms/Reports/Queries content but never tells an agent the on-disk path/glob to per-object files, nor points at the interpretation guide or schema version. A consumer with only the binary + output (no source repo) has no documented way to interpret what it reads. | design | **High** | Manifest top-level keys are content arrays + `Summary`; no per-category file index, no `documentation`/`schemaGuide` pointer. The consumer had to discover `docs/harvester/interpretation-guide.md` by spelunking. |
| **3** | **Field-type semantics undocumented.** Attachment (DAO 101), OLE Object (11), Hyperlink, multi-valued, and Lookup field representation are not explained; nor is "metadata-only — no row-level binary," nor how to decode `Ddl` + `Attributes`. Northwind's `Employees.Attachments` / `ProductCategories` picture are **Attachment fields (101)**, not the OLE Objects a naive consumer expects. | docs | Medium | `TableMetadata.cs:179` (`DataType` label incl. `AttachmentField`), `:187-193` (`DataTypeCode`), `:287-290` (`Attributes` bitmask preserved for `dbHyperlinkField` etc.); `interpretation-guide.md:621-640` §5.5 covers table/rel structure only, not per-field types. |
| **4** | **Re-run leaves stale files.** `run` creates dirs and overwrites files individually but never clears the target — objects deleted from a re-harvested DB linger, drifting IR/manifest. | code | Medium | `Pipeline.cs:124,140,154,181` (`Directory.CreateDirectory` + `File.Copy(..., overwrite: true)`, no clear-target). |
| **5** | **No batch / COM-lifecycle guidance.** Nothing documents many-DB sequential operation, MSACCESS zombie/RCW hygiene across runs, or a cross-DB roll-up. | docs | Medium | `Pipeline.cs:107-108` (`BaseDir = <out>/AccessMetaData/<dbName>`, supports many DBs under one root) + `:127` (`using AccessComSession` per-DB); `README.md:38-39` (sequential-only, testing context). |
| **6** | **`--json-status` envelope lacks provenance.** No harvester version, timestamp, or duration — the consumer hand-added these to its run log. | code | Low | `manifest.json` carries `HarvesterVersion` + `ExportedAt`; the `run`/`extract`/`translate` status envelope does not mirror them. |

Strengths confirmed (so the set stays scoped): extraction is solid (197/197,
0 failures); per-field IR is rich (type code+name, size, required, default,
validation rule, input mask, format, ordinal, FK name) plus a full
`CREATE TABLE` DDL with PK/FK constraints; schema-validated; clean exit-code
contract (0/1/2/3); real deterministic release pipeline
(`.github/workflows/release.yml`, `v1.0.0` tag). **The core is good — the gaps
are all in the index/navigation layer and batch hardening, which is what
bites at the consumer's stated scale of hundreds of databases.**

---

## Cross-cutting constraints (apply to every code session)

- **Schema lockstep.** The output validator fails the harvester on schema
  drift. Session 1 changes *populate existing schema fields* (no schema
  change). Session 2 *adds* fields → an **additive `harvester-schema` bump
  (v1.0 → v1.1, backward-compatible per the repo's schema policy)** with the
  embedded copy and `docs/test-cases/harvester-schema.v1.x.json` updated
  together, plus `--version`'s schema string.
- **Golden regeneration.** Any manifest-shape change requires regenerating
  **both** committed goldens (`samples/GoldenOutput/NorthwindDevEdition/` and
  `.../HomeHealthCare/`) on Windows+Access, and updating any
  `examples/northwind-output.json` used by the `validate` smoke test.
- **Determinism gate.** Two publishes / two runs must stay byte-identical.
  New non-deterministic fields (Session 6's timestamp/duration) MUST be
  excluded from the determinism comparison the same way `ExportedAt` is, or
  they break the gate.
- **`generate-docs` already exists** and writes a `schema-summary.md`; prefer
  extending/wiring it over inventing a parallel doc generator.

---

## Sessions

### Session 1 of 4: Manifest completeness — populate Tables, Relationships, Modules (Finding #1)

**Goal:** `manifest.json` faithfully reflects every harvested category. No
schema change — only fill fields the schema already defines.

**Steps:**
1. Reproduce: run against a committed fixture; confirm `Summary.TableCount == 0`
   while `ir/Tables/` is non-empty. Diff golden vs. on-disk IR counts.
2. Fix the `run` call site (`Pipeline.cs:~344`) to pass `tables`,
   `relationships` (and confirm `modules`) into `DatabaseManifestAggregator.Build()`.
3. **Root-cause Modules independently** — determine why `ModuleCount`/`Modules`
   come out empty even though the call site appears to pass `moduleExports`.
   Fix the real cause, not the symptom.
4. Add a **regression test** asserting, per category, that
   `Summary.<X>Count == manifest.<X>.Length == count of `ir/<Category>/*.ir.json``
   on disk — the invariant that would have caught this. (Mirror the existing
   `DatabaseManifestAggregatorTests` style.)
5. Regenerate both goldens; update the `validate` smoke fixture; re-run the
   determinism gate.

**Creates/changes:** `AccessHarvester.Core/Pipeline.cs`, a manifest-completeness
regression test, regenerated goldens.
**Ends with:** Every category present in the manifest with correct counts; the
count-equality invariant is test-enforced; goldens + determinism green.

### Session 2 of 4: Manifest as a self-describing index (Finding #2)

**Goal:** A consuming agent reads `manifest.json` first and learns *what
exists, where each file is, and how to interpret it* — without prior knowledge
of the repo layout or docs. Additive schema bump v1.0 → v1.1.

**Steps:**
1. Design the index additions (route as `architecture` if not orchestrator-matched):
   - Per-category **file map**: count + relative path/glob to the per-object
     IR/KB files (e.g. `"tables": { "count": 29, "ir": "ir/Tables/*.ir.json" }`),
     enabling a cheap completeness check by the consumer.
   - A **`documentation` / `schemaGuide` pointer**: the interpretation-guide
     URL/relative path + the schema version the output conforms to, so the
     output is self-interpreting for a binary-only consumer.
2. Bump `harvester-schema` additively (embedded + on-disk + `--version` string);
   keep v1.0 inputs valid per the backward-compat policy.
3. **Decide & implement the agent entry point** (open decision — see
   Finalization): either (a) extend `manifest.json` only, or (b) also have
   `run` auto-emit a tiny top-level `READ-ME-FIRST.md`/`index.md` (wiring the
   existing `generate-docs` summary) that says "start at manifest.json."
4. Regenerate goldens; update validator/smoke fixtures; determinism gate.

**Creates/changes:** `DatabaseManifest`/aggregator, schema files, optional
`generate-docs` wiring, regenerated goldens, schema-version doc.
**Ends with:** Manifest is a complete navigation index with a documentation
pointer; schema v1.1 validates old + new; goldens + determinism green.

### Session 3 of 4: Batch-run ergonomics — stale-file clean + status provenance (Findings #4, #6, opt. cross-DB index)

**Goal:** `run` is safe and auditable to invoke across many databases in one
batch.

**Steps:**
1. **Stale-file handling (#4):** add target-clean semantics — e.g. a `--clean`
   flag that clears `<out>/AccessMetaData/<dbName>/{raw,kb,ir}` before write,
   and/or always rewrite the manifest from scratch. Document the default
   (overwrite-file-by-file leaves stale objects) vs. `--clean`. Test that a
   re-run after deleting a source object does not leave an orphan IR file.
2. **Status provenance (#6):** add `HarvesterVersion`, a run timestamp, and
   duration to the `--json-status` envelope for `run`/`extract`/`translate`.
   Exclude the non-deterministic fields from the determinism comparison.
3. **(Optional) Cross-DB roll-up index** — when `--out` holds many
   `AccessMetaData/<dbName>/` dirs, emit/refresh a root index listing each DB +
   its manifest path. Decide in/out at Finalization.
4. Build + test + determinism gate.

**Creates/changes:** CLI option wiring, status-envelope model, optional
root-index emitter, tests.
**Ends with:** Re-runs don't leave stale artifacts; status envelopes carry
provenance; determinism gate green.

### Session 4 of 4: Interpretation docs — field-type semantics + batch operations guide (Findings #3, #5) + closeout

**Goal:** The interpretation guide explains how to read per-field types, and a
new operations doc covers batch runs. Docs land **last** so they describe the
final (post-Session-1–3) behavior.

**Steps:**
1. **Field-type semantics (#3):** extend `interpretation-guide.md` with a
   per-field-type table — Attachment (101), OLE Object (11), Hyperlink,
   multi-valued, Lookup, Memo/LongText, Currency, AutoNumber/COUNTER — each
   mapped to its `DataType`/`DataTypeCode`/`Attributes`/`Ddl` representation;
   state explicitly that the harvester is **metadata-only (no row-level binary)**
   and that attachment/multi-valued child schemas are not expanded. Use the
   real Northwind `Attachments` field as the worked example.
2. **Batch operations guide (#5):** new doc (or `install.md`/operations section)
   covering the `<out>/AccessMetaData/<dbName>/` convention for many DBs,
   sequential-only operation, MSACCESS/RCW hygiene across runs, the `--clean`
   semantics from Session 3, and the optional roll-up index.
3. Closeout: `change-log.md`, `next-session-set-recommendation.md`, Step 9
   reorganization review (lessons-learned / project-guidance candidates), ROADMAP
   row + `session-state.json` final flip.

**Creates/changes:** `docs/harvester/interpretation-guide.md`, a batch-operations
doc, set closeout artifacts.
**Ends with:** A consumer can interpret every field type and run the harvester
across many DBs from the docs alone; set closed and reflected in the ROADMAP.

---

## Open decisions for the human (resolve before/at Session 1)

1. **Entry-point shape (Session 2):** manifest-only, or also auto-emit a
   top-level `READ-ME-FIRST.md`/`index.md`? (Consumer leans toward *both* —
   manifest as machine index + a one-line human pointer.)
2. **Schema version:** confirm additive **v1.1** is acceptable vs. holding for a
   larger schema change.
3. **Cross-DB roll-up index (Session 3):** in scope or defer?
4. **Documentation pointer form (Session 2):** embed a public URL (works for
   binary-only consumers) vs. a relative repo path (breaks once detached from
   the repo). Consumer recommends the URL + schema version.
5. **Sizing/sequencing:** is 4 sessions right, or split S2 (schema bump is
   high-stakes)? Should the docs session (S4) run in parallel with S3 once S1–S2
   land?

## Finalization checklist (human, in this repo)

- [ ] Confirm/adjust slug, `totalSessions`, and the Session Set Configuration block.
- [ ] Author `ai-assignment.md` (per-session orchestrator + routing), per the repo convention.
- [ ] Create the `session-state.json` stub (v4 shape, omit-null) per the canonical schema.
- [ ] Add a ROADMAP row (maintenance / consumption-triggered group) linking this spec.
- [ ] Reconfirm the Findings line numbers against current source before Session 1.

---

> **Provenance:** drafted from the `Dabbler.NorthwindToBlazor` Set 001 Session 2
> harvest of `NorthwindDevEdition.accdb` (2026-05-29). The consumer-side record
> of these findings lives in that repo's `harvest/README.md` and Set 001
> session notes.
