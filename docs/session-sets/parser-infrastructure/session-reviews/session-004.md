1. Yes. **`preprocessorStatement`** is the right enum value. `directive` is too generic and would blur body-level conditional-compilation lines with other directive-like constructs. A distinct value is appropriate because:
   - it matches the AST type already introduced (`PreprocessorStatement`)
   - it preserves semantic intent for downstream consumers
   - it avoids overloading `directive` with two different meanings

2. Yes. The fixture statement kinds are correct:
   - `Dim startTime As Long` → **`declaration`**
   - `startTime = GetTickCount()` → **`assignment`**
   - `#If / #Else / #End If` lines → **`preprocessorStatement`**

3. Yes. The shape is appropriate for the current schema:
   - `kind: "preprocessorStatement"`
   - `details.preprocessorKind: "If" | "Else" | "EndIf"`
   - `details.body: <raw trailing text>`
   
   Given that `details` is intentionally open-ended, this is a good minimal representation. It is specific enough to be useful and simple enough not to overfit. The only requirement is consistency in the normalization of `preprocessorKind`.

4. Yes. **`unresolved`** is the most accurate enum value of the options you listed.
   - **not `ambiguous`**: there are not multiple equally valid resolved targets
   - **not `partiallyResolved`** unless your resolver actually attaches a concrete candidate symbol with caveats
   - **`unresolved`** correctly reflects that the analysis does not evaluate conditional compilation and therefore cannot guarantee a callable target

5. Yes. The Provenance refactor deferral is acceptable. It is a cleanup/refinement task, not a correctness fix, and would create broad call-site churn without changing the validated behavior delivered in this session.

VERIFIED