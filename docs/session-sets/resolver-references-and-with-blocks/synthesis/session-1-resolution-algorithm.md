# Session 1 Synthesis — Reference Resolution Algorithm

> **Authored:** 2026-04-27 in-conversation by Claude Opus 4.7 (orchestrator).
> **Routing decision:** Per `project-guidance.md` Convention "When the
> orchestrator's model matches the routing target...", Opus 4.7 authored
> this synthesis directly rather than calling `route(architecture)`,
> whose target is also Opus. The non-negotiable cross-provider gate
> (`session-verification`) still runs against gpt-5-4 at end of session.
> **Scope:** Reference resolution algorithm per Phase 3.3 of
> `docs/VBA-Parser-Migration-Harvester-Plan.md`. With-block flattening
> (Phase 3.4) is Session 2; ResolvedModule wrapper shape is Session 3;
> default-member resolution and cross-module visibility are
> downstream-set work (#9, Phase 3.5).

## 1. Goal

For each name use site in a parsed VBA module's AST, produce a
`ResolvedReference` annotation that pins the use to one of:

- a `Declaration` from #7's scope tree
- a curated VBA built-in function (Format, IIf, Nz, etc.)
- the form/report/class type indicated by `Me`
- an Access object-model member resolved through a future #11 registry
- an explicit `Unresolved("<reason>")` provenance per the
  project-guidance.md → VBA Parser Strategy: "Unresolvable constructs
  are explicit, not silent."

Side effect: when a use resolves to a `Declaration`, the resolver
calls `Declaration.AddReference` so #7's reverse-lookup is populated.

## 2. Inputs and outputs

```
Inputs
  ModuleScope moduleScope          // built by ScopeBuildingVisitor (#7)
  Module module                    // parser AST for the same module
  IAccessObjectModelRegistry?      // optional; #11 fills implementations

Output
  IReadOnlyList<ResolvedReference> // one entry per name-use site
```

A `ResolvedReference` carries:

- `Expression Expression` — the AST node that names something
- `SourceRange Location` — the use site's source location
- `Scope EnclosingScope` — the innermost scope that contained the use
- `ResolutionOutcome Outcome` — the discriminated-union result

## 3. ResolutionOutcome variants

| Variant | When |
|---|---|
| `Resolved(Declaration decl)` | Name matched a Declaration in the scope chain (an in-source VBA declaration; case-insensitive). |
| `ResolvedToObjectModel(Declaration decl)` | Name matched a synthetic Declaration returned by `_accessRegistry`. Distinct from `Resolved` so downstream consumers can tell registry-backed bindings apart from in-source ones without inspecting Provenance. |
| `ResolvedToBuiltIn(BuiltInFunction fn)` | Name matched a curated built-in function. |
| `ResolvedToMe(ClassDeclaration class)` | `Me` used in a class/form/report module; `class` is the module's `OwningClass`. |
| `Unresolved(string reason)` | Explicit unresolved provenance with a reason string per `project-guidance.md`. |

### 3.1 `Unresolved` reason strings (closed set for Session 1)

- `not-in-scope` — name didn't match any scope, registry, or built-in.
  Covers cross-module Public visibility (Phase 3.4 / #9 territory) and
  for/foreach loop variables that lack a Dim (implicit-declaration
  synthesis is Session 3+).
- `me-in-standard-module` — `Me` used inside a Standard module (no class
  context).
- `default-member-not-yet-resolved` — bang or dot member access whose
  target type would need Phase 3.5 default-member resolution.
- `with-block-flattening-deferred` — implicit-with member (`.Foo`,
  `!Foo` outside an explicit qualifier). Session 2 replaces this branch.
- `member-access-on-unresolved-target` — chain root failed; tail
  member can't be resolved.

These reasons are matched verbatim by tests, so they form a stable
contract. New reasons added in later sessions extend the set.

## 4. Algorithm

### 4.1 Scope-stack maintenance

Resolution context tracks the innermost enclosing `Scope`. The
`ReferenceResolver` walks the AST in lockstep with the existing scope
tree built by `ScopeBuildingVisitor`. The walker pushes/pops on the
same constructs the visitor introduced scopes for:

- `SubProcedure` / `FunctionProcedure` / `PropertyProcedure` →
  match the matching child `ProcedureScope` of `moduleScope` by name
  + ordering, push, walk body, pop.
- `WithStatement` → match the matching child `WithBlockScope` of the
  current scope by source range, push, walk body, pop. (Session 1 does
  not yet *use* the WithBlockScope to flatten implicit-with member
  access — that is Session 2's job. But the scope is still pushed so
  Session 2's flattening pass has the right context.)
- `ForEachStatement` → resolve `Collection` against the enclosing scope first (so its names see the outer chain, not the loop scope), then match child `ForEachScope`, push, walk body, pop.

Other body-bearing constructs (`If`, `For`, `Do`, `While`, `Select`)
do **not** introduce scopes — VBA has no block locals — so the
walker recurses without touching the scope stack.

### 4.2 Reference-site dispatch

The walker visits AST nodes via `VbaSyntaxWalker` recursion. At each
"reference site" the walker emits exactly one `ResolvedReference`
entry; tail recursion into the site's children continues so chained
expressions produce one entry per name.

| AST node | Resolution |
|---|---|
| `IdentifierExpression(name)` | Scope-walk algorithm (§4.3). |
| `MeExpression` | `ResolvedToMe(OwningClass)` if module's `OwningClass != null`; else `Unresolved("me-in-standard-module")`. |
| `BangAccessExpression(target, member)` | Resolve target recursively. Optimistic Access-registry lookup when target resolved to `Me` on a Form/Report. Else `Unresolved("default-member-not-yet-resolved")`. |
| `MemberAccessExpression(target, member)` | Resolve target recursively. Optimistic Access-registry lookup when target resolved to a known control with a `.Form`/`.Report` accessor. Else `Unresolved("default-member-not-yet-resolved")`. |
| `ImplicitWithMemberExpression(member)` | `Unresolved("with-block-flattening-deferred")` (Session 2 replaces). |
| `ImplicitWithBangExpression(member)` | Same as above. |
| `IndexOrCallExpression(target, args)` | Resolve target; arguments recurse. |
| All other Expression variants | Recurse into children; no per-site emission. |

The walker NEVER emits a site for nodes that are not name uses
(literals, operators, etc.). Recursion proceeds via `DefaultVisit`.

### 4.3 Scope-walk algorithm (root identifiers)

For an `IdentifierExpression(name)` at enclosing scope `S`:

1. **Local scope chain.** Starting at `S`, walk up `S.Parent` repeatedly,
   calling `scope.TryFindLocal(name)`. First non-null result →
   `Resolved(decl)`. The walk halts at `ProjectScope` (which holds no
   declarations).
2. **Access object model fallback.** If the chain returns nothing,
   call `_accessRegistry?.TryResolveTopLevelMember(name)`. If
   non-null → `ResolvedToObjectModel(decl)`.
3. **Built-in function fallback.** If still nothing,
   `BuiltInFunctionRegistry.TryGet(name)`. If non-null →
   `ResolvedToBuiltIn(fn)`.
4. **Unresolved.** Else `Unresolved("not-in-scope")`.

ParentScope walking honors VBA's case-insensitive name matching via
`Declaration.NameComparer` (already enforced by `Scope.TryFindLocal`).

The `Option Explicit` flag is intentionally NOT consulted for Session 1
— implicit-declaration synthesis is a Session 3+ concern. `Unresolved`
is the honest answer at this layer regardless of Option Explicit.

### 4.4 `BangAccessExpression` and `MemberAccessExpression` cascade

Bang and dot access nodes have a `Target` expression. Two-step process:

1. **Resolve the target first** so its outcome is recorded as its own
   `ResolvedReference` entry.
2. **Decide the member's outcome** based on the target's outcome.

The decision table for Session 1:

| Target outcome | Outcome for the access node |
|---|---|
| `ResolvedToMe(class)` for a Form/Report module | Optimistic `_accessRegistry.TryResolveControlOnForm/Report(moduleName, member)`. If hit → `ResolvedToObjectModel(decl)`. Else `Unresolved("default-member-not-yet-resolved")`. |
| `ResolvedToObjectModel(decl)` | Optimistic `SubChildLookup(decl, member)` (Session 1 stub returns null; #11 may wire subform/subreport navigation). If hit → `ResolvedToObjectModel(child)`. Else `Unresolved("default-member-not-yet-resolved")`. |
| `Resolved(decl)` of any kind | Optimistic `SubChildLookup(decl, member)`. Else `Unresolved("default-member-not-yet-resolved")` (needs type info from Phase 3.5/3.6). |
| `Unresolved(...)` | `Unresolved("member-access-on-unresolved-target")`. |
| `ResolvedToBuiltIn(...)` or no recorded outcome (non-emitting wrapper such as `IndexOrCallExpression`) | `Unresolved("default-member-not-yet-resolved")`. |

Bang vs. dot does not change Session 1 dispatch. Phase 3.5 may
diverge them later when default-member semantics enter the picture.

This decision table is deliberately conservative. Honest unresolved
entries at this layer let Phase 3.5 progressively replace branches
without reshaping the algorithm's input/output contract.

### 4.5 Loop variables

Loop variables on `For`/`ForEachStatement` appear in the AST as the
statement's `LoopVariable` field (a bare string), not as an
`IdentifierExpression` reference site. Uses of the loop variable
inside the body are normal `IdentifierExpression` nodes and resolve
via §4.3 — `Resolved(decl)` when the enclosing ProcedureScope chain
contains a matching declaration, `Unresolved("not-in-scope")`
otherwise. Implicit-declaration synthesis at the use site (when
`Option Explicit` is off and the name is novel) is a Session 3+
concern; until then, the unresolved reason is the same as for any
other unbound identifier. No dedicated reason string is reserved for
loop variables at this layer.

### 4.6 RedimStatement

`ScopeBuildingVisitor` already no-ops on RedimStatement — its
"redeclares vs. resizes" decision belongs at this layer. For Session 1,
the resolver visits the statement's bound expressions but emits
`Unresolved("default-member-not-yet-resolved")` for the redim target
when no declaration of that name exists in scope (since the
implicit-declaration path is deferred). When a declaration exists, the
target identifier resolves normally.

## 5. Built-in function registry

A static curated list lives in `BuiltInFunctionRegistry`. Each entry
is a `BuiltInFunction(string Name, BuiltInCategory Category, string?
ReturnType)`. Categories: `String`, `Math`, `DateTime`, `Information`,
`Conversion`, `IO`, `Interaction`, `AccessSpecific`. `ReturnType` is
the canonical VBA type spelling (`"String"`, `"Variant"`, etc.) when
known, null when polymorphic / context-dependent.

Phase 3.3 of the plan calls out **Format, IIf, Nz** as canonical
examples. Session 1's seed list also includes the very-common
identifiers a fixture `02`/`03`/`09`/`08-unresolvable` pass needs, so
the resolver does not fire `Unresolved` on names that should obviously
hit the built-in fallback. The exact list is documented in code.

The registry is intentionally NOT loaded at runtime. Per the spec's
Standards: "the list lives in source as a static registry, not loaded
at runtime."

## 6. Access object model registry interface

```csharp
public interface IAccessObjectModelRegistry
{
    Declaration? TryResolveTopLevelMember(string name);
    Declaration? TryResolveControlOnForm(string formName, string controlName);
    Declaration? TryResolveControlOnReport(string reportName, string controlName);
}
```

Three lookup methods cover the Session-1-relevant cases:

- `TryResolveTopLevelMember` — `Forms`, `Reports`, `DoCmd`,
  `Application`, etc. as bare-name uses.
- `TryResolveControlOnForm` / `OnReport` — `Me!ctl` and `Me.ctl`
  in a form/report module.

Session 1 lands the interface only. A `NullAccessObjectModelRegistry`
implementation that always returns `null` is supplied as the default
(passed when callers don't provide one). #11 (the
`access-resolver-symbol-table-integration` set) fills the real
implementation.

## 7. What this session deliberately does NOT do

Listed so verifiers do not flag these as missing:

- **With-block flattening** — Session 2's job. Session 1 emits
  `Unresolved("with-block-flattening-deferred")` for
  `ImplicitWithMemberExpression` / `ImplicitWithBangExpression`.
- **`ResolvedModule` wrapper type** — Session 3's job. Session 1
  returns `IReadOnlyList<ResolvedReference>` directly.
- **Default-member resolution (Phase 3.5)** — separate downstream set
  (#9). Session 1 always emits
  `Unresolved("default-member-not-yet-resolved")` for typed-target
  member access.
- **Type inference (Phase 3.6)** — separate downstream set.
- **Cross-reference index (Phase 3.7)** — already partially seeded by
  `Declaration.References` populated as a side effect; the
  Session 3 ResolvedModule layer will add the public query API.
- **Implicit declaration synthesis (Option-non-Explicit handling)** —
  later session.
- **`Me` resolution beyond pointing at `OwningClass`** — class members
  are not navigated yet; Phase 3.5 default-member work covers it.

## 8. File layout

```
AccessHarvester.Vba.Resolver/References/
  BuiltInFunction.cs                  // record + category enum
  BuiltInFunctionRegistry.cs          // static curated dictionary
  IAccessObjectModelRegistry.cs       // interface + null implementation
  ResolutionOutcome.cs                // discriminated-union (abstract record + variants)
  ResolvedReference.cs                // (Expression, SourceRange, Scope, ResolutionOutcome)
  ReferenceResolver.cs                // ResolveModule entry point + walker
```

Tests:

```
tests/AccessHarvester.Vba.Resolver.Tests/References/
  BuiltInFunctionRegistryTests.cs
  ReferenceResolverTests.cs           // unit tests on parsed fixtures
```

## 9. Test plan

Session 1 tests do not need #11's registry data. They use:

1. **Synthetic toy modules** built directly in test code (no fixtures)
   for tight unit coverage of each branch:
   - bare identifier resolves to local Dim
   - bare identifier resolves to module-level Const
   - bare identifier resolves to procedure parameter
   - bare identifier resolves to built-in (e.g., `Format`)
   - bare identifier unresolved when no match
   - `Me` in Form module → `ResolvedToMe`
   - `Me` in Standard module → `Unresolved("me-in-standard-module")`
   - `Me!ctl` with null registry → `Unresolved("default-member-not-yet-resolved")`
   - `Me!ctl` with stub registry that returns a fake declaration →
     `ResolvedToObjectModel`
   - `.Foo` (implicit-with) → `Unresolved("with-block-flattening-deferred")`
   - For Each collection expression names resolve at the enclosing
     scope (e.g., `For Each x In items` → `items` resolves to the
     enclosing Dim)
   - For Each loop variable used inside the body, when matching
     enclosing Dim exists → `Resolved`
   - Top-level Access registry takes precedence over the built-in
     fallback (e.g., a registry hit for a name that is also a
     curated built-in returns `ResolvedToObjectModel`)
2. **Fixture-driven smoke tests** loading the canonical inputs (`02`,
   `03`, `09`, `08-unresolvable.bas`) and asserting that:
   - The resolver runs to completion without exception
   - At least one of each expected outcome variant appears
   - No unresolved entry uses an unrecognized reason string

Session 4 (mechanical tightening) extends the fixture-driven assertions
to per-fixture expected counts.

## 10. Stability commitments

Once landed, the following surfaces are stable for Sessions 2-4:

- `ResolutionOutcome` variant set (Session 2 may add reasons; existing
  variants and reasons must not be renamed or repurposed).
- `IAccessObjectModelRegistry` method signatures (#11 fills, but the
  three-method shape is the contract).
- `ReferenceResolver.ResolveModule` signature.
- `BuiltInFunctionRegistry` — adding entries is fine; renaming the type
  or changing the lookup semantics is breaking.

Session 2's WithBlockScope flattening pass replaces the
`ImplicitWith*` branches in §4.2; Session 3's ResolvedModule shape
wraps the `IReadOnlyList<ResolvedReference>` output without reshaping
it. Both extensions are additive.
