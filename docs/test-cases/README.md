# Access Migration Harvester Test Cases

## Purpose

This is a hand-curated set of test cases for the VBA parser, resolver, and
migration harvester described in `VBA-Parser-Migration-Harvester-Plan.md`.
Each case is a paired source/expected-output set that exercises a specific
combination of features the harvester must handle.

The test cases serve three roles in the project:

1. **Specification** — the expected outputs document what the harvester
   should produce for each input pattern. They drive schema design and
   resolver behavior.
2. **Regression suite** — once the harvester is implemented, running it
   against these inputs and diffing against the expected outputs catches
   regressions.
3. **Onboarding artifact** — for a new developer joining the project, the
   cases plus expected outputs are the fastest way to understand what the
   harvester is doing and what "correct" looks like.

The test cases were authored before the harvester implementation. They
represent the design intent. As real Access databases reveal patterns
these cases don't cover, add new cases rather than modifying existing ones.

## Structure

Each case is a pair of files:

- `NN-name.{bas|cls|sql|txt}` — the source artifact as if extracted by a
  text dump from an Access database.
- `NN-name.expected.json` — the structured output the harvester should
  produce, validated against `harvester-schema.v0.json`.

The `harvester-schema.v0.json` file is the provisional output schema. It
will iterate; the schema version field in each expected output tracks the
schema version the case was authored against.

## Running validation

```bash
pip install jsonschema --break-system-packages
python3 validate.py            # validates all cases
python3 validate_verbose.py 03 # gets full errors for case matching '03'
```

A successful run prints `PASS:` for each `*.expected.json` file. The
schema is verified to be a valid Draft 2020-12 JSON Schema before
instance validation.

## The 12 cases

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
  harvester-schema.v0.json               # provisional output schema
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
```
