namespace AccessHarvester.Sql.Parser.Ast;

// Span from the first token's start to the last token's stop, both
// as character offsets into the source string (ANTLR's StartIndex /
// StopIndex convention, 0-based, inclusive).
public sealed record SourceRange(int StartIndex, int StopIndex);
