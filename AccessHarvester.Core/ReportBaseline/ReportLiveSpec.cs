using System.Collections.Generic;
using System.Collections.Immutable;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// What `report-baseline` needs to know about a single report at runtime to
/// drive an aggregate capture: the bound RecordSource and the
/// outermost-first list of group-level keys. Everything else carried by
/// <see cref="AccessHarvester.Access.ReportMetadata"/> (PageSetup,
/// ModuleName, control tree, ...) is out of scope for baseline capture.
/// </summary>
public sealed record ReportLiveSpec
{
    public required string ReportName { get; init; }

    /// <summary>
    /// The RecordSource string Access stores on the report. <c>null</c> or
    /// empty for unbound reports — the runner records those as
    /// <c>Skipped</c> with reason <c>UnboundReport</c>.
    /// </summary>
    public string? RecordSource { get; init; }

    /// <summary>
    /// Group-level keys, outermost first. Empty when the report has no
    /// group levels.
    /// </summary>
    public IReadOnlyList<ReportGroupLevelKey> GroupLevels { get; init; }
        = ImmutableArray<ReportGroupLevelKey>.Empty;
}
