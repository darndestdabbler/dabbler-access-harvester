using System;
using System.Collections.Generic;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;

namespace AccessHarvester.Vba.Parser;

// Single entry point for parsing a VBA module source string. Mode
// selection (Strict / Recovering / Lossy) follows the contract in
// docs/session-sets/parser-infrastructure/error-recovery-strategy.md §2.
public static class VbaParserPipeline
{
    public static ParseResult Parse(string source, ParseOptions? options = null)
    {
        options ??= new ParseOptions();

        // ANTLR's grammar threads NEWLINE between every block; sources
        // that don't end with one cause a spurious EOF mismatch. Match
        // the convention the existing harness uses.
        if (!source.EndsWith("\n"))
            source += "\n";

        return options.RequestedMode switch
        {
            ParseMode.Strict => ParseStrict(source, options),
            ParseMode.Lossy => ParseLossy(source, options),
            _ => ParseRecovering(source, options),
        };
    }

    private static ParseResult ParseStrict(string source, ParseOptions options)
    {
        var listener = new ThrowingErrorListener(options.SourcePath);
        var (lexer, parser, _) = BuildPipeline(source);
        lexer.AddErrorListener(listener);
        parser.AddErrorListener(listener);
        parser.ErrorHandler = new BailErrorStrategy();

        // BailErrorStrategy bypasses parser error listeners — its
        // ParseCancellationException is the path real failures take.
        // Wrap to surface SourcePath in the exception message when set.
        // The ThrowingErrorListener still fires for lexer errors, where
        // it owns its own SourcePath formatting.
        try
        {
            var tree = parser.module();
            return new ParseResult(tree, System.Array.Empty<ParseDiagnostic>(), ParseMode.Strict);
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

        var (lexer, parser, tokens) = BuildPipeline(source);
        lexer.AddErrorListener(listener);
        parser.AddErrorListener(listener);
        parser.ErrorHandler = new StatementBoundaryErrorStrategy();

        // Force lexing so we can scan for ERROR_TOKEN occurrences (the
        // grammar's catch-all rule) before the parser runs. Without this
        // pre-scan VBA0001 is unreachable because the lexer succeeds at
        // tokenizing every input — the lexer SyntaxError callback only
        // fires when lexer rule matching fails outright.
        tokens.Fill();
        ScanForLexerErrors(tokens, diagnostics, options.SourcePath);

        var tree = parser.module();

        if (diagnostics.Count > options.LossyFallbackErrorThreshold)
        {
            diagnostics.Add(new ParseDiagnostic(
                Severity: DiagnosticSeverity.Warning,
                Code: "VBA0050",
                Message: WithSource(
                    $"Lossy fallback engaged: {diagnostics.Count} diagnostics exceed threshold {options.LossyFallbackErrorThreshold}.",
                    options.SourcePath),
                Line: 1,
                Column: 0,
                StartIndex: -1,
                StopIndex: -1));
            return new ParseResult(tree, diagnostics, ParseMode.Lossy);
        }

        return new ParseResult(tree, diagnostics, ParseMode.Recovering);
    }

    private static ParseResult ParseLossy(string source, ParseOptions options)
    {
        // Lossy currently shares the recovering pipeline and labels the
        // result accordingly. The procedure-header salvage scan is a
        // planned extension point per error-recovery-strategy.md §6 —
        // landed when corpus evidence shows ANTLR's default recovery
        // alone is insufficient.
        var result = ParseRecovering(source, options);
        if (result.ModeUsed == ParseMode.Lossy) return result;

        var diagnostics = new List<ParseDiagnostic>(result.Diagnostics)
        {
            new ParseDiagnostic(
                Severity: DiagnosticSeverity.Warning,
                Code: "VBA0050",
                Message: WithSource("Lossy mode requested explicitly.", options.SourcePath),
                Line: 1,
                Column: 0,
                StartIndex: -1,
                StopIndex: -1),
        };
        return new ParseResult(result.ParseTree, diagnostics, ParseMode.Lossy);
    }

    private static (VbaLexer Lexer, VbaParser Parser, CommonTokenStream Tokens) BuildPipeline(string source)
    {
        var stream = new AntlrInputStream(source);
        var lexer = new VbaLexer(stream);
        lexer.RemoveErrorListeners();

        var tokens = new CommonTokenStream(lexer);
        var parser = new VbaParser(tokens);
        parser.RemoveErrorListeners();

        return (lexer, parser, tokens);
    }

    private static void ScanForLexerErrors(
        CommonTokenStream tokens,
        List<ParseDiagnostic> diagnostics,
        string? sourcePath)
    {
        foreach (IToken token in tokens.GetTokens())
        {
            if (token.Type == VbaLexer.ERROR_TOKEN)
            {
                diagnostics.Add(new ParseDiagnostic(
                    Severity: DiagnosticSeverity.Error,
                    Code: "VBA0001",
                    Message: WithSource(
                        $"Unrecognized character '{token.Text}'.",
                        sourcePath),
                    Line: token.Line,
                    Column: token.Column,
                    StartIndex: token.StartIndex,
                    StopIndex: token.StopIndex,
                    OffendingTokenText: token.Text));
            }
        }
    }

    private static string WithSource(string msg, string? sourcePath) =>
        sourcePath is null ? msg : $"[{sourcePath}] {msg}";
}
