namespace AccessHarvester.Vba.Parser.Ast;

// Discriminated union recording how an AST node was produced. Per
// docs/session-sets/parser-infrastructure/ast-shape.md §3 and the
// forward contract in error-recovery-strategy.md §7. Session 3 is
// expected to extend Provenance with a richer SourceRange field; the
// Source vs Recovered discriminator is the stable contract.
public abstract record Provenance(int Line, int Column);

public sealed record SourceProvenance(int Line, int Column)
    : Provenance(Line, Column);

public sealed record RecoveredProvenance(string Reason, int Line, int Column)
    : Provenance(Line, Column);
