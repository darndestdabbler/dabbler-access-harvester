namespace AccessHarvester.Sql.Parser.Ast;

// Discriminated union recording how an AST node was produced. Mirrors
// VbaSyntaxNode Provenance conventions: SourceProvenance for normally
// parsed nodes, RecoveredProvenance for nodes that were reconstructed
// during error recovery.
public abstract record Provenance(int Line, int Column, SourceRange? Range = null);

public sealed record SourceProvenance(int Line, int Column, SourceRange? Range = null)
    : Provenance(Line, Column, Range);

public sealed record RecoveredProvenance(string Reason, int Line, int Column, SourceRange? Range = null)
    : Provenance(Line, Column, Range);
