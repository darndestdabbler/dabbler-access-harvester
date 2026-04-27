namespace AccessHarvester.Vba.Parser.Ast;

// Public surface for converting a ParseResult into an AST. Per
// ast-shape.md §6: one extension method, pure function, never throws.
public static class ParseResultAstExtensions
{
    // sourceFile is threaded into every SourceLocation's File field.
    // Pass null (or omit) when no file path is available — File will
    // be the empty string in that case.
    public static Module BuildAst(this ParseResult result, string? sourceFile = null)
    {
        var builder = new AstBuilder(result.Diagnostics, sourceFile ?? "");
        return builder.BuildModule(result.ParseTree);
    }
}
