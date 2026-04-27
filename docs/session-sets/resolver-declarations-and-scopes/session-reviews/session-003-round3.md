## ISSUES_FOUND

- **Issue** → Non-property procedure kind is still not verified end-to-end; a `Function` misresolved as a `Sub` would still pass these tests.  
  **Location** → `Fixture11_ProducesTwoProcedureScopes`; `AllFixtures_EachProcedureScopeHasDeclarationInModule` non-property branches.  
  **Fix** → Assert `module.Declarations.OfType<FunctionDeclaration>()` contains exactly `Initialize` and `GetAppVersion`, and compare concrete declaration type for non-property procedures in the cross-fixture invariant instead of matching on name only.

- **Issue** → Fixture 09 still does not assert exact module declaration inventory; extra module-level declarations would be ignored.  
  **Location** → `Fixture09_AllProceduresHaveDeclarations`.  
  **Fix** → Assert `module.Declarations` contains exactly the 6 expected `ResolverProcedureDeclaration`s and no `VariableDeclaration` or other non-procedure declarations.

- **Issue** → Fixture 11 module-variable coverage is still count/access-only; six wrong public variables would pass.  
  **Location** → `Fixture11_SixPublicModuleVariables_AreRegistered`.  
  **Fix** → Assert the exact 6 expected variable names, and optionally assert there are no additional non-procedure declarations beyond that expected set.