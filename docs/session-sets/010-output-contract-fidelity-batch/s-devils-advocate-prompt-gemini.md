# Devil's Advocate Review (Gemini Pro) - Set 010

## Pass 1: Per-item Contrarian Hypotheses

**1. F-2-1 / F-3-7 (decode + AST QueryType)**
*Hypothesis:* `QuerySqlProjector`'s AST-derived `Sql.Parsed.QueryType` disagrees with the DAO `QueryTypeCode` for some shape.
**PROVEN TRUE.** `QuerySqlProjector.ShapeToSchemaType` maps `QueryShape.Select` to `"Select"`. In Access SQL, a Make-Table query usually uses `SELECT ... INTO ... FROM ...`, which the AST parses as a `Select` shape. A consumer reading `QueryTypeCode` `80` receives `"MakeTable"` in the top-level property, but the parsed AST (`Sql.Parsed.QueryType`) will confidently assert it is `"Select"`. This creates exactly the contradicting signals the hypothesis anticipated.

**2. F-3-5 (PassingMechanism)**
*Hypothesis:* Omitting `PassingMechanism` for `Unspecified` hides explicitly authored `ByRef` modifiers because some path that *did* carry an explicit mode now omits it.
**PROVEN FALSE.** `ParserOutputProjector.BuildParameter` does *not* omit explicitly declared modes. It emits `"PassingMechanism": "ByVal"` for `ParameterPassMode.ByVal` and `"PassingMechanism": "ByRef"` for `ParameterPassMode.ByRef`. It only omits the output for `Unspecified`. Therefore, the 5 explicitly named `ByRef` instances in the regenerated manifest successfully survived. Steelman fails; the code is correct.

**3. F-2-4 (Q6)**
*Hypothesis:* The OutOfBand-suppression added hides a legitimately-needed question.
**PROVEN TRUE.** `QuestionGenerator.cs` suppresses the generic `OutOfBandEntryPossible` pile-on when `referencesOnlyQueries` is true. However, if a top-level query is entirely orphaned (no harvested opener) but meets the criteria of only referencing other queries (Q6), the user will solely be asked: *"Query 'X' references only other queries — is it an intermediate computation step... or can it be merged into its callers?"* The generic question *"is it genuinely unused?"* is discarded. Worse, the assertion update in `LauncherSurfaceTests` confirms that such an orphaned query's classification shifts from `PossiblyUnused` to `Unknown`, completely losing its garbage-collection value.

**4. F-2-5 (inline-SQL table credit)**
*Hypothesis:* Crediting all catalog FROM/JOIN names into `UsedAsRecordSource` over-credits a merely mentioned table.
**PROVEN TRUE.** `SignalIndex.ExtractNamedTargetsFromSql` yields every table referenced after a `FROM` or `JOIN`, regardless of context (e.g., inside a subquery or secondary join). When applied against inline-SQL RecordSources, a simple lookup table (e.g., `tblServiceRateLimits` in an `IN (...)` clause) is incorrectly credited as a primary `UsedAsRecordSource`. This immediately bumps lookup tables from `ReferenceData` or `StandaloneUtility` into `MainDataEntry`, overcrediting their role. 

**5. F-2-7 (numeric/bit validation)**
*Hypothesis:* The regex `^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?$` rejects legitimate numeric values.
**PROVEN TRUE.** The regex `\d+` before the decimal unconditionally requires at least one leading digit. Values such as `.5`, which are legal strings Access might emit for floats, are rejected. By the strictness of the regex, legitimate locale or abbreviated number strings degrade directly to `NULL` strings.

**6. F-1-4 / F-1-7 (missing spec / system name)**
*Hypothesis:* `SystemObjectNames` over-skips user objects named `MSys` or `~`.
**PROVEN TRUE.** The prior exporter in `QueryExporter.cs` explicitly used `if (name.StartsWith("~")) continue;` without skipping `MSys` queries. By abstracting this logic to `SystemObjectNames.IsSystemObjectName`, which matches both `MSys` and `~`, the harvester will now permanently skip queries starting with `MSys`, which it previously kept and parsed. 

**7. Encoding fix (latent UTF-8 mojibake)**
*Hypothesis:* `SaveAsTextDecoder` fallback mis-decodes CP1252 files whose bytes form valid UTF-8.
**PROVEN TRUE (Theoretically).** A CP1252 file containing purely valid UTF-8 sequences (such as exactly `Ã©` resulting in C3 A9) does not throw a `DecoderFallbackException` when decoded via `StrictUtf8`. The heuristic will consume the ANSI text as UTF-8, shifting `Ã©` to `é`. While statistically rare to align perfectly over long strings, short strings with specific accents will suffer silent data corruption instead of falling back to CP1252.

**8. Golden regen integrity**
*Hypothesis:* Golden regen contains unexplained deltas.
**PROVEN FALSE.** `git diff` confirms all golden files underneath `Forms/`, `Reports/`, `Tables/`, and `migration/` remained entirely untouched. The observed metadata adjustments (crosstab values, `PassingMechanism`) cleanly trace back to the changes above, and the UTF-8 BOM drop is verified.

**9. Decision-consensus soundness**
*Hypothesis:* Defending the "defer" side of F-2-4 and F-2-5.
- **F-2-4 (Defer):** Activating Q6 alters the semantic meaning of orphan intermediate queries, breaking their previously stable `PossiblyUnused` classification and reducing confidence to `Unknown`. Fixing a minor UX string isn't worth regressing graph-level garbage collection detection.
- **F-2-5 (Defer):** Blindly extracting tokens via string-splits to set `MainDataEntry` status sacrifices architectural precision. It trades false-negatives (missing a source) for widespread false-positives (tainting lookup tables) without true AST context.

## Pass 2: Completeness-critic pass

- **Test Weakening:** `LauncherSurfaceTests` explicitly dropped its `PossiblyUnused` assertion for `qryOuter`, substituting `Unknown`. This accommodation of the behavioral change masked the aforementioned regression in tracking unreferenced Q6 queries.
- **Unrun Modality:** The COM extraction path was technically omitted from testing for MakeTable outputs. Furthermore, no actual validation proved `Sql.Parsed.QueryType` correctly survives a MakeTable query with the real Access SQL syntax `SELECT INTO`. 
- **Cost of Manual Regen:** The golden was regenerated manually by the tools, keeping the dates intact to evade simple diff noise. But this means that integration behavior over real MS Access COM execution—where `.accdb` files might spit out unusual BOMs under different O/S environments—was not subjected to full continuous integration checks, merely unit logic tests.

## Verdict

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    "F-2-1: `QuerySqlProjector` AST Shape for MakeTable (`SELECT INTO`) will likely derive 'Select', contradicting the DAO type.",
    "F-2-4: Q6 rule incorrectly bumps unreferenced intermediate queries from `PossiblyUnused` to `Unknown` and suppresses the 'genuinely unused' question.",
    "F-2-5: Inline-SQL token extraction over-credits lookup tables in subqueries/secondary joins, forcing them to `MainDataEntry`.",
    "F-2-7: `NumericToken` regex unconditionally demands leading digits, improperly rejecting valid numbers like `.5`.",
    "F-1-7: `SystemObjectNames.IsSystemObjectName` unifies skipping logic, quietly resulting in `QueryExporter` ignoring `MSys` prefix queries it used to extract."
  ]
}
```