using System.Collections.Generic;
using System.Collections.Immutable;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Input bundle for <see cref="ReportAggregateComputer.Compute"/>. Carries
/// the report's identity, its raw RecordSource, the group levels to break
/// the result set out by, and the parameter values that drove the
/// execution (recorded verbatim into provenance).
/// </summary>
public sealed record ReportAggregateInput
{
    /// <summary>The report's name as Access stores it.</summary>
    public required string ReportName { get; init; }

    /// <summary>
    /// The raw RecordSource string the report is bound to (object name or
    /// inline SQL). Resolution happens inside the computer; callers do not
    /// need to pre-classify.
    /// </summary>
    public required string RecordSource { get; init; }

    /// <summary>
    /// The report's group levels, as projected from
    /// <c>ReportMetadata.GroupLevels</c>. Levels whose
    /// <see cref="ReportGroupLevelKey.GroupOn"/> is an Access expression
    /// (begins with <c>=</c>) are recorded as a Skipped item rather than
    /// computed, per the S1 query-result-aggregate contract.
    /// </summary>
    public IReadOnlyList<ReportGroupLevelKey> GroupLevels { get; init; }
        = ImmutableArray<ReportGroupLevelKey>.Empty;

    /// <summary>
    /// Parameter values used to execute the RecordSource (empty when the
    /// report runs unfiltered). Recorded verbatim into provenance.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ParametersUsed { get; init; }
        = ImmutableDictionary<string, object?>.Empty;
}

/// <summary>
/// A single group level the report breaks its detail rows out by. Projected
/// from <c>AccessHarvester.Access.GroupLevel</c> — only the fields the
/// aggregate contract needs (level + key field).
/// </summary>
public sealed record ReportGroupLevelKey
{
    /// <summary>Zero-based level; 0 is the outermost group.</summary>
    public required int Level { get; init; }

    /// <summary>
    /// The field name or expression the group is keyed on. Plain field
    /// names produce a populated group bucket; values that begin with
    /// <c>=</c> are recorded as a Skipped item with kind
    /// <c>GroupLevelExpression</c>.
    /// </summary>
    public required string GroupOn { get; init; }
}
