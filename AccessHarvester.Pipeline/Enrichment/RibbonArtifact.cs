using System;
using System.Collections.Generic;

namespace AccessHarvester.Pipeline.Enrichment;

/// <summary>
/// Serialization shape of <c>raw/Navigation/ribbon-xml.json</c> — the
/// Access-native, lossless row dump of every <c>USysRibbons</c>-shaped table
/// (detected by column signature, not name: a table carrying both a
/// <c>RibbonName</c> and a <c>RibbonXML</c> column). Shared by the extractor
/// (<see cref="Extraction.RibbonExporter"/>, writes it) and the manifest build
/// (reads it via <see cref="RibbonStructureBuilder"/>).
/// </summary>
/// <remarks>
/// A faithful row dump, not a structured view: each row's raw customUI XML is
/// preserved exactly as stored. Parsing the XML into a
/// <see cref="RibbonStructure"/> happens downstream in the pure
/// <see cref="RibbonXmlParser"/>, so the raw tier stays a verbatim copy and
/// every authored ribbon definition (even ones the singular
/// <c>NavigationGraph.Ribbon</c> view does not surface) is recoverable here.
/// </remarks>
public sealed record RibbonXmlArtifact
{
    /// <summary>Always <c>"usys-ribbons-row"</c>.</summary>
    public string Source { get; init; } = "usys-ribbons-row";

    /// <summary>
    /// Names of the tables flagged as ribbon-shaped, in the order the
    /// extractor encountered them (sorted). Empty when none were detected.
    /// </summary>
    public IReadOnlyList<string> DetectedTables { get; init; } = Array.Empty<string>();

    /// <summary>One entry per <c>(table, RibbonName)</c> row, carrying the raw XML.</summary>
    public IReadOnlyList<RibbonXmlRow> Rows { get; init; } = Array.Empty<RibbonXmlRow>();
}

/// <summary>One raw <c>USysRibbons</c> row: a named ribbon definition.</summary>
public sealed record RibbonXmlRow
{
    /// <summary>The source table the row came from (e.g. <c>"USysRibbons"</c>).</summary>
    public required string Table { get; init; }

    /// <summary>
    /// The <c>RibbonName</c> value (the ribbon's name, e.g. <c>"Main"</c>), or
    /// null when the column was null.
    /// </summary>
    public string? RibbonName { get; init; }

    /// <summary>
    /// The verbatim <c>RibbonXML</c> customUI markup, or null when the column
    /// was null. Preserved exactly as stored; parsed downstream.
    /// </summary>
    public string? RibbonXml { get; init; }
}
