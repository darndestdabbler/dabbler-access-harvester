namespace AccessHarvester.Vba.Parser.Ast;

// File + line + column triple. Column is 0-based (ANTLR convention).
// File is empty-string when no source path was supplied to the parser.
public sealed record SourceLocation(string File, int Line, int Column);
