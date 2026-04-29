using System;
using AccessHarvester.Vba.Parser.Ast;

namespace AccessHarvester.SymbolDb.Extraction;

// Captures the raw source body for a procedure (or any node with a
// SourceRange) from the original module text. The parser stores 0-based
// columns and 1-based lines on SourceLocation; SourceRange is half-open
// over [Start, End). The extracted text is the procedure header through
// the End Sub / End Function / End Property line inclusive.
public static class SourceBodyExtractor
{
    public static (int? StartLine, int? EndLine, string? Body) Extract(
        string source,
        SourceRange? range)
    {
        if (range is null) return (null, null, null);

        var lines = source.Replace("\r\n", "\n").Split('\n');
        int startLine = range.Start.Line;
        int endLine = range.End.Line;

        // SourceRange is half-open at the End column, but procedures are
        // line-oriented so the end-line itself (containing `End Sub`) is
        // included verbatim.
        if (startLine < 1 || startLine > lines.Length)
            return (startLine, endLine, null);
        int safeStart = Math.Max(startLine, 1);
        int safeEnd = Math.Min(endLine, lines.Length);

        var slice = new string[safeEnd - safeStart + 1];
        Array.Copy(lines, safeStart - 1, slice, 0, slice.Length);
        var body = string.Join("\n", slice);

        return (startLine, endLine, body);
    }
}
