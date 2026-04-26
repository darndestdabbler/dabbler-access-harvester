using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Xunit;

namespace AccessHarvester.Vba.Grammar.Tests.Modules;

// Grammar harness for the full VBA-module fixture corpus (Session 2 of vba-grammar-validation).
//
// Reads every .bas and .cls file from docs/test-cases/ and docs/test-cases/edge/
// and parses each through the ANTLR `module` start rule using BailErrorStrategy
// (throws on first error, no recovery) plus a throwing error listener.
//
// Corpus breakdown: 9 .bas + 1 .cls = 10 module-level fixtures; 5 .bas edge fixtures.
// Total VBA-module fixtures: 15. (.sql and .txt fixtures are intentionally excluded —
// they exercise the JET-SQL and Access expression grammars, out of scope for this set.)
//
// A failing test means a grammar bug in Vba.g4; fix and re-run.
public class VbaCorpusParserTests
{
    private sealed class ThrowingErrorListener
        : BaseErrorListener, IAntlrErrorListener<int>
    {
        public static readonly ThrowingErrorListener Instance = new();

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new InvalidOperationException(
                $"Parser syntax error at line {line}:{charPositionInLine}: {msg}");
        }

        public void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            int offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new InvalidOperationException(
                $"Lexer syntax error at line {line}:{charPositionInLine}: {msg}");
        }
    }

    private static VbaParser.ModuleContext Parse(string source)
    {
        if (!source.EndsWith("\n", StringComparison.Ordinal))
            source += "\n";

        var stream = new AntlrInputStream(source);
        var lexer = new VbaLexer(stream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(ThrowingErrorListener.Instance);

        var tokens = new CommonTokenStream(lexer);
        var parser = new VbaParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(ThrowingErrorListener.Instance);
        // BailErrorStrategy throws ParseCancellationException immediately on any
        // error rather than attempting recovery — ensures the harness cannot silently
        // mis-parse a fixture via ANTLR's default error-recovery mechanism.
        parser.ErrorHandler = new BailErrorStrategy();

        return parser.module();
    }

    private static string LocateCorpusRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate docs/test-cases walking up from '{AppContext.BaseDirectory}'.");
    }

    public static IEnumerable<object[]> ModuleLevelFixtures()
    {
        string root = LocateCorpusRoot();
        var files = Directory.EnumerateFiles(root, "*.bas")
            .Concat(Directory.EnumerateFiles(root, "*.cls"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
            yield return new object[] { Path.GetFileName(file), file };
    }

    public static IEnumerable<object[]> EdgeFixtures()
    {
        string root = Path.Combine(LocateCorpusRoot(), "edge");
        if (!Directory.Exists(root)) yield break;
        var files = Directory.EnumerateFiles(root, "*.bas")
            .Concat(Directory.EnumerateFiles(root, "*.cls"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
            yield return new object[] { Path.GetFileName(file), file };
    }

    [Theory]
    [MemberData(nameof(ModuleLevelFixtures))]
    public void ModuleLevel_FixtureParsesWithoutError(string label, string path)
    {
        string source = File.ReadAllText(path);
        Parse(source);   // throws on any lexer / parser syntax error
    }

    [Theory]
    [MemberData(nameof(EdgeFixtures))]
    public void Edge_FixtureParsesWithoutError(string label, string path)
    {
        string source = File.ReadAllText(path);
        Parse(source);   // throws on any lexer / parser syntax error
    }
}
