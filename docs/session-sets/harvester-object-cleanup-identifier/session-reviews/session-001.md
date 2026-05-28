# Verification Round 1

1. **Signal-family coverage.**  
§3 does cover all three required signal families structurally, but not yet at closeout-grade fidelity.

- **Name patterns:** this is the weakest part. The strategy names the right families of patterns, but several regexes are not implementation-safe as written.
  - The table-cell regexes use markdown-escaped alternation like `\|`. In rendered markdown that is understandable, but if Session 2 copies literally, the regex means a literal pipe, not alternation.
  - More importantly, the documented patterns do **not** actually match some of the examples the spec requires. `\b(19|20)\d{2}\b` will not match `_2023` because `_` is a regex word character, so there is no word boundary before `2023`. The same problem affects `_Q12025` and underscore-delimited year/month forms.
  - The documented `year-month` pattern matches `2024Jan` / `2024-01`, but it does **not** match the explicitly cited example `Jan2024`.
- **Date signals:** this is mostly well specified. Primary COM path, DAO/`LastUpdated` fallback, linked-table exclusion, and threshold semantics are all clear enough for Session 2.
- **Cross-reference absence:** the section provides a useful starting matrix, but it is not complete enough to eliminate guesswork. The biggest gap is that it treats `RecordSource`/`RowSource` mainly as direct object-name bindings and does not fully specify how inline SQL in those properties should be parsed. That is a common Access reference path.

So: all three families are present, but §3 is **not fully contract-safe** yet because the regex contract is incorrect/incomplete and the cross-reference matrix still leaves important reference paths underspecified.

2. **Confidence-tier algorithm.**  
The combinator is mostly unambiguous, and on the specific questions:

- **Can a Medium finding ever become High via cross-reference absence?** No. §4 explicitly says cross-reference absence is a Low-only signal and does not upgrade Medium or High. I think that is the right call. Cross-reference absence is weak evidence and should not promote otherwise moderate findings.
- **If a name pattern fires and cross-reference absence fires for a non-table, is that High or Medium?** It is **Medium**. High requires `NamePattern + DateSignal`; non-tables cannot have `DateSignal`. Because §4 says Low applies only when cross-reference absence is the **only** signal, name-pattern + absence still resolves to Medium.

I would still tighten the prose with a tiny truth table, because the phrase “exactly one of `NamePattern` or `DateSignal` fired” can make readers pause when `CrossReferenceAbsence` also fired. But the intended result is recoverable from the current text, and I do **not** view the tiering logic itself as a major design flaw.

3. **JSON Schema correctness.**  
The schema is mostly solid, but it has one real contract mismatch.

- **Tagged union:** the `oneOf` pattern is correctly discriminated by `signalType` `const` values. Because each variant also has `required` fields and `additionalProperties: false`, the union is clean and not accidentally overlapping.
- **`additionalProperties: false` / `required`:** these are present and appropriately strict on the top-level object, `CleanupFinding`, and each concrete signal variant.
- **`firedSignals.minItems: 1`:** yes, this aligns with the “no fired signal → no finding” rule. A finding must have at least one signal.

The schema defect is:
- **`harvestDate` mismatch.** Strategy §5 describes top-level `harvestDate` as an **ISO 8601 date**, but the schema enforces `format: "date-time"`. The CLI section also talks about “today” semantics, which reads like date-only behavior. Session 2 should not have to guess whether to emit `2026-05-28` or `2026-05-28T00:00:00Z`.

There is also a smaller documentation bug in the schema: the `DateSignal.thresholdDays` description says it is “Always >= `daysSinceMax`”, which is backwards for a stale finding; a stale signal should require `daysSinceMax` to exceed the threshold.

4. **Cross-reference absence semantics.**  
This is the second major weak spot after the regexes.

What is good:
- The detector is clearly intended to be **IR-only** and to treat absence as weak evidence.
- The self-reference rule is directionally correct: own-form VBA and query self-reference should not count.

What is missing or underspecified:
- **Inline SQL in `RecordSource` / `RowSource`.** The matrix says a table/query is referenced when its name appears as a form/report `RecordSource`, and `RowSource` excludes inline `SELECT` SQL. That misses a very common Access pattern: forms/reports/controls often use inline SQL instead of a saved query name. Under the current contract, a table referenced only inside `SELECT ... FROM tblX ...` in a `RecordSource` or `RowSource` can be falsely treated as unreferenced.
- **Event-property macro references.** The matrix says macros are referenced by “RunMacro / OnLoad-style event property bindings”, but the source list only explicitly mentions `ir/Macros/*.ir.json` action nodes and VBA regexes. It does not clearly identify the IR path where form/report/control event properties are read, so Session 2 still has to infer where those bindings live.
- **Type taxonomy drift.** The matrix says `Module / Class`, but the output schema and §5 finding type system only allow `Module`. Separately, the strategy/schema include `Macro` as a first-class object type even though the spec excerpt’s primary object list is tables/queries/forms/reports/modules. That is an unresolved scope/taxonomy mismatch.

So the matrix is not exhaustive enough yet for closeout-grade implementation without guesswork.

5. **uat-checklist-editor mapping.**  
At the shape level, the mapping looks plausibly compatible with `uat-checklist-editor`, and the **`Passes: false` default polarity** is correctly aligned with the described downstream `harvester-object-use-identifier --exclude-cleanup` contract: the reviewer flips `Passes` to `true` to mean “exclude this object.”

The problem is the **`Subarea` bracket-prefix convention**:
- §6 says `Subarea` will be `"[High] tblClients_OLD_2023"`.
- §10 also says downstream matching must preserve object naming with “no transformations between detectors and the checklist.”

Those two statements conflict. If downstream exclusion reads the checklist and expects exact object names, prefixing confidence into `Subarea` mutates the identity and will either break exclusion matching or force Session 2/3 to invent undocumented parsing rules. For a machine-consumed checklist, the canonical object name should stay raw. Confidence can go into `ItemLabel`, `FunctionalArea`, or another non-identity field.

So the mapping is **not contract-safe** as written, even though the polarity itself is fine.

6. **CLI surface.**  
The CLI section is understandable, but it is not fully explicit enough for Session 2.

- **`--in` / `--manifest`:** the text implies they are alternatives, but it never says the validation rule in one sentence: **exactly one must be supplied**. It also does not say what happens if both are provided.
- **`--harvest-date`:** not well defined because the document alternates between date-only and date-time semantics. That affects parsing, schema emission, and tests.
- **`--threshold-days`:** meaning is clear; minimum valid value should be stated in the CLI contract, not only in the schema.
- **`--no-com`:** well defined.
- **`--json-status`:** “same envelope as other subcommands” is probably enough for an in-repo implementer and Program.cs dispatcher hookup.

So Program.cs integration is straightforward, but the input-argument contract still needs one round of tightening.

7. **Degradation paths.**  
The date-related degradation story is mostly good.

- **Access COM unavailable:** clearly handled. Fallback to DAO metadata is specified, and `--no-com` is defined as a force-fallback switch.
- **`LastUpdated` absent:** also handled. If neither COM nor `LastUpdated` yields data, no date signal fires.
- **Observability:** good for the date path. Top-level `dateSignalSource` can be `com`, `dao-metadata`, or `unavailable`, and per-finding `DateSignal.source` captures the specific source.

The weaker degradation path is:
- **Missing query SQL (`harvester-body-and-query-sql` soft prereq):** §10 says the tool degrades gracefully and may produce less reliable Low-confidence results, but that degradation is **not surfaced in output**. A reviewer looking at a Low-confidence “unreferenced query” cannot tell whether it was judged with full SQL evidence or with SQL entirely missing. That is not fatal, but it is a real observability gap for a closeout-grade contract.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/planning/object-cleanup-strategy.md §3.1",
      "summary": "The documented year/year-month/quarter regexes do not safely encode the required examples and are ambiguous because of markdown-escaped alternation.",
      "fix": "Rewrite the regex contract in implementation-safe form outside the markdown table, explicitly covering underscore-delimited tokens like `_2023` and month-first forms like `Jan2024`, and avoid `\\|` notation that could be copied literally."
    },
    {
      "severity": "Major",
      "location": "docs/planning/object-cleanup-strategy.md §3.3",
      "summary": "The cross-reference absence matrix misses common reference paths such as inline SQL in RecordSource/RowSource and underspecifies where event-property macro references are read from IR.",
      "fix": "Extend §3.3 to parse inline SQL in form/report RecordSource and control RowSource, specify the exact IR property locations for event-property macro bindings, and clarify whether class modules are folded into Module or need separate handling."
    },
    {
      "severity": "Major",
      "location": "docs/planning/object-cleanup-strategy.md §§6,10",
      "summary": "Prefixing confidence into `Subarea` changes the canonical object name and conflicts with the stated downstream no-transformation naming contract.",
      "fix": "Keep `Subarea` as the raw object name and move confidence into `ItemLabel` or another non-identity field, or explicitly document and implement a required strip-prefix rule in the downstream consumer."
    },
    {
      "severity": "Major",
      "location": "docs/planning/object-cleanup-strategy.md §§5,8 and AccessHarvester.Pipeline/Cleanup/object-cleanup-schema.json",
      "summary": "`harvestDate` is described as an ISO date in the strategy but enforced as `date-time` in the schema, leaving the CLI/output contract inconsistent.",
      "fix": "Choose a single canonical representation for `harvestDate` (preferably date-only if thresholding is day-based) and align the strategy examples, CLI argument definition, and schema format accordingly."
    },
    {
      "severity": "Minor",
      "location": "docs/planning/object-cleanup-strategy.md §8",
      "summary": "The CLI contract implies but does not explicitly state that exactly one of `--in` or `--manifest` must be provided.",
      "fix": "Add explicit required-one-of language and define the error behavior when both or neither options are supplied."
    },
    {
      "severity": "Minor",
      "location": "docs/planning/object-cleanup-strategy.md §10",
      "summary": "Graceful degradation when query SQL is missing is documented, but the reduced cross-reference coverage is not surfaced in the output.",
      "fix": "Add a top-level diagnostics/capabilities field or warning entry indicating when SQL-derived cross-reference detection was unavailable so Low-confidence findings can be interpreted correctly."
    }
  ]
}
```

---

# Verification Round 2

1. **R1 finding #1 — Regex contract**

This is **not fully resolved yet**.

The rewrite is much better than the R1 version: the regexes are now outside tables, the boundary rationale is explicit, the `year` and `quarter` patterns are implementation-safe, and the numeric-duplicate rule is clear enough to code directly.

However, **§3.1.3 still does not match its own documented contract**. The prose says month-first year-month tokens include examples such as:

- `Jan2024`
- `01-2024`
- `Jan_2024`

But the actual month-first branch of the regex is:

```regex
(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[-_]?((?:19|20)\d{2})
```

That branch allows **named months only**. It does **not** allow numeric month-first forms like `01-2024`, which the prose explicitly promises.

So Session 2 cannot implement this “verbatim” and satisfy the written spec at the same time. The fix is straightforward: either remove numeric month-first examples from the prose, or extend the month-first branch to also accept `0[1-9]|1[0-2]`.

Everything else in §3.1 is materially improved. The blocking defect is specifically the missing numeric month-first alternative.

2. **R1 finding #2 — Cross-reference matrix**

This is **mostly resolved, but not completely**.

What is now clearly fixed:

- **§3.3.1** explicitly covers inline SQL inside `RecordSource` / `RowSource`.
- **§3.3.2** explicitly covers event-property bindings.
- The **Module taxonomy** is now clear that standard modules and class modules share one IR source path and one `Module` finding type.

Those additions do close the main R1 omission.

But **§3.3.2 contains a new internal contradiction**:

> “Each property whose name begins with `On` ... is a candidate event-property binding”

and then its examples include:

- `BeforeUpdate`
- `AfterUpdate`

Those example names do **not** begin with `On`.

That leaves Session 2 with an implementation ambiguity:

- inspect only `On*` properties, or
- inspect all event-like properties including `BeforeUpdate` / `AfterUpdate`?

Because this affects actual reference discovery, I do not consider the cross-reference contract fully unambiguous yet. The section needs either:

- a precise predicate, or
- an explicit allowlist / rule such as “all event properties, including `On*`, `BeforeUpdate`, `AfterUpdate`, ...”.

3. **R1 finding #3 — Subarea polarity**

This **is resolved**.

The current strategy is now internally consistent on the no-transformation rule:

- `Subarea` is explicitly the raw object name,
- it must be **byte-identical** to `CleanupFinding.objectName`,
- prefixing moved to `ItemLabel`,
- §10 reinforces that object naming must not be transformed,
- §11’s calibration hook now correctly points at the `ItemLabel` prefix, not `Subarea`.

I did not find any remaining contradiction in the deliverables on this point. The identity contract needed by downstream `--exclude-cleanup` consumers is now stated clearly and consistently.

4. **R1 finding #4 — `harvestDate` type**

This **is resolved**.

The strategy, CLI contract, and schema are aligned:

- Strategy §5: `harvestDate` is a calendar date `YYYY-MM-DD`
- Schema: `"format": "date"`
- CLI docs: `--harvest-date <YYYY-MM-DD>`

Also, the distinction between:

- `harvestDate` as date-only, and
- `generatedAt` as date-time

is now coherent and well explained.

5. **R1 finding #5 — CLI mutual exclusion**

This **is resolved**.

The contract is explicit enough for implementation and testing:

- “Exactly one of `--in` or `--manifest` must be supplied”
- both supplied = usage error
- neither supplied = usage error
- usage message to stderr
- exit code 2

That is precise and leaves no meaningful ambiguity.

6. **R1 finding #6 — `degradationNotes`**

This **is resolved** with one caveat noted in §7 below.

The observability gap itself is closed:

- `degradationNotes` is present in strategy §5,
- it is present in the schema,
- it is in the schema’s top-level `required` list,
- §10 ties missing query SQL to a degradation note.

So the output shape now has a proper place to surface degraded detection coverage.

The caveat is not about the existence of the field; it is about one inaccurate sentence in §10 describing **which** detection path depends on missing query SQL. That is a cross-reference wording bug, not an observability-shape gap.

7. **Regression check**

I checked the specific regression points you called out:

- **Schema `required` list:** looks correct. Top-level required fields now include `degradationNotes`, and the finding/signal unions are structurally consistent.
- **§11 calibration hooks:** updated correctly. They now refer to the `ItemLabel` bracket-prefix convention, which matches §6.
- **§10 inline-SQL prereq language:** **still incorrect**.

The problem in §10 is this sentence:

> `harvester-body-and-query-sql` ... populates the `Sql` field on `ir/Queries/*.ir.json`, which §3.3 uses for `QueryTableReference` detection and which §3.3.1 uses for inline-SQL parsing inside `RecordSource` / `RowSource`.

That second clause is wrong. **§3.3.1 parses inline SQL directly from `RecordSource` / `RowSource` property values**, not from `ir/Queries/*.ir.json` `Sql` fields. Query SQL is relevant to **`QueryTableReference`**, but not to whether a form/report/control property contains inline SQL.

So the remaining regression status is:

- no new schema `required` problem,
- no stale §11 calibration reference,
- **one remaining cross-section contradiction in §10**.

Overall verdict: the deliverables are much improved and 4 of the 6 R1 findings are cleanly fixed, but I cannot mark them **VERIFIED** yet because there are still two implementation-significant doc issues and one remaining cross-reference inconsistency.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "docs/planning/object-cleanup-strategy.md §3.1.3",
      "summary": "The year-month regex does not implement the documented numeric month-first form.",
      "details": "The prose explicitly says month-first tokens include examples like `01-2024`, but the regex's month-first branch only allows named months (`Jan`..`Dec`). Session 2 cannot implement the regex verbatim and still satisfy the written contract."
    },
    {
      "severity": "Major",
      "location": "docs/planning/object-cleanup-strategy.md §3.3.2",
      "summary": "The event-property binding rule is internally contradictory.",
      "details": "The section says candidate properties are those whose names begin with `On`, but its own examples include `BeforeUpdate` and `AfterUpdate`, which do not. The allowed property-name set/predicate must be defined precisely so Session 2 knows what to inspect."
    },
    {
      "severity": "Minor",
      "location": "docs/planning/object-cleanup-strategy.md §10",
      "summary": "The soft-prerequisite note incorrectly says query `Sql` fields are used by §3.3.1 inline-SQL parsing.",
      "details": "§3.3.1 parses inline SQL from `RecordSource` / `RowSource` property values themselves. Missing `ir/Queries/*.ir.json` `Sql` fields degrade `QueryTableReference`, but not §3.3.1. The current wording conflicts with §3.3.1 and overstates the dependency on `harvester-body-and-query-sql`."
    }
  ]
}
```

---

# Verification Round 3

### 1. R2 finding #1 — year-month regex (§3.1.3)

This specific R2 issue is closed.

The revised `year-month` regex now includes both numeric months (`0[1-9]|1[0-2]`) and named months (`Jan`–`Dec`) on both alternation branches:

- year-first: `((?:19|20)\d{2})[-_]?(0[1-9]|1[0-2]|Jan|...|Dec)`
- month-first: `(0[1-9]|1[0-2]|Jan|...|Dec)[-_]?((?:19|20)\d{2})`

That means all prose examples in §3.1.3 now match:

- year-first:
  - `2024Jan`
  - `2024-01`
  - `2024_Jan`
  - `202401`
- month-first:
  - `Jan2024`
  - `01-2024`
  - `Jan_2024`
  - `012024`

I do not see a remaining prose/example mismatch in §3.1.3.

---

### 2. R2 finding #2 — event-property predicate (§3.3.2)

This R2 issue is also closed.

The predicate is now deterministic:

- candidate property iff name starts with:
  - `On`, or
  - `Before`, or
  - `After`
- matching is explicitly case-sensitive
- the detector inspects the property **value**, not the name, for navigation evidence
- only bare-identifier values matching `^[A-Za-z][\w]*$` and resolving to an existing macro count as a macro navigation edge
- `[Event Procedure]`, `=` expressions, and non-bare-identifier values are explicitly excluded
- traversal scope is explicit: top-level `Properties` plus every child control `Properties`

So from a Session 2 implementation standpoint, the rule is unambiguous. It may be intentionally best-effort in coverage, but it is sufficiently specified.

---

### 3. R2 finding #3 — §10 prereq scope

This is improved, but not fully closed.

The important R2 fix did land: §10 now correctly limits the soft prerequisite to **`QueryTableReference` detection only**, and it explicitly states that §3.3.1 inline-SQL parsing of `RecordSource` / `RowSource` is independent of that prerequisite. That part is correct.

However, the remaining wording in §10 and the example degradation note in §5 still describe the downstream impact too narrowly. Missing query SQL does not only raise false positives for **queries**. Because `QueryTableReference` is an applicable reference type for both **Table** and **Query** objects, missing `ir/Queries/*.ir.json` `Sql` also removes evidence that **tables** are referenced by queries.

So the dependency surface is now scoped correctly, but the impact statement is still incomplete.

---

### 4. Final regression check

I do **not** see stale internal section references, and the §3.1 pattern examples now line up with their regexes. I also do **not** see a direct §6-versus-§5 checklist/output contract divergence; the checklist mapping itself is consistent with the `CleanupFinding` shape.

However, I still found remaining implementation-affecting issues, so I cannot mark the package `VERIFIED`:

1. **Event-handler code-behind module exclusion is internally inconsistent.**  
   §6 says `Form_*` / `Report_*` code-behind modules are not included in the checklist and are filtered upstream. But §3.1 still applies `NamePatternDetector` to all modules and excludes only system objects, while §7.3 says those modules are skipped by **§3.3** only. So a module like `Form_OldReports` could still produce a `NamePattern` finding, contradicting §6. Session 2 needs one explicit rule: either globally exclude these modules from all findings, or revise §6/§7.3 to allow them.

2. **The soft-prereq degradation messaging is still query-only when tables are affected too.**  
   This is the unfinished part of concern #3. The scoping fix is good, but the documented impact still under-describes the affected finding set.

3. **`DateSignal.maxValue` example conflicts with the schema’s `date-time` format.**  
   The strategy example uses `2022-03-15T00:00:00` with no timezone, while the schema uses JSON Schema `format: "date-time"`, which is RFC3339 and requires a timezone offset or `Z`. That is a real doc/schema mismatch.

Because of those remaining issues, the document is **not yet fully implementation-ready for Session 2**.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "section": "§3.1, §3.3, §6, §7.3",
      "title": "Event-handler code-behind modules are inconsistently excluded",
      "details": "§6 says Form_*/Report_* modules are not included in the checklist, but §3.1 applies NamePatternDetector to all modules and excludes only system objects, while §7.3 excludes those modules from §3.3 only. A module such as Form_OldReports could still produce a NamePattern finding. The strategy needs one unambiguous rule: globally exclude these modules from all findings, or update §6/§7.3 to allow them."
    },
    {
      "severity": "Minor",
      "section": "§5, §10",
      "title": "Query SQL soft-prereq impact is still described too narrowly",
      "details": "The prereq is now correctly scoped to QueryTableReference detection only, and §3.3.1 is correctly stated as independent. However the degradation note and §10 text say only Low-confidence query findings are affected. Missing ir/Queries/*.ir.json Sql also removes inbound query-reference evidence for tables, so Low-confidence table findings can false-positive too."
    },
    {
      "severity": "Minor",
      "section": "§5 and object-cleanup-schema.json#/definitions/DateSignal/properties/maxValue",
      "title": "DateSignal maxValue example conflicts with schema date-time format",
      "details": "The strategy example uses \"2022-03-15T00:00:00\" without a timezone, but the schema requires format: date-time, which in draft-07/RFC3339 requires an offset or Z. Either the example/spec text should require a zoned timestamp (for example 2022-03-15T00:00:00Z) or the schema should be relaxed."
    }
  ]
}
```

---

# Verification Round 4

1. **R3 Major — event-handler code-behind global exclusion**

The substantive R3 Major fix is in place. The operative contract across the cited sections is now coherent:

- **§7.3** explicitly promotes `Form_*` / `Report_*` code-behind modules to a **global exclusion**.
- It explicitly says they are skipped by **§3.1**, **§3.2**, and **§3.3**.
- It explicitly says they are **never present** in either `object-cleanup.json` or `object-cleanup-checklist.json`.
- **§3.3** still carries the detector-level exclusion, which is consistent with the global rule.
- **§6** now matches that outcome by stating they are not included in the checklist.

So for the asked §3.1↔§3.3↔§6↔§7.3 contract, the prior contradiction is resolved.

2. **R3 Minor — bidirectional SQL impact**

This is now described correctly. The updated text in **§10** properly states the loss of `QueryTableReference` evidence in both directions when query SQL is absent:

- **queries** lose evidence that they reference other tables/queries, and
- **tables** lose evidence that they are referenced by queries.

The **§5** `degradationNotes` example now mirrors that same two-sided degradation surface. I do not see a remaining one-sided description in the strategy/schema pair on this point.

3. **R3 Minor — `maxValue` timezone**

This fix is also correctly aligned now.

- The strategy example uses a timezone-qualified value: `2022-03-15T00:00:00Z`.
- The schema keeps `maxValue` as `format: "date-time"`.
- The schema description now explicitly requires a timezone offset and tells DAO/`LastUpdated` callers to emit `Z`.

That makes the strategy example and schema contract consistent for `DateSignal.maxValue`.

4. **Closeout-grade final pass**

I do see **one remaining doc-consistency issue**, so I cannot give a clean VERIFIED.

In **§5**, the `DateSignal` example is internally inconsistent on the day-count arithmetic:

- `maxValue`: `2022-03-15T00:00:00Z`
- `harvestDate`: `2026-05-28`
- `daysSinceMax`: `1080`

Those dates do **not** correspond to 1080 days under any normal whole-day interpretation; the elapsed time is about **1535 days**. The `summary` string repeats the same incorrect `1080` value, so the example is self-consistent with itself but inconsistent with its own dates.

I did not find other strategy↔schema structural contradictions beyond that remaining example defect, but this one is still a real consistency issue.

{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "sections": ["§5"],
      "summary": "DateSignal example day-count is inconsistent with its own dates.",
      "details": "The example uses maxValue '2022-03-15T00:00:00Z' and harvestDate '2026-05-28' but says daysSinceMax is 1080. That date pair is roughly 1535 days apart, not 1080. Update either the dates or the daysSinceMax/summary text so the example is internally consistent."
    }
  ]
}
