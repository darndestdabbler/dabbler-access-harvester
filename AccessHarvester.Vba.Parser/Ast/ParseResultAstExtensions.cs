namespace AccessHarvester.Vba.Parser.Ast;

// Public surface for converting a ParseResult into an AST. Per
// ast-shape.md §6: one extension method, pure function, never throws.
public static class ParseResultAstExtensions
{
    public static Module BuildAst(this ParseResult result)
    {
        var builder = new AstBuilder(result.Diagnostics);
        return builder.BuildModule(result.ParseTree);
    }
}
