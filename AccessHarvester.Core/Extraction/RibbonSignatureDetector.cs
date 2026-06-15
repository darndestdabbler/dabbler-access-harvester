using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Detects a custom-ribbon table by <b>column signature, not table name</b>:
/// a table is a ribbon-definition table when its field set contains both a
/// <c>RibbonName</c> and a <c>RibbonXML</c> column (case-insensitively),
/// tolerating extras such as the <c>ID</c> autonumber. Name-independent, so it
/// matches the canonical <c>USysRibbons</c> table and any hand-rolled variant,
/// and yields zero false positives on shape-mismatched tables.
/// </summary>
public static class RibbonSignatureDetector
{
    /// <summary>The column carrying the ribbon's name.</summary>
    public const string RibbonNameColumn = "RibbonName";

    /// <summary>The column carrying the customUI XML markup.</summary>
    public const string RibbonXmlColumn = "RibbonXML";

    /// <summary>
    /// The canonical column names a table must carry (case-insensitively) to be
    /// flagged as a custom-ribbon table. Order is irrelevant; extras tolerated.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalColumns = new[]
    {
        RibbonNameColumn, RibbonXmlColumn,
    };

    /// <summary>
    /// True when <paramref name="fieldNames"/> contains every canonical column
    /// (case-insensitive). Null/whitespace entries are ignored.
    /// </summary>
    public static bool IsRibbonTable(IEnumerable<string?> fieldNames)
    {
        if (fieldNames is null) throw new ArgumentNullException(nameof(fieldNames));
        var present = new HashSet<string>(
            fieldNames.Where(n => !string.IsNullOrWhiteSpace(n))!,
            StringComparer.OrdinalIgnoreCase);
        return CanonicalColumns.All(present.Contains);
    }
}
