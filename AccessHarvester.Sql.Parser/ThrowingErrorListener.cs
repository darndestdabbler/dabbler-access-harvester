using System;
using System.IO;
using Antlr4.Runtime;

namespace AccessHarvester.Sql.Parser;

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
