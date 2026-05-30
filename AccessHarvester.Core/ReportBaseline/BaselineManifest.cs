using System.Collections.Generic;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Top-level index for a report-baseline capture run. One entry per
/// report the run considered (reports filtered out by <c>--reports</c>
/// are not included). Downstream consumers read this instead of walking
/// the directory tree.
///
/// <para>
/// JSON is byte-stable: <see cref="Reports"/> is sorted ordinal-ascending
/// by <see cref="BaselineManifestEntry.Name"/>, per-entry
/// <see cref="BaselineManifestEntry.Paths"/> dictionaries are
/// ordinal-sorted, and no wall-clock timestamps are recorded. The PDF
/// binaries written alongside this manifest are excluded from the
/// determinism gate's byte comparison the way <c>ExportedAt</c> is for
/// the other tiers.
/// </para>
/// </summary>
public sealed record BaselineManifest
{
    /// <summary>The .accdb the run captured against (full path).</summary>
    public required string Database { get; init; }

    /// <summary>The harvester binary version (deterministic provenance).</summary>
    public required string HarvesterVersion { get; init; }

    /// <summary>
    /// Per-report entries, ordered ordinal-ascending by
    /// <see cref="BaselineManifestEntry.Name"/>.
    /// </summary>
    public required IReadOnlyList<BaselineManifestEntry> Reports { get; init; }
}

/// <summary>
/// Single per-report row on the baseline manifest index.
/// </summary>
public sealed record BaselineManifestEntry
{
    /// <summary>The report's name as Access stores it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// One of <c>Captured</c>, <c>Skipped</c>, <c>Failed</c>:
    /// <list type="bullet">
    ///   <item><c>Captured</c> — PDF and aggregates both written.</item>
    ///   <item><c>Skipped</c> — nothing to capture (e.g. unbound report);
    ///   see <see cref="Reason"/>.</item>
    ///   <item><c>Failed</c> — one or both artifacts could not be produced;
    ///   see <see cref="Reason"/>. Any artifacts that did succeed are still
    ///   listed in <see cref="Paths"/>.</item>
    /// </list>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Per-artifact paths relative to the run's reports-baseline root,
    /// keyed by artifact kind (<c>pdf</c>, <c>aggregates</c>) and
    /// ordinal-sorted for byte-stable JSON. Empty when no artifact was
    /// produced.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Paths { get; init; }

    /// <summary>
    /// Why a report was skipped or failed. <c>null</c> when
    /// <see cref="Status"/> is <c>Captured</c>. Skip reasons:
    /// <c>UnboundReport</c>. Failure reasons are
    /// <c>ReadSpecFailed: &lt;message&gt;</c> (the live spec read threw
    /// — neither artifact attempted), <c>RenderFailed: &lt;message&gt;</c>,
    /// and / or <c>AggregateFailed: &lt;message&gt;</c>; render + aggregate
    /// failures from the same report are joined with <c>"; "</c>.
    /// </summary>
    public string? Reason { get; init; }
}
