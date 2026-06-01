# Verification Round 1

Scope-wise, this Session 1 slice is mostly aligned with the stated plan, and I am **not** treating Session 2/3 items as missing. The main positives are: the small `MigrationInputs` model is appropriately lean; driving generation from structured field metadata instead of re-parsing `Ddl` is the right call here; the CREATE TABLE / post-CREATE FK split is sensible; and the implementation generally follows the repo’s honesty rule by flagging degradations instead of inventing substitutes. That said, I do see a few concrete issues: one real byte-determinism problem, one honesty/correctness risk in default translation, and some test gaps relative to the explicit “cover each branch / each degradation path” requirement.

**1. Type map correctness, nullability, IDENTITY, DEFAULTs**

The Access→SQL Server type map is broadly correct and consistent with the disclosed plan:

- `dbBoolean`→`bit`
- `dbByte`→`tinyint`
- `dbInteger`→`smallint`
- `dbLong`→`int`, with `IsAutoIncrement` promoted to `IDENTITY(1,1)`
- `dbBigInt`→`bigint`
- `dbCurrency`→`money`
- `dbSingle`→`real`
- `dbDouble` / `dbFloat`→`float`
- `dbDate`→`datetime2`
- `dbGuid`→`uniqueidentifier`
- `dbMemo`→`nvarchar(max)`
- `dbLongBinary` (OLE Object)→`varbinary(max)` — correct, and importantly **not** degraded
- `dbText`→`nvarchar(n)` with default 255 when size absent
- `dbChar`→`nchar(n)`
- `dbNumeric` / `dbDecimal`→`decimal(18,4)` plus an informational degradation
- `dbAttachment` / unknown codes→flagged omission

Nullability handling is also correct for the stated scope:
- identity columns are forced `NOT NULL`
- PK columns are forced `NOT NULL` even if `Required=false`
- everything else follows `Required`

Default emission is mostly honest and correctly constrained:
- no DEFAULT on identity columns
- translated defaults are emitted as `DEFAULT (...)`
- untranslatable expressions are dropped and flagged

The translator correctly handles the cases you explicitly called out: `Date()`, `Now()`, `True/False`, `Yes/No`, numeric literals, and double-quoted strings. It also safely handles `Time()` and `On/Off`, which is fine.

The one correctness/honesty problem is the extra `#...#` date-literal branch. Right now **any** Access `#...#` literal is turned into a quoted SQL string. That is only safe for strict ISO-like literals. For locale-shaped literals such as `#1/2/2020#`, SQL Server interpretation can vary by session language/dateformat, which means this branch can silently change meaning instead of degrading honestly. That violates the project’s “never fabricate” standard more than the other translator branches do.

**2. FK emission correctness and safety**

The FK strategy is sound for Session 1:
- post-CREATE `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY` is the right choice if CREATE order should not depend on the relationship graph
- non-enforced Access relationships are correctly **not** turned into SQL Server FKs, and are flagged instead
- relationships to non-migrated tables are dropped and flagged
- relationships that reference columns degraded out of either side are dropped and flagged
- system-table relationships are excluded
- identifiers are correctly bracket-quoted, with embedded `]` escaped as `]]`
- FK names are uniquified deterministically enough for a given input ordering

I do not see a semantic problem with decision C; it is the honest call. Emitting SQL Server FKs for Access relationships that were informational-only would indeed over-constrain dirty source data.

Two smaller caveats:
- There is no explicit cross-check that the referenced parent column set is backed by a PK/UNIQUE constraint in the generated schema. In practice, enforced Access RI should imply a unique parent key, so this is acceptable for Session 1.
- A relationship with `FieldPairs.Count == 0` is silently skipped. That is a small honesty hole: a malformed relationship IR record can disappear without degradation.

**3. Determinism / byte-stability**

Semantically, the generator is mostly deterministic:
- tables sorted by name
- fields sorted by ordinal then name
- relationships sorted by name
- no timestamps in the script
- stable quoting / naming rules

However, the implementation is **not fully byte-stable across platforms** because SQL is assembled with `StringBuilder.AppendLine()`, which uses `Environment.NewLine`. Windows emits CRLF; Linux/macOS emit LF. So the current “Deterministic: no timestamps” comment is only true within a single OS family, not as a byte-identical artifact across environments.

A secondary determinism caveat: `CompareDegradations` ignores `Reason`, so multiple degradations with the same `(Table, Column, Kind)` compare equal. `List<T>.Sort()` is not stable, so later manifest ordering could drift if multiple FK-drop degradations land on the same child table. That does not affect the SQL text in Session 1, but it is worth fixing before Session 3 if the manifest order matters.

**4. Honest-degradation completeness**

For the in-scope behaviors, the implementation is mostly honest:
- unmappable column types are omitted and flagged
- classification-marked multi-value columns are omitted and flagged
- linked tables are skipped and flagged
- untranslated defaults are dropped and flagged
- missing/degraded FK references are dropped and flagged
- non-enforced relationships are dropped and flagged

So the main schema-generation surface is not silently inventing substitutes.

The notable exceptions are:
- system and temp tables are silently excluded — I think this is acceptable because they are explicitly out of migration scope
- empty-field relationships are silently ignored
- arbitrary `#...#` defaults are “best-effort” translated when they should only be translated if clearly safe

**5. Design decisions A–H**

A. **Structured metadata over `Ddl`: correct.** Given your note about `TableDdlBuilder.cs`, the structured collections are the authoritative source; the pre-flight spec assumption about verbatim Access DDL is wrong.

B. **Post-CREATE FKs: reasonable and correct for Session 1.**

C. **Drop non-enforced relationships as degradations: correct honesty call.**

D. **Type map: mostly sound.** OLE Object→`varbinary(max)` is right; 101/unknown as unmappable is right; `decimal(18,4)` with informational degradation is a reasonable lean default.

E. **Autonumber→`int IDENTITY(1,1) NOT NULL`: sound for the `dbLong` autonumber case in scope.**

F. **Classification used only for multi-value cross-reference in Session 1: sound.**

G. **No separate calculated-column handling because the fixture IR exposes no signal: acceptable and honestly disclosed.**

H. **New `MigrationDegradationKind` enum: correct separation of concerns.**

Overall, A–H are consistent with the repo’s lean/honest principles.

**6. Test adequacy**

The fixture stop-rule test is strong and valuable. Re-confirming against real committed IR is exactly the right kind of guardrail, and the “only two attachment degradations” result is a good Session 1 checkpoint.

The synthetic hermetic tests are good but **not yet complete enough to satisfy the explicit Session 1 testing claim**:
- type-map coverage misses implemented branches for `dbBinary` and `dbVarBinary`
- default translation tests miss `Time()` and safe/unsafe `#...#` behavior
- FK tests do not cover the missing-column drop path
- FK tests do not cover duplicate-name uniquification
- FK tests do not cover bracket-escaping in identifiers
- the sanitize-contract test does not really pin RFC 4180 quoting behavior, header behavior, or on-disk naming; it only checks a single row’s token formatting

So the testing story is good in spirit, but not yet at the “every branch / every explicit degradation path” bar stated in the session scope.

**7. Parsing risks in `MigrationIrReader`**

The basic parsing shape is correct, and using a dedicated migration-facing model is appropriate.

The main risk is that the reader is fairly permissive:
- missing `TypeCode` becomes `0`
- missing relationship names/tables/fields become empty strings
- missing booleans collapse to `false`

That is fine for trusted, self-produced IR, but it means malformed IR can degrade into silent omissions or invalid identifiers instead of failing fast. The most notable downstream effect is the earlier point: malformed/empty relationships can get silently skipped. I would not call this a blocker for Session 1, but it is the main parsing correctness risk in the current code.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Migration/SqlServerDdlGenerator.cs",
      "summary": "SQL assembly uses platform-dependent line endings via AppendLine(), so the generated script is not byte-stable across operating systems.",
      "fix": "Emit a fixed newline convention explicitly (for example '\\n' everywhere via a helper) instead of relying on Environment.NewLine."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/Migration/DefaultValueTranslator.cs",
      "summary": "Arbitrary Access #...# date literals are translated to quoted SQL strings even when the literal is locale-dependent or ambiguous, which can silently change meaning instead of honestly degrading.",
      "fix": "Only translate #...# literals when they match a strictly safe ISO-8601 date/time pattern; otherwise return false so the default is omitted and flagged."
    },
    {
      "severity": "Major",
      "location": "tests/AccessHarvester.Pipeline.Tests/Migration/MigrationDdlGeneratorTests.cs",
      "summary": "The hermetic tests do not yet cover all implemented Session-1 branches and explicit cross-check paths, including dbBinary/dbVarBinary mapping, missing-column FK drops, duplicate FK naming, and identifier escaping.",
      "fix": "Add targeted tests for the remaining type-map branches, FK degradation/name/escaping paths, and extend the sanitize-contract test to assert real RFC4180 quoting, header, and file-naming behavior."
    }
  ]
}
```

---

# Verification Round 2

Major fix 1 — byte determinism: confirmed. `SqlServerDdlGenerator` now uses a single `NL = "\n"` constant, `BuildCreateTable()` emits only that terminator inside multi-line `CREATE TABLE` statements, and `Assemble()` appends lines through `Line(...)` instead of `AppendLine()`. `BuildForeignKeys()` emits single-line statements and does not reintroduce platform newlines. The new test `GenerateDdl_uses_fixed_lf_newlines_for_cross_platform_byte_stability` correctly guards the original failure mode (`\r\n` leakage). I do not see a regression here.

Major fix 2 — honesty for `#...#` date literals: confirmed. `DefaultValueTranslator.TryTranslate()` now translates Access `#...#` literals only when the inner text matches the strict year-first regex, and otherwise returns `false`. In the generator, that means the default is omitted and an `UntranslatedDefault` degradation is emitted instead of fabricating a potentially wrong SQL literal. The added test cases cover the intended accept/reject behavior, including the previously problematic `#1/2/2020#`. Existing translations for booleans, numerics, quoted strings, and date/time functions remain intact. No regression found from this change.

Major fix 3 — coverage gaps: confirmed for the gaps called out in Round 1. The added type-map cases exercise the fixed/variable binary branches that were previously missing. The new DDL tests cover: dropping an FK when a referenced column did not survive migration, uniquifying duplicate FK names, and correct identifier escaping for embedded `]`. The CSV contract tests now pin RFC4180 quoting, header-row presence, and file extension. Given the reported full green test run and that the real-IR fixture stop-rule still passes, this closes the specific coverage holes that were identified without changing production behavior in a harmful way.

Minor fix — empty `FieldPairs` relationship silently skipped: confirmed. `BuildForeignKeys()` now emits a `ForeignKeyDropped` degradation when `rel.FieldPairs.Count == 0` instead of disappearing the relationship silently. That is the honest behavior and matches the rest of the FK-drop handling.

Minor fix — degradation ordering ignored `Reason`: confirmed. `CompareDegradations()` now uses `Reason` as a final comparator, which makes ordering deterministic for otherwise-equal degradations. This is the right stabilizer and does not change migration semantics.

New issues introduced by these fixes: none found in the shown code/tests. The fixes are correct, complete for the Round-1 findings, and I do not see a regression.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
