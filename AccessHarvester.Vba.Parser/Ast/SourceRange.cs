namespace AccessHarvester.Vba.Parser.Ast;

// Half-open [Start, End) range over a source file. End.Column is the
// column immediately after the last character of the range (exclusive).
// Both Start and End carry the same File string.
public sealed record SourceRange(SourceLocation Start, SourceLocation End);
