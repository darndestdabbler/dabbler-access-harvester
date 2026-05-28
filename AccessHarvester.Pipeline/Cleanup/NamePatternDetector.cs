using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AccessHarvester.Pipeline.UsageIdentification;

namespace AccessHarvester.Pipeline.Cleanup;

/// <summary>Name-pattern signal detector — see strategy doc §3.1.</summary>
public static class NamePatternDetector
{
    // (?i) is scoped to the vocabulary alternation only. A global (?i) would also
    // case-fold the [a-z] / [A-Z] character classes in the boundary lookarounds, which
    // collapses the CamelCase transition check into a generic letter-letter check and
    // produces false positives like "tEmp" matching inside "Employee". Calibrated in
    // Session 3 against NorthwindDevEdition; see object-cleanup-strategy.md §3.1.1.
    private static readonly Regex VocabularyPattern =
        new(@"(?:^|[^A-Za-z]|(?<=[a-z])(?=[A-Z]))(?i:Old|Copy|Bak|Archive|Tmp|Temp|Hold|Save|Backup|Snapshot)(?:$|[^A-Za-z]|(?<=[a-z])(?=[A-Z]))",
            RegexOptions.Compiled);

    private static readonly Regex YearPattern =
        new(@"(?:^|[^0-9A-Za-z])((?:19|20)\d{2})(?:$|[^0-9A-Za-z])",
            RegexOptions.Compiled);

    private static readonly Regex YearMonthPattern =
        new(@"(?i)(?:^|[^0-9A-Za-z])(?:((?:19|20)\d{2})[-_]?(0[1-9]|1[0-2]|Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)|(0[1-9]|1[0-2]|Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[-_]?((?:19|20)\d{2}))(?:$|[^0-9A-Za-z])",
            RegexOptions.Compiled);

    private static readonly Regex QuarterPattern =
        new(@"(?i)(?:^|[^0-9A-Za-z])(?:Q([1-4])[-_]?((?:19|20)\d{2})|((?:19|20)\d{2})[-_]?Q([1-4]))(?:$|[^0-9A-Za-z])",
            RegexOptions.Compiled);

    private static readonly Regex NumericSuffixPattern =
        new(@"^(.+?)(\d+)$", RegexOptions.Compiled);

    private static readonly string[] ConventionalPrefixes =
        ["tbl", "qry", "frm", "rpt", "mcr", "bas", "cls"];

    /// <summary>One signal per fired pattern. Empty list when nothing fires.</summary>
    public static IReadOnlyList<NamePatternSignal> Detect(string objectName, string objectType,
        IEnumerable<(string Name, string Type)> peerObjects)
    {
        var signals = new List<NamePatternSignal>();
        if (string.IsNullOrEmpty(objectName)) return signals;

        // matchedSubstring uses the full regex match (Value), with only the trailing
        // boundary char trimmed when consumed by [^A-Za-z]/[^0-9A-Za-z]. The leading
        // boundary char is intentionally retained (e.g., '_OLD' rather than 'OLD',
        // '_2023' rather than '2023') so the recorded substring faithfully reflects
        // what was matched against the object name — see strategy §3.1.6 and §5 example.
        var vocab = VocabularyPattern.Match(objectName);
        if (vocab.Success)
        {
            string matched = TrimTrailingBoundary(vocab.Value);
            signals.Add(new NamePatternSignal
            {
                Summary = $"Matched 'vocabulary' pattern: substring '{matched}'",
                MatchedSubstring = matched,
                Category = "vocabulary",
            });
        }

        var year = YearPattern.Match(objectName);
        if (year.Success)
        {
            string matched = TrimTrailingBoundary(year.Value);
            signals.Add(new NamePatternSignal
            {
                Summary = $"Matched 'year' pattern: substring '{matched}'",
                MatchedSubstring = matched,
                Category = "year",
            });
        }

        var ym = YearMonthPattern.Match(objectName);
        if (ym.Success)
        {
            string matched = TrimTrailingBoundary(ym.Value);
            signals.Add(new NamePatternSignal
            {
                Summary = $"Matched 'year-month' pattern: substring '{matched}'",
                MatchedSubstring = matched,
                Category = "year-month",
            });
        }

        var q = QuarterPattern.Match(objectName);
        if (q.Success)
        {
            string matched = TrimTrailingBoundary(q.Value);
            signals.Add(new NamePatternSignal
            {
                Summary = $"Matched 'quarter' pattern: substring '{matched}'",
                MatchedSubstring = matched,
                Category = "quarter",
            });
        }

        if (TryNumericDuplicate(objectName, objectType, peerObjects, out string suffix))
        {
            signals.Add(new NamePatternSignal
            {
                Summary = $"Matched 'numeric-duplicate' pattern: suffix '{suffix}' with sibling base name",
                MatchedSubstring = suffix,
                Category = "numeric-duplicate",
            });
        }

        return signals;
    }

    private static string TrimTrailingBoundary(string raw)
    {
        int end = raw.Length;
        while (end > 0 && !char.IsLetterOrDigit(raw[end - 1])) end--;
        return raw[..end];
    }

    private static bool TryNumericDuplicate(string objectName, string objectType,
        IEnumerable<(string Name, string Type)> peerObjects, out string suffix)
    {
        suffix = string.Empty;
        (string prefix, string tail) = SplitPrefix(objectName);
        var m = NumericSuffixPattern.Match(tail);
        if (!m.Success) return false;
        string baseName = m.Groups[1].Value;
        suffix = m.Groups[2].Value;
        if (string.IsNullOrEmpty(baseName)) return false;

        foreach (var (peerName, peerType) in peerObjects)
        {
            if (!string.Equals(peerType, objectType, StringComparison.Ordinal)) continue;
            if (string.Equals(peerName, objectName, StringComparison.Ordinal)) continue;
            (string peerPrefix, string peerTail) = SplitPrefix(peerName);
            if (!string.Equals(peerPrefix, prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(peerTail, baseName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static (string Prefix, string Tail) SplitPrefix(string name)
    {
        foreach (var pfx in ConventionalPrefixes)
        {
            if (name.Length > pfx.Length
                && name.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
            {
                return (name[..pfx.Length], name[pfx.Length..]);
            }
        }
        return (string.Empty, name);
    }

    /// <summary>True when the object name is a system object excluded by §7.1 / §7.3.</summary>
    public static bool IsExcludedSystemObject(string objectType, string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return true;
        switch (objectType)
        {
            case "Table":
                return objectName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)
                    || objectName.StartsWith("USys", StringComparison.OrdinalIgnoreCase);
            case "Query":
                return objectName.StartsWith("~sq_", StringComparison.OrdinalIgnoreCase)
                    || objectName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase);
            case "Form":
            case "Report":
                return objectName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase);
            case "Macro":
                return objectName.StartsWith("MSysNavPane", StringComparison.OrdinalIgnoreCase);
            case "Module":
                return IsCodeBehindModuleName(objectName);
        }
        return false;
    }

    /// <summary>True when the module name is event-handler code-behind per §7.3.</summary>
    public static bool IsCodeBehindModuleName(string moduleName)
        => moduleName.StartsWith("Form_", StringComparison.Ordinal)
        || moduleName.StartsWith("Report_", StringComparison.Ordinal);
}
