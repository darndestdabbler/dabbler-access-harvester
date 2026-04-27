namespace AccessHarvester.Vba.Parser.Ast;

// Argument in a call/index expression or RaiseEvent statement.
// MissingArgument preserves H-D08 (empty positional slots in
// `f(a, , b)` calls).
public abstract record Argument(Provenance Provenance) : VbaSyntaxNode(Provenance);

public sealed record PositionalArgument(
    Provenance Provenance,
    Expression Value)
    : Argument(Provenance);

public sealed record NamedArgument(
    Provenance Provenance,
    string Name,
    Expression Value)
    : Argument(Provenance);

public sealed record MissingArgument(Provenance Provenance) : Argument(Provenance);
