# Verification Round 1

- [Major] Access control coverage is too coarse to satisfy the spec’s control-resolution requirements
  Reason: The spec explicitly calls out `Control (base + common subtypes)` and default-member handling for constructs like `txtFirstName = "..."`. The implementation only registers `Access.Control`, does not register common subtypes such as `Access.TextBox` / `Access.ComboBox`, and sets `Access.Control.DefaultMemberName = null`. As a result, `TryGetType("Access.TextBox", ...)` fails and there is no default-member path for the common control-`Value` shorthand the contract says it must support.
  Suggested fix: Register the common Access control subtypes with their subtype-specific/default surfaces (`Value` default where applicable), or add an explicit base-type/alias resolution mechanism to the contract and tests so subtype lookups can deterministically fall back to `Access.Control`.

- [Major] The curated seed contains at least some incorrect member metadata
  Reason: `Access.Application.CurrentDb` is registered as a `Property`, but the official Access object model exposes it as a method; `DAO.Recordset` also includes `OpenRecordset`, which is not a `DAO.Recordset` member. These are correctness issues in the registry itself, and the tests currently reinforce at least one of them (`CurrentDb`).
  Suggested fix: Audit each `Build*()` entry against the cited object-model docs, correct the bad entries (`CurrentDb` kind, remove/fix `DAO.Recordset.OpenRecordset`, etc.), and add tests that pin those corrected facts.

- [Major] ADO default-member resolution is incomplete because `ADO.Fields` is referenced but not registered
  Reason: `ADO.Recordset` advertises `Fields` as its default member and gives it return type `ADO.Fields`, but `ADO.Fields` is not in the registry. That means the common ADO field-access chain dead-ends immediately for `rs("FieldName")` / `rs!FieldName`-style usage even though the registry suggests that path is supported.
  Suggested fix: Add an `ADO.Fields` entry with at least `Item` as the default member and basic collection members, plus tests covering the ADO default-member chain.

- [Major] The tests do not cover “every registered type’s surface” as required by the spec
  Reason: The spec asks for hermetic tests covering every registered type’s surface. The current suite verifies type presence, some representative members, and some invariants, but it explicitly says it is “not exhaustive.” Wrong/missing members, wrong return types, and wrong kinds on most entries can slip through unnoticed.
  Suggested fix: Add data-driven assertions for the full expected member set of each registered type, including member name, kind, return type, and default-member flag.

- [Major] `TryGet*` can throw on null inputs, which violates the “never throws” graceful-degradation contract
  Reason: All three lookup methods pass their string arguments directly into `Dictionary.TryGetValue`; a runtime `null` key causes `ArgumentNullException`. The interface uses non-nullable annotations, but that does not enforce safety at runtime, and the spec says the registry itself never throws.
  Suggested fix: Guard `typeName` / `memberName` with null-or-empty checks and return `false`/`null` instead; add unit tests for null and empty-string inputs.

- [Minor] The implementation does not meet the “source-of-truth comment per registered type” standard
  Reason: There are source comments per library section (DAO/ADO/Office/Access), but not per registered type entry as the spec requires. That makes future curation/audit harder for individual types.
  Suggested fix: Add a short source citation comment above each `Build*()` method, or add a source field/metadata block per `TypeInfo`.

- [Minor] The `Members()` helper silently overwrites duplicate member names
  Reason: The helper inserts into a case-insensitive dictionary with `dict[m.Name] = m;`. Any accidental duplicate name or property/event collision will replace the previous entry silently, masking curation mistakes instead of surfacing them.
  Suggested fix: Change construction to fail fast on duplicates (`Add`/`TryAdd` with an explicit exception) and add a test that duplicate member names are rejected.

VERDICT: ISSUES_FOUND

---

# Verification Round 2

- [Major] Inherited default members still report `IsDefault=false` through `TryGetMember`
  Reason: In `AccessHarvester.Access/TypeLibraryRegistry.cs`, `TryGetDefaultMember("Access.TextBox")` projects `Value` to `IsDefault=true`, but `TryGetMember("Access.TextBox", "Value")` returns the inherited `Access.Control.Value` record unchanged with `IsDefault=false`. That makes default-member metadata depend on which API was called, and a downstream consumer using `TryGetMember` cannot tell that `Value` is the default member of `Access.TextBox` / `Access.ComboBox` / `Access.ListBox`.
  Suggested fix: In `TryGetMember`, preserve the originally requested type and project `IsDefault=true` when the resolved member name matches that type’s effective default member. Add tests asserting `TryGetMember("Access.TextBox", "Value")`, `TryGetMember("Access.ComboBox", "Value")`, and `TryGetMember("Access.ListBox", "Value")` return `IsDefault=true`.

- [Major] `Access.Control` is still too broad, and the new inheritance model now gives non-data-bound controls invalid members
  Reason: `BuildAccessControl()` in `AccessHarvester.Access/TypeLibraryRegistry.cs` declares members it explicitly describes as “common across data-bound subtypes” (`ControlSource`, `Value`, `DefaultValue`, `Format`, `InputMask`, `ValidationRule`, `ValidationText`, `BeforeUpdate`, `AfterUpdate`, `Change`, `Updated`, etc.), but `Access.CommandButton` and `Access.Label` both inherit from that same base. As a result, lookups like `Access.Label.Value` or `Access.CommandButton.BeforeUpdate` now incorrectly succeed via inheritance.
  Suggested fix: Keep `Access.Control` to truly universal members only, and move data-bound/value-oriented members/events either onto `Access.TextBox` / `Access.ComboBox` / `Access.ListBox` directly or into a separate intermediate base used only by those subtypes. Add negative tests for members that must not resolve on `Access.Label` and `Access.CommandButton`.

- [Major] The round-1 test-coverage finding is still not fully fixed
  Reason: `tests/AccessHarvester.Tests/TypeLibraryRegistryTests.cs` adds direct-member count freezes and spot checks, but it still does not verify each curated member’s metadata (kind, return type, default flag) across every registered type surface, and it does not validate effective inherited surfaces. This would not catch regressions like the original `CurrentDb` kind bug, a wrong return type, or the new inherited-default / over-broad-base issues. The tests also rely on a duplicated hard-coded type list, so a newly registered type can be missed if that list is not updated.
  Suggested fix: Add data-driven per-member assertions for each registered type’s curated surface, including inherited effective members for control subtypes and representative “must not resolve” cases. Also add a test that compares the registry’s actual registered type set to the expected set so new types cannot bypass coverage.

- [Minor] The 32-hop inheritance guard masks bad registry curation instead of failing fast
  Reason: In `TryGetMember` and `TryGetDefaultMember`, a cycle or malformed base chain just devolves into a `false` “not found” result after 32 hops. That silently hides invalid registry data, unlike duplicate type/member registration which already fails fast during construction.
  Suggested fix: Validate `BaseTypeName` chains and effective `DefaultMemberName` resolution at construction time and throw `InvalidOperationException` for cycles, missing base types, or unresolved default-member references.

VERDICT: ISSUES_FOUND

---

# Verification Round 3

- [Minor] `TryGetMember` only projects `IsDefault=true`; it never clears an inherited `IsDefault=true` that is not the requested type's effective default
  Reason: In `AccessHarvester.Access/TypeLibraryRegistry.cs`, `TryGetMember` returns the underlying `MemberInfo` unchanged unless it needs to flip `false -> true`. That is correct for the current Access control graph, but it is not fully requested-type-relative in the general case: if a future subtype inherits from a base type whose direct member is marked `IsDefault=true` and the subtype declares a different default (or no default), `TryGetMember(subtype, inheritedBaseDefaultMember, ...)` would incorrectly leak `IsDefault=true`. That makes the implementation only partially consistent with the “projection is requested-type-relative” rule described in the round-3 response.
  Suggested fix: In `TryGetMember`, compute `var isEffectiveDefault = effectiveDefault is not null && string.Equals(m.Name, effectiveDefault, StringComparison.OrdinalIgnoreCase);` and return `m` with `IsDefault = isEffectiveDefault` whenever `m.IsDefault != isEffectiveDefault`, not only when projecting `false -> true`.

- [Minor] Public XML/comments still describe pre-refactor invariants that are now false
  Reason: Several comments no longer match the landed behavior: `AccessHarvester.Access/ITypeLibraryRegistry.cs` says `DefaultMemberName` “appears as a key in Members” and that base-type cycles “are not detected”; both are now false after inherited defaults and construction-time validation. `AccessHarvester.Access/TypeLibraryRegistry.cs` still says control subtypes are “NOT registered separately in v1,” and `BuildAccessTextBox()` says `Value` is inherited from `Access.Control` instead of `Access.DataBoundControl`. These are doc-level mismatches, but they are on public contract/types and can mislead future curators/consumers.
  Suggested fix: Update the XML/comments to reflect the current model: default members may be declared on a type while the member itself lives in a base type, constructor validation detects missing bases/cycles/unreachable defaults, control subtypes are explicitly registered, and `TextBox/ComboBox/ListBox` inherit `Value` via `Access.DataBoundControl`.

VERDICT: ISSUES_FOUND
