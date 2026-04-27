using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.Vba.Resolver.Scopes;

// `For Each <var> In <coll> ... Next` body. The For Each loop variable
// is recorded as a structural member of the scope rather than as a
// declaration — the variable itself either resolves to a previously
// declared name in the enclosing ProcedureScope (the common case) or
// causes an ImplicitDeclaration to be created at #8 reference-
// resolution time when the module is not Option Explicit. The
// ForEachScope's job at this layer is to mark the lexical span and
// expose the loop variable's name + suffix so #8 can perform that
// resolve-or-implicit decision against the right scope.
//
// The numeric `For` loop is intentionally not given its own scope kind
// per the spec's Feature 2 → Scope kinds list. Its loop variable goes
// through the same resolve-or-implicit path #8 applies to any other
// identifier expression inside a ProcedureScope.
public sealed class ForEachScope : Scope
{
    // The loop variable's source-spelled name. Case-preserved; matched
    // case-insensitively via Declaration.NameComparer like every other
    // VBA name.
    public string LoopVariable { get; }

    // Type-suffix character on the loop variable's TYPED_IDENTIFIER
    // ('$', '%', '&', '!', '#', '@'), or null when the source had
    // none. Preserved verbatim from the parser-AST ForEachStatement.
    public string? LoopVariableTypeSuffix { get; }

    public ForEachScope(
        Scope parent,
        string loopVariable,
        string? loopVariableTypeSuffix,
        SourceRange? range)
        : base(parent, range)
    {
        LoopVariable = loopVariable;
        LoopVariableTypeSuffix = loopVariableTypeSuffix;
    }
}
