using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Scopes;

// `With <expr> ... End With` block. Structural at this layer: it
// records the target expression and the lexical span of the block, so
// #8 (resolver-references-and-with-blocks) can flatten implicit-with
// member access (`.Foo`, `!Foo`) against the enclosing target. No
// declarations are owned by a WithBlockScope — nested locals inside a
// With body still belong to the enclosing ProcedureScope per VBA's
// no-block-locals rule.
//
// Nested With blocks produce nested WithBlockScopes; the innermost
// WithBlockScope's Target is the binding for an `.Foo` use site, which
// is again #8's job to flatten.
public sealed class WithBlockScope : Scope
{
    // The target expression after `With`. Stored verbatim from the
    // parser AST — #8 resolves it against the enclosing scope chain
    // when flattening implicit-with member access.
    public Expression Target { get; }

    public WithBlockScope(Scope parent, Expression target, SourceRange? range)
        : base(parent, range)
    {
        Target = target;
    }
}
