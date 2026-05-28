using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AccessHarvester.Pipeline.Classification;

/// <summary>
/// Derives a canonical format pattern (e.g., <c>###-##-####</c> for SSN,
/// <c>MRN-#######</c> for a medical record number) from a small batch of
/// sampled values WITHOUT keeping the raw values. The output conforms to
/// the schema-imposed structural constraints (plan § 4 and
/// <c>classification-schema.json</c> &#x2192; <c>Column.valueFormatPattern</c>):
/// only uppercase ASCII letters, the placeholder tokens <c>#</c> (digit)
/// and <c>?</c> (any single character), and a small separator set may
/// appear; at least one non-alphabetic placeholder must be present so an
/// all-uppercase literal cannot validate.
/// </summary>
/// <remarks>
/// The detector returns <c>null</c> when the sampled values do not share a
/// consistent shape — that is the schema-friendly signal for "no pattern
/// emerged." Letter-class placeholders (<c>X</c>, <c>A</c>) are deliberately
/// not produced because they collide with real uppercase letters in
/// literal labels like <c>MRN</c> or <c>ZIP</c>; lowercase letters in
/// sampled values are normalized to <c>?</c>.
/// </remarks>
public static class ValueFormatDetector
{
    private static readonly HashSet<char> AllowedSeparators = new() { ' ', ',', '.', '(', ')', '+', ':', '/', '-' };
    private static readonly Regex SchemaPattern = new(@"^[A-Z#? .,()+:/-]*$", RegexOptions.Compiled);
    private static readonly Regex HasPlaceholder = new(@"[#?]",              RegexOptions.Compiled);

    /// <summary>
    /// Returns the canonical format pattern shared by the supplied values, or
    /// <c>null</c> if the values do not converge on one shape or the shape
    /// fails the schema's structural rules.
    /// </summary>
    public static string? Detect(IReadOnlyList<string?> sampledValues)
    {
        if (sampledValues is null) return null;

        string? first = null;
        foreach (var v in sampledValues)
        {
            if (string.IsNullOrEmpty(v)) continue;
            string pattern = Tokenize(v);
            if (first is null) { first = pattern; continue; }
            if (!string.Equals(first, pattern, StringComparison.Ordinal)) return null;
        }
        if (first is null) return null;

        // Final schema-conformance gate: drop the pattern if it would be
        // structurally rejected (e.g., only literal uppercase letters with
        // no placeholder) rather than emit something the writer would
        // refuse later.
        if (!SchemaPattern.IsMatch(first)) return null;
        if (!HasPlaceholder.IsMatch(first)) return null;
        return first;
    }

    private static string Tokenize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (char.IsDigit(c))                                sb.Append('#');
            else if (c >= 'A' && c <= 'Z')                      sb.Append(c);
            else if (c >= 'a' && c <= 'z')                      sb.Append('?'); // collapse to any-char placeholder
            else if (AllowedSeparators.Contains(c))             sb.Append(c);
            else                                                sb.Append('?');
        }
        return sb.ToString();
    }
}
