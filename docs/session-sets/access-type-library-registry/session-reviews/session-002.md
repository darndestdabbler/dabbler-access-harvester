# Verification Round 1

**ISSUES_FOUND**

1. **Issue:** Tests do not exhaustively verify each registered type’s full surface  
   **Severity:** Major  
   **Location:** `tests/AccessHarvester.Access.Tests/TypeLibraryRegistryTests.cs` — especially `TryGetMember_KnownMembers_ResolveWithExpectedKindAndReturnType`, `RegisteredTypeSet_MatchesCatalogExactly`, and `EveryRegisteredType_HasExpectedDirectMemberCount`  
   **Reason:** The spec calls for hermetic tests covering every registered type’s surface. The current suite proves every type exists, checks representative members, and freezes direct-member counts, but it does not assert the exact member set and per-member metadata (`Name`, `Kind`, `ReturnType`, `IsDefault`) for every registered member. A regression can slip through if counts stay the same and the sampled members still pass.  
   **Suggested fix:** Add an exhaustive expected catalog per type and assert exact equality against `TypeInfo.Members` for every registered type, including member names, kinds, return types, and default-member flags.

2. **Issue:** Several common object/collection members are missing return-type identifiers  
   **Severity:** Major  
   **Location:** `AccessHarvester.Access/TypeLibraryRegistry.cs` — examples include `BuildDaoDatabase` (`QueryDefs`, `Recordsets`, `TableDefs`), `BuildDaoQueryDef` (`Parameters`), `BuildAdoCommand` (`Parameters`), `BuildOfficeFileDialog` (`Filters`, `SelectedItems`), `BuildAccessApplication` (`Forms`, `Reports`, `Modules`)  
   **Reason:** The spec requires registered members to carry return-type identifiers. Multiple common object-valued members are left with `ReturnType = null` even where the return type is well-known. That reduces the registry’s usefulness for the future resolver’s chained, type-aware member resolution.  
   **Suggested fix:** Populate obvious qualified return-type strings for these members now. Where the returned type is not yet fully curated, still emit the identifier string and add tests for it; add minimal type stubs only where v1 chained lookup is expected to work.

3. **Issue:** Access-surface banner comment is stale and contradicts the implementation  
   **Severity:** Minor  
   **Location:** `AccessHarvester.Access/TypeLibraryRegistry.cs` — Access section comment above `BuildAccessForm()`  
   **Reason:** The comment says control subtypes “are NOT registered separately in v1; callers fall back to Access.Control,” but the shipped registry does register `Access.TextBox`, `Access.ComboBox`, `Access.ListBox`, `Access.CommandButton`, and `Access.Label`. This is misleading documentation.  
   **Suggested fix:** Update the comment to describe the actual model: `Access.Control` as the universal base, `Access.DataBoundControl` as the curated intermediate base, and the concrete registered control subtypes.

---

# Verification Round 2

**ISSUES_FOUND**

1. **Issue (Major): Public registry includes a fabricated non-type-library type**
   - **Location:** `TypeLibraryRegistry.cs` — `BuildAccessDataBoundControl()`, constructor `Register(types, BuildAccessDataBoundControl())`, and corresponding test/catalog entries in `TypeLibraryRegistryTests.cs`
   - **Fix:** Remove `Access.DataBoundControl` from the public registered type set. Keep the shared data-bound surface as a private implementation helper/template, or flatten those shared members into `Access.TextBox`, `Access.ComboBox`, and `Access.ListBox`. The public `ITypeLibraryRegistry` surface should only succeed for real external/library type names.

2. **Issue (Minor): Registry is documented as immutable but exposes mutable backing dictionaries**
   - **Location:** `TypeLibraryRegistry.cs` — `Members(...)` returns a mutable `Dictionary<string, MemberInfo>` as `IReadOnlyDictionary`; returned `TypeInfo.Members` instances expose that same backing object
   - **Fix:** Wrap member/type maps in `ReadOnlyDictionary` or switch to `ImmutableDictionary` before storing/exposing them. That makes the “immutable after construction” contract true even if a caller casts the returned collection.

---

# Verification Round 3

## Verdict

**ISSUES_FOUND**

1. **Issue:** Default-member metadata is still inconsistent on the public `TypeInfo.Members` surface for the three Access data-bound controls.  
   **Severity:** Major  
   **Location:** `AccessHarvester.Access/TypeLibraryRegistry.cs` — `DataBoundControlMembers()`, `BuildAccessTextBox()`, `BuildAccessComboBox()`, `BuildAccessListBox()`; also codified in `tests/AccessHarvester.Access.Tests/TypeLibraryRegistryTests.cs` under `ExpectedDirectMembers` and related comments.  
   **Reason:** `Access.TextBox`, `Access.ComboBox`, and `Access.ListBox` each declare `DefaultMemberName = "Value"`, but their direct `Members["Value"]` entries carry `IsDefault = false`. That contradicts both the spec’s “flag distinguishing default members from named ones” requirement and the `MemberInfo.IsDefault` contract docs (“true if this member is the type’s default member”). Lookup-time projection makes `TryGetMember`/`TryGetDefaultMember` behave correctly, but callers using `TryGetType(...).Members` still see incorrect default-member metadata for these registered types.  
   **Suggested fix:** Make the stored direct member metadata self-consistent. E.g. parameterize `DataBoundControlMembers()` so `Value` can be emitted with `isDefault: true` for `TextBox`/`ComboBox`/`ListBox`, or clone/patch the `Value` member per subtype before building `Members`. Then add/update a construction-time invariant and tests so any type whose `DefaultMemberName` names a direct member also has that direct member flagged `IsDefault = true`.

2. **Issue:** Stale comments still describe the removed fabricated `Access.DataBoundControl` type.  
   **Severity:** Minor  
   **Location:** `AccessHarvester.Access/TypeLibraryRegistry.cs` — Access section banner and nearby XML/comments; `AccessHarvester.Access/ITypeLibraryRegistry.cs` — `TypeInfo.DefaultMemberName` XML example.  
   **Reason:** The implementation no longer registers `Access.DataBoundControl`, but comments still say the registry has that intermediate type and that `TextBox`/`ComboBox`/`ListBox` inherit from it. This now contradicts the actual registry surface and the spec requirement that only real type-library identifiers be exposed publicly.  
   **Suggested fix:** Remove all remaining `Access.DataBoundControl` references from comments/docs and rewrite them to describe the current composition model: `TextBox`/`ComboBox`/`ListBox` inherit directly from `Access.Control` and compose the shared data-bound members via `DataBoundControlMembers()`.
