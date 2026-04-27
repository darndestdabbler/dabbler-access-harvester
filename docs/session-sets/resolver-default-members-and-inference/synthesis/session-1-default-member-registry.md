# Session 1 Synthesis — Default-Member Registry

> **Authored:** 2026-04-27 in-conversation by Claude Opus 4.7
> (orchestrator).
> **Routing decision:** Per `project-guidance.md` Convention "When the
> orchestrator's model matches the routing target...", Opus 4.7 authored
> this synthesis directly rather than calling
> `route(task_type="architecture")`, whose target is also Opus. The
> non-negotiable cross-provider gate (`session-verification`) still runs
> against gpt-5-4 at end of session.
> **Scope:** Phase 3.5 of `docs/VBA-Parser-Migration-Harvester-Plan.md`
> — *registry data model + curated seed coverage + property-triplet
> pairing*. Type inference (Phase 3.6, Session 2) and the resolver
> wiring that consults the registry to retire
> `Unresolved("default-member-not-yet-resolved")` (Session 4 cross-cut)
> are explicitly out of scope here. This session lands the data the
> later sessions will consume.

## 1. Goal

Two related deliverables:

1. **A curated registry of types and their default members.** When the
   reference resolver sees a member-less reference on an expression of
   a known typed value (e.g., `txt = rs("Field")`), the registry tells
   it what implicit member is being invoked. Phase 3.5 calls this out
   as the resolver's pivot from "best-effort scope walking" to
   "type-aware member resolution."
2. **Property-triplet pairing for in-source declarations.** VBA splits
   a single logical property into up to three procedure declarations
   (`Property Get`, `Property Let`, `Property Set`). The parser-AST and
   `Declaration` model intentionally leave them as independent
   procedures (per `DeclarationModelTests.PropertyDeclaration_…`'s
   "Pairing is deferred to #9" comment). This session introduces the
   `PropertyGroup` concept so downstream consumers can pick the right
   reader/writer without re-pairing on every query.

## 2. Inputs and outputs

### 2.1 Curated registry

```
Inputs   (no runtime state — pure static initialization)
Output   IReadOnlyDictionary<string, DefaultMember>
         DefaultMember? TryGet(string typeName)
         DefaultMember? TryGet(IReadOnlyList<string> qualifiedName)
```

The registry is **data, not code branching** per the spec's Standards.
Adding a new type means adding a row, not editing the resolver.

### 2.2 Property-triplet pairing

```
Input   Scope scope                  // any Scope; ModuleScope is the
                                     // typical caller (Phase 3.5 §)
Output  IReadOnlyList<PropertyGroup> // one entry per distinct
                                     // case-insensitive property name
                                     // declared in `scope.Declarations`
```

The builder reads `scope.Declarations`, filters to
`PropertyDeclaration`, groups by `Declaration.NameComparer` (ordinal
case-insensitive), and emits a `PropertyGroup` per name with whichever
of `Get` / `Let` / `Set` were declared in source. Procedures of a
non-property kind are ignored — they are unrelated declarations even
when their name collides with a property.

## 3. Data model

### 3.1 `DefaultMember`

```csharp
public sealed record DefaultMember(
    string TypeName,         // canonical type spelling — last segment
                             // of TypeReference.QualifiedName when the
                             // source qualifies (e.g., "Recordset" for
                             // ["DAO","Recordset"]).
    string MemberName,       // ".Value", ".Fields", ".Item", ".Form",
                             // ".Report".
    DefaultMemberKind Kind,  // Property | Method
    string? ReturnType,      // qualified-name leaf of the return type.
                             // Two distinct semantics, do not conflate:
                             //   null      = unknown / polymorphic.
                             //               Session 2 stops chaining.
                             //   "Variant" = explicitly Variant return.
                             //               Still a known type.
    bool IsParameterized);   // true when the canonical call form takes
                             // an index argument (rs("X"), col("k")).
                             // false for `.Value`, `.Form`, `.Report`.
```

The shape mirrors `BuiltInFunction` in `References/`: a small immutable
record with the minimum the resolver needs at lookup time. No
back-pointer to a synthetic Declaration — that synthesis happens at
resolver-wiring time (Session 4) so the registry stays addable-by-row.

### 3.2 `DefaultMemberKind`

```csharp
public enum DefaultMemberKind { Property, Method }
```

`Property` is the common case for every seeded entry — `.Value` on
controls and Field; `.Fields` on Recordset; `.Item` on Collection /
Dictionary / Controls. The seeded set has no rows of kind `Method`;
the enum value stays in the public contract so a future seed (or a
consumer-extension layer) can declare a side-effecting method call as
its type's default if the COM dispatch warrants it.

### 3.3 `PropertyGroup`

```csharp
public sealed class PropertyGroup
{
    public string Name { get; }                      // case-preserved
    public IDeclarationScope Scope { get; }          // pairing's owner
    public PropertyDeclaration? Get { get; }
    public PropertyDeclaration? Let { get; }
    public PropertyDeclaration? Set { get; }

    public bool IsReadable => Get is not null;
    public bool IsWritableValue => Let is not null;
    public bool IsWritableObject => Set is not null;
}
```

A class (not a record) because each group is a unique entity in the
resolver graph and equality on PropertyGroup is reference equality —
two groups with the same name in different scopes must not coalesce.

`Get`, `Let`, and `Set` are mutually optional. VBA permits a property
with only `Let` (write-only value) or only `Set` (write-only object)
or only `Get` (read-only). Same name with both `Let` AND `Set` is
invalid VBA, but the parser may emit both; the group records both and
leaves diagnostic emission to a later layer rather than dropping data.

## 4. Curated seed coverage

The seed list covers exactly the types the harvester's canonical
fixtures (`02`, `03`, `09`, `11` per the spec's test-case anchors) plus
Phase 3.5's enumerated minimum: Access controls, DAO Recordset and
Field, ADO Recordset and Field, VBA Collection, and the Scripting
Dictionary. Conservative-add bias: the cost of a missing entry is one
spurious `Unresolved("default-member-not-yet-resolved")` that future
sessions can fix by adding a row; the cost of an over-eager entry is a
silent miscoercion that the test fixtures will not catch because they
exercise canonical types only.

| Type | Default member | Kind | ReturnType | IsParameterized | Source / Why |
|---|---|---|---|---|---|
| `TextBox`     | `Value`  | Property | `Variant` | false | Access control. Bare `Me!txt` reads control's stored value. |
| `ComboBox`    | `Value`  | Property | `Variant` | false | Same. |
| `ListBox`     | `Value`  | Property | `Variant` | false | Same. |
| `Label`       | `Caption`| Property | `String`  | false | Display-only control; `.Caption` is the read default. |
| `CheckBox`    | `Value`  | Property | `Variant` | false | Tri-state Variant (True / False / Null). |
| `OptionButton`| `Value`  | Property | `Variant` | false | Same. |
| `ToggleButton`| `Value`  | Property | `Variant` | false | Same. |
| `SubForm`     | `Form`   | Property | `Form`    | false | Subform control's `.Form` is the contained form. Phase 3.5 calls this out by name. |
| `SubReport`   | `Report` | Property | `Report`  | false | Subreport analogue. |
| `Recordset`   | `Fields` | Property | `Field`   | true  | DAO / ADO recordset. `rs("X")` ≡ `rs.Fields("X")`. |
| `Field`       | `Value`  | Property | `Variant` | false | Chained hit: `rs("X")` → `rs.Fields("X")` → `Field` → `.Value`. |
| `Recordset2`  | `Fields` | Property | `Field`   | true  | DAO `Recordset2`. Same shape as `Recordset`. |
| `Collection`  | `Item`   | Property | `Variant` | true  | VBA `Collection`. `coll(i)` ≡ `coll.Item(i)`. Modeled as Property because COM type info exposes `Item` as PROPERTYGET (DISPID_VALUE), even though VBA help calls it a method. |
| `Dictionary`  | `Item`   | Property | `Variant` | true  | Scripting.Dictionary. `dict(k)` ≡ `dict.Item(k)`. Genuine Property Get/Let surface. |
| `Form`        | `Controls`| Property| `Control` | true  | `Forms!frmFoo("ctlBar")` reads through `Form.Controls` as default. |
| `Report`      | `Controls`| Property| `Control` | true  | Same for reports. |
| `Controls`    | `Item`   | Property | `Control` | true  | The Controls collection default-members `Item`. |

**Intentionally NOT seeded:** `Control -> Value`. Access.Control is a
heterogeneous base — Label, CommandButton, Tab, etc. do not share a
uniform `.Value` default. Session 4's wiring narrows to a concrete
control subtype using form/report metadata before applying a second
default-member hop. Seeding `Control -> Value` would silently
misresolve every label/button reference.

The `Form`/`Report`/`Controls` rows let an `Forms!frmFoo!ctlBar` chain
collapse mechanically: `Forms` → `frmFoo` (an `IAccessObjectModelRegistry`
hit) → `Form` (synthetic) → default `Controls` → default `Item("ctlBar")`
→ `Control` → default `Value`. Session 4's wiring exercise will validate
this collapses end-to-end on fixtures `02` and `03`.

## 5. API surface

### 5.1 `DefaultMemberRegistry`

```csharp
public static class DefaultMemberRegistry
{
    public static IReadOnlyDictionary<string, DefaultMember> All { get; }

    // Lookup by leaf type name. Case-insensitive (matches
    // Declaration.NameComparer = OrdinalIgnoreCase).
    public static DefaultMember? TryGet(string typeName);

    // Convenience: look up by a TypeReference's QualifiedName. Uses
    // the LAST segment for matching — DAO.Recordset and Recordset
    // resolve to the same row. The qualifier is intentionally not
    // checked: Access projects are inconsistent about whether they
    // qualify (`Dim r As DAO.Recordset` vs. `Dim r As Recordset`),
    // and the registry has no name-collision risk in the seeded set.
    public static DefaultMember? TryGet(IReadOnlyList<string> qualifiedName);
}
```

The leaf-only match is a deliberate trade. If a future seed introduces
two types with the same leaf (e.g., `ADODB.Recordset` and a
hypothetical `MyLib.Recordset`), the resolver will pick whichever was
registered first and the test fixtures catch the conflict during
Session 4 wiring. Until then, leaf-only is simpler than encoding the
full QN.

### 5.2 `PropertyGroupBuilder`

```csharp
public static class PropertyGroupBuilder
{
    // Pair Property Get/Let/Set declarations within `scope` into one
    // PropertyGroup per case-insensitively-distinct name. Walks
    // `scope.Declarations` only — does not recurse into child scopes.
    // Callers decide whether to invoke per ModuleScope (the typical
    // case) or per smaller scope.
    public static IReadOnlyList<PropertyGroup> BuildForScope(
        Scope scope);
}
```

Output ordering: groups are emitted in the order each name was first
seen in `scope.Declarations`. This matches the source-order convention
the rest of the resolver uses and keeps tests deterministic.

## 6. Resolver integration: explicitly deferred

This session does **not** wire the registry into `ReferenceResolver`.
The existing `DecideMemberOutcome` continues to emit
`Unresolved("default-member-not-yet-resolved")` on the cases the
registry would resolve, exactly as it does today.

Wiring is Session 4 (cross-cut + mechanical tightening) work. The
reasons to split:

- Session 1's surface is the data — tested independently of the
  resolver walker.
- Session 2 (type inference) lands the type-tracking that the wiring
  needs to ask "what is the static type of this expression target?".
- Wiring without inference would only resolve the trivial cases
  (declared-type variables on the immediate target) and miss the
  chained `rs("X")` form, which is the spec's motivating example.

The registry is a **dependency** Session 4 picks up; this session
exposes the API and the property-triplet pairing the resolver-side
work depends on, and stops there.

## 7. Out of scope for this session

- **Resolver `DecideMemberOutcome` integration.** Session 4.
- **Type inference.** Session 2. The registry's `ReturnType` field is
  populated so Session 2 can chain through it (`Set rs =
  CurrentDb.OpenRecordset(...)` → infer `rs : Recordset` because the
  registered return type of `OpenRecordset` is `Recordset`), but the
  inference algorithm itself is Session 2's deliverable.
- **Cross-reference index.** Session 3.
- **Override / extend protocol for the registry.** A consumer-supplied
  registry contract (analogous to `IAccessObjectModelRegistry`) is not
  introduced here. The spec's seed list is small and stable enough that
  a static class is sufficient. If a future host project needs to
  augment the seed (custom controls, third-party COM types), introduce
  an interface then; doing so now would be premature abstraction per
  `project-guidance.md` → "Lean By Default".
- **Implicit-default-member emission.** Phase 3.5 calls for the
  resolved AST to carry an annotation that the default-member access
  was inserted. The annotation shape is decided in Session 4 along
  with the wiring; this session does not pre-commit a node type.

## 8. Test plan

### 8.1 Registry tests
(`tests/AccessHarvester.Vba.Resolver.Tests/DefaultMembers/DefaultMemberRegistryTests.cs`)

- `TryGet_ReturnsHit_ForCanonicalSeedTypes` — every type in the §4
  table is present.
- `TryGet_IsCaseInsensitive` — `recordset` / `RECORDSET` / `Recordset`
  hit the same row.
- `TryGet_QualifiedName_MatchesByLeafSegment` —
  `["DAO","Recordset"]` resolves identically to `Recordset`.
- `TryGet_QualifiedName_EmptyList_ReturnsNull` — defensive.
- `TryGet_Unknown_ReturnsNull` — coverage gap is silent at this layer
  (the existing `Unresolved` provenance handles downstream).
- `Recordset_DefaultMember_IsParameterizedFields` — pinpoint test on
  the canonical Phase 3.5 example.
- `Field_DefaultMember_IsValue` — chain target.
- `CollectionLikeTypes_DefaultMember_IsItem_Property` — theory over
  `Collection` and `Dictionary`; both modeled as Property per their
  COM PROPERTYGET dispatch.
- `TryGet_BareControl_NotInRegistry` — regression for the deliberate
  no-`Control`-row decision.
- `TryGet_NullTypeName_Throws` / `TryGet_NullQualifiedName_Throws` —
  defensive-contract pin.
- `All_BackingMap_IsImmutable` — regression for the
  `ReadOnlyDictionary` wrapping.
- `All_EnumeratesEverySeed` — count lower-bound (≥ 15 entries).
- A theory parameterised on every type in §4 asserting both `TryGet`
  forms agree.

### 8.2 PropertyGroup tests
(`tests/AccessHarvester.Vba.Resolver.Tests/DefaultMembers/PropertyGroupBuilderTests.cs`)

- `BuildForScope_PairsGetAndLet` — `Property Get Foo` + `Property Let Foo`
  produce one group with both populated.
- `BuildForScope_PairsGetAndSet` — `Property Get Bar` + `Property Set Bar`
  produce one group; the `Bar` reader is the Get, the writer is the Set.
- `BuildForScope_GroupsCaseInsensitively` —
  `Property Get value` + `Property Let VALUE` end up in the same group.
- `BuildForScope_SoloGetIsOwnGroup` — read-only property still emits a
  group with `Let` and `Set` null.
- `BuildForScope_SoloLetIsOwnGroup` — write-only-value.
- `BuildForScope_SoloSetIsOwnGroup` — write-only-object.
- `BuildForScope_LetAndSetCoexist_StillOneGroup` — invalid-VBA shape is
  preserved verbatim; pairing does not drop data.
- `BuildForScope_IgnoresNonProperty` — `Sub Foo` and `Property Get Foo`
  produce one group containing the Get only; the Sub is not in any
  group.
- `BuildForScope_EmptyScope_ReturnsEmpty` — defensive.
- `BuildForScope_PreservesSourceOrder` — first-seen-name dictates emit
  order.

## 9. Stable-contract notes

The §3 record/class shapes form a public contract. Future sessions may
*extend* (add fields, add seed rows) but should not *rename* or
*reshape* without coordinating with consumers — Session 2's type
inference reads `DefaultMember.ReturnType`, Session 4's resolver
wiring reads every field. Conservative-add bias same as the seeded
type list: a missing row produces a recoverable `Unresolved`; a
reshaped record produces a compile-time fan-out across the resolver.

The `IsParameterized` flag is the one piece downstream callers should
not infer from `MemberName`. `Item` looks parameterized (and is, on
`Collection`) but on a hypothetical future `Dictionary.Item`
overloadable form it could be both. Read the flag, don't pattern-match
the member name.
