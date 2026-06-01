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
/// ordinal-sorted, and no wall-clock timestamps are recorded. This manifest
/// indexes only the deterministic gated corpus and never references a PDF:
/// the optional <c>--include-pdf</c> visual spot-check writes its PDFs and a
/// separate <see cref="SpotCheckManifest"/> into the non-gated
/// <c>reports-baseline-spot-check/</c> sibling, so the gated capture is
/// byte-identical whether or not PDFs were rendered.
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
    /// One of <c>Captured</c>, <c>Skipped</c>, <c>Failed</c>. Status reflects
    /// the deterministic aggregate capture ONLY — the optional PDF spot-check
    /// never affects it (its outcome lives on the <see cref="SpotCheckManifest"/>):
    /// <list type="bullet">
    ///   <item><c>Captured</c> — <c>aggregates.json</c> was written.</item>
    ///   <item><c>Skipped</c> — nothing to capture (e.g. unbound report);
    ///   see <see cref="Reason"/>.</item>
    ///   <item><c>Failed</c> — the aggregate capture could not be produced;
    ///   see <see cref="Reason"/>.</item>
    /// </list>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Per-artifact paths relative to the run's reports-baseline root,
    /// keyed by artifact kind (only <c>aggregates</c> today) and
    /// ordinal-sorted for byte-stable JSON. Empty when no artifact was
    /// produced. PDFs are never listed here — they live in the non-gated
    /// spot-check sibling and are indexed by <see cref="SpotCheckManifest"/>.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Paths { get; init; }

    /// <summary>
    /// Why a report was skipped or failed. <c>null</c> when
    /// <see cref="Status"/> is <c>Captured</c>. Skip reasons:
    /// <c>UnboundReport</c>. Failure reasons are
    /// <c>ReadSpecFailed: &lt;message&gt;</c> (the live spec read threw)
    /// and <c>AggregateFailed: &lt;message&gt;</c> (the deterministic
    /// aggregate capture failed). A PDF render failure is non-blocking and
    /// recorded on the spot-check manifest, never here.
    /// </summary>
    public string? Reason { get; init; }
}
