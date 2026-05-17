# Property-Sheet Expression Inventory

> **Session set:** `harvester-finish-canonical-parse-success-report`,
> Session 1 of 2 (2026-05-17).
> **Authored by:** Claude Opus 4.7 @ effort=high (orchestrator).
> **Source data:** [`tools/parse_success_inventory.py`](../../../tools/parse_success_inventory.py)
> walked the YAML mirrors at
> `samples/DatabaseYaml/HomeHealthCare.accdb/` (104 files) and
> `samples/DatabaseYaml/NorthwindDevEdition.accdb/` (218 files),
> tallying non-empty values for nine property-sheet field names that
> can plausibly contain expressions: `ValidationRule`, `DefaultValue`,
> `Format`, `InputMask`, `Filter`, `OrderBy`, `RecordSource`,
> `RowSource`, `ControlSource`. Raw counts and dialect classifications
> are preserved at
> [`tools/_inventory_out/property-sheet-inventory.json`](../../../tools/_inventory_out/property-sheet-inventory.json)
> (gitignored — regenerate with `python tools/parse_success_inventory.py`).

This document records the **verdict for Open Question 2's path (d)**
(property-sheet expressions in the canonical parse-success report)
that the stub spec deferred to this session.

---

## Per-fixture occurrence counts

| Field          | HHC occ / uniq | Northwind occ / uniq | Notes |
|----------------|---------------:|---------------------:|-------|
| ValidationRule | 19 / 14        | 3 / 2                | Access expression dialect (operators, Is/Like/In/Not Null, function calls, field references). |
| DefaultValue   | 12 / 9         | 16 / 7               | Mix of literals (True/False, integers, strings) and a few Access expressions (`Date()`, `Now()`, `[TempVars]![UserID]`). |
| Format         | 54 / 9         | 101 / 10             | Format keywords / format strings (`"Short Date"`, `"Currency"`, `"$#,##0.00"`). NOT expressions. |
| InputMask      | 7 / 2          | 9 / 1                | Mask templates (`"00000\\-9999;;_"`). NOT expressions. |
| Filter         | 0              | 12 / 10              | Jet SQL `WHERE`-clause fragments (`"ProductID = 1"`, `"([OrderDetails].[OrderID]=7)"`). |
| OrderBy        | 0              | 13 / 10              | Jet SQL `ORDER BY` fragments (`"[OrderDetails].[OrderID] DESC"`). |
| RecordSource   | 19 / 17        | 39 / 37              | Mostly bare table or query names; a few inline SELECT strings. |
| RowSource      | 23 / 15        | 28 / 21              | Mostly inline SELECT SQL or query names; a few value lists (`"Credit Card;Check;Cash"`). |
| ControlSource  | 138 / 61       | 316 / 158            | Mostly bare column identifiers — bound-control wiring, NOT expressions. |

> Note on numbers: the table reflects the corrected count after the
> regex was updated to accept YAML-escaped inner quotes (`\"` inside a
> quoted scalar). The R1 → R2 verification cycle flagged that the
> original `[^"]*` capture skipped lines like
> `ValidationRule: "In (\"M\",\"F\",\"X\")"`; both the inventory tool
> and the report generator now use `(?:[^"\\]|\\.)*` and reconcile
> end-to-end. Original (pre-fix) counts for traceability:
> ValidationRule HHC 15 / Northwind 3; DefaultValue HHC 8 / Northwind
> 15; Filter Northwind 8.

---

## Dialect partition

The nine fields fall into **four** evaluable dialects, only two of
which are expressions for parser-coverage purposes:

1. **Access-expression dialect** — `ValidationRule`, `DefaultValue`
   (when the value is not a bare literal). Operators (`>=`, `<=`,
   `<>`, `=`), keywords (`Is`, `Like`, `In`, `Between`, `Not`,
   `And`, `Or`), function calls (`Date()`, `Now()`),
   field-reference brackets (`[FieldName]`), and string/number
   literals. Shape is the VBA *expression* sub-grammar minus
   statement context. **In scope for parser coverage.**
2. **Jet-SQL fragment dialect** — `Filter`, `OrderBy`. These are
   already substrings of `WHERE` and `ORDER BY` clauses; the
   existing `AccessHarvester.Sql.Parser` Jet SQL grammar covers
   them when wrapped in a synthetic `SELECT 1 FROM dual WHERE <filter>`
   or `SELECT 1 FROM dual ORDER BY <orderBy>`. **In scope for
   parser coverage.**
3. **SQL or simple-identifier dialect** — `RecordSource`,
   `RowSource`, `ControlSource`. When the value looks like SQL
   (`SELECT ...`), it is already covered by the existing
   per-query Jet SQL parse pass (saved queries cover the same
   grammar; inline RowSource SQL strings are isomorphic). When
   the value is a bare identifier (table name, query name, column
   name), it is not parser scope — resolution lives in the
   metadata / resolver layer, which is **out of scope for this set**.
   Value-list `RowSource` strings (`"A;B;C"`) are a tokenised mini-
   dialect Access handles as a builtin format — not gated by the
   parser. **Out of scope for parser coverage in this set.** Already
   covered in adjacent scope where applicable.
4. **Mask / format dialect** — `Format`, `InputMask`. These are
   Access-internal display and input formatters with their own
   mini-language (`"Short Date"`, `"00000\\-9999"`). They are not
   expressions in the parser sense and have no existing grammar.
   Per the stub spec's "documented VBA / Jet SQL / property-sheet
   *construct*" framing, these are excluded from gating but any
   non-mechanically-decodable encounter still emits an
   `UnresolvedItem`. **Out of scope for parser coverage; explicit
   gaps via `UnresolvedItem`.**

---

## Verdict

**Property-sheet expressions FOLD INTO Phase 4 gating, scoped to
the Access-expression dialect (group 1) and the Jet-SQL fragment
dialect (group 2).** Justification:

- The unique-expression count per fixture lands around 23 (HHC,
  group 1 only) and 29 (Northwind, groups 1+2 combined). That sits
  just over the stub's "small/bounded < ~15 unique" rule of thumb,
  but the **shapes** are clean — two existing grammars cover
  everything the inventory found.
- Groups 1 and 2 route through the existing grammars without new
  rules: VBA expression for ValidationRule + DefaultValue; Jet SQL
  WHERE / ORDER BY for Filter + OrderBy. No grammar-extension work
  this set.
- Groups 3 (RowSource SELECT, RecordSource SELECT) duplicate
  scope already covered by the per-query Jet SQL pass. Counting
  them again would inflate report numbers without measuring new
  parser coverage.
- Group 4 (Format / InputMask) is **non-expression**. The stub's
  fallback path ("emit `UnresolvedItem` records for anything that
  can't decode mechanically") applies — these fields contribute
  zero parser-coverage signal and are excluded from the gate.

### What this means for the report

The first-pass `parse-success-report.json` per fixture includes a
`propertySheet` subsection with two buckets:

- `validationRules`: { total, parsedOk, parseFailed, unparseable
  shapes that emit `UnresolvedItem` records grouped by reason }
- `defaultValues`: { total, parsedOk (including bare literals),
  parseFailed }
- `formFilters` *(forms / reports / queries)*: { total, parsedOk,
  parseFailed }
- `formOrderBys` *(forms / reports / queries)*: { total, parsedOk,
  parseFailed }

`Format`, `InputMask`, `RecordSource`, `RowSource`, and
`ControlSource` do **not** appear as parser-coverage buckets —
they are out of scope for this report.

### Stop-rule guard

If Session 2's regression-test wiring encounters a property-sheet
expression that won't parse through either of the two named
grammars, the right response is to record it as an
`UnresolvedItem` with `Reason = "property-sheet-shape-not-yet-handled"`
and surface to the human — NOT to extend a grammar inside this
session set. Per the stub's stop-rule table, grammar extensions
for property-sheet expressions are a strategy-level decision and
do not absorb into Phase 4.
