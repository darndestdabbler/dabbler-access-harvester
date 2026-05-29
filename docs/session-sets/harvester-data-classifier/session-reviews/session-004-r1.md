This document presents the independent audit and verification of the deliverables from Session 4 of the `harvester-data-classifier` session set. The audit confirms that the implementation is robust, correct, and adheres to the project's key specifications and privacy invariants.

### 1. FK Consistency Invariant

The implementation correctly maintains the foreign key consistency invariant. The core logic chain, from `FkGraphBuilder` through `RemapRegistry`, ensures that a primary key and all foreign keys that reference it are remapped to the same new value in lockstep. The `RemapIdHandler` correctly uses the FK's target (`FkTarget`) as the lookup key into the `RemapRegistry`, guaranteeing that both the PK definition and the FK reference consult the same value map. This design is sound and is confirmed by the `FkConsistencyInvariantTests` suite. The use of a case-insensitive comparer in `TableColumn`, which is subsequently used in `FkGraphBuilder` and `RemapRegistry`, correctly handles Access's case-insensitive identifier semantics across FK boundaries. Self-referencing foreign keys, as tested in `FkConsistencyInvariantTests`, are handled correctly by this generic mechanism. Furthermore, the system gracefully handles missing FK targets; `FkGraphBuilder` detects them, defaults the corresponding FK to a `preserve` strategy to avoid crashing, and surfaces the missing targets in the final report, triggering the correct exit code (3) from `SanitizeCommand`.

### 2. Schema FK-authority allOf Rule

The sanitizer's logic correctly respects the schema's `allOf` rule for foreign key columns, which constrains overrides. The implementation is robust against schema-non-conforming documents. In `FkGraphBuilder` and `EffectiveCategoryResolver`, the masking strategy for any column marked with `isForeignKey: true` is not read from its `humanOverride` block but is instead mechanically derived from the remap decision made for its referenced primary key. This ensures the sanitizer always applies either `preserve` or `remap-id` to an FK, in lockstep with its target. A classification document that violates the schema rule (e.g., by specifying `maskingStrategy: "redact"` on an FK) will not cause a runtime crash; the invalid override will be silently ignored, and the correct, mechanically-derived strategy will be applied instead. This behavior makes the sanitizer resilient and enforces the core consistency invariant at a level deeper than just the schema validation.

### 3. Privacy Invariant

The privacy invariant, which dictates that raw sampled values must never be leaked to disk, is upheld. An audit of the `IMaskingHandler` implementations in `MaskingHandlers.cs` confirms that no handler accidentally echoes its input value, with the sole, correct exception of `PreserveHandler`. Every other handler (`MockReplaceHandler`, `GeneralizeHandler`, `RedactHandler`, etc.) performs a deterministic transformation of the input. Even the fallback logic in `GeneralizeHandler` transforms the input into a length-bucket label rather than returning the original `ToString()` representation. The risk of data leakage is therefore confined to configuration error (i.e., a human reviewer incorrectly classifying a sensitive column with a `preserve` strategy), which is outside the control of the sanitizer's implementation.

### 4. Determinism

Repeat runs will produce identical output. Determinism is ensured across all relevant components. `MockReplaceHandler` uses a deterministic FNV-1a hash of the table name, column name, and row index to seed a simple, deterministic Linear Congruential Generator in `FormatPatternMocker`, avoiding platform-dependent or randomized hash codes. `RemapRegistry` assigns new IDs using a simple counter based on the first-seen order of original values; because the `OleDbRowSource` reads rows in a stable order from a static database file, this process is deterministic. Other handlers like `GeneralizeHandler` rely only on column metadata and the input value itself. There are no identified sources of non-determinism, such as reliance on unordered dictionary iteration, that would affect the final CSV output.

### 5. CSV Correctness (RFC 4180)

The `CsvWriter.cs` implementation correctly adheres to RFC 4180. The quoting logic in the `Quote` method is sound: it correctly detects fields containing commas, quotes, or line breaks, wraps them in double quotes, and doubles any embedded quotes. Null values are correctly rendered as an empty field. All formatted values, particularly dates (`yyyy-MM-ddTHH:mm:ss`) and numbers, use `CultureInfo.InvariantCulture` to prevent localization issues, such as decimal commas, that could break the format. The logic correctly handles all `IFormattable` types, ensuring that any value whose string representation contains a special character will be properly quoted. There is no identified injection risk attributable to the writer; it correctly encodes data per the standard.

### 6. CLI Usability + Exit Codes

The `SanitizeCommand.cs` implementation provides a good user experience. The usage text clearly documents the required `--classification`, `--in`, and `--out` options. When a required argument is missing, the command prints a clear error message and the usage text, then exits with code 1, which is appropriate for a usage error. The exit code mapping is correctly implemented: 0 for full success, 1 for usage errors, 2 for unexpected exceptions, and 3 for partial success where missing FK targets were detected. This behavior is consistent with standard CLI tool practices and provides the necessary signals for scripting and automation.

### 7. SQL Identifier Escaping

The defense-in-depth strategy for SQL identifier escaping in `OleDbRowSource.cs` is effective. The `EscapeIdentifier` method prohibits characters (`]`, `\0`, `\r`, `\n`) that could be used to break out of the `[...]` quoting used in the generated `SELECT` statement. This prevents SQL injection via hostile table or column names supplied in a `classification.json` file. As the scanner already rejects such names, this serves as a valuable secondary check, ensuring the sanitizer is independently secure.

### 8. OLE DB / COM Resource Hygiene

Resource management in `OleDbRowSource.cs` is correct and follows best practices. The use of nested `using` statements for the `OleDbConnection`, `OleDbCommand`, and `OleDbDataReader` ensures that all OLE DB and underlying COM resources are properly released, even if an exception occurs during data enumeration. Because `ReadRows` is an iterator method, this pattern correctly handles the deferred execution and ensures disposal happens once the enumeration is complete. The code relies on the managed `System.Data.OleDb` provider to handle RCW lifecycle, which is the correct approach.

### 9. Test Coverage

The project's test coverage is comprehensive. The unit test suite provides excellent coverage for individual components, including all masking handlers, the effective category resolver, and the remap registry. Crucially, the `FkConsistencyInvariantTests` suite validates the primary correctness concern of the entire pipeline, including standard, self-referencing, and preserved FK scenarios. The integration test in `SanitizeCommandTests.cs` successfully proves the end-to-end functionality, from CLI argument parsing to using the `OleDbRowSource` against a live `.accdb` file and writing the final CSV and report files. While direct unit tests for `CsvWriter`'s helpers or a test specifically for the exit code 3 path would be minor additions, their absence does not constitute a significant gap in coverage.

### 10. Spec Deviations or Open Decisions

The implementation adheres to the specification and all documented decisions. The decision to output to CSV per the spec's stop rule was correctly implemented. The minor deviation of collapsing the `MaskingHandlers/*.cs` files into a single `MaskingHandlers.cs` file is a reasonable choice for code organization and does not affect functionality. All other created files represent a logical and sound decomposition of the problem, consistent with the spirit of the specification. There are no other material deviations or unaddressed open decisions.

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```