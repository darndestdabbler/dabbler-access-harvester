using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Serialization shape of <c>raw/Navigation/switchboard-items.json</c> —
/// the Access-native, lossless row dump of every switchboard-shaped table
/// the extractor detected (by column signature, not table name). Shared by
/// the extractor (writes it) and the manifest build (reads it via
/// <see cref="SwitchboardStructureBuilder"/>).
/// </summary>
/// <remarks>
/// This is a faithful row dump, not a structured view: rows are preserved
/// exactly as read (all canonical columns plus <c>ParentID</c> when the
/// table carries it), so the downstream consumer can document a canonical
/// <i>or</i> a hand-rolled custom switchboard from the same artifact.
/// </remarks>
public sealed record SwitchboardItemsArtifact
{
    /// <summary>Always <c>"switchboard-row"</c>.</summary>
    public string Source { get; init; } = "switchboard-row";

    /// <summary>
    /// Names of the tables flagged as switchboard-shaped, in the order
    /// the extractor encountered them. Empty when none were detected.
    /// </summary>
    public IReadOnlyList<string> DetectedTables { get; init; } = Array.Empty<string>();

    /// <summary>One entry per detected table, carrying its rows.</summary>
    public IReadOnlyList<SwitchboardTableRows> Tables { get; init; } = Array.Empty<SwitchboardTableRows>();
}

/// <summary>The materialised rows of one switchboard-shaped table.</summary>
public sealed record SwitchboardTableRows
{
    /// <summary>Table name.</summary>
    public required string Table { get; init; }

    /// <summary>
    /// True when the table carries a <c>ParentID</c> column (a
    /// hand-rolled hierarchy table). Drives the page-grouping rule:
    /// <c>ParentID</c> when present, else <c>SwitchboardID</c>.
    /// </summary>
    public bool HasParentId { get; init; }

    /// <summary>
    /// Rows in the bounded SELECT order
    /// (<c>SwitchboardID, ItemNumber</c>).
    /// </summary>
    public required IReadOnlyList<SwitchboardRawRow> Rows { get; init; }
}

/// <summary>
/// One raw switchboard row. Every column is nullable because Access
/// permits nulls / zero-length strings in these tables; preserving the
/// distinction is the point of a faithful dump.
/// </summary>
public sealed record SwitchboardRawRow
{
    public int? SwitchboardID { get; init; }
    public int? ItemNumber { get; init; }
    public string? ItemText { get; init; }
    public int? Command { get; init; }
    public string? Argument { get; init; }

    /// <summary>The <c>ParentID</c> value, when the table has that column.</summary>
    public int? ParentID { get; init; }
}

/// <summary>
/// Serialization shape of <c>raw/Navigation/startup-options.json</c> —
/// database-level startup options read from DAO database properties.
/// </summary>
public sealed record StartupOptionsArtifact
{
    /// <summary>
    /// The Access <c>StartUpForm</c> database property (the "Display
    /// Form/Page" startup option), or null when no startup form is set
    /// (or the property is absent).
    /// </summary>
    public string? StartUpForm { get; init; }
}
