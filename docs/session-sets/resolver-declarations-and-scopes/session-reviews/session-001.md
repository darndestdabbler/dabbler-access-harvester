# Verification Round 1

## Verdict

`ISSUES_FOUND`

## Issues

1. **Major**  
   **Issue →** `Const` is modeled outside the spec’s `VariableDeclaration` kind. Feature 1 explicitly defines `VariableDeclaration` as covering `Dim / Public / Private / Static / Const`, but the implementation splits constants into a sibling `ConstantDeclaration`. That changes the declaration taxonomy later resolver stages will consume.  
   **Location →** `AccessHarvester.Vba.Resolver/Declarations/VariableDeclaration.cs`  
   **Fix →** Align the surface with the spec by either folding consts into `VariableDeclaration` or making `ConstantDeclaration : VariableDeclaration` so `Const` declarations are still consumable as `VariableDeclaration` while retaining constant-specific fields.

2. **Major**  
   **Issue →** `ImplicitDeclaration` does not carry an explicit type signature. The comments say implicit declarations are “always Variant-typed at this layer”, but the object model exposes no `Type`/signature property for that fact. Feature 1 says each declaration carries a type signature.  
   **Location →** `AccessHarvester.Vba.Resolver/Declarations/VariableDeclaration.cs` (`ImplicitDeclaration`)  
   **Fix →** Add an explicit type-signature surface for implicit declarations, e.g. a `TypeReference`/intrinsic-type property fixed to `Variant`, or move variable-like declarations behind a common typed base that exposes it uniformly.

3. **Minor**  
   **Issue →** Case-insensitive name comparison is only documented, not encoded in the model API. The model preserves source casing, but there is no shared comparer/key/helper to make the case-insensitive rule hard to violate in Session 2/#8.  
   **Location →** `AccessHarvester.Vba.Resolver/Declarations/Declaration.cs`  
   **Fix →** Add an explicit comparison contract such as a shared `StringComparer.OrdinalIgnoreCase`, a normalized lookup key, or helper APIs that scope/reference-resolution code must use.

4. **Minor**  
   **Issue →** `SyntheticDeclarationProvenance` is speculative for Session 1. The spec/session scope requires explicit and implicit declaration modeling; synthetic declarations are described as future possibilities only. This is extra surface area not yet justified by Phase 3.1.  
   **Location →** `AccessHarvester.Vba.Resolver/Declarations/DeclarationProvenance.cs`  
   **Fix →** Remove it until a later resolver phase first needs synthetic declarations, or make it internal and introduce it in that session.

---

# Verification Round 2

VERIFIED
