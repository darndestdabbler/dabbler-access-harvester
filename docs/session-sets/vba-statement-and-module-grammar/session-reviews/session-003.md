# Verification Round 1

1. **CLI fix** — **Yes.**  
   `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` at process startup is the correct fix for `.NET Core/.NET` throwing `No data is available for encoding 1252`, assuming it runs **before** any Windows-1252 file read and `System.Text.Encoding.CodePages` is referenced.

2. **Preprocessor rule** — **Partially.**  
   For **module-level** directive lines, `preprocessorLine : (HASH_IF | HASH_ELSEIF | HASH_ELSE | HASH_END_IF | HASH_CONST) ~NEWLINE*` is fine and will not disrupt `moduleItem*`. A `#If ... #End If` block that wraps a **full procedure declaration** will parse as:

   - `HASH_IF ...`
   - `procedureDeclaration`
   - `HASH_ELSE ...`
   - `procedureDeclaration`
   - `HASH_END_IF ...`

   **Issue → Location → Fix**  
   **Major: incomplete conditional-compilation coverage** → `moduleItem` only → Also allow `preprocessorLine` in the procedure/body statement container (`blockStmt` / `statementList` / equivalent), otherwise valid VBA with `#If ... #End If` **inside a procedure body** can still fail.

   Note: this approach accepts the lines, but it does **not** preserve branch structure semantically; both branches are just parsed as ordinary items/statements.

3. **Schema-validation correctness** — **Mostly yes, with one caveat.**  
   The statement **“all must rows covered”** is consistent with the matrix shown and with the 10/10 `.bas`/`.cls` corpus parse result.  
   The statement **“one should gap fixed (preprocessor directives)”** is **not strictly auditable from the pasted matrix excerpt**, because no preprocessor row appears in the rows shown. If that row exists elsewhere, the claim is fine; if not, the matrix excerpt should include it.

4. **Harness classification** — **Yes.**  
   - `04-saved-query-form-params.sql` → expected failure; SQL is not a VBA module.  
   - `06-calculated-expressions.txt` → expected failure; Access expression-language content is not a VBA module.

**VERDICT: ISSUES_FOUND (0 Critical, 1 Major)**

---

# Verification Round 2

VERDICT: VERIFIED
