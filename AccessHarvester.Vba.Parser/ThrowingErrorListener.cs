using System;
using System.IO;
using Antlr4.Runtime;

namespace AccessHarvester.Vba.Parser;

// Strict-mode error listener. Mirrors the behavior the existing
// VbaCorpusParserTests harness uses: any lex or parse error is fatal
// and surfaces as an InvalidOperationException. Used only when callers
// opt into ParseMode.Strict; the default Recovering / Lossy modes route
// errors through DiagnosticCollectingErrorListener instead.
internal sealed class ThrowingErrorListener
    : BaseErrorListener, IAntlrErrorListener<int>
{
    private readonly string? _sourcePath;

    public ThrowingErrorListener(string? sourcePath)
    {
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
        throw new InvalidOperationException(
            $"{Prefix()}Parser syntax error at line {line}:{charPositionInLine}: {msg}");
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
            $"{Prefix()}Lexer syntax error at line {line}:{charPositionInLine}: {msg}");
    }

    private string Prefix() => _sourcePath is null ? string.Empty : $"[{_sourcePath}] ";
}
