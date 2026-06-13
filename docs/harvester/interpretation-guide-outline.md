# Interpretation Guide — Outline

> **Status:** Outline authored by Session 1 of
> `harvester-finish-operations-and-interpretation-docs`. Session 2 fills
> the body sections against this outline; Session 3 adds the pointer
> page at `docs/README.md` and a README link. Section ordering and
> doc-home placement are decisions of record from this session; section
> content is a Session 2 deliverable.
>
> **Audience:** A downstream AI consumer reading a harvester run output.
> Not a human onboarding to the harvester project (that audience is
> served by [`README.md`](../../README.md)).
>
> **Working title for the body doc:** `interpretation-guide.md` (lands
> at [`docs/harvester/interpretation-guide.md`](./interpretation-guide.md)).

---

## Decisions of record (Session 1)

### Doc-home: `docs/harvester/`

The new consumer-facing docs land under `docs/harvester/` rather than
flat in `docs/`. Rationale:

- The top of `docs/` already holds eleven project-mechanics docs
  (strategy, workflow, planning, schema, etc.). Those are *about how
  the harvester project is run.* The interpretation guide is *about
  how to read what the harvester produces* — a different audience and
  a different use site.
- A directory boundary signals the audience split at glance, and is
  cheap to revisit (the directory can be flattened or renamed later
  without breaking the linking pattern established here).
- Discoverability from the repo root is preserved via the Session 3
  pointer page (see below).

### Pointer page: `docs/README.md`

The Session 3 pointer page lands at [`docs/README.md`](../README.md)
rather than inside `docs/harvester/`. Rationale: GitHub and most
file-browsing AI tooling auto-render `README.md` when listing the
containing directory. An AI consumer crawling the repo from the root
lands on `docs/` and immediately sees the pointer page without
needing to guess at filenames.

### Length budget

Target ~3,500–5,000 words for [`interpretation-guide.md`](./interpretation-guide.md)
(roughly 25–35 KB). Each per-category subsection is one to three
short paragraphs plus one or two JSON snippets. The lean-by-default
Principle in [`docs/planning/project-guidance.md`](../planning/project-guidance.md)
applies: enough prose for the downstream AI to interpret the JSON,
no more. Stop rules in the spec trigger if the body exceeds ~150
pages or strays from interpretation into a human operator's manual.

---

## Section list (the outline proper)

The numbering below is the proposed table-of-contents shape for
[`interpretation-guide.md`](./interpretation-guide.md). Each leaf
section names the JSON shapes it covers and the source it draws from.

### 1. Audience and purpose

What this document is for; what it is not; one paragraph explaining
that the harvester's `ir/` JSON is the contract surface and this doc
is the prose annotation layer.

- Source: [`docs/planning/project-guidance.md`](../planning/project-guidance.md)
  → Principles → Project Scope And Audience.

### 2. What the harvester produces (output map)

The three-tier model — `raw/`, `kb/`, `ir/` — and the two top-level
artifacts that sit alongside the tiers: `manifest.json` and
`unresolved-items.json`. Two important framing facts here:

- The **golden output corpus** at
  [`samples/GoldenOutput/<fixture>/`](../../samples/GoldenOutput/) is
  the canonical reference for what a real run looks like. It includes
  `kb/`, `ir/`, `manifest.json`, and `unresolved-items.json`.
- `raw/` is **deliberately excluded** from the golden corpus
  (Session 1 of `harvester-finish-canonical-golden-output` pinned this
  subset). `raw/` is an intermediate artifact useful for debugging the
  harvester itself, not for downstream interpretation.

- Source: [`README.md`](../../README.md) → CLI Usage; the change-log
  of [`harvester-finish-canonical-golden-output`](../session-sets/harvester-finish-canonical-golden-output/change-log.md).

### 3. Reading order: where to start

Top-down: open `manifest.json` first for inventory, then walk into
`ir/<category>/` for the per-record detail, then consult
`unresolved-items.json` for anything the harvester could not resolve.
The `kb/` tier is the pre-enrichment view; read it only when an `ir/`
field's provenance is in question. This section also covers the
dependency DAG: `Forms` reference `Queries` and `Tables`; `Queries`
reference `Tables` and other `Queries`; `Modules` reference one
another via the navigation graph. Walking inward from the entry-point
form is the typical traversal.

**Session 2 nice-to-have (from the audience-check probe):** add one
paragraph (or one bullet under §2) noting that `kb/` mirrors `ir/`'s
per-category subdirectory layout (`kb/Forms/<name>.kb.json` ↔
`ir/Forms/<name>.ir.json`) and omits only the enrichment-added
fields (e.g., `GeometryIn`, `NonDefaultProperties`, `Procedures`).
That makes the "consult `kb/` for provenance" instruction
actionable.

- Source: the canonical golden corpus structure; the
  `DatabaseManifestAggregator` shape from
  `AccessHarvester.Pipeline/Manifest/`.

### 4. The DatabaseManifest envelope

A pass through the 13 top-level fields of `manifest.json`, in their
on-disk order, naming what each contains and where the per-record
detail lives:

- `SchemaType`, `SchemaVersion`, `HarvesterVersion` — the envelope
  identification.
- `Database` — `Name`, `FilePath`, `ExportedAt` (the run's source and
  timestamp).
- `Summary` — count fields, one per category.
- `Forms`, `Reports`, `Queries`, `Modules`, `Tables`, `Relationships`,
  `ValidationRules`, `RowSourceSpecs`, `FunctionClassifications`,
  `EventHandlerClassifications`, `UnresolvedItems`, `Diagnostics` —
  one bullet each: what kind of record it carries, what the consumer
  uses it for, and which `ir/` subdirectory holds the per-record
  detail when the manifest entry is a stub.

Snippet: an excerpt from
[`samples/GoldenOutput/HomeHealthCare/manifest.json`](../../samples/GoldenOutput/HomeHealthCare/manifest.json)
showing the envelope and a non-empty `Summary`.

**Session 2 must include (from the audience-check probe):** for the
non-trivial classification/dependency arrays (`RowSourceSpecs`,
`FunctionClassifications`, `EventHandlerClassifications`,
`UnresolvedItems`, `Diagnostics`), a one-record JSON snippet per array
with field-by-field explanation. The canonical golden corpus shows
the envelope but those arrays are empty in
`samples/GoldenOutput/HomeHealthCare/manifest.json` (`SchemaVersion: 1.0`
normalized form). Source the record shapes from the C# record
definitions:
- `RowSourceSpec` → `AccessHarvester.Pipeline/Enrichment/RowSourceSpec.cs`
- `FunctionClassification` → `AccessHarvester.Pipeline/Enrichment/FunctionClassification.cs`
- `EventHandlerClassification` → `AccessHarvester.Pipeline/Enrichment/EventHandlerClassification.cs`

Diagnostics record-shape gap (single probe; nice-to-have): add one
bullet to this section sketching what a `Diagnostics` entry contains
and the recommended downstream-AI action class.

### 5. Per-category interpretation

One subsection per category. Each subsection is short — the JSON
shape is mostly self-documenting; the prose explains the
non-obvious conventions and points at the schema doc for full
field-by-field detail.

#### 5.1 Forms (`ir/Forms/*.ir.json`)

- The `Properties` bag carries Access-native property values verbatim
  (strings as Access wrote them). Decoded numeric units land in
  separate fields (e.g., `GeometryIn` in inches alongside raw twip
  values in `Properties.Width`).
- The `Children` array is the form's section/control tree, recursive.
- `VbaCode` is the form's class module source, verbatim, with line
  endings as Access stored them (`\r\n`).
- `Procedures` is the harvester-extracted list of procedures from
  `VbaCode` — `Name`, `ProcedureType`, `AccessModifier`,
  `Parameters`, `StartLine`. Use this in preference to re-parsing
  `VbaCode` from string.
- `NonDefaultProperties` is the harvester's "interesting fields" hint:
  property names that diverge from Access defaults. Use this to filter
  out boilerplate when summarizing.
- Event-binding shape: a property like `"OnLoad": "[Event Procedure]"`
  signals VBA event-handler presence; the handler body is in
  `Procedures` and in `VbaCode`.
- Embedded-macro correlation: `_AXL:` comments inside the `Children`
  tree carry embedded UI macro XML, fragmented across multiple
  `Begin` nodes. Concatenate the `Comment` strings to reassemble.
- **Session 2 must include (from the audience-check probe):** a brief
  sketch of the reassembled `<UserInterfaceMacro>` XML shape
  (`Statements` → `ConditionalBlock`/`Action` elements, the
  `accessservices/2009/11/application` namespace) and one paragraph
  on how Access macro action names map to the `<Action Name="...">`
  attribute. A consumer who reassembles the XML without this hint
  has a blob, not a readable macro.

#### 5.2 Reports (`ir/Reports/*.ir.json`)

Same shape as Forms, with one structural addition: reports have
**group levels** in the section tree (`GroupLevel0`, `GroupLevel1`,
etc.) carrying `GroupHeader`, `GroupFooter`, sort fields, and
break-on conditions. Subsection points the consumer at the group-
level shape and notes that report-bound queries land in
`RowSourceSpecs`.

#### 5.3 Queries (`ir/Queries/*.ir.json`)

- `Sql` is the verbatim Jet SQL.
- `QueryTypeCode` / `QueryTypeName` distinguishes Select, Update,
  Delete, etc.
- `Connect` is non-empty for pass-through queries.
- Dependency facts (which tables and other queries this query
  references) come from `RowSourceSpecs` in the manifest, not from
  the per-query `ir/` file. Cross-link explained here.
- **Session 2 must include (from the audience-check probe):** a JSON
  snippet of one `RowSourceSpec` record showing the join key back to
  the named query or form control (`SourceFormName`, `ControlName`,
  `ControlType`, `Kind`, `TargetName` / `InlineSql`, `Columns`).
  Without this snippet the form → query → table traversal the
  Reading-Order section promises is blocked.

#### 5.4 Modules and class modules (`ir/ModulesAndClasses/*.ir.json`)

- `Procedures` carries the same shape as on Forms — use it instead of
  re-parsing.
- `FunctionClassifications` in the manifest tells the consumer which
  procedures are event handlers, helper subs, lifecycle hooks, etc.
- Navigation-graph edges (caller → callee) are surfaced via the
  manifest's `EventHandlerClassifications` and the procedures' own
  call sites.
- **Session 2 must include (from the audience-check probe):** a JSON
  snippet of one record from each of `FunctionClassifications` and
  `EventHandlerClassifications`, naming the join key back to the
  per-module `ir/` file explicitly (e.g., `ModuleName` +
  `ProcedureName` pair). Without these snippets the module call-graph
  reconstruction this subsection implies is blocked.

#### 5.5 Tables and Relationships

- `ir/Tables/*.ir.json` carries column definitions, types, indexes,
  and table-level validation rules.
- `ir/Relationships/*.ir.json` carries inter-table relationships,
  cascade rules, and join keys.
- `ValidationRules` in the manifest is the flat consumer-friendly
  index over both table-level and field-level rules.
- **Session 2 nice-to-have (from the audience-check probe):** add one
  sentence on how relationship cardinality (one-to-one /
  one-to-many) is encoded — likely via the relationship's index
  attributes — so the consumer reads it directly rather than
  inferring.

#### 5.6 Macros — standalone and embedded

- `ir/Macros/*.ir.json` carries standalone macros (objects in the
  Access nav pane).
- Embedded UI macros live inside Form/Report `Children` trees as
  `_AXL:` XML fragments (see §5.1). The harvester does not lift
  embedded macros into the standalone Macros directory — they remain
  in-place to preserve the binding to their host control.
- **Session 2 must include (from the audience-check probe):** the
  same `<UserInterfaceMacro>` XML shape sketch as in §5.1, scoped
  here to the embedded-macro flavor. The two appearances can
  cross-link rather than duplicate; the consumer reading §5.6
  needs the same XML guidance the §5.1 reader needs.

### 6. The `UnresolvedItems` array

The shape of an `UnresolvedItem` record and per-reason interpretation.
The per-reason narrative is already authored at
[`docs/session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md`](../session-sets/harvester-finish-canonical-parse-success-report/unresolved-reason-narrative.md);
this section summarizes and links to it rather than duplicating it.
Covers:

- The record shape (`source`, `reason`, `location`, `context`).
- Reason-code summary table with one-line "what it means" plus a
  pointer to the full narrative.
- Recommended downstream-AI handling per reason class (parse
  failure, dynamic SQL, unknown reference, etc.).

### 7. Provenance conventions

Why some fields look like `{ source: "...", ... }` instead of bare
values. Covers:

- The `{ source, ... }` shape for fields that may be resolved,
  attached, or unresolved (e.g., `Label: { source: "Attached", text:
  "First Name" }` vs. `Label: { source: "Unresolved" }`; the
  `source` value is a closed PascalCase set —
  `Attached`/`Proximity`/`FromName`/`FromBinding`/`Unresolved`).
- Why some fields are nullable rather than omitted: nullability means
  "the harvester looked; Access did not provide one." Omission means
  "Access used its default for this property."
- The downstream-AI implication: read `null` as "explicitly absent,"
  not "missing data point we forgot to check."
- **Session 2 nice-to-have (from the audience-check probe):** add one
  pointer line naming where Access defaults are recoverable when a
  consumer needs to reconstruct complete property state (the
  `Defaults.json` artifact under each fixture's
  `samples/GoldenOutput/<fixture>/kb/` is the current source).

- Source: [`docs/planning/project-guidance.md`](../planning/project-guidance.md)
  → Conventions → Output JSON Conventions.

### 8. Known limitations

What the harvester does **not** do, drawn from
[`docs/planning/strategy.md`](../planning/strategy.md) → *Frozen
Unless Reauthorized (2026-05-17)*. Two purposes:

- Tells the downstream AI not to expect work that was explicitly
  deferred (e.g., production IR codegen beyond the
  `tools/AccessHarvester.IrCodegen/` probe scope).
- Makes the frozen list visible at the consumer's reading surface so
  the AI doesn't independently re-derive "the harvester must surely
  do X" from incomplete context.

Out of scope explicitly noted here, with a one-line "deferred to the
future migration repo" callout: Blazor consumer integration, IR
codegen production use, `dabbler-platform-discovery` refresh, CRUD
scaffolder expansion, additional Northwind workflows.

### 9. Examples gallery

Pointers (not duplications) to the three existing example files:

- [`examples/minimal-form.json`](../../examples/minimal-form.json) —
  the simplest possible Form + Table case.
- [`examples/complex-form.json`](../../examples/complex-form.json) —
  a Form with subforms, validation, and event bindings.
- [`examples/northwind-output.json`](../../examples/northwind-output.json) —
  a full Northwind run snapshot.

Plus a pointer to the full canonical corpora at
[`samples/GoldenOutput/HomeHealthCare/`](../../samples/GoldenOutput/HomeHealthCare/)
and
[`samples/GoldenOutput/NorthwindDevEdition/`](../../samples/GoldenOutput/NorthwindDevEdition/)
with one-paragraph notes on which fixture exhibits which structural
characteristics (per
[`docs/homehealthcare-northwind-comparison.md`](../homehealthcare-northwind-comparison.md)).

### 10. Where to look next

A short index of related docs and what each contains. This is
intentionally a short version of the Session 3 pointer page; it
serves the consumer who is reading the interpretation guide end-to-
end and wants the next stop without leaving the file. The full index
lives at `docs/README.md` (Session 3).

- Schema details: [`docs/schema/v0-vs-tiers.md`](../schema/v0-vs-tiers.md)
  and [`docs/schema/v1-adr.md`](../schema/v1-adr.md).
- IR shape (forms/reports): [`docs/ir-schema-forms-reports.md`](../ir-schema-forms-reports.md).
- CLI usage: [`README.md`](../../README.md).
- Parser test cases (canonical input/expected pairs):
  [`docs/test-cases/`](../test-cases/).

---

## Reading-order recap

For a downstream AI landing on a harvester output directory:

1. Read `manifest.json` for inventory and counts.
2. For each form/report the consumer cares about, read the
   corresponding `ir/Forms/<name>.ir.json` or
   `ir/Reports/<name>.ir.json`.
3. For queries referenced by those forms/reports, read the matching
   `ir/Queries/<name>.ir.json` *and* the manifest's
   `RowSourceSpecs` entry (the per-query file does not carry
   dependency facts).
4. For modules invoked from forms/reports, read
   `ir/ModulesAndClasses/<name>.ir.json` and consult the manifest's
   `EventHandlerClassifications` for caller/callee context.
5. After interpreting the records, consult `unresolved-items.json`
   for anything the harvester flagged as unresolvable; the per-reason
   narrative is the authority for what each reason code means.

For a downstream AI doing repo-wide synthesis: walk in the order
above for each fixture, then cross-reference findings against
`Diagnostics` in the manifest.

---

## Out of scope for the body

To keep Session 2 lean, these are explicitly out of scope and should
be referenced rather than duplicated when relevant:

- Re-explaining the parser grammar (`docs/test-cases/` is the contract).
- Re-explaining the schema field-by-field (`docs/schema/*.md` is the
  authority).
- Operator-facing install / troubleshooting prose (README's job).
- Anything about consumers of the IR (Blazor migration, codegen
  applications) — those belong in the future migration repo per the
  strategy reset.

---

## What Session 2 produces

[`docs/harvester/interpretation-guide.md`](./interpretation-guide.md)
with sections 1–10 as above, length within budget, JSON snippets
sourced from the canonical golden corpus, cross-links to the
authoritative sources named at the leaf-section bullets.

## What Session 3 produces

[`docs/README.md`](../README.md) — the consumer-discoverable pointer
page that lists every doc in `docs/` (and its subdirectories) with a
one-line "what it is and who reads it." A new link is also added to
the top-level [`README.md`](../../README.md) under *Planning &
Documentation* pointing at the interpretation guide.
