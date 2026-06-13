Overall, this session fixes the core off-by-one defect for the common DAO query types, and the `PassingMechanism` change is the right direction. But I do not think the session is fully correct as committed, because the DAO enum fidelity is still incomplete at code `144` and the usage-classification rules do not consistently account for that code.

1. **DAO decode accuracy against `QueryDefTypeEnum`**
   - The **numeric slots** are now aligned for the main sequence: `0, 16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 224`.
   - The **remaining problem** is `144`: DAO’s enum member is **`dbQSPTBulk`**, but `QueryExporter.QueryTypeName(144)` still returns **`"SqlPassthrough"`**. That is not a faithful decode of the enum member; it collapses two distinct DAO values (`112` and `144`) to one label.
   - `128 => "Union"` is a reasonable schema-facing normalization of `dbQSetOperation`, and `160 => "Compound"` / `224 => "Procedure"` look fine.
   - So: **the off-by-one bug is fixed, but the decode table is not fully faithful because 144 is still mislabeled**.

2. **Centralizing `DaoQueryTypeCodes` / rewiring `UsageClassifier` and `QuestionGenerator`**
   - Centralization itself is good and reduces drift.
   - **Q1** now correctly keys crosstabs on `16` instead of the old wrong slot.
   - **Q8/Q9** now correctly use action-query codes `{32, 48, 64, 80}` via `IsActionQuery`, which preserves the intended semantics.
   - **Q10** appears preserved: union/set-operation queries at `128` still fall through the select-like path.
   - **Q11 is incomplete**: the new DAO table acknowledges `144 = dbQSPTBulk`, but `UsageClassifier` only treats `{96, 112}` as `StandaloneUtility`. Unless `dbQSPTBulk` is intentionally unsupported, that is an unintended semantic gap. A bulk pass-through query would not take the pass-through/utility branch.
   - The updated strategy doc reinforces this concern: it lists `144 = Pass-through (bulk)` in the enum table, but the Q11 rule still excludes it. That inconsistency suggests the omission was accidental, not deliberate.

3. **F-3-7: deriving `Sql.Parsed.QueryType` from `abstraction.Shape` with DAO fallback**
   - This is the right fix for part (a). The parser-derived layer should come from the parsed AST, not be overwritten by DAO decode.
   - The explicit mappings shown are sensible:
     - `Select -> Select`
     - `Union -> Union`
     - `Transform -> Crosstab`
     - `Insert -> Insert`
     - `Update -> Update`
     - `Delete -> Delete`
   - The fallback behavior is also correct: if there is no abstraction, or the shape maps to `null`, retain the DAO-derived type.
   - I do **not** see an obviously wrong mapped shape in the diff itself.
   - Deferring part (b), i.e. projecting the embedded SELECT for `TRANSFORM`, is reasonable and appropriately recorded as separate deferred work.

4. **F-3-5: `PassingMechanism` decision**
   - I think this is the **right contract-fidelity choice**.
   - Emitting the schema-declared `PassingMechanism` field is better than extending the old undeclared boolean shape with another ad hoc boolean.
   - Retiring the old bare `ByRef` boolean is justified if the authoritative contract is the schema and a sibling emitter already uses `PassingMechanism`.
   - Omitting the field for unspecified/default pass mode is acceptable **if** the repository convention is truly “omission = default”. That is consistent with the stated convention and avoids inventing data where the source was not explicit.
   - The only caveat is that the schema enum includes `ByRefDefault`; if cross-surface consistency requires emitting that literal, this change would diverge. I do not see evidence in the provided diff that this inconsistency exists, so I would not fail the session on that basis.
   - Deferring **DW-3** is reasonable: it is separate scope, zero-corpus, and not necessary to resolve F-3-5.

5. **Tests**
   - The new tests are helpful:
     - crosstab classification now pinned at `16`
     - action/delete and union codes pinned to corrected DAO values
     - AST-derived `Sql.Parsed.QueryType` for `TRANSFORM` is pinned
     - explicit `ByVal` / `ByRef` emission is pinned
   - However, they are **not fully non-weakening** because they **cement the remaining 144 problem**:
     - `QueryExporterDecodeTests` asserts `144 -> "SqlPassthrough"`, which is not the faithful DAO decode.
     - There is no classification test ensuring `dbQSPTBulk` gets Q11/`StandaloneUtility` treatment.
   - So the test suite would currently protect an incomplete fix.

My bottom line: the session is close, but I would **not** mark it fully verified because `dbQSPTBulk (144)` is still mishandled in both the decode surface and the classifier/rule layer.

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Core/Extraction/QueryExporter.cs",
      "summary": "DAO code 144 is still decoded as \"SqlPassthrough\" instead of the distinct QueryDefTypeEnum member dbQSPTBulk, so the decode table is not fully faithful to Microsoft's enum.",
      "fix": "Return a distinct label for 144 (for example \"SptBulk\" or \"SPTBulk\"), update QuerySqlProjector.QueryTypeMap to map that label to the schema's pass-through type, and correct QueryExporterDecodeTests accordingly."
    },
    {
      "severity": "Major",
      "location": "AccessHarvester.Pipeline/UsageIdentification/UsageClassifier.cs",
      "summary": "Q11 handling excludes dbQSPTBulk (144), so bulk pass-through queries will bypass the intended StandaloneUtility classification despite the corrected DAO enum table documenting 144 as pass-through bulk.",
      "fix": "Include DaoQueryTypeCodes.SptBulk anywhere pass-through utility queries are handled (UsageClassifier and the strategy doc, and any related question/rule documentation), and add a classifier test pinning QueryTypeCode 144 to StandaloneUtility."
    }
  ]
}
```