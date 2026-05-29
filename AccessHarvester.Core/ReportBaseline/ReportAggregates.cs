using System.Collections.Generic;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// The aggregate baseline for a single report: row count, per-numeric-column
/// grand totals, per-group-level bucket totals, plus provenance metadata.
///
/// <para>
/// <strong>Contract:</strong> aggregates reflect the report's
/// <c>RecordSource</c> result set as run through ACE OLE DB, not the rendered
/// report's footer-control expressions. A report whose footer evaluates
/// <c>=Sum(Price*Qty)</c> over a derived column will not match the
/// <see cref="GrandTotals"/> here; downstream parity-check consumers must
/// compare like-for-like (query-result vs. query-result). See
/// <see cref="ReportAggregateProvenance.ContractNote"/>.
/// </para>
/// </summary>
public sealed record ReportAggregates
{
    /// <summary>The report's name as Access stores it.</summary>
    public required string ReportName { get; init; }

    /// <summary>The raw RecordSource string the report was bound to.</summary>
    public required string RecordSource { get; init; }

    /// <summary>
    /// How <see cref="RecordSource"/> was classified before execution
    /// (string form of <see cref="ReportBaseline.RecordSourceKind"/>).
    /// </summary>
    public required string RecordSourceKind { get; init; }

    /// <summary>The SQL actually executed against ACE OLE DB.</summary>
    public required string ResolvedSql { get; init; }

    /// <summary>Total rows returned by <see cref="ResolvedSql"/>.</summary>
    public required long RowCount { get; init; }

    /// <summary>
    /// Names of the columns the aggregator treated as numeric. Empty when
    /// the result set contains no numeric columns.
    /// </summary>
    public required IReadOnlyList<string> NumericColumns { get; init; }

    /// <summary>
    /// Grand totals per numeric column, keyed by column name. Enumerated
    /// in ordinal-sorted key order so the serialized JSON is byte-stable.
    /// A null value indicates the column sum could not be computed (e.g.
    /// overflow when projecting a wide <see cref="double"/> into
    /// <see cref="decimal"/>).
    /// </summary>
    public required IReadOnlyDictionary<string, decimal?> GrandTotals { get; init; }

    /// <summary>
    /// Per-group-level breakdowns. Ordered by group level (outermost first).
    /// Empty when no group levels were supplied or every level was an
    /// expression (see <see cref="Skipped"/>).
    /// </summary>
    public required IReadOnlyList<ReportGroupLevelAggregate> GroupLevels { get; init; }

    /// <summary>
    /// Items the aggregator could not compute. Possible <c>Kind</c> values:
    /// <c>GroupLevelExpression</c> (Access <c>=...</c> expressions),
    /// <c>GroupLevelMissingColumn</c> (the named GroupOn column is absent
    /// from the executed result set), and <c>NumericOverflow</c> (a
    /// numeric-column sum could not fit in <see cref="decimal"/>). Empty
    /// when nothing was skipped.
    /// </summary>
    public required IReadOnlyList<SkippedItem> Skipped { get; init; }

    /// <summary>
    /// Determinism + contract provenance: harvester version, aggregate
    /// contract name, the parameter values used.
    /// </summary>
    public required ReportAggregateProvenance Provenance { get; init; }
}

/// <summary>
/// Per-group-level aggregate: one bucket per distinct value of
/// <see cref="GroupOn"/>, each bucket carrying its row count and per-column
/// subtotals.
/// </summary>
public sealed record ReportGroupLevelAggregate
{
    /// <summary>Zero-based group level (0 = outermost).</summary>
    public required int Level { get; init; }

    /// <summary>The field name the group is keyed on.</summary>
    public required string GroupOn { get; init; }

    /// <summary>
    /// One bucket per distinct value of <see cref="GroupOn"/> in the result
    /// set. Order is stable: ordered by <see cref="GroupBucket.Key"/>
    /// using ordinal string comparison so two runs over the same data
    /// produce byte-identical JSON.
    /// </summary>
    public required IReadOnlyList<GroupBucket> Buckets { get; init; }
}

/// <summary>One row of a per-group-level aggregate.</summary>
/// <remarks>
/// The pair (<see cref="IsNull"/>, <see cref="Key"/>) is the bucket's
/// identity. <see cref="IsNull"/> being true means the source value was
/// <see cref="DBNull"/> or CLR null; in that case <see cref="Key"/> is
/// the empty string and downstream consumers must not interpret
/// <see cref="Key"/> as a real data value. A row whose <em>actual</em>
/// string value is the empty string is also encoded with
/// <see cref="Key"/> = "" but <see cref="IsNull"/> = false — the flag
/// is what distinguishes them. This prevents a real string value (e.g.
/// the literal <c>"(null)"</c>) from collapsing into the null bucket.
/// </remarks>
public sealed record GroupBucket
{
    /// <summary>
    /// <c>true</c> when the source value for this bucket was
    /// <see cref="DBNull"/> or CLR null. When <c>true</c>, <see cref="Key"/>
    /// is the empty string and carries no data meaning.
    /// </summary>
    public required bool IsNull { get; init; }

    /// <summary>
    /// The group key, formatted as an invariant-culture string when
    /// <see cref="IsNull"/> is <c>false</c>; the empty string when
    /// <see cref="IsNull"/> is <c>true</c>. <see cref="DateTime"/> values
    /// use the round-trip <c>"O"</c> format so two runs match
    /// deterministically.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Number of rows in this bucket.</summary>
    public required long RowCount { get; init; }

    /// <summary>
    /// Per-numeric-column subtotals for this bucket. Keyed by column
    /// name, enumerated in ordinal-sorted key order so the serialized
    /// JSON is byte-stable. A null value matches the null semantics of
    /// <see cref="ReportAggregates.GrandTotals"/> — the column sum could
    /// not be computed (e.g. overflow when projecting a wide
    /// <see cref="double"/> into <see cref="decimal"/>).
    /// </summary>
    public required IReadOnlyDictionary<string, decimal?> Subtotals { get; init; }
}

/// <summary>
/// One thing the aggregator chose not to compute, with enough detail for a
/// downstream consumer to decide whether to escalate it (e.g. wire a future
/// session set to add expression evaluation).
/// </summary>
public sealed record SkippedItem
{
    /// <summary>
    /// One of <c>GroupLevelExpression</c>, <c>NumericOverflow</c>. Extend
    /// the enum-shaped string set rather than break the contract.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Human-readable detail: what was skipped and why.</summary>
    public required string Detail { get; init; }
}

/// <summary>
/// Determinism + contract metadata for an <see cref="ReportAggregates"/>
/// document.
/// </summary>
public sealed record ReportAggregateProvenance
{
    /// <summary>
    /// The harvester version that produced this document. Sourced from
    /// <c>AccessHarvester.Pipeline.Manifest.DatabaseManifest.ResolveHarvesterVersion()</c>
    /// by callers — the computer is purely functional and trusts the
    /// value passed in.
    /// </summary>
    public required string HarvesterVersion { get; init; }

    /// <summary>
    /// The aggregate contract this document conforms to. The S1 value is
    /// always <c>RecordSourceResultSet</c>; if a future session adds
    /// footer-expression evaluation it will mint a new value.
    /// </summary>
    public required string AggregateContract { get; init; }

    /// <summary>
    /// Human-readable note describing the contract's semantics, intended
    /// to be read by a downstream AI consumer when interpreting the
    /// document. Identical for every document at a given contract version.
    /// </summary>
    public required string ContractNote { get; init; }

    /// <summary>
    /// The parameter values used to execute
    /// <see cref="ReportAggregates.ResolvedSql"/>, keyed by parameter
    /// name. Enumerated in ordinal-sorted key order so the serialized
    /// JSON is byte-stable; snapshotted at compute-time so a caller
    /// mutating the original dictionary after the call does not corrupt
    /// provenance. Empty when the report ran unfiltered.
    ///
    /// <para>
    /// <strong>Provenance shape, not execution binding contract.</strong>
    /// Access OLE DB text commands bind parameters <em>positionally</em>,
    /// not by name; the name->value pairs here document what each
    /// positional slot represented at execution time. The positional
    /// binding contract lives on <c>OleDbReportQueryExecutor.Execute</c>.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, object?> ParametersUsed { get; init; }
}
