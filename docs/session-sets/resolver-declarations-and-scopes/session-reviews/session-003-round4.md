# ISSUES_FOUND

- **Issue →** The bidirectional procedure-declaration check is too weak in the forward direction for non-property procedures. It matches against **all** module declarations by name, so a same-named non-procedure declaration could satisfy the assertion and hide a missing `ProcedureDeclaration`.  
  **Location →** `AllFixtures_EachProcedureScopeHasDeclarationInModule`, non-property branch of the “Forward” loop:
  ```csharp
  Assert.Contains(
      module.Declarations,
      d => Declaration.NameComparer.Equals(d.Name, procedure.Name));
  ```
  **Fix →** Restrict the search to procedure declarations:
  ```csharp
  Assert.Contains(
      module.Declarations.OfType<ResolverProcedureDeclaration>(),
      d => Declaration.NameComparer.Equals(d.Name, procedure.Name));
  ```

- **Issue →** The `TryFindLocal` test does not actually prove that **every declaration** is retrievable when multiple declarations share the same name. Fixture 10 has `Property Get`/`Let` pairs (`Enabled`, `EntityName`), and `module.TryFindLocal(decl.Name)` can return the same declaration instance for both iterations, so the assertion passes even if one accessor is not independently discoverable.  
  **Location →** `AllFixtures_TryFindLocal_FindsAllModuleLevelDeclarations`
  ```csharp
  foreach (var decl in module.Declarations)
  {
      var found = module.TryFindLocal(decl.Name);
      Assert.NotNull(found);
  }
  ```
  **Fix →** Either:
  - change the test to validate only **unique names** and update the comment to match the API’s behavior, or
  - use an API that can retrieve **all declarations for a name** / distinguish properties by `(name, kind)`.

  Minimal safe revision if `TryFindLocal` is intentionally name-only:
  ```csharp
  foreach (var name in module.Declarations
      .Select(d => d.Name)
      .Distinct(Declaration.NameComparer))
  {
      Assert.NotNull(module.TryFindLocal(name));
  }
  ```