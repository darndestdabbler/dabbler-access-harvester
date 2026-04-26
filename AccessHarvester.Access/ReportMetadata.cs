using System.Text.Json;

namespace AccessHarvester.Access;

/// <summary>
/// Structured metadata for a single Access report, covering the deliverables
/// of Plan Phase 4.2. Mirrors <see cref="FormMetadata"/> for the fields forms
/// and reports share (name, module, bound data source, filter / order, raw
/// property bag, geometry, design tree, code-behind) and adds report-only
/// metadata: <see cref="GroupLevels"/> and <see cref="PageSetup"/>.
/// </summary>
/// <remarks>
/// Field promotion follows the same exact-value-vs-decoded rule as
/// <see cref="FormMetadata"/>: <see cref="RecordSource"/>,
/// <see cref="Filter"/>, <see cref="OrderBy"/> are exact-value lifts (removed
/// from <see cref="Properties"/> when promoted); <see cref="DefaultView"/> is a
/// decoded lift (the raw numeric code is retained in <see cref="Properties"/>).
/// Subreports appear inside <see cref="Children"/> as <c>Subreport</c> control
/// nodes referencing their contained report by <c>SourceObject</c>; the
/// contained report is extracted separately as its own
/// <see cref="ReportMetadata"/>, never inlined — see
/// <see cref="AccessControl.Children"/>.
/// </remarks>
public sealed record ReportMetadata
{
    /// <summary>The report's name as Access stores it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The VBA class module backing this report (typically
    /// <c>Report_&lt;Name&gt;</c>), or null when there is no code-behind. Used
    /// in Plan Phase 4.4 the same way <see cref="FormMetadata.ModuleName"/> is
    /// for forms.
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// The report's bound data source: a table name, a saved query name, or a
    /// SQL string. Null for unbound reports.
    /// </summary>
    public string? RecordSource { get; init; }

    /// <summary>
    /// The Filter clause Access applies on top of <see cref="RecordSource"/>,
    /// in JET SQL <c>WHERE</c>-fragment form. Null when no filter is set.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// The report's top-level Order By, in JET SQL <c>ORDER BY</c>-fragment
    /// form. Null when no top-level sort is set; per-group sorts live in
    /// <see cref="GroupLevels"/>.
    /// </summary>
    public string? OrderBy { get; init; }

    /// <summary>
    /// The view Access opens this report in by default (typically
    /// <c>Print Preview</c> or <c>Report View</c>). Decoded from Access's
    /// numeric code; the raw code is preserved in <see cref="Properties"/>
    /// per the decoded-lift rule in the type-level remarks.
    /// </summary>
    public string? DefaultView { get; init; }

    /// <summary>
    /// The report-level property bag as Access wrote it, with exact-value
    /// lifts removed (<see cref="RecordSource"/>, <see cref="Filter"/>,
    /// <see cref="OrderBy"/>) but decoded lifts left intact (the raw numeric
    /// <c>DefaultView</c> code remains here alongside the decoded
    /// <see cref="DefaultView"/> label). See <see cref="AccessControl.Properties"/>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>Inch-converted report-level geometry.</summary>
    public GeometryInches? GeometryIn { get; init; }

    /// <summary>
    /// Report-level properties that were the literal <c>"NotDefault"</c>
    /// sentinel in the source.
    /// </summary>
    public IReadOnlyList<string>? NonDefaultProperties { get; init; }

    /// <summary>
    /// The report's design tree: page header / footer, group headers /
    /// footers, detail section, and the controls within each section.
    /// Subreports appear as <c>Subreport</c> control nodes referencing their
    /// contained report by <c>SourceObject</c> — see
    /// <see cref="AccessControl.Children"/> for the acyclic-by-reference
    /// contract that single-sources this rule.
    /// </summary>
    public IReadOnlyList<AccessControl>? Children { get; init; }

    /// <summary>
    /// The report's grouping / sorting metadata, ordered outermost first
    /// (<see cref="GroupLevel.Level"/> = 0 is the outermost group). Forms
    /// have no analogue; <see cref="OrderBy"/> alone covers their sorting.
    /// Null when the report has no group levels.
    /// </summary>
    public IReadOnlyList<GroupLevel>? GroupLevels { get; init; }

    /// <summary>
    /// Page setup (orientation, paper, margins) sourced from the
    /// <c>PrtMip</c> / <c>PrtDevMode</c> structures Access stores. Decoded
    /// best-effort; absent fields remain null.
    /// </summary>
    public PageSetup? PageSetup { get; init; }

    /// <summary>The complete VBA code-behind module text, or null.</summary>
    public string? VbaCode { get; init; }
}

/// <summary>
/// One grouping / sorting level in a report. Reports use these to drive group
/// header and footer sections (named <c>GroupLevel0Header</c> /
/// <c>GroupLevel0Footer</c> in the design tree) and to keep records together.
/// </summary>
/// <remarks>
/// Sparse: only fields Access actually wrote are non-null. The
/// <see cref="HasHeader"/> / <see cref="HasFooter"/> flags are derived rather
/// than direct properties — extractors set them based on whether a matching
/// header / footer section exists in the report's <c>Children</c>.
/// </remarks>
public sealed record GroupLevel
{
    /// <summary>
    /// Zero-based grouping level (0 = outermost). Matches the suffix on the
    /// <c>GroupLevel&lt;N&gt;Header</c> / <c>...Footer</c> section names in
    /// the design tree.
    /// </summary>
    public required int Level { get; init; }

    /// <summary>
    /// The field name or expression the group is keyed on — e.g. a column
    /// from <see cref="ReportMetadata.RecordSource"/>, or an expression like
    /// <c>=Year([OrderDate])</c>.
    /// </summary>
    public string? GroupOn { get; init; }

    /// <summary>
    /// One of Access's grouping modes — <c>EachValue</c>, <c>Prefix</c>,
    /// <c>Year</c>, <c>Quarter</c>, <c>Month</c>, <c>Week</c>, <c>Day</c>,
    /// <c>Hour</c>, <c>Minute</c>, <c>Interval</c>. Lifted from the
    /// underlying Access numeric code.
    /// </summary>
    public string? GroupingMode { get; init; }

    /// <summary>
    /// Group interval value when <see cref="GroupingMode"/> is <c>Prefix</c>
    /// or <c>Interval</c> (e.g. group by first 3 characters, or every N
    /// units). Null otherwise.
    /// </summary>
    public int? GroupInterval { get; init; }

    /// <summary>
    /// One of <c>Ascending</c> or <c>Descending</c>. Null when Access did not
    /// set an explicit direction.
    /// </summary>
    public string? SortOrder { get; init; }

    /// <summary>True when a matching <c>GroupLevel&lt;N&gt;Header</c> section exists.</summary>
    public bool? HasHeader { get; init; }

    /// <summary>True when a matching <c>GroupLevel&lt;N&gt;Footer</c> section exists.</summary>
    public bool? HasFooter { get; init; }

    /// <summary>
    /// Access's <c>KeepTogether</c> flag for this level — typically
    /// <c>WholeGroup</c>, <c>WithFirstDetail</c>, or <c>None</c>.
    /// </summary>
    public string? KeepTogether { get; init; }
}

/// <summary>
/// Page-setup metadata for a report (orientation, paper size, margins).
/// Sourced from Access's <c>PrtMip</c> (margins, in twips) and
/// <c>PrtDevMode</c> (orientation, paper) binary properties — extractors
/// decode best-effort. Margins are exposed in inches per the
/// "explicit-units in field names" Output JSON Convention.
/// </summary>
public sealed record PageSetup
{
    /// <summary>One of <c>Portrait</c>, <c>Landscape</c>. Null if not decoded.</summary>
    public string? Orientation { get; init; }

    /// <summary>
    /// One of Access's paper-size labels (<c>Letter</c>, <c>Legal</c>,
    /// <c>A4</c>, ...). Null if not decoded.
    /// </summary>
    public string? PaperSize { get; init; }

    /// <summary>The four page margins in inches. Null when no margin block was decoded.</summary>
    public Margins? Margins { get; init; }
}

/// <summary>
/// Page margins in inches. Each side is independently nullable so absent /
/// undecoded sides can be omitted rather than synthesized as zero.
/// </summary>
public sealed record Margins
{
    /// <summary>Top margin in inches.</summary>
    public double? TopIn { get; init; }

    /// <summary>Bottom margin in inches.</summary>
    public double? BottomIn { get; init; }

    /// <summary>Left margin in inches.</summary>
    public double? LeftIn { get; init; }

    /// <summary>Right margin in inches.</summary>
    public double? RightIn { get; init; }
}
