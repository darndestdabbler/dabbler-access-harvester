# Object Cleanup Identification Strategy

> **Status:** Authored in Session 1 of `harvester-object-cleanup-identifier`.
> **Authority:** This document is the design specification for the
> `AccessHarvester identify-cleanup` subcommand. Session 2 implements it
> exactly as described here; divergences require human approval.

---

## 1. Purpose

The `identify-cleanup` subcommand analyses KB/IR output from a harvested
Access database and flags objects (tables, queries, forms, reports,
modules, macros) that are likely snapshotted, archived, or no longer in
use. It produces two artefacts:

1. `object-cleanup.json` — intermediate structured findings (one entry
   per flagged object, with the signals that fired and a confidence
   tier).
2. `object-cleanup-checklist.json` — a `uat-checklist-editor`-
   compatible JSON checklist the developer and user review together.

The reviewed checklist becomes the exclusion list consumed by
`harvester-object-use-identifier` (via its `--exclude-cleanup`
option) and any other downstream analysis that benefits from
operating only on the *current* objects in the database.

Most signals come from already-harvested KB/IR files. The optional
date-signal path runs a lightweight Access COM pass — gated as
`[SkippableFact]` integration tests and skipped at runtime when Access
is unavailable (see §3.2 fallback).

---

## 2. Confidence Tiers

Each flagged object carries a categorical confidence tier rather than a
numeric score. The three tiers reflect how strong the combined signal
evidence is, not a probability:

| Tier | When it fires |
|---|---|
| **High** | Name-pattern signal AND date-signal both fire for the same object. |
| **Medium** | Name-pattern signal OR date-signal fires (but not both). |
| **Low** | Only cross-reference absence fires (no name pattern, no date signal). |

Low-confidence findings are deliberately separated because legitimate
utility objects (administrative reports, scratch query libraries, ad-hoc
export queries) are routinely unreferenced and would otherwise drown
out the high-signal cases. The pilot in Session 3 calibrates whether
Low findings ship to the checklist by default or are suppressed.

> **Note on numeric scoring.** The use-identifier (`object-use.json`)
> emits a 0.0–1.0 confidence score; the cleanup identifier does not.
> The cleanup classification is a small-cardinality categorical
> judgement ("is this stale?") whose evidence does not compose
> additively the way use-classification evidence does, so the tiered
> shape is the better fit.

---

## 3. Signal Families

Three signal collectors feed the classifier. Sources and required
inputs differ; the strategy degrades gracefully when an input is
absent.

### 3.1 NamePatternDetector

- **Source:** KB manifest object names (no IR file reads, no COM).
- **Applies to:** All object types — Form, Report, Query, Table,
  Module, Macro.

The detector runs five sub-patterns. Each is given in
implementation-safe form below — Session 2 should drop these into
`Regex` constants verbatim. **Note on boundaries:** `\b` is unsafe
here because `_` is a regex word character, so `\b` does NOT
separate `_2023` from a preceding token. The patterns below use
explicit character-class boundaries `(?:^|[^0-9A-Za-z])` /
`(?:$|[^0-9A-Za-z])` to handle underscore-delimited tokens
correctly.

#### 3.1.1 `vocabulary` pattern

Matches a staleness token bounded by word break, underscore/other
non-letter, or a CamelCase transition. Case-insensitive. Tokens:
`Old`, `Copy`, `Bak`, `Archive`, `Tmp`, `Temp`, `Hold`, `Save`,
`Backup`, `Snapshot`.

```regex
(?i)(?:^|[^A-Za-z]|(?<=[a-z])(?=[A-Z]))(Old|Copy|Bak|Archive|Tmp|Temp|Hold|Save|Backup|Snapshot)(?:$|[^A-Za-z]|(?<=[a-z])(?=[A-Z]))
```

Matches: `tblClients_OLD`, `frmBackup`, `qryReports_Archive`,
`OldReports` (CamelCase), `frmCopyOfClients`.

#### 3.1.2 `year` pattern

A 4-digit year 1900–2099 surrounded by character-class boundaries
(handles `_2023`, `Reports_2024`, `qry-2025`).

```regex
(?:^|[^0-9A-Za-z])((?:19|20)\d{2})(?:$|[^0-9A-Za-z])
```

The captured year is the `matchedSubstring` recorded on the signal.

#### 3.1.3 `year-month` pattern

Year-month tokens in either year-first (`2024Jan`, `2024-01`,
`2024_Jan`, `202401`) or month-first (`Jan2024`, `01-2024`,
`Jan_2024`, `012024`) order. Case-insensitive. Separator is
optional `-` or `_`. Both numeric (`01`–`12`) and named (`Jan`
through `Dec`) months are accepted on both sides.

```regex
(?i)(?:^|[^0-9A-Za-z])(?:((?:19|20)\d{2})[-_]?(0[1-9]|1[0-2]|Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)|(0[1-9]|1[0-2]|Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[-_]?((?:19|20)\d{2}))(?:$|[^0-9A-Za-z])
```

Both year-first and month-first variants are accepted; the
detector records whichever matched.

#### 3.1.4 `quarter` pattern

Year-quarter tokens in either order: `Q12025`, `2025Q1`,
`Q1_2025`, `2025-Q1`. Case-insensitive.

```regex
(?i)(?:^|[^0-9A-Za-z])(?:Q([1-4])[-_]?((?:19|20)\d{2})|((?:19|20)\d{2})[-_]?Q([1-4]))(?:$|[^0-9A-Za-z])
```

#### 3.1.5 `numeric-duplicate` pattern (algorithmic, not regex)

An object's name is a numeric duplicate when:

1. Strip any leading conventional prefix (`tbl`, `qry`, `frm`,
   `rpt`, `mcr`, `bas`, `cls`, case-insensitive) from the object
   name; call the remainder `tail`.
2. If `tail` matches `^(.+?)(\d+)$`, the base name is the first
   capture group.
3. Look up a same-prefix object whose `tail` equals the base name
   (case-insensitive comparison), scoped to the same object type.
4. If such an object exists, fire `numeric-duplicate` with
   `matchedSubstring` = the numeric suffix captured in step 2.

Examples: `frmClients2` matches when `frmClients` exists in
`Forms/`. `qryReports_OLD3` would also match `qryReports_OLD` if
both exist (the staleness vocabulary is preserved as part of the
base name; the duplicate detector does not strip it).

#### 3.1.6 Detector output

- **Produces:** One `NamePatternSignal` per fired pattern. A single
  object may fire multiple name patterns (e.g., `tblClients_OLD_2023`
  fires both `vocabulary` and `year`); each is recorded as a separate
  signal entry on the same finding.

- **Excludes:** System objects (see §7).

### 3.2 DateMaximaSampler

- **Source (primary):** Live Access COM pass that, for each non-system
  table containing at least one DateTime column, runs
  `SELECT MAX(<col>) FROM <table>` per DateTime column and keeps the
  greatest non-null value as the table's "last modified data" date.
- **Source (fallback):** IR `LastUpdated` field on the table object
  (already present per
  `samples/GoldenOutput/HomeHealthCare/ir/Tables/tblClients.ir.json` →
  top-level `LastUpdated`). Used when COM is unavailable
  (`--no-com`, non-Windows host, Access not installed) or when the
  table has no DateTime columns.
- **Applies to:** Tables only. Queries/forms/reports/modules/macros
  do not carry meaningful "last data update" signals at the object
  level.
- **Threshold:** Default 90 days before the harvest date. The harvest
  date is the date the source `.accdb` was extracted; the CLI exposes
  `--harvest-date <ISO>` to override (default: today). The threshold
  is tunable via `--threshold-days <N>`.
- **Excludes:** System tables (`MSys*`, `USys*`); linked tables
  (Connect non-empty) — their data freshness reflects the remote
  source, not the harvested DB.

- **Produces:** One `DateSignal` per stale table, recording:
  - `source`: `com` (primary) or `dao-metadata` (fallback)
  - `maxValue`: ISO timestamp of the latest data observed
  - `daysSinceMax`: integer days between `maxValue` and `harvestDate`
  - `thresholdDays`: the effective threshold used for the decision

- **Degradation:** If neither COM nor `LastUpdated` is available for a
  given table, no date signal is emitted. The finding can still reach
  Medium tier via a name pattern alone. `dateSignalSource` on the
  output file records which path was used overall (see §5).

### 3.3 CrossReferenceAbsenceDetector

- **Source:** Existing IR files only. No COM.
- **Reference types checked:**

  | Reference type | Source IR | Per object type, "referenced" means |
  |---|---|---|
  | `RecordSource` | `ir/Forms/*.ir.json`, `ir/Reports/*.ir.json` | The table/query name appears (a) as the literal `Properties.RecordSource` value, OR (b) in a `FROM`/`JOIN` clause inside an inline-SQL `RecordSource` value — see §3.3.1 below. |
  | `RowSource` | `ir/Forms/*.ir.json` Children control nodes | The table/query name appears (a) as the literal `Properties.RowSource` value (when not a value-list literal), OR (b) in a `FROM`/`JOIN` clause inside an inline-SQL `RowSource` value — see §3.3.1 below. |
  | `NavigationEdge` | `ir/Macros/*.ir.json` action nodes + VBA `DoCmd.Open*` regex + form/report/control event-property bindings (see §3.3.2) | The form/report/query/macro name appears as the target of an `OpenForm`/`OpenReport`/`OpenQuery`/`OpenMacro`/`RunMacro` edge, OR as the value of a recognised event-property binding (§3.3.2). |
  | `VbaReference` | `VbaCode` strings in form/report/module IR | The table/query name appears in `DCount`/`DLookup`/`DSum`/`DMin`/`DMax`/`DAvg` second-arg or `OpenRecordset` first-arg. For modules and classes, the module/class name appears as a `Call`, `Set ... = New`, or qualifying namespace token in any other module's `VbaCode` (best-effort regex). |
  | `QueryTableReference` | `ir/Queries/*.ir.json` `Sql` field | The table/query name appears in another query's `FROM`/`JOIN` clauses (per `harvester-object-use-identifier` §3.5 simple FROM parse). |
  | `FkRelationship` | `ir/Relationships/*.ir.json` | The table name appears as the parent or child of any non-`MSysNavPane*` relationship. |

#### 3.3.1 Inline-SQL parsing for `RecordSource` / `RowSource`

Access forms and controls commonly bind to inline SQL strings such
as `SELECT * FROM tblClients WHERE Active = True` rather than a
saved query name. The detector applies a simple inline-SQL parser
to `RecordSource` / `RowSource` values when:

- The property value is a string whose first non-whitespace token
  matches `(?i)^\s*SELECT\b` (single-statement select), `(?i)^\s*WITH\b`
  (CTE), or contains a `FROM` keyword that is not preceded by
  any non-keyword characters that would indicate plain text.
- Reuse the FROM/JOIN regex from `harvester-object-use-identifier`
  §3.5: extract every `FROM <name>` and `JOIN <name>` token,
  handling `AS <alias>` (alias is discarded).
- Value-list literals (starting with `(`, `"`, `'`, or a digit
  followed by `;`) are NOT inline SQL — skip the parse.
- An inline-SQL parse failure (no `FROM` keyword, parse exception)
  silently fires no reference; the object the inline SQL belongs
  to is the "referrer" — the *target* tables/queries are what
  this detector counts toward.

#### 3.3.2 Event-property navigation bindings

Forms, reports, and their child controls expose event properties
in their IR `Properties` dictionary. A property is a candidate
event-property binding when its name matches the predicate:

```
name starts with "On"                    (case-sensitive)
  OR name starts with "Before"           (case-sensitive)
  OR name starts with "After"            (case-sensitive)
```

This covers the full Access event-property surface — every form,
report, and control event property in current Access starts with
one of these three prefixes (e.g., `OnLoad`, `OnClick`,
`OnCurrent`, `OnDblClick`, `OnTimer`, `OnFormat`, `BeforeUpdate`,
`BeforeInsert`, `BeforeDelConfirm`, `AfterUpdate`, `AfterInsert`,
`AfterDelConfirm`). The detector inspects the property *value*,
not its name, for navigation evidence.

A bare-identifier value (matching `^[A-Za-z][\w]*$`) that names
an existing macro in the KB manifest's `Macros/` index counts as
a `NavigationEdge` to that macro. Values of `[Event Procedure]`
(handled in code-behind VBA), function expressions (starting
with `=`), or anything not matching the bare-identifier shape
are not event-property bindings for this detector's purposes.

Walk the form/report IR tree fully: the top-level `Properties`
and every Child control's `Properties` are inspected.

#### 3.3.3 Per-object-type matrix

Each object type checks a subset of reference types; an object is
considered unreferenced only when *all applicable* reference
types come back empty.

  | Object type | Applicable reference types |
  |---|---|
  | Table | `RecordSource`, `RowSource`, `VbaReference`, `QueryTableReference`, `FkRelationship` |
  | Query | `RecordSource`, `RowSource`, `VbaReference`, `QueryTableReference`, `NavigationEdge` (OpenQuery) |
  | Form | `NavigationEdge` (OpenForm), `VbaReference` (only the form-name token, not VBA-on-the-form-itself) |
  | Report | `NavigationEdge` (OpenReport), `VbaReference` (only the report-name token) |
  | Module | `VbaReference` (module/class name appears as qualifier or callable in another module's VBA). Standard and class modules share the same IR source path (`ir/ModulesAndClasses/*.ir.json`) and share the same `Module` finding type. |
  | Macro | `NavigationEdge` (RunMacro action + bare-identifier event-property bindings per §3.3.2), `VbaReference` (`DoCmd.RunMacro "<name>"`) |

- **Produces:** One `CrossReferenceAbsenceSignal` per unreferenced
  object, listing the reference types that were checked and came up
  empty. The signal does not include "evidence" strings — its
  defining property is the *absence* of evidence.

- **Self-reference exclusion:** A form's own VBA code can name the
  form itself (e.g., `Me.Caption`); a query's SQL can name itself via
  alias. Self-references are not counted as cross-reference evidence.

- **Excludes:** System objects (see §7); event-handler code-behind
  modules whose name matches `Form_*` / `Report_*` (these are implicit
  components of their parent form/report and inherit its
  referenced-ness).

---

## 4. Detection Rules

Run the three detectors against every non-excluded object and combine
their outputs into per-object findings:

1. **Collect signals.** For each object, ask each applicable detector
   whether it fires. Record every fired signal on the object.
2. **Assign confidence tier:**
   - **High** if both a `NamePattern` signal AND a `DateSignal` fired
     for the same object. (Date signals only apply to tables, so
     High-tier findings are tables that are both named with a
     staleness vocabulary/date and whose data has not changed in the
     threshold window.)
   - **Medium** if exactly one of `NamePattern` or `DateSignal` fired.
   - **Low** if only `CrossReferenceAbsence` fired (and no name
     pattern or date signal). Cross-reference absence does not
     upgrade a Medium-or-High finding — it is a Low-only signal.
3. **No fired signal → no finding.** Objects with zero fired signals
   do not appear in the output. The checklist is a flag list, not a
   complete object inventory.
4. **Stable ordering.** Findings are sorted by `(confidenceTierRank
   descending, objectType ascending, objectName ascending)` where
   `confidenceTierRank` is `High=3, Medium=2, Low=1`. This puts the
   highest-confidence flags at the top of the checklist for review.

---

## 5. Output: `object-cleanup.json`

Intermediate machine-readable findings. JSON Schema at
`AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json`.

Top-level structure:

```json
{
  "version": "1.0",
  "database": "<database name without path or extension>",
  "generatedAt": "<ISO 8601 date-time, e.g., 2026-05-28T14:30:00Z>",
  "harvestDate": "<ISO 8601 calendar date, e.g., 2026-05-28>",
  "dateSignalSource": "com" | "dao-metadata" | "unavailable",
  "thresholdDays": 90,
  "degradationNotes": [ "<one entry per degraded signal path, see below>" ],
  "findings": [ <CleanupFinding>, ... ]
}
```

`harvestDate` is intentionally a calendar date (YYYY-MM-DD), not a
date-time. The date-signal threshold is day-based; storing
sub-day precision implies a precision the threshold does not use
and would force Session 2 / Session 3 to choose an arbitrary time
component. `generatedAt` remains a full date-time because it
records when the analysis ran.

`degradationNotes` is an array of one-line human-readable
strings, one entry per signal path that ran in a degraded mode.
Examples:

- `"Cross-reference QueryTableReference detection unavailable: ir/Queries/*.ir.json Sql fields are absent (soft prereq harvester-body-and-query-sql not satisfied). Low-confidence findings for both queries (cannot prove they reference other tables) AND tables (cannot prove they are referenced by other queries) may have higher false-positive rate."`
- `"Date sampling unavailable: COM declined AND no IR LastUpdated values present. Date-signal path produced no findings; only NamePattern and CrossReferenceAbsence applied."`

When all signal paths ran at full fidelity, `degradationNotes` is
an empty array.

`dateSignalSource` reflects the path used overall. `com` means the
COM-based `DateMaximaSampler` ran successfully for at least one
table; `dao-metadata` means COM was skipped or unavailable and the
`LastUpdated` fallback was used; `unavailable` means neither path
produced data (e.g., `--no-com` and no `LastUpdated` in IR).
Per-finding `dateSignal.source` reflects the source of that specific
signal — the top-level value may be `com` while individual findings
report `dao-metadata` for tables with no DateTime columns.

`CleanupFinding` per object:

```json
{
  "objectType": "Form" | "Report" | "Query" | "Table" | "Module" | "Macro",
  "objectName": "string",
  "confidenceTier": "High" | "Medium" | "Low",
  "explanation": "one-sentence human-readable why-flagged summary",
  "firedSignals": [ <Signal>, ... ]
}
```

`explanation` is the one-line template used as the checklist's
`ItemLabel`. Templates by tier:

- **High:** `"{objectType} '{name}' has staleness-pattern name AND no data updated in {daysSinceMax} days — likely archived/superseded."`
- **Medium (name pattern only):** `"{objectType} '{name}' name matches staleness pattern '{pattern}' — likely archived/superseded."`
- **Medium (date signal only):** `"Table '{name}' has had no data updated in {daysSinceMax} days (threshold: {thresholdDays}) — possibly stale."`
- **Low (cross-reference absence only):** `"{objectType} '{name}' is not referenced anywhere in the harvested database — possibly unused."`

`Signal` is a tagged union of three variants discriminated by
`signalType`:

```jsonc
// NamePatternSignal
{
  "signalType": "NamePattern",
  "summary": "Matched 'vocabulary' pattern: substring '_OLD'",
  "matchedSubstring": "_OLD",
  "category": "vocabulary" | "year" | "year-month" | "quarter" | "numeric-duplicate"
}

// DateSignal
{
  "signalType": "DateSignal",
  "summary": "Max DateTime value 2022-03-15 is 1535 days before harvest date 2026-05-28",
  "source": "com" | "dao-metadata",
  "maxValue": "2022-03-15T00:00:00Z",
  "daysSinceMax": 1535,
  "thresholdDays": 90
}

// CrossReferenceAbsenceSignal
{
  "signalType": "CrossReferenceAbsence",
  "summary": "Not referenced as RecordSource, RowSource, VbaReference, QueryTableReference, or FkRelationship",
  "checkedReferenceTypes": ["RecordSource", "RowSource", "VbaReference", "QueryTableReference", "FkRelationship"]
}
```

The schema enforces the tagged-union shape via `oneOf` over the three
variant definitions; each variant uses `additionalProperties: false`
and a `const` discriminator on `signalType`.

---

## 6. Output: `object-cleanup-checklist.json`

`uat-checklist-editor`-compatible JSON
(`https://github.com/darndestdabbler/uat-checklist-editor/blob/main/checklist-schema.json`).
Top-level matches the editor's schema (`ProjectName`, `Review[]`).

`ProjectName` = `"<database name> — Object Cleanup Checklist"`.

Each `Review` item maps a single `CleanupFinding`:

| Checklist field | Value |
|---|---|
| `FunctionalArea` | Object type — `"Form"`, `"Report"`, `"Query"`, `"Table"`, `"Module"`, `"Macro"`. |
| `Subarea` | The raw object name, byte-identical to `CleanupFinding.objectName`. **No prefixing, no mutation.** This invariant is required so downstream consumers (the `--exclude-cleanup` path in `harvester-object-use-identifier`) can match checklist entries to objects without parsing rules. See §10. |
| `ItemLabel` | The string `"[<Tier>] <explanation>"` where `<Tier>` is `High` / `Medium` / `Low` and `<explanation>` is the `CleanupFinding.explanation` text (templated per §5). The bracket prefix lets the reviewer triage the checklist visually without breaking the `Subarea` identity contract. |
| `Steps` | A single step: `[{ "StepNumber": 1, "Action": "Review with user: is this object still needed, or can it be excluded from downstream analysis?", "Verifications": [] }]`. |
| `IsJudgmentItem` | `true`. The cleanup decision is a judgement call by definition; suppressing the mechanical-verification floor matches `uatStyle: "ad-hoc"` semantics for this set. |
| `Passes` | `false` (initial). The user flips to `true` to mark the object as stale and excludable from downstream analysis. |
| `Feedback` | `""` (initial). |

**Semantics of `Passes`.** This is the inverted convention used by the
existing `--exclude-cleanup` contract in
`harvester-object-use-identifier` (strategy §9): `Passes: true` after
review means "yes, this is stale — exclude it"; `Passes: false` means
"keep it." This is intentionally counter-intuitive ("passes" sounding
like "still in use") but matches the existing downstream consumer and
mirrors the user's existing review workflow on other harvester
checklists. The Step 1 Action wording makes the polarity explicit.

System objects (`MSys*`, `USys*`) and event-handler code-behind
modules (`Form_*`, `Report_*`) are not included in the checklist —
they are filtered upstream by the detectors (see §3).

---

## 7. Exclusions

### 7.1 System objects (always excluded)

- Tables: name matches `MSys*` or `USys*`.
- Queries: name matches `~sq_*` (Access internal subquery cache),
  `MSys*`.
- Forms: `MSys*`.
- Reports: `MSys*`.
- Modules: none by name; only the event-handler code-behind
  filtering of §3.3 applies.
- Macros: `MSysNavPane*`.

### 7.2 Linked tables (excluded from date signal only)

Tables with non-empty `Connect` strings (linked tables) are excluded
from §3.2 date sampling — their data freshness reflects the remote
source and is not a property of this database. Linked tables are
still subject to §3.1 name patterns and §3.3 cross-reference absence
detection.

### 7.3 Event-handler code-behind modules (global)

Module IR files whose name matches `Form_<form-name>` or
`Report_<report-name>` are **globally excluded from the
detector pipeline** — they are skipped by §3.1
(NamePatternDetector), §3.2 (which is table-only and would not
have considered them anyway), and §3.3
(CrossReferenceAbsenceDetector). Their lifecycle derives from
their parent form/report; an `OldReports` parent form produces
`Form_OldReports` code-behind whose staleness vocabulary
trivially mirrors the parent's. Letting them surface as
independent findings would double-count the parent's signal
and clutter the checklist. Consequently they are never present
in `object-cleanup.json` and never present in
`object-cleanup-checklist.json` (§6 invariant).

---

## 8. CLI Interface

```
AccessHarvester identify-cleanup
    (--in <harvested-output-root> | --manifest <path>)   # exactly one of these
    [--harvest-date <YYYY-MM-DD>]    # calendar date for date-signal threshold (default: today)
    [--threshold-days <N>]           # date-signal threshold in days, N >= 1 (default: 90)
    [--no-com]                       # skip COM date sampling; rely on DAO LastUpdated fallback
    [--json-status]                  # emit structured JSON status envelope
```

**Required-one-of rule.** Exactly one of `--in <dir>` or
`--manifest <path>` must be supplied. Supplying both, or neither,
is a usage error: the CLI prints a usage message to stderr and
exits with code 2 (consistent with other harvester subcommand
usage errors).

**Argument formats.**
- `--harvest-date` accepts an ISO 8601 calendar date (`YYYY-MM-DD`,
  e.g., `2026-05-28`). Time-of-day suffixes are not accepted —
  the date-signal threshold is day-based. Defaults to the
  process's local date when omitted.
- `--threshold-days` accepts a positive integer. Values `< 1` are
  a usage error.

When `--manifest <path>` is supplied, the manifest's directory is
used as the input root and outputs are written beside the manifest.
When `--in <dir>` is supplied, outputs are written to `<dir>/`:

- `object-cleanup.json`
- `object-cleanup-checklist.json`

`--no-com` forces the DAO-metadata fallback path; useful on
non-Windows hosts and in CI. The fallback path also activates
automatically when COM extraction throws or the host platform is
non-Windows — `--no-com` is a discipline knob, not a correctness
gate.

`--json-status` follows the same envelope shape used by other
harvester subcommands (per existing CLI conventions in
`AccessHarvester.Cli`).

---

## 9. Signal Source Matrix

Summary of which signals require Access COM vs. derivable from
existing KB/IR files. Used by Session 2 to scope what runs hermetic
and what gates `[SkippableFact]`.

| Signal | Primary source | Requires COM? | Fallback if COM unavailable |
|---|---|---|---|
| §3.1 Name patterns | KB manifest object names | No | n/a |
| §3.2 Date signal (primary) | COM `MAX(<DateTime col>)` sampling | Yes | §3.2 fallback (DAO `LastUpdated`) |
| §3.2 Date signal (fallback) | IR table `LastUpdated` | No | none (signal does not fire) |
| §3.3 Cross-reference absence | IR navigation graph, VBA strings, RecordSource/RowSource, FK | No | n/a |

Unit tests for §3.1 and §3.3 run anywhere. Integration tests for the
§3.2 COM path are `[SkippableFact]`-gated and run only on Windows
with Access installed (canonical fixtures
`samples/HomeHealthCare.accdb` and `samples/NorthwindDevEdition.accdb`).

---

## 10. Precedence and Soft-Prereq Notes

- **`harvester-object-use-identifier`** is the downstream consumer.
  Its `--exclude-cleanup <path>` reads the
  `object-cleanup-checklist.json` produced here; objects with
  `Passes: true` are excluded from use classification. The two sets
  must agree on the checklist's polarity (§6) and on object naming
  (no transformations between detectors and the checklist).
- **`harvester-body-and-query-sql`** is a soft prerequisite. Its
  output populates the `Sql` field on `ir/Queries/*.ir.json`, which
  §3.3 uses for `QueryTableReference` detection (cross-query
  FROM/JOIN edges). When query SQL is unavailable the detection
  loses evidence in **both directions** of the edge: queries cannot
  prove they reference other tables/queries, AND tables cannot
  prove they are referenced by some query. Both query and table
  cross-reference-absence findings can therefore false-positive at
  Low confidence. The subcommand degrades gracefully (it does not
  fail) but produces fewer reliable signals; the Session 3 pilot
  documents the impact on accuracy. **Observability:** when query
  SQL is missing the subcommand appends an entry to the top-level
  `degradationNotes` array (see §5) so a reviewer inspecting
  Low-confidence query OR table findings can see at a glance that
  the cross-reference path ran with reduced coverage. Note:
  §3.3.1's inline-SQL parsing of `RecordSource` / `RowSource` is
  independent of this prereq — it reads the property values
  themselves from form/report IR, not from `ir/Queries/`.
- **`harvester-schema-v1-adr-followups`** is a hard prerequisite —
  its KB/IR shape is what §3 reads.

---

## 11. Calibration Hooks (Session 3)

The Session 3 pilot exercises the subcommand against the canonical
fixtures and may tune any of the following without revisiting this
strategy doc:

- The `vocabulary` token list in §3.1 (add or remove tokens).
- The default `thresholdDays` value in §3.2.
- The `explanation` templates in §5.
- The decision to ship Low-confidence findings to the checklist by
  default vs. suppressing them behind a flag (e.g.,
  `--include-low-confidence`).
- The `[<Tier>]` bracket-prefix convention on `ItemLabel` in §6
  (e.g., drop the prefix, or change the wording).

Any structural change to the signal families, the confidence tier
algorithm, the `object-cleanup.json` schema, or the checklist field
mapping is a revision of this strategy doc and requires human
approval.
