# Verification Round 1

- **Issue** → `Collection.Item` and `Dictionary.Item` are modeled as `Method`, but in VBA they are default properties. `Dictionary.Item` is especially incorrect because it is a Property Get/Let/Set surface, so the current kind will mislead downstream reader/writer selection.  
  **Location** → `AccessHarvester.Vba.Resolver/DefaultMembers/DefaultMemberRegistry.cs` (`AddMethod("Collection", ...)`, `AddMethod("Dictionary", ...)`); mirrored by `tests/AccessHarvester.Vba.Resolver.Tests/DefaultMembers/DefaultMemberRegistryTests.cs` (`Collection_DefaultMember_IsItem_Method`) and the synthesis doc seed table.  
  **Fix** → Change both rows to `AddProperty(...)`, update the tests/docs accordingly, and if later sessions need to distinguish writable vs read-only default properties, add explicit metadata for that instead of using `DefaultMemberKind.Method`.

- **Issue** → The `Control -> Value` seed is over-broad and violates the session’s stated conservative-add bias. `Access.Control` is a heterogeneous base type; labels, command buttons, tabs, etc. do not share a uniform `.Value` default member. This creates silent misresolution for references statically typed as `Control`.  
  **Location** → `AccessHarvester.Vba.Resolver/DefaultMembers/DefaultMemberRegistry.cs` (`AddProperty("Control", "Value", "Variant", isParameterized: false)`).  
  **Fix** → Remove the `Control` row. Keep `Controls.Item` returning `Control`, and let Session 4 apply a second default-member hop only after narrowing to a concrete control subtype from form/report metadata.

- **Issue** → `DefaultMemberRegistry.All` exposes the live backing `Dictionary`. A caller can cast the returned object back to `Dictionary<string, DefaultMember>` and mutate global registry state, which breaks the intended “curated static registry” contract.  
  **Location** → `AccessHarvester.Vba.Resolver/DefaultMembers/DefaultMemberRegistry.cs` (`private static readonly Dictionary<string, DefaultMember> _byTypeName = BuildRegistry();` and `public static IReadOnlyDictionary<string, DefaultMember> All => _byTypeName;`).  
  **Fix** → Store/expose an immutable or read-only map (`FrozenDictionary`, `ImmutableDictionary`, or `ReadOnlyDictionary`) and type the backing field/property to that read-only shape.

- **Issue** → The `ReturnType` contract is internally inconsistent. Comments say `null` means “polymorphic / Variant”, but the registry actually uses the literal string `"Variant"` for concrete Variant returns. Session 2 is supposed to consume this field, so this ambiguity is dangerous.  
  **Location** → `AccessHarvester.Vba.Resolver/DefaultMembers/DefaultMember.cs` (`ReturnType` comment); same semantics are repeated in the synthesis-derived comments.  
  **Fix** → Tighten the contract to: `null` = unknown/polymorphic, `"Variant"` = explicitly Variant. Update comments/docs/tests to match the implementation.

- **Issue** → Registry test coverage misses the null-argument contract on both `TryGet` overloads, and it currently locks in the incorrect `Collection` kind while never pinning `Dictionary` semantics.  
  **Location** → `tests/AccessHarvester.Vba.Resolver.Tests/DefaultMembers/DefaultMemberRegistryTests.cs`.  
  **Fix** → Add `TryGet_NullTypeName_Throws` and `TryGet_NullQualifiedName_Throws`; replace the `Collection_DefaultMember_IsItem_Method` assertion with property-kind assertions for both `Collection` and `Dictionary`; after removing the unsafe `Control` seed, add a regression test asserting `TryGet("Control")` is null.

---

# Verification Round 2

```json
{
  "verdict": "pass_with_minor_issues",
  "confidence": "high",
  "summary": "All five Round 1 findings are resolved in the implementation and tests: `Collection.Item` and `Dictionary.Item` are now modeled as `Property`; the unsafe `Control -> Value` seed is removed and pinned by regression test; `All` no longer exposes a mutable `Dictionary`; the `ReturnType` null-vs-`\"Variant\"` contract is clarified; and null-argument plus corrected registry semantics are now covered by tests. No functional/session-scope blockers remain. Two documentation/comment mismatches still need cleanup to fully align the artifacts with the updated synthesis/doc intent.",
  "issues": [
    {
      "issue": "Stale enum commentary still describes `coll(i)` / `Item` as a method call, which contradicts the corrected registry semantics and updated COM-property rationale.",
      "location": "AccessHarvester.Vba.Resolver/DefaultMembers/DefaultMemberKind.cs — comment above `DefaultMemberKind`.",
      "fix": "Update the comment to reflect that the currently seeded `Item` defaults are modeled as `Property`, or replace the example with a genuinely method-shaped default member."
    },
    {
      "issue": "Stale test commentary still says the synthesis doc enumerates 19 rows, but after removing the `Control` seed the registry now contains 18 seeded rows.",
      "location": "tests/AccessHarvester.Vba.Resolver.Tests/DefaultMembers/DefaultMemberRegistryTests.cs — comment in `All_EnumeratesEverySeed`.",
      "fix": "Change the comment to 18 rows or remove the hardcoded count reference."
    }
  ]
}
```
