# Verification Round 1

- **Major** → `docs/test-cases/13-preprocessor.expected.json` → The golden file is not a faithful route output: `originalSource` is elided with `...` instead of preserving the real source text. That makes the fixture unlikely to match actual `route(test-generation)` output and weakens schema verification. → Regenerate this file from the route and commit the exact emitted JSON; keep `originalSource` verbatim if the schema includes it.

- **Major** → `docs/test-cases/13-preprocessor.expected.json` → `source.kind` is `"procedure"`, but `source.location` spans the entire `.bas` file (`startLine: 1` through `endLine: 34`) and the source contains module-level directives/declares. That is internally inconsistent and likely schema-invalid or at least golden-output-invalid. → Make `source.kind` match the full-file/module span, or narrow the source span/content to the procedure only based on the actual route output.

- **Minor** → `AstBuilderShapeTests.cs` (Session 3 section) → Coverage is good for range presence and preprocessor-node construction, but it does not directly prove walker/visitor dispatch or exact `#If/#Else/#End If` sequencing/body/range behavior. → Add targeted tests for `VisitPreprocessorStatement` dispatch and for the exact body/kind/range sequence produced inside a guarded procedure.

**ISSUES_FOUND**

---

# Verification Round 2

- **Major** Issue → Round 1 `FIXED-MAJ-2` is only partially resolved: `source.location` no longer spans the whole file, but it still does not match the now-verbatim `originalSource`. The shown `originalSource` contains 14 lines starting at line 21 and includes the closing `End Sub`, so a span ending at line 33 excludes the final line of the procedure.  
  **Location** → `docs/test-cases/13-preprocessor.expected.json` (`source.location` vs `source.originalSource`)  
  **Fix** → Extend `source.location` to the full procedure text represented by `originalSource`, including the `End Sub` line. With the shown text, `endLine` should be `34`; set `endColumn` to the actual end of `End Sub` per your column convention.

**ISSUES_FOUND**

---

# Verification Round 3

- **Resolved** → `docs/test-cases/13-preprocessor.expected.json` → `source.location.endLine` is now `34`, matching `End Sub` on line 34 and correctly spanning lines 21–34 inclusive.
- **No new issues found** → N/A → N/A

**VERIFIED**
