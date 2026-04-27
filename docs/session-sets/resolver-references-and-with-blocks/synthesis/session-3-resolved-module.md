# Session 3 Synthesis — `ResolvedModule` Annotations + Provenance

> **Authored:** 2026-04-27 in-conversation by Claude Opus 4.7 (orchestrator).
> **Routing decision:** Per `project-guidance.md` Convention "When the
> orchestrator's model matches the routing target...", Opus 4.7 authored
> this synthesis directly rather than calling `route(architecture)`,
> whose target is also Opus. The cross-provider gate
> (`session-verification`) still runs against gpt-5-4 at end of session.
> **Scope:** The `ResolvedModule` wrapper for the per-module resolver
> output, the lookup helpers it exposes, and the diagnostics surface that
> later phases extend. Builds on Session 1's `ReferenceResolver` and
> Session 2's `ResolvedViaWithFlattening` outcome.

---

## 1. Goal

Sessions 1 and 2 left the resolver returning a flat
`IReadOnlyList<ResolvedReference>` from
`ReferenceResolver.ResolveModule(ModuleScope, Module)`. That shape is
fine for unit-test assertions but wrong for the downstream consumer
(an AI agent walking a module's resolved tree to interrogate
references, declarations, and diagnostics). The downstream consumer
needs:

1. A single object that bundles **the AST**, **the scope tree root**,
   and **the resolved references** so consumers don't need to re-thread
   three arguments to every helper.
2. **O(1) lookup** from an AST `Expression` node to its
   `ResolvedReference` (and from there to a `Declaration` when one
   exists). Without this, every consumer rebuilds the same dictionary.
3. **An explicit unresolved-list** so callers can pose the common
   question "what couldn't this module resolve?" without scanning every
   reference.
4. **A diagnostics list** that initially mirrors the unresolved set
   (each unresolved reference is reported as one diagnostic) and is
   shaped so later phases (Phase 3.5 default-member resolution, Phase
   3.7 cross-reference index) can append additional diagnostics
   without reshaping the surface.

The Session 1 stability commitment in §10 already named this layer:
"Session 3's ResolvedModule shape wraps the
`IReadOnlyList<ResolvedReference>` output without reshaping it. Both
extensions are additive." This session honours that — the existing
`ResolveModule` signature stays; a new `Resolve` method returns a
`ResolvedModule`. Internal implementation reuses the walker in place;
no resolver-walker changes are needed.

## 2. Inputs and outputs

**Inputs (unchanged from S1/S2):**
- `ModuleScope module` — the scope tree built by Session 2 of #7.
- `Module ast` — the parser AST.
- Optional `IAccessObjectModelRegistry` (constructor parameter).

**New output:**
- `ResolvedModule` — a sealed class bundling the AST, scope, the
  reference list, the unresolved subset, the diagnostics, and the
  lookup map.

**Existing output stays:**
- `ResolveModule(ModuleScope, Module)` continues to return the flat
  `IReadOnlyList<ResolvedReference>`. Callers (the existing test suite)
  do not change. Per §10 of session-1, this is a stable surface.

## 3. `ResolvedModule` shape

```csharp
public sealed class ResolvedModule
{
    public Module Ast { get; }
    public ModuleScope ModuleScope { get; }
    public IReadOnlyList<ResolvedReference> References { get; }
    public IReadOnlyList<ResolvedReference> UnresolvedReferences { get; }
    public IReadOnlyList<ResolverDiagnostic> Diagnostics { get; }

    // Lookups. All keyed by ReferenceEqualityComparer on the AST node.
    public ResolvedReference? TryGetReference(Expression expression);

    // Returns the bound Declaration if the outcome is Resolved or
    // ResolvedToObjectModel (peeking through ResolvedViaWithFlattening
    // to the inner outcome). Returns null for ResolvedToBuiltIn,
    // ResolvedToMe, Unresolved, or any expression with no recorded
    // outcome.
    public Declaration? TryGetDeclaration(Expression expression);
}
```

### 3.1 Lookup keying — reference equality on the AST node

The walker already keys its internal dictionary by AST `Expression`
identity (`ReferenceEqualityComparer.Instance`). `ResolvedModule` reuses
the same comparer when materialising its public lookup. Two
ResolvedReference entries can share the same `Provenance` (e.g.,
synthesised With-flattened nodes share their site's provenance), so a
provenance-keyed map would alias them. AST node identity is unambiguous
because the parser produces one node per use site.

If a single AST node appears as the `Expression` of more than one
ResolvedReference (it does not under the current resolver, but a
defensive last-write-wins is fine), the **last** entry in
`References` wins for the lookup. Tests document this with an explicit
single-write assertion on the canonical fixtures.

### 3.2 Declaration peek-through

`TryGetDeclaration` is a convenience over `TryGetReference` that
collapses the outcome into the bound declaration when one exists:

```
Resolved(decl)                  → decl
ResolvedToObjectModel(decl)     → decl
ResolvedViaWithFlattening(_, i) → recurse on i (bounded — see S2 §3:
                                  InnerOutcome is never another
                                  ResolvedViaWithFlattening, so the
                                  recursion is at most one level)
ResolvedToBuiltIn               → null  (built-ins are not Declarations)
ResolvedToMe                    → null  (the OwningClass is a
                                  ClassDeclaration, but the reference
                                  is to `Me` itself, not to the class
                                  by name; if a downstream consumer
                                  wants the class, it pulls
                                  ResolvedToMe.OwningClass off the
                                  outcome directly)
Unresolved                      → null
no recorded reference           → null
```

The peek-through deliberately mirrors the resolver's existing
peek-through in `DecideMemberOutcome`. Centralising it here means the
downstream consumer never has to reproduce the switch.

### 3.3 Why no `ReferencesTo(Declaration)` query

The reverse lookup is already on `Declaration.References`, populated by
the walker's `BackPopulate` side effect. Adding a duplicate API to
`ResolvedModule` would be a redundant convenience — and ambiguous,
because `Declaration.References` is project-wide while a hypothetical
`ResolvedModule.ReferencesTo` would only see this module's references.
Per the lean-by-default convention, omitted.

## 4. Diagnostics

### 4.1 Initial diagnostic set: one per unresolved reference

The spec asks for a diagnostics list. Phase 3.3-3.4 produces no
diagnostics that aren't already encoded as `Unresolved` outcomes —
every "I couldn't resolve this" is already a `ResolutionOutcome.Unresolved`
on a specific `ResolvedReference`. Re-encoding those as diagnostics is
the natural reading of "annotations + provenance".

For each `ResolvedReference` whose outcome is `Unresolved`, emit one
`ResolverDiagnostic`:

```csharp
public sealed record ResolverDiagnostic(
    ResolverDiagnosticSeverity Severity,
    string Code,
    string Message,
    SourceRange Location);

public enum ResolverDiagnosticSeverity { Info, Warning, Error }
```

- **`Severity`** — `Warning` for `not-in-scope` and
  `me-in-standard-module` (these flag user-visible defects in the
  source); `Info` for `default-member-not-yet-resolved`,
  `with-block-flattening-deferred`, and
  `member-access-on-unresolved-target` (these are deferred-by-design
  partial resolutions that downstream phases close out, not source
  defects).
- **`Code`** — the `UnresolvedReasons` constant verbatim. Tests pin
  the exact strings, so adding a new reason in a later session must
  also add (not rename) any code mapping.
- **`Message`** — a short human-readable form, e.g.,
  `"Identifier 'foo' is not in scope."` Built lazily; the format is
  not part of the public contract (downstream AI consumers should
  match on `Code`, not on `Message`).
- **`Location`** — the underlying `ResolvedReference.Location`.

### 4.2 Closed severity scale

`Info | Warning | Error` is the closed set. `Error` is not emitted by
this layer — it is reserved for diagnostics that mean the resolver
itself failed in a way the consumer must surface to a human (e.g., a
future "scope construction failed for procedure X" diagnostic emitted
by a downstream pass). Keeping `Error` defined but unused now lets
later phases populate it without a breaking enum extension.

### 4.3 Why diagnostics are derived, not stored separately

Storing diagnostics as a derived projection over `UnresolvedReferences`
guarantees no drift between the two surfaces. The materialisation is a
single pass at construction time; the resulting list is then immutable.
Future phases that emit non-unresolved-derived diagnostics (e.g., a
warning about an `Option Explicit` rule violation that resolved
successfully but is suspect) append additional entries to the
diagnostics list during construction, after the unresolved-derived
seed.

## 5. Construction

`ReferenceResolver.Resolve(ModuleScope, Module)` is the new entry point:

```csharp
public ResolvedModule Resolve(ModuleScope moduleScope, Module module)
{
    var references = ResolveModule(moduleScope, module);
    return new ResolvedModule(module, moduleScope, references);
}
```

The `ResolvedModule` constructor materialises the unresolved subset,
the lookup map, and the diagnostics in one pass. Construction is the
only place the materialisation runs; all public properties expose
already-built immutable views.

`ResolveModule(ModuleScope, Module)` keeps its existing signature and
behaviour — internally it is the unchanged walker entry point. The new
`Resolve` is a thin wrapper that calls `ResolveModule` and constructs
the `ResolvedModule`. This is the additive change S1 §10 promised.

## 6. What this session deliberately does NOT do

Listed so verifiers do not flag these as missing:

- **Cross-module reference index (Phase 3.7)** — the
  `Declaration.References` list is already populated as a side effect of
  the walker; the project-wide query API is a separate downstream set.
  `ResolvedModule` is per-module by design.
- **Type inference (Phase 3.6)** — diagnostics that depend on type
  information (e.g., "`Variant` used where `Long` expected") belong to
  a later phase. The diagnostics surface is shaped to receive them.
- **Default-member resolution (Phase 3.5 / set #9)** — the
  `default-member-not-yet-resolved` Unresolved entries continue to
  appear; #9 will replace them with resolved outcomes, at which point
  the corresponding diagnostics drop out automatically.
- **Diagnostic message localisation / formatting standards** — the
  messages are short English strings. Formatting policy is a downstream
  decision; the `Code` is the stable contract.
- **Mutating the resolver walker** — the walker's behaviour does not
  change. All Session 3 work is a wrapper layer.
- **A `ReferencesTo(Declaration)` API** on `ResolvedModule` — see §3.3.

## 7. File layout

New:

```
AccessHarvester.Vba.Resolver/References/
  ResolvedModule.cs                     // class + lookup helpers
  ResolverDiagnostic.cs                 // record + ResolverDiagnosticSeverity enum
```

Modified:

```
AccessHarvester.Vba.Resolver/References/
  ReferenceResolver.cs                  // adds Resolve() method
```

Tests:

```
tests/AccessHarvester.Vba.Resolver.Tests/References/
  ResolvedModuleTests.cs                // wrapper shape, lookups, diagnostics
```

## 8. Test plan

Hermetic unit tests on synthetic toy modules cover:

- `Resolve` returns a `ResolvedModule` whose `References` exactly
  equals what `ResolveModule` returned for the same inputs (sanity
  check that the wrapper is purely additive).
- `Ast` and `ModuleScope` are propagated by reference identity.
- `UnresolvedReferences` contains exactly the references whose outcome
  is `Unresolved`.
- `TryGetReference(node)` returns the `ResolvedReference` for a node
  whose outcome was emitted, and `null` for nodes the walker did not
  emit on (e.g., `IndexOrCallExpression`, `BinaryExpression`).
- `TryGetDeclaration` returns:
  - the bound declaration for `Resolved` and `ResolvedToObjectModel`;
  - the inner-bound declaration when the outcome is
    `ResolvedViaWithFlattening` over a `Resolved` inner;
  - `null` for `ResolvedToBuiltIn`, `ResolvedToMe`, and `Unresolved`;
  - `null` when the input expression was never visited.
- `Diagnostics`:
  - one entry per `Unresolved` reference;
  - severity matches the §4.1 mapping for each closed-set reason;
  - `Code` equals the corresponding `UnresolvedReasons` constant;
  - `Location` matches the underlying `ResolvedReference.Location`.

A fixture-driven smoke test confirms the canonical Session-1 anchors
(`02`, `03`, `09`, `08-unresolvable.bas`) all produce a non-null
`ResolvedModule` and that `Diagnostics.Count ==
UnresolvedReferences.Count` on each.

## 9. Stability commitments

Once landed, the following surfaces are stable for Session 4 and
downstream sets:

- `ResolvedModule` property set: `Ast`, `ModuleScope`, `References`,
  `UnresolvedReferences`, `Diagnostics`, plus `TryGetReference` and
  `TryGetDeclaration`.
- `ResolverDiagnostic` record shape and `ResolverDiagnosticSeverity` enum
  (`Info | Warning | Error`).
- The §4.1 reason-string-to-severity mapping (adding new reasons in
  later sessions extends the mapping; existing entries do not change).
- `ReferenceResolver.Resolve(ModuleScope, Module)` signature.

Session 4's mechanical tightening exercises these surfaces through
`schema-validation` against the canonical fixtures' `expected.json`
contracts. Any drift would surface there.
