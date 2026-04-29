using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AccessHarvester.SymbolDb.Extraction;

// Computes body_hash_md5 for a procedure source body. Hash equivalence is
// the duplicate-detection signal; small textual variations that do not
// change behavior (extra blank lines, whitespace, comments, leading line
// numbers Access generates for error-trapping) must hash identically so
// the AI interpretation pipeline can copy interpretations across twins.
//
// The comment / line-number / whitespace stripping is implemented as a
// single-pass token-aware walker rather than a regex chain because VBA
// string literals can contain apostrophes (e.g. "Bob's") and `REM` as a
// substring; a naive regex would treat them as comment starts and produce
// false duplicate matches. The walker tracks string-literal state and only
// recognises a comment when the apostrophe / REM sits outside a string.
public static class BodyHasher
{
    // Match a leading line number on a code line ("10", "100", etc.) followed
    // by whitespace. Line numbers in VBA are an Access-generated artifact for
    // ERL() error reporting and never affect semantics.
    private static readonly Regex LeadingLineNumber = new(
        @"^\s*\d+\s+", RegexOptions.Compiled | RegexOptions.Multiline);

    // Collapse runs of whitespace (including newlines) to a single space.
    private static readonly Regex Whitespace = new(
        @"\s+", RegexOptions.Compiled);

    public static string Compute(string source)
    {
        string normalized = Normalize(source);
        byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Normalize(string source)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        string s = LeadingLineNumber.Replace(source, string.Empty);
        s = StripVbaComments(s);
        s = Whitespace.Replace(s, " ");
        return s.Trim();
    }

    // VBA comment grammar:
    //   * `'` outside a string starts an end-of-line comment.
    //   * `REM` outside a string at statement-start position
    //     (line-start or after `:`) starts an end-of-line comment.
    // VBA string literal grammar:
    //   * `"..."` with `""` as the embedded-quote escape.
    private static string StripVbaComments(string source)
    {
        var result = new StringBuilder(source.Length);
        bool inString = false;
        bool atStatementStart = true; // matters for REM detection only
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];

            if (inString)
            {
                result.Append(c);
                if (c == '"')
                {
                    // VBA string literal escape is ""; if the next char is also
                    // a quote, consume both as an escaped quote and stay inside.
                    if (i + 1 < source.Length && source[i + 1] == '"')
                    {
                        result.Append('"');
                        i += 2;
                        continue;
                    }
                    inString = false;
                }
                i++;
                continue;
            }

            // Outside a string.
            if (c == '"')
            {
                inString = true;
                atStatementStart = false;
                result.Append(c);
                i++;
                continue;
            }

            if (c == '\'')
            {
                // Comment to end-of-line. Skip until we hit a newline or EOF.
                while (i < source.Length && source[i] != '\r' && source[i] != '\n') i++;
                atStatementStart = true; // newline is implicit
                continue;
            }

            // REM keyword detection. Must be at statement start (line start or
            // immediately after a `:` statement separator) and followed by a
            // whitespace / EOL boundary so substrings like `RemX` are not
            // misclassified.
            if (atStatementStart
                && (c == 'R' || c == 'r')
                && i + 2 < source.Length
                && (source[i + 1] == 'E' || source[i + 1] == 'e')
                && (source[i + 2] == 'M' || source[i + 2] == 'm')
                && (i + 3 == source.Length || IsRemBoundary(source[i + 3])))
            {
                while (i < source.Length && source[i] != '\r' && source[i] != '\n') i++;
                atStatementStart = true;
                continue;
            }

            // Track statement-start state for the next char. Line-end and
            // colon both reset to "next position is a statement start". Any
            // non-whitespace char clears the flag.
            if (c == '\r' || c == '\n' || c == ':')
            {
                atStatementStart = true;
            }
            else if (!char.IsWhiteSpace(c))
            {
                atStatementStart = false;
            }

            result.Append(c);
            i++;
        }
        return result.ToString();
    }

    private static bool IsRemBoundary(char c)
        => char.IsWhiteSpace(c) || c == ':' || c == '\r' || c == '\n';
}
