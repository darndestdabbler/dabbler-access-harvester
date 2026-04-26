1. **Yes.**
   - **`localDeclHead` restriction**: correct and complete for the reported issue. Restricting procedure-body declaration heads to `DIM | STATIC` and splitting local `Const` into `localConstStatement` closes the prior over-acceptance of `Public`/`Private`/`Global` in local statement position.
   - **`typeSpec` / `variableTypeSpec` split**: correct and complete for the reported issue. Using `variableTypeSpec` only in variable-declaration positions and `typeSpec` everywhere else structurally rejects `As New` in parameters, returns, event params, `Const`, and other non-variable contexts.

2. **Yes.**
   - **2a. Do...Loop dual-condition**: sound as a documented permissive-parsing boundary. It is an intentional grammar over-acceptance with downstream semantic rejection, and your disclosure is explicit enough.
   - **2b. Option directives / arbitrary identifiers**: sound as a documented minimal-keyword boundary. Treating contextual words as `IDENTIFIER` and deferring value validation to semantics is consistent with the stated parser design.

3. **No remaining unaddressed grammar over-acceptance is evident from the fixes and disclosures you provided.**

4. **Yes.** The session set’s deliverable contract is met.

**VERDICT: VERIFIED**