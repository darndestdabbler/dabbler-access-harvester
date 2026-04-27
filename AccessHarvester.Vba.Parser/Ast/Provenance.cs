namespace AccessHarvester.Vba.Parser.Ast;

// Discriminated union recording how an AST node was produced. Per
// docs/session-sets/parser-infrastructure/ast-shape.md §3 and the
// forward contract in error-recovery-strategy.md §7.
//
// Session 3 adds SourceRange? Range (additive — defaults to null so
// existing call sites that pass only Line/Column continue to compile).
// Line and Column are kept as first-class positional fields for this
// transitional session; Session 4 may promote Range to the primary
// location representation and remove the redundant scalars.
public abstract record Provenance(int Line, int Column, SourceRange? Range = null);

public sealed record SourceProvenance(int Line, int Column, SourceRange? Range = null)
    : Provenance(Line, Column, Range);

public sealed record RecoveredProvenance(string Reason, int Line, int Column, SourceRange? Range = null)
    : Provenance(Line, Column, Range);
