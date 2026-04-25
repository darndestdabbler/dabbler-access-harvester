using System.Text;
using System.Text.RegularExpressions;

namespace AccessHarvester.Core.Enrichment.Vba;

public sealed record VbaProcedureBlock(int StartLine, string SignatureLine, string Body);

/// <summary>
/// Splits a multi-procedure VBA source string into one
/// <see cref="VbaProcedureBlock"/> per Sub / Function / Property / Declare. The
/// scanner pre-strips line comments before its detection regex so commented-out
/// signatures (<c>' Sub Foo()</c>) are ignored. Bodies are captured verbatim
/// from raw lines (comments preserved).
/// </summary>
public static class VbaModuleSplitter
{
    private static readonly Regex StartLineRegex = new(
        @"^\s*(?:(?:Public|Private|Friend|Global)\s+)?(?:Static\s+)?(?:Sub|Function|Property|Declare)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndRegex = new(
        @"^\s*End\s+(?:Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeclarePrefixRegex = new(
        @"^\s*(?:(?:Public|Private)\s+)?Declare\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<VbaProcedureBlock> Split(string vbaText)
    {
        if (string.IsNullOrWhiteSpace(vbaText)) return Array.Empty<VbaProcedureBlock>();

        var lines = vbaText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var results = new List<VbaProcedureBlock>();
        int i = 0;

        while (i < lines.Length)
        {
            string strippedI = VbaSignatureParser.StripComments(lines[i]);
            if (!StartLineRegex.IsMatch(strippedI))
            {
                i++;
                continue;
            }

            int startLine = i + 1; // 1-based

            // Fold any line continuations into the logical signature line.
            var sigBuilder = new StringBuilder();
            int sigEnd = i;
            while (sigEnd < lines.Length)
            {
                string sigStripped = VbaSignatureParser.StripComments(lines[sigEnd]).TrimEnd();
                if (sigStripped.EndsWith(" _", StringComparison.Ordinal))
                {
                    sigBuilder.Append(sigStripped, 0, sigStripped.Length - 2);
                    sigBuilder.Append(' ');
                    sigEnd++;
                }
                else
                {
                    sigBuilder.Append(sigStripped);
                    break;
                }
            }
            string signatureLine = sigBuilder.ToString().Trim();

            // Declare statements have no body and no End line — capture nothing
            // and advance past the (possibly multi-line) declaration.
            bool isDeclare = DeclarePrefixRegex.IsMatch(signatureLine);
            if (isDeclare)
            {
                results.Add(new VbaProcedureBlock(startLine, signatureLine, ""));
                i = sigEnd + 1;
                continue;
            }

            // Walk forward for the body. Stop at matching End ... or the next
            // procedure start (defensive — handles malformed source).
            int bodyEnd = sigEnd;
            int j = sigEnd + 1;
            while (j < lines.Length)
            {
                string strippedJ = VbaSignatureParser.StripComments(lines[j]);
                if (EndRegex.IsMatch(strippedJ)) { bodyEnd = j; break; }
                if (StartLineRegex.IsMatch(strippedJ)) { bodyEnd = j - 1; break; }
                bodyEnd = j;
                j++;
            }

            var bodyBuilder = new StringBuilder();
            for (int k = i; k <= bodyEnd && k < lines.Length; k++)
            {
                if (k > i) bodyBuilder.Append('\n');
                bodyBuilder.Append(lines[k]);
            }

            results.Add(new VbaProcedureBlock(startLine, signatureLine, bodyBuilder.ToString()));
            i = bodyEnd + 1;
        }

        return results;
    }
}
