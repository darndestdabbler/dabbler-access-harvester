using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;

namespace AccessHarvester.Sql.Parser;

internal sealed class DiagnosticCollectingErrorListener
    : BaseErrorListener, IAntlrErrorListener<int>
{
    private readonly List<ParseDiagnostic> _diagnostics;
    private readonly string? _sourcePath;

    public DiagnosticCollectingErrorListener(List<ParseDiagnostic> sink, string? sourcePath)
    {
        _diagnostics = sink;
        _sourcePath = sourcePath;
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        string code = ClassifyParserError(msg, e);

        IReadOnlyList<string>? expected = null;
        if (e is not null && recognizer is Antlr4.Runtime.Parser parser)
        {
            try
            {
                var expectedSet = e.GetExpectedTokens();
                if (expectedSet is not null)
                {
                    var vocabulary = parser.Vocabulary;
                    expected = expectedSet
                        .ToIntegerList()
                        .Select(t => vocabulary.GetSymbolicName(t)
                                     ?? vocabulary.GetDisplayName(t)
                                     ?? t.ToString())
                        .ToList();
                }
            }
            catch
            {
                expected = null;
            }
        }

        int startIndex = offendingSymbol?.StartIndex ?? -1;
        int stopIndex = offendingSymbol?.StopIndex ?? -1;

        _diagnostics.Add(new ParseDiagnostic(
            Severity: DiagnosticSeverity.Error,
            Code: code,
            Message: WithSource(msg),
            Line: line,
            Column: charPositionInLine,
            StartIndex: startIndex,
            StopIndex: stopIndex,
            OffendingTokenText: offendingSymbol?.Text,
            ExpectedTokens: expected));
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
        _diagnostics.Add(new ParseDiagnostic(
            Severity: DiagnosticSeverity.Error,
            Code: "SQL0001",
            Message: WithSource(msg),
            Line: line,
            Column: charPositionInLine,
            StartIndex: -1,
            StopIndex: -1,
            OffendingTokenText: null,
            ExpectedTokens: null));
    }

    private string WithSource(string msg) =>
        _sourcePath is null ? msg : $"[{_sourcePath}] {msg}";

    private static string ClassifyParserError(string msg, RecognitionException? e)
    {
        if (e is NoViableAltException) return "SQL0004";
        if (msg is null) return "SQL0002";
        if (msg.StartsWith("missing ", StringComparison.Ordinal)) return "SQL0003";
        if (msg.StartsWith("extraneous input ", StringComparison.Ordinal)) return "SQL0002";
        if (msg.StartsWith("mismatched input ", StringComparison.Ordinal)) return "SQL0002";
        if (msg.StartsWith("no viable alternative", StringComparison.Ordinal)) return "SQL0004";
        return "SQL0002";
    }
}
