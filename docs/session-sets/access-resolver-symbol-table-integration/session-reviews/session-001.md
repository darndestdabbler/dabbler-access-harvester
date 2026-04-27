# Verification Round 1

{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Major",
      "location": "AccessHarvester.Vba.Resolver/References/ReferenceResolver.cs",
      "summary": "Bare control identifiers inside form/report modules still do not resolve because ResolveIdentifier only checks the scope chain, top-level Access names, and built-ins, but never consults the current form/report's control registry.",
      "fix": "In ResolveIdentifier, after the scope-chain walk and before built-ins, add a form/report-context lookup that calls TryResolveControlOnForm/TryResolveControlOnReport using _module.OwningClass.Name when _module.Kind is Form/Report; add an integration test for code like `txtFirstName = \"\"` inside a form module."
    },
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/AccessIntegration/ReferenceResolverAccessIntegrationTests.cs",
      "summary": "NullRegistry_PreservesPreviousFallthroughBehavior is a false-positive regression test for the interface change because it exercises an unresolved target path, not the changed SubChildLookup/TryResolveMemberOf null-registry path.",
      "fix": "Replace or supplement it with a test where the target resolves successfully but the null registry returns null from TryResolveMemberOf—for example a resolved local variable member access or `Me.Foo` in a form module—and assert `default-member-not-yet-resolved`."
    },
    {
      "severity": "Minor",
      "location": "AccessHarvester.Vba.Resolver/AccessIntegration/AccessObjectModelRegistry.cs, AccessHarvester.Vba.Resolver/AccessIntegration/SyntheticDeclarations.cs",
      "summary": "SyntheticTypeLibraryMemberDeclaration.OwnerTypeName does not match the documented contract because inherited members are recorded with the lookup type, not the declaring type in the base chain.",
      "fix": "Either extend ITypeLibraryRegistry with an API that returns the declaring type alongside MemberInfo and populate OwnerTypeName from that value, or rename/document the property as the lookup type so the implementation and contract agree."
    }
  ]
}

---

# Verification Round 2

```json
{
  "verdict": "ISSUES_FOUND",
  "issues": [
    {
      "severity": "Minor",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/AccessIntegration/ReferenceResolverAccessIntegrationTests.cs",
      "summary": "The new bare-control tests cover positive resolution, Step-1 local shadowing, and standard-module exclusion, but they do not exercise a control name collision with a top-level Access name or built-in, so the claimed Step-2-over-Step-3/4 shadowing order is still unpinned.",
      "fix": "Add a form/report-module test with a control whose name matches `Forms`, `Reports`, or a known built-in and assert the bare identifier resolves to the synthetic control declaration rather than the top-level or built-in candidate."
    }
  ]
}
```

---

# Verification Round 3

```json
{
  "verdict": "VERIFIED",
  "issues": []
}
```
