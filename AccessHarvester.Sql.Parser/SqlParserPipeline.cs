using System;
using System.Collections.Generic;
using AccessHarvester.Sql.Grammar.Generated;
using AccessHarvester.Sql.Parser.Ast;
using Antlr4.Runtime;

namespace AccessHarvester.Sql.Parser;

// Single entry point for parsing a JET SQL source string. Mode
// selection (Strict / Recovering / Lossy) follows the same contract as
// VbaParserPipeline. Entry rule is `parse` (full statements with optional
// trailing semicolon). ParseFragment() uses the `sqlFragment` entry rule
// (no optional semicolon) for dynamic-SQL strings reconstructed from VBA.
public static class SqlParserPipeline
{
    public static ParseResult Parse(string source, ParseOptions? options = null)
    {
        options ??= new ParseOptions();
        return options.RequestedMode switch
        {
            ParseMode.Strict => ParseStrict(source, options),
            ParseMode.Lossy => ParseLossy(source, options),
            _ => ParseRecovering(source, options),
        };
    }

    // Parse a SQL string using the `sqlFragment` entry rule. Intended for
    // dynamically-constructed SQL strings extracted from VBA (fixture 05)
    // where the source omits the optional trailing semicolon.
    public static ParseResult ParseFragment(string source, ParseOptions? options = null)
    {
        options ??= new ParseOptions();
        return options.RequestedMode switch
        {
            ParseMode.Strict => ParseFragmentStrict(source, options),
            ParseMode.Lossy => ParseFragmentLossy(source, options),
            _ => ParseFragmentRecovering(source, options),
        };
    }


    private static ParseResult ParseStrict(string source, ParseOptions options)
    {
        var listener = new ThrowingErrorListener(options.SourcePath);
        var (lexer, parser) = BuildPipeline(source);
        lexer.AddErrorListener(listener);
        parser.AddErrorListener(listener);
        parser.ErrorHandler = new BailErrorStrategy();

        try
        {
            var tree = parser.parse();
            return new ParseResult(tree.sqlStatement(), System.Array.Empty<ParseDiagnostic>(), ParseMode.Strict);
        }
        catch (Antlr4.Runtime.Misc.ParseCanceledException ex) when (options.SourcePath is not null)
        {
            throw new InvalidOperationException(
                $"[{options.SourcePath}] {ex.InnerException?.Message ?? ex.Message}",
                ex);
        }
    }

    private static ParseResult ParseRecovering(string source, ParseOptions options)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var listener = new DiagnosticCollectingErrorListener(diagnostics, options.SourcePath);

        var (lexer, parser) = BuildPipeline(source);
        lexer.AddErrorListener(listener);
        parser.AddErrorListener(listener);
        parser.ErrorHandler = new StatementBoundaryErrorStrategy();

        var tree = parser.parse();

        if (diagnostics.Count > options.LossyFallbackErrorThreshold)
        {
            diagnostics.Add(new ParseDiagnostic(
                Severity: DiagnosticSeverity.Warning,
                Code: "SQL0050",
                Message: WithSource(
                    $"Lossy fallback engaged: {diagnostics.Count} diagnostics exceed threshold {options.LossyFallbackErrorThreshold}.",
                    options.SourcePath),
                Line: 1,
                Column: 0,
                StartIndex: -1,
                StopIndex: -1));
            return new ParseResult(tree.sqlStatement(), diagnostics, ParseMode.Lossy);
        }

        return new ParseResult(tree.sqlStatement(), diagnostics, ParseMode.Recovering);
    }

    private static ParseResult ParseLossy(string source, ParseOptions options)
    {
        var result = ParseRecovering(source, options);
        if (result.ModeUsed == ParseMode.Lossy) return result;

        var diagnostics = new List<ParseDiagnostic>(result.Diagnostics)
        {
            new ParseDiagnostic(
                Severity: DiagnosticSeverity.Warning,
                Code: "SQL0050",
                Message: WithSource("Lossy mode requested explicitly.", options.SourcePath),
                Line: 1,
                Column: 0,
                StartIndex: -1,
                StopIndex: -1),
        };
        return new ParseResult(result.ParseTree, diagnostics, ParseMode.Lossy);
    }

    private static ParseResult ParseFragmentStrict(string source, ParseOptions options)
    {
        var listener = new ThrowingErrorListener(options.SourcePath);
        var (lexer, parser) = BuildPipeline(source);
        lexer.AddErrorListener(listener);
        parser.AddErrorListener(listener);
        parser.ErrorHandler = new BailErrorStrategy();

        try
        {
            var tree = parser.sqlFragment();
            return new ParseResult(tree.sqlStatement(), System.Array.Empty<ParseDiagnostic>(), ParseMode.Strict);
        }
        catch (Antlr4.Runtime.Misc.ParseCanceledException ex) when (options.SourcePath is not null)
        {
            throw new InvalidOperationException(
                $"[{options.SourcePath}] {ex.InnerException?.Message ?? ex.Message}",
                ex);
        }
    }

    private static ParseResult ParseFragmentRecovering(string source, ParseOptions options)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var listener = new DiagnosticCollectingErrorListener(diagnostics, options.SourcePath);

        var (lexer, parser) = BuildPipeline(source);
        lexer.AddErrorListener(listener);
        parser.AddErrorListener(listener);
        parser.ErrorHandler = new StatementBoundaryErrorStrategy();

        var tree = parser.sqlFragment();

        if (diagnostics.Count > options.LossyFallbackErrorThreshold)
        {
            diagnostics.Add(new ParseDiagnostic(
                Severity: DiagnosticSeverity.Warning,
                Code: "SQL0050",
                Message: WithSource(
                    $"Lossy fallback engaged: {diagnostics.Count} diagnostics exceed threshold {options.LossyFallbackErrorThreshold}.",
                    options.SourcePath),
                Line: 1,
                Column: 0,
                StartIndex: -1,
                StopIndex: -1));
            return new ParseResult(tree.sqlStatement(), diagnostics, ParseMode.Lossy);
        }

        return new ParseResult(tree.sqlStatement(), diagnostics, ParseMode.Recovering);
    }

    private static ParseResult ParseFragmentLossy(string source, ParseOptions options)
    {
        var result = ParseFragmentRecovering(source, options);
        if (result.ModeUsed == ParseMode.Lossy) return result;

        var diagnostics = new List<ParseDiagnostic>(result.Diagnostics)
        {
            new ParseDiagnostic(
                Severity: DiagnosticSeverity.Warning,
                Code: "SQL0050",
                Message: WithSource("Lossy mode requested explicitly.", options.SourcePath),
                Line: 1,
                Column: 0,
                StartIndex: -1,
                StopIndex: -1),
        };
        return new ParseResult(result.ParseTree, diagnostics, ParseMode.Lossy);
    }


    private static (JetSqlLexer Lexer, JetSqlParser Parser) BuildPipeline(string source)
    {
        var stream = new AntlrInputStream(source);
        var lexer = new JetSqlLexer(stream);
        lexer.RemoveErrorListeners();

        var tokens = new CommonTokenStream(lexer);
        var parser = new JetSqlParser(tokens);
        parser.RemoveErrorListeners();

        return (lexer, parser);
    }


    private static string WithSource(string msg, string? sourcePath) =>
        sourcePath is null ? msg : $"[{sourcePath}] {msg}";
}

// Extension: build an AST from a ParseResult.
public static class ParseResultExtensions
{
    public static SqlStatement BuildAst(this ParseResult result, string sourceFile = "")
    {
        var builder = new AstBuilder(result.Diagnostics, sourceFile);
        return builder.Build(result.ParseTree);  // ParseTree is SqlStatementContext
    }
}
