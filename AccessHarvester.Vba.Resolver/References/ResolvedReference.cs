using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Scopes;

namespace AccessHarvester.Vba.Resolver.References;

// One reference site with its resolution outcome. The resolver emits
// one ResolvedReference per name-use position in the AST — for chained
// expressions like `Me!ctl.Foo`, three entries are emitted (Me, ctl,
// Foo) so the downstream AI consumer can pin each name independently.
//
// Stability: the four-property shape is a public contract per
// session-1-resolution-algorithm.md §10. Session 3's ResolvedModule
// wraps a list of these without reshaping them. Session 2 added the
// ResolvedViaWithFlattening outcome variant — implicit-with sites
// (`.Foo` / `!Foo` inside a With block) are emitted with their original
// AST node as `Expression` and a flattening-wrapper as `Outcome` whose
// SynthesizedTarget records the prepended-target full reference.
public sealed record ResolvedReference(
    // The AST node naming this reference site.
    // Matches one of: IdentifierExpression, MeExpression,
    // BangAccessExpression, MemberAccessExpression,
    // ImplicitWithMemberExpression, ImplicitWithBangExpression.
    Expression Expression,
    // Source location of the reference site. Materialised from the
    // expression's Provenance.Range when present, falling back to a
    // single-character range derived from Line+Column when the
    // parser's Range population is still in transition.
    SourceRange Location,
    // The innermost scope that contained the reference site. For a
    // bare identifier inside a procedure body, this is the
    // ProcedureScope; inside a With block, the WithBlockScope; inside
    // a ForEach body, the ForEachScope. The walker in
    // ReferenceResolver populates this faithfully so Session 2's
    // with-block flattening can read it without re-deriving.
    Scope EnclosingScope,
    ResolutionOutcome Outcome);
