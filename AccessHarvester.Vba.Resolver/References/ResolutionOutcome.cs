using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;

namespace AccessHarvester.Vba.Resolver.References;

// Discriminated union of resolution results for one reference site.
// Matched on by downstream consumers; subtype names form a stable
// public contract per session-1-resolution-algorithm.md §10.
//
// Variants:
//   Resolved                  — name matched a Declaration in the scope
//                               chain (an in-source VBA declaration).
//   ResolvedToObjectModel     — name matched a synthetic Declaration
//                               from the Access object-model registry.
//                               Distinct from Resolved so downstream
//                               consumers can tell registry-backed
//                               bindings apart from in-source ones
//                               without inspecting the Declaration's
//                               Provenance.
//   ResolvedToBuiltIn         — name matched a curated VBA built-in.
//   ResolvedToMe              — `Me` reference resolved to the module's
//                               OwningClass (Form / Report / Class).
//   ResolvedViaWithFlattening — implicit-with member (`.Foo` / `!Foo`)
//                               inside a `With` block. SynthesizedTarget
//                               is the synthesized full reference
//                               (`MemberAccessExpression` /
//                               `BangAccessExpression` whose target is
//                               the enclosing With block's expression);
//                               InnerOutcome is the result of resolving
//                               that synthesized reference. The original
//                               implicit-with AST node is preserved on
//                               the ResolvedReference. Per session-2
//                               synthesis §3, InnerOutcome is never
//                               itself a ResolvedViaWithFlattening — at
//                               most one level of wrapping.
//   Unresolved                — explicit unresolved provenance with a
//                               reason string per project-guidance.md →
//                               VBA Parser Strategy.
//
// The base type is closed: the constructor is private, and the only
// valid variants are nested below. Switch expressions may treat the
// set as exhaustive. Adding a variant later is a deliberate breaking
// change that must coordinate with all consumers.
public abstract record ResolutionOutcome
{
    // private (not private protected) so no other type — even in this
    // assembly — can derive from ResolutionOutcome. Nested variants
    // below have access via C#'s nested-type rule.
    private ResolutionOutcome() { }

    public sealed record Resolved(Declaration Declaration) : ResolutionOutcome;

    public sealed record ResolvedToObjectModel(Declaration Declaration) : ResolutionOutcome;

    public sealed record ResolvedToBuiltIn(BuiltInFunction Function) : ResolutionOutcome;

    public sealed record ResolvedToMe(ClassDeclaration OwningClass) : ResolutionOutcome;

    public sealed record ResolvedViaWithFlattening(
        Expression SynthesizedTarget,
        ResolutionOutcome InnerOutcome) : ResolutionOutcome;

    public sealed record Unresolved(string Reason) : ResolutionOutcome;
}

// Closed set of reason strings emitted by Session 1's resolver. Tests
// match on these values exactly; later sessions extend (never rename)
// the set. New reasons added in future sessions append to this class.
public static class UnresolvedReasons
{
    // Name walked the scope chain and all fallbacks without a match.
    // Includes the "would resolve in another module's Public scope"
    // case — cross-module visibility is Phase 3.4 / #9 territory and
    // remains unresolved at this layer.
    public const string NotInScope = "not-in-scope";

    // `Me` used inside a Standard module, which has no class context.
    public const string MeInStandardModule = "me-in-standard-module";

    // Bang or dot member access whose resolution depends on type info
    // from Phase 3.5 (default-member resolution) or Phase 3.6 (type
    // inference). The target was resolved; the member name was not.
    public const string DefaultMemberNotYetResolved = "default-member-not-yet-resolved";

    // Implicit-with member (`.Foo` / `!Foo`) emitted only as a
    // defensive fallback when the resolver cannot find an enclosing
    // WithBlockScope (structurally unreachable from valid input;
    // retained for parser/scope-construction defects). Session 2 wired
    // the flattening pass: ordinary implicit-with sites now produce
    // ResolvedViaWithFlattening instead.
    public const string WithBlockFlatteningDeferred = "with-block-flattening-deferred";

    // Member access whose target failed to resolve. Tail member
    // resolution is impossible without target type info.
    public const string MemberAccessOnUnresolvedTarget = "member-access-on-unresolved-target";
}
