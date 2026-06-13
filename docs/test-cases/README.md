# Access Migration Harvester Test Cases

## Purpose

This is a hand-curated set of test cases for the VBA parser, resolver, and
migration harvester described in `VBA-Parser-Migration-Harvester-Plan.md`.
Each case is a paired source/expected-output set that exercises a specific
combination of features the harvester must handle.

The test cases serve two roles in the project:

1. **Specification (design intent)** — the expected outputs document what
   the harvester *should* produce for each input pattern. They drove schema
   design and resolver behavior. They were authored **before** the
   implementation and encode intent fields the harvester does not (and may
   never) emit, in a shape that predates the operative `DatabaseManifest`
   output. Treat them as illustrative design intent, **not** an executable
   output contract.
2. **Onboarding artifact** — for a new developer joining the project, the
   cases plus expected outputs are the fastest way to understand what the
   harvester is doing and what "correct" looks like.

> **What is actually enforced (008 Session 3, F-3-9 — decided by
> cross-provider consensus).** No test diffs harvester output against these
> `expected.json` files, and none is intended to. The enforced parser
> contract is two-layered: (1) **schema-shape conformance** — every
> `<n>.expected.json` validates against the operative harvester schema
> (`TestCaseFixtureConformanceTests`), with one stop-rule exclusion (case
> 12, see the v1 ADR); and (2) **targeted behavioral tests** that parse the
> inputs and assert selected annotations on named cases
> (`ResolverFixtureAnnotationTests`, `FixtureCrossCutTests`,
> `DynamicSqlFixtureTests`, the grammar corpus tests), backed by the
> byte-level full-output golden on the real fixture
> (`CanonicalGoldenRegressionTests`). When a parser feature lands, add a
> targeted behavioral pin and (optionally) a corpus case as illustration —
> not a whole-corpus diff. Rationale + the consensus record:
> `docs/session-sets/008-manifest-vba-ingestion-and-parser-hardening/s3-f39-decision.md`.

As real Access databases reveal patterns these cases don't cover, add new
cases rather than modifying existing ones.

## Structure

Each case is a pair of files:

- `NN-name.{bas|cls|sql|txt}` — the source artifact as if extracted by a
  text dump from an Access database.
- `NN-name.expected.json` — the design-intent structured output, validated
  for **shape** against the operative harvester schema
  (`harvester-schema.v1.1.json`).

The expected outputs were migrated from the provisional `harvester-schema.v0.json`
to the v1.0 `DatabaseManifest` envelope (PascalCase) and are now pinned for
shape against the operative `harvester-schema.v1.1.json` by
`TestCaseFixtureConformanceTests`. See the v1 ADR
([`docs/schema/v1-adr.md`](../schema/v1-adr.md)) for the migration and the
case-12 stop-rule exclusion. The `SchemaVersion` field inside each expected
output records the schema version the case currently conforms to.

## Running validation

```bash
pip install jsonschema --break-system-packages
python3 validate.py            # validates all cases
python3 validate_verbose.py 03 # gets full errors for case matching '03'
```

A successful run prints `PASS:` for each `*.expected.json` file. The
schema is verified to be a valid Draft 2020-12 JSON Schema before
instance validation.

## The 14 cases

Cases 01–12 are the original design-intent set; 13–14 were added as the
parser gained features (every parser feature lands with an illustrative
corpus case per the testing convention).

### Difficulty: low

**01 - Simple validation** (`01-simple-validation.bas`)
Baseline `BeforeUpdate` handler with required-field checks. Tests basic
`Me` resolution, control resolution against form metadata, and
detection of validation patterns suitable for migration to Blazor
validators. The minimum viable case.

### Difficulty: medium

**04 - Saved query with form parameters**
(`04-saved-query-form-params.sql`)
JET SQL with PARAMETERS clause referencing `[Forms]![X]![Y]`, nested
`IIf`, JOIN, date math, and parameter resolution. Tests the
query-abstraction generation: lifting form references to method
parameters, inferring return shape with nullability, suggesting a
method name. Common government data pattern.

**06 - Calculated control source expressions**
(`06-calculated-expressions.txt`)
Six expressions in non-VBA contexts (control sources, validation rules,
default values). Includes Nz wrapping, nested IIf age categorization,
SQL Between, address concatenation with conditional Address2,
fiscal-year calculation, and a domain function in a control source
(with attached performance warning). Tests expression-context parsing,
which differs from VBA expression parsing in subtle ways.

### Difficulty: medium-high

**02 - Cascading combo with subform filtering**
(`02-cascading-combo-subform.bas`)
The canonical complex form pattern. Tests bang operator, subform
navigation via `.Form`, `Column(N)` access, dynamically-built RowSource,
and concatenated filter strings. Identifies several migration patterns:
cascading combo, combo column display, subform filter.

**09 - Report module with grouping events**
(`09-report-grouping-events.bas`)
Report-specific event model: `Format`/`Retreat` event pairs with
`FormatCount` parameters, module-level running totals (compensated in
Retreat), conditional formatting via ForeColor/FontBold, and
conditional visibility. Migration insight: most of this disappears when
running totals move into the data source SQL via window functions.

### Difficulty: high

**03 - With-block shadowing**
(`03-with-block-shadowing.bas`)
The trickiest resolution case. Outer `With rs`, inner
`With Me.sfrmInspections.Form`, with bang accesses inside the inner
block that *might* be intentionally shadowing the outer—or might be a
latent bug. The expected output flags the ambiguity rather than picking
silently. Demonstrates the "graceful uncertainty" pattern.

**10 - Class module with WithEvents**
(`10-class-with-events.cls`)
A standalone audit-logger class. Tests `Implements IAuditLogger`,
`WithEvents m_Form As Form` with three event sinks, Property Get/Let
pairs, lifecycle events (Initialize/Terminate), late-bound Scripting
Dictionary, and SQL injection via Replace-based quote escaping. The
"runtime-bound form audit" pattern is unusual in modern frameworks and
needs explicit interface definition in migration.

**11 - AutoExec startup chain**
(`11-autoexec-startup-chain.bas`)
Mixes a macro (AutoExec) with a VBA module (mdlStartup). Tests entry
point reconstruction, macro action extraction, RunCode dispatch,
role-based switchboard selection, public module variables as
application globals, Windows authentication via Environ('USERNAME'),
and session logging. Migration target: ASP.NET startup pipeline plus
authentication middleware.

**12 - Switchboard form**
(`12-switchboard-navigation.bas`)
The form-based alternative to ribbon navigation. 12 command buttons
with click-handler dispatch, role-conditional visibility (4 controls),
OpenForm/OpenReport/OpenArgs/Filter patterns, hidden-form-as-parameter-
container, and session-end logging on exit. Reconstructed navigation
graph spans 9 forms and 1 report. Migration target: top-level menu
component with role-based visibility.

### Difficulty: very high

**05 - Dynamic SQL with conditional construction**
(`05-dynamic-sql-construction.bas`)
Multi-branch SQL assembly across `sSQL`/`sWhere`/`sOrder` with five
conditionals producing approximately 32 possible final SQL forms. The
expected output enumerates 5 representative templates with confidence
scores rather than pretending to enumerate all combinations. This case
exists primarily to test the harvester's *partial* handling of dynamic
SQL — an area where confidence and explicit "manual review" routing
matter more than completeness.

**07 - Legacy patterns** (`07-legacy-patterns.bas`)
"What state government 2002-vintage code actually looks like."
Includes BASIC-style line numbers (10, 20, 30, ...), GoSub/Return with
implicit state sharing, type-declaration character suffixes ($, &),
late-bound COM via CreateObject("Excel.Application"),
On Error Resume Next at procedure top level, mixed return type
(Variant function returning Long on success, String on failure), and
SSN handling that needs PII compliance review. Heavy diagnostics
output; flagged as `manualReview` for translatability.

### Difficulty: unresolvable (intentionally)

**08 - Constructs that defeat static analysis**
(`08-unresolvable.bas`)
Tests *graceful failure*. The procedure uses `Eval()` of
database-stored expressions, dynamic form name from
`tblFormConfig.FormName`, and `Application.Run` with a runtime callback
name. None of these can be statically analyzed. The expected output
routes everything to `unresolvedItems` with `suggestedNextStep` notes
and zero confidence on the runtime-determined targets. Confirms the
harvester says "I can't resolve this" rather than guessing.

### Added as the parser gained features

**13 - Preprocessor directives** (`13-preprocessor.bas`)
`#If` / `#Else` / `#End If` / `#Const` at module and procedure level,
including `#If VBA7 Then` conditional `Declare PtrSafe` API declarations.
Exercises the parser's conditional-compilation handling. Added with the
preprocessor grammar/AstBuilder support.

**14 - Type-suffixed names and contextual keywords**
(`14-typed-suffix-and-contextual-names.bas`)
Type-declaration suffix on a `Function` return name (`SqlStr$`), on a
module variable (`g_sTitle$`) and a local (`msg$`); the contextual keyword
`step` as a parameter and a local name; named-argument binding of a
contextual-keyword parameter (`step:=1`); and anchored
`Option Compare Database` winning over the keyword. Every construct parses
cleanly (no recovery). Added by 008 Session 2 (F-3-2 / F-3-3 + review
fixes); the deliberately un-handled variants are recorded as DW-1 / DW-4 in
`docs/planning/deferred-work.md`.

> **Stop-rule exclusion — case 12.** `12-switchboard-navigation.expected.json`
> is migrated to the v1.0 envelope but retains one semantic incompatibility
> (a VBA variable name where the schema requires a `ParserQueryParameter.Source`
> enum value) and is excluded from the schema-shape conformance gate via the
> `StopRuleExclusions` set in `TestCaseFixtureConformanceTests`. See
> [`docs/schema/v1-adr.md`](../schema/v1-adr.md) → Known Issues.

## Schema design notes

The provisional `harvester-schema.v0.json` includes several design
choices that emerged from authoring the cases:

**`confidence` field on every reference (0.0 to 1.0)**
Lets downstream tooling treat resolved-with-low-confidence
differently from resolved-with-certainty. 1.0 is deterministic;
< 0.9 is heuristic and worth a second look.

**`implicitContext` on references**
Records what `Me`, `!`, or `.` expanded to. Auditable resolution.

**`templateCandidates` on dynamic SQL with per-template confidence**
Acknowledges that dynamic SQL has multiple legitimate forms and
the harvester is identifying the most likely templates, not the
complete enumeration.

**`diagnostics` array with stable codes (HARV-W040 etc.)**
Enables filtering, suppression, and prioritization without parsing
message strings. Worth maintaining a code registry as patterns
emerge.

**`unresolvedItems` array with `suggestedNextStep`**
The explicit failure surface. When the harvester can't process
something, it says exactly that and tells the reviewer what to do
next.

**`resolvedTarget` allows null**
For unresolved references the resolver legitimately has no target.
Honest representation; null means "tried and failed," omitted means
"didn't attempt."

## What's not covered yet

Patterns that deserve cases but aren't in this set:

- **Linked-table connections** — many state government databases use
  ODBC linked tables to SQL Server backends. Connection string
  extraction and schema mapping deserves its own case.
- **PassThrough queries** — JET-dialect queries that pass through to a
  backend server. Different SQL dialect handling.
- **DAO recordset transactions** — `BeginTrans` / `CommitTrans` /
  `Rollback` patterns.
- **Query-of-queries** — saved queries that reference other saved
  queries, requiring transitive dependency resolution.
- **Form-with-no-record-source** — unbound forms used as dialogs.
  Different migration pattern than data-bound forms.
- **Imported modules from add-ins** — references to procedures that
  exist in `.mda` add-ins not bundled with the database.

Add cases for these as you encounter them in the migration scope.

## Authoring guidance

When adding new cases:

1. Create the source artifact first. Make it realistic — patterns from
   actual Access codebases beat synthesized ideal examples.
2. Hand-author the expected output. This forces you to think through
   what resolution should produce.
3. Run `validate.py` to confirm the expected output validates against
   the schema. If it doesn't, either the data is wrong or the schema
   is missing something — fix whichever is appropriate.
4. Add the case to this README's case list with a one-paragraph
   description of what it tests.
5. Note any new diagnostic codes added (HARV-W### / HARV-I### / HARV-E###)
   and what they mean.

## Files

```
test-cases/
  README.md                              # this file
  harvester-schema.v0.json               # provisional (archived) output schema
  harvester-schema.v1.0.json             # v1.0 DatabaseManifest schema
  harvester-schema.v1.1.json             # operative schema (shape-conformance target)
  validate.py                            # all-cases validator
  validate_verbose.py                    # detailed error reporter
  fix_locations.py                       # data-fix helper

  01-simple-validation.bas
  01-simple-validation.expected.json
  02-cascading-combo-subform.bas
  02-cascading-combo-subform.expected.json
  03-with-block-shadowing.bas
  03-with-block-shadowing.expected.json
  04-saved-query-form-params.sql
  04-saved-query-form-params.expected.json
  05-dynamic-sql-construction.bas
  05-dynamic-sql-construction.expected.json
  06-calculated-expressions.txt
  06-calculated-expressions.expected.json
  07-legacy-patterns.bas
  07-legacy-patterns.expected.json
  08-unresolvable.bas
  08-unresolvable.expected.json
  09-report-grouping-events.bas
  09-report-grouping-events.expected.json
  10-class-with-events.cls
  10-class-with-events.expected.json
  11-autoexec-startup-chain.bas
  11-autoexec-startup-chain.expected.json
  12-switchboard-navigation.bas
  12-switchboard-navigation.expected.json
  13-preprocessor.bas
  13-preprocessor.expected.json
  14-typed-suffix-and-contextual-names.bas
  14-typed-suffix-and-contextual-names.expected.json
```
