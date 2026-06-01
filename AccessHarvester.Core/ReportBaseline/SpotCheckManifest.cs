using System.Collections.Generic;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Top-level index for the OPTIONAL, non-gated PDF visual spot-check run
/// (<c>--include-pdf</c>). Written to <c>spot-check-manifest.json</c> in the
/// sibling <c>reports-baseline-spot-check/</c> root, deliberately OUTSIDE the
/// deterministic <c>reports-baseline/</c> corpus the golden regression gate
/// byte-diffs.
///
/// <para>
/// Rendered PDFs are not byte-deterministic (Access embeds wall-clock and
/// machine metadata), so neither the PDFs nor this manifest participate in the
/// regression gate. Their only consumer is a human doing a layout/format/
/// grouping visual sanity check. The gated <see cref="BaselineManifest"/> never
/// references a PDF, so a report's gated capture is byte-identical whether or
/// not <c>--include-pdf</c> was passed.
/// </para>
/// </summary>
public sealed record SpotCheckManifest
{
    /// <summary>The .accdb the run captured against (full path).</summary>
    public required string Database { get; init; }

    /// <summary>The harvester binary version that produced the PDFs.</summary>
    public required string HarvesterVersion { get; init; }

    /// <summary>
    /// Per-report spot-check entries, ordered ordinal-ascending by
    /// <see cref="SpotCheckManifestEntry.Name"/>. Every report the run
    /// considered appears here so a skipped/failed render is surfaced, never
    /// silently dropped.
    /// </summary>
    public required IReadOnlyList<SpotCheckManifestEntry> Reports { get; init; }
}

/// <summary>
/// Single per-report row on the spot-check manifest index.
/// </summary>
public sealed record SpotCheckManifestEntry
{
    /// <summary>The report's name as Access stores it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// One of:
    /// <list type="bullet">
    ///   <item><c>Rendered</c> — the PDF was rendered to <see cref="Path"/>.</item>
    ///   <item><c>Skipped</c> — no render was attempted because the report's
    ///   deterministic aggregate capture did not succeed (unbound report or
    ///   aggregate failure); see <see cref="Reason"/>. The spot-check is
    ///   gated behind a successful aggregate capture so a report that already
    ///   failed deterministically does not enter the modal COM render path.</item>
    ///   <item><c>Failed</c> — a render was attempted but threw; see
    ///   <see cref="Reason"/>. This never affects the gated capture's status.</item>
    /// </list>
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The rendered PDF path relative to the spot-check root, forward-slashed.
    /// Non-null only when <see cref="Status"/> is <c>Rendered</c>.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Why a render was skipped or failed. <c>null</c> when
    /// <see cref="Status"/> is <c>Rendered</c>. Skip reasons mirror the gated
    /// manifest's (<c>UnboundReport</c>, <c>AggregateFailed: &lt;message&gt;</c>,
    /// <c>ReadSpecFailed: &lt;message&gt;</c>); the failure reason is
    /// <c>RenderFailed: &lt;message&gt;</c>.
    /// </summary>
    public string? Reason { get; init; }
}
