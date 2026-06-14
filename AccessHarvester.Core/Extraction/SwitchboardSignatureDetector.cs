using System;
using System.Collections.Generic;
using System.Linq;

namespace AccessHarvester.Core.Extraction;

/// <summary>
/// Detects a switchboard-shaped table by <b>column signature, not table
/// name</b>: a table is a switchboard when its field set contains the
/// canonical Switchboard Manager columns
/// (<c>SwitchboardID, ItemNumber, ItemText, Command, Argument</c>),
/// tolerating extras such as <c>ParentID</c>. Locale- and name-independent;
/// matches both the wizard-generated <c>Switchboard Items</c> table and
/// hand-rolled custom variants, and yields zero false positives on
/// shape-mismatched tables.
/// </summary>
public static class SwitchboardSignatureDetector
{
    /// <summary>
    /// The canonical column names a table must carry (case-insensitively)
    /// to be flagged as a switchboard. Order is irrelevant; extras are
    /// tolerated.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalColumns = new[]
    {
        "SwitchboardID", "ItemNumber", "ItemText", "Command", "Argument",
    };

    /// <summary>The optional hierarchy column some custom tables carry.</summary>
    public const string ParentIdColumn = "ParentID";

    /// <summary>
    /// True when <paramref name="fieldNames"/> contains every canonical
    /// column (case-insensitive). Null/whitespace entries are ignored.
    /// </summary>
    public static bool IsSwitchboardTable(IEnumerable<string?> fieldNames)
    {
        if (fieldNames is null) throw new ArgumentNullException(nameof(fieldNames));
        var present = new HashSet<string>(
            fieldNames.Where(n => !string.IsNullOrWhiteSpace(n))!,
            StringComparer.OrdinalIgnoreCase);
        return CanonicalColumns.All(present.Contains);
    }

    /// <summary>
    /// True when <paramref name="fieldNames"/> contains a
    /// <c>ParentID</c> column (case-insensitive). Used to decide the
    /// page-grouping key.
    /// </summary>
    public static bool HasParentIdColumn(IEnumerable<string?> fieldNames)
    {
        if (fieldNames is null) throw new ArgumentNullException(nameof(fieldNames));
        return fieldNames.Any(n =>
            !string.IsNullOrWhiteSpace(n)
            && string.Equals(n, ParentIdColumn, StringComparison.OrdinalIgnoreCase));
    }
}
