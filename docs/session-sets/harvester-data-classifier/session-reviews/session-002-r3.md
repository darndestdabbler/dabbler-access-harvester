1. **R2 #1 fix — lookup-promotion exclusion list.**  
The change in `AccessHarvester.Pipeline/Classification/HeuristicsEngine.cs` resolves the specific R2 defect. The post-pass now promotes lookup-table payload columns using a **negative exclusion list** instead of the old effective allowlist. Concretely, once `ClassifyTable` returns `lookupGuess == true` and `hint == TableHint.LookupReference`, the code skips promotion for:
   - `c.IsPrimaryKey`
   - `c.IsForeignKey`
   - `c.IsMemo`
   - any `(table.Name, c.Name)` found in `systemColumnSet`
   - already-structural/sensitive categories: `SurrogateAutonumberPk`, `ForeignKey`, `FreeTextMemo`, `SystemAudit`, `DirectIdentifierPii`, `QuasiIdentifier`, `LookupReference`

   Because `SensitiveNonPii` is **not** excluded anymore, a column that landed there only because `ValueFormatPattern` was recognized will now promote to `LookupReference` inside a lookup table, which is exactly the missed case from R2. At the same time, PK/FK/memo/system-audit/PII columns are still left alone by explicit guards. So the behavior now matches the intent described in the retry note.

   The new test `Lookup_payload_promotion_includes_format_pattern_sensitive_columns` is a good pin for the original bug. It sets up the exact failure mode: a thin lookup table whose payload column has a stable format pattern and therefore initially classifies as `sensitive-non-pii`, then verifies that the post-pass promotes it to `lookup-reference`. That is tight enough for the regression it is meant to cover. Existing coverage like `Lookup_payload_promotion_skips_structural_columns` also helps on the “don’t overwrite PK/FK” side. I would not ask for broader test expansion at this stage.

2. **R2 #2 fix — escaper-rejected identifiers become degradations.**  
This is fixed in `AccessHarvester.Core/Com/OleDbValueSampler.cs`. `EscapeIdentifier(table.Name)` is now inside its own `try/catch`, and an `ArgumentException` adds a `ClassificationDegradation` with `Kind: SamplingError`, then `continue`s the outer table loop. Likewise, `EscapeIdentifier(col.Name)` is inside its own `try/catch`, records `SamplingError`, and `continue`s the inner column loop.

   That means the control flow now does what R2 required:
   - a rejected **table** identifier skips only that table and proceeds to the next table;
   - a rejected **column** identifier skips only that column and proceeds to the next column in the same table.

   On the actual classify path, `ClassificationRunner` calls the sampler through `SampleWithDegradations`, so these degradations are surfaced to the CLI instead of disappearing. This is no longer a fatal abort path.

3. **R2 #3 fix — DAO property HRESULT filter.**  
This is fixed correctly in `AccessHarvester.Core/Com/OleDbDaoSchemaReader.cs`. Both `TryReadBoolProperty` and `TryReadStringProperty` now use:

   - `catch (COMException ex) when (DaoPropertyAbsentHResults.Contains(ex.HResult))`

   and `DaoPropertyAbsentHResults` contains:
   - `0x800A0CC1` — DAO 3265, “Item not found in this collection”
   - `0x800A0CB3` — DAO 3251, “Operation not supported for this type of object”

   Those are the right two “treat as absent/unsupported property” cases for this code path. The important part is what happens to everything else: other COM failures are no longer swallowed. They bubble back to the surrounding per-field catch in `ReadDaoAugmentations`, which records a `SchemaReadError` degradation and continues. So real DAO property-read failures now surface as degradations instead of being misclassified as “property absent.”

4. **R2 #4 fix — composite-FK detection.**  
`DetectCompositeFks` now behaves the right way for the R2 finding. It does two separate things:

   - First, it **always** records per-column FK targets into `sink[(fk.FkTableName, fk.FkColumnName)] = new RawFkTarget(...)` for every FK row.
   - Second, composite detection only runs against rows where `ConstraintName` / `FK_NAME` is populated, grouping by that name and emitting `CompositeFkUnsupported` only when the group has more than one row.

   That means:
   - composite degradations are now emitted **only when `FK_NAME` is present**;
   - the detector no longer uses the removed coarse fallback that grouped by source→target table pair;
   - two parallel single-column FKs to the same target table are no longer falsely conflated when `FK_NAME` is absent;
   - per-column FK metadata is still preserved even if composite detection cannot authoritatively say more.

   The code does not explicitly distinct on `FK_COLUMN_NAME`, but given the OLE DB `Foreign_Keys` rowset shape—one row per FK column—that is sufficient for the intended detection. I do not see a remaining real problem here.

5. **Re-check of prior resolved findings and regression scan.**  
The previously resolved items remain intact:

   - **R1 #2 RCW cleanup:** still intact. `currentDbObj`, `tableDefsObj`, each `td`, each `fieldsObj`, each `fd`, and property RCWs are all released in paired `finally` blocks.
   - **R1 #5 implicit-FK resolver:** still intact. `ResolveImplicitFk` still avoids fabricating `fkTarget.column = "*"`, prefers the authoritative PK column, and degrades unresolved cases.
   - **R2 fixes:** all four remain present in the current code as described above.

   I do not see a regression introduced by these R2 changes.

   On the privacy invariant, the scanner still holds the line from plan § 4. `classification.json` only carries derived signals (`piiValueMatch`, counts, format patterns, etc.). Degradations are not written into `classification.json` at all; they only travel through the CLI status surface. And within those degradation messages, I do not see any code path that injects raw sampled row values. The details are built from identifiers, relationship names, `RowSource` metadata, and exception messages from schema/query operations—not from the sampled value list itself.

6. **Close-out vs. deferred.**  
I do not see anything left that should block close-out for this round. The four R2 findings are resolved in code, and I do not see a genuine new defect introduced by the fixes. The only thing I would note is that the provided test additions visibly pin R2 #1 more directly than R2 #2–#4, but the implementations for #2–#4 are straightforward and verifiable by inspection, and this is the second retry with the workflow cap in mind. I would not spin another round for that.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```