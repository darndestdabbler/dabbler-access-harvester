using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Composes <see cref="IReportSource"/> + <see cref="IReportPdfRenderer"/>
/// + <see cref="ReportBaselineService"/> to capture baselines for a batch
/// of reports. Writes per-report <c>aggregates.json</c> (always) into the
/// deterministic <c>reportsBaselineRoot</c> corpus, plus a top-level
/// <c>baseline-manifest.json</c>. Returns the manifest so the CLI can emit
/// its summary into <c>--json-status</c>.
///
/// <para>
/// The gated corpus under <c>reportsBaselineRoot</c> is PDF-free: the
/// optional <c>output.pdf</c> visual spot-check (only when <c>includePdf</c>
/// is set — see <see cref="Run"/>) is written into a SEPARATE
/// <c>spotCheckRoot</c> sibling, never inside the byte-diffed corpus, so a
/// report's gated capture is byte-identical whether or not PDFs were
/// rendered and a PDF can never contaminate the determinism gate. The
/// per-report directory is <c>&lt;root&gt;/&lt;reportName&gt;/</c> under each
/// root. Paths recorded on the manifests are forward-slashed relative paths
/// so the JSON is byte-identical across OS path separators.
/// </para>
/// </summary>
public sealed class ReportBaselineRunner
{
    private readonly IReportSource _source;
    private readonly IReportPdfRenderer _renderer;
    private readonly ReportBaselineService _service;
    private readonly string _harvesterVersion;

    public ReportBaselineRunner(
        IReportSource source,
        IReportPdfRenderer renderer,
        ReportBaselineService service,
        string harvesterVersion)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        if (string.IsNullOrWhiteSpace(harvesterVersion))
            throw new ArgumentException("HarvesterVersion is required.", nameof(harvesterVersion));
        _harvesterVersion = harvesterVersion;
    }

    /// <summary>
    /// Capture baselines for every report in <paramref name="reportNames"/>.
    /// Writes the per-report directories under
    /// <paramref name="reportsBaselineRoot"/> and the
    /// <c>baseline-manifest.json</c> index at its root, and returns the
    /// manifest. The caller is responsible for filtering
    /// <paramref name="reportNames"/> (<c>--reports</c> allowlist); the
    /// runner does not re-filter.
    /// </summary>
    /// <param name="databasePath">
    /// The <c>.accdb</c> path the run captured against; recorded on the
    /// manifest as <see cref="BaselineManifest.Database"/>.
    /// </param>
    /// <param name="reportNames">
    /// Reports to process, in any order. Per-report failures do not
    /// abort the batch; each becomes a <c>Failed</c> manifest entry.
    /// </param>
    /// <param name="reportsBaselineRoot">
    /// The per-database <c>reports-baseline</c> directory, typically
    /// <c>&lt;out&gt;/AccessMetaData/&lt;dbName&gt;/reports-baseline</c>.
    /// </param>
    /// <param name="parametersByReport">
    /// Optional per-report parameter values; unmentioned reports run
    /// unfiltered. Each report's value is an explicitly ordered list of
    /// (name, value) pairs; the list order is the positional binding
    /// order against the SQL's <c>?</c> placeholders (Access OLE DB
    /// ignores parameter names for text commands), and the same pairs
    /// are also collected name-keyed into <c>ParametersUsed</c>
    /// provenance. Using an explicitly ordered carrier rather than a
    /// dictionary avoids relying on a particular dictionary
    /// implementation's enumeration-order guarantee.
    /// </param>
    /// <param name="cleanRoot">
    /// When <c>true</c> (the default), the entire
    /// <paramref name="reportsBaselineRoot"/> directory is deleted and
    /// recreated before capture, so a re-run cannot inherit stale
    /// per-report artifacts (PDFs or aggregates.json files from prior
    /// runs whose reports are now skipped or filtered out). When
    /// <c>false</c>, per-report directories are still recreated
    /// fresh for every name in <paramref name="reportNames"/> so
    /// captured-this-run artifacts are not contaminated by prior-run
    /// state — but un-captured reports' prior artifacts remain.
    /// </param>
    /// <param name="includePdf">
    /// When <c>false</c> (the default), only the deterministic
    /// <c>aggregates.json</c> is written and the COM PDF render is
    /// skipped entirely; <paramref name="spotCheckRoot"/> is left untouched
    /// and no <c>spot-check-manifest.json</c> is written. Aggregates are the
    /// RecordSource-result-set parity ground truth; the rendered PDF is a
    /// non-deterministic binary whose only consumer is human visual review,
    /// so it is opt-in. When <c>true</c>, each captured report additionally
    /// renders <c>output.pdf</c> via <see cref="IReportPdfRenderer"/> into the
    /// SEPARATE <paramref name="spotCheckRoot"/> sibling — never inside the
    /// gated <paramref name="reportsBaselineRoot"/> corpus — and a
    /// <c>spot-check-manifest.json</c> is written there. Aggregate capture
    /// remains the gate: if the report's RecordSource cannot run (for example,
    /// required parameters were not supplied), the runner records the
    /// aggregate failure and skips PDF rendering so the batch does not enter a
    /// modal COM render path for a report that already failed. A PDF render
    /// failure is non-blocking: it is recorded only on the spot-check manifest
    /// and never changes the report's gated capture status. The CLI surfaces
    /// this as the <c>--include-pdf</c> flag.
    /// </param>
    /// <param name="spotCheckRoot">
    /// The per-database <c>reports-baseline-spot-check</c> directory, a sibling
    /// of <paramref name="reportsBaselineRoot"/>. Required (non-empty) when
    /// <paramref name="includePdf"/> is <c>true</c>; ignored otherwise. PDFs
    /// and the spot-check manifest live here, outside the determinism gate.
    /// </param>
    public BaselineManifest Run(
        string databasePath,
        IReadOnlyList<string> reportNames,
        string reportsBaselineRoot,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? parametersByReport = null,
        bool cleanRoot = true,
        bool includePdf = false,
        string? spotCheckRoot = null)
    {
        if (reportNames is null) throw new ArgumentNullException(nameof(reportNames));
        if (string.IsNullOrWhiteSpace(reportsBaselineRoot))
            throw new ArgumentException("reportsBaselineRoot is empty.", nameof(reportsBaselineRoot));
        if (includePdf && string.IsNullOrWhiteSpace(spotCheckRoot))
            throw new ArgumentException(
                "spotCheckRoot is required when includePdf is true.", nameof(spotCheckRoot));
        // The non-gated spot-check tree must be physically disjoint from the
        // gated corpus, or a non-deterministic PDF could land inside the
        // byte-diffed reports-baseline/ tree and contaminate the determinism
        // gate. The CLI always computes a proper sibling, but enforce it at the
        // API boundary so a direct library caller cannot reintroduce the leak.
        if (includePdf && PathsOverlap(reportsBaselineRoot, spotCheckRoot!))
            throw new ArgumentException(
                "spotCheckRoot must be disjoint from reportsBaselineRoot (equal, nested, " +
                $"or containing paths are rejected so PDFs never enter the gated corpus): " +
                $"reportsBaselineRoot='{reportsBaselineRoot}', spotCheckRoot='{spotCheckRoot}'.",
                nameof(spotCheckRoot));

        if (cleanRoot && Directory.Exists(reportsBaselineRoot))
            Directory.Delete(reportsBaselineRoot, recursive: true);
        Directory.CreateDirectory(reportsBaselineRoot);

        // The spot-check root is the report-baseline corpus's non-gated
        // sibling. Clean it on every opt-in run so stale PDFs from a prior
        // run (different --reports scope, a report that now fails) do not
        // linger and mislead a human reviewer.
        if (includePdf && cleanRoot && Directory.Exists(spotCheckRoot!))
            Directory.Delete(spotCheckRoot!, recursive: true);

        var entries = new List<BaselineManifestEntry>(reportNames.Count);
        var spotEntries = new List<SpotCheckManifestEntry>(includePdf ? reportNames.Count : 0);
        foreach (var name in reportNames)
        {
            var (gated, spot) = CaptureOne(name, reportsBaselineRoot, spotCheckRoot, parametersByReport, includePdf);
            entries.Add(gated);
            if (spot is not null)
                spotEntries.Add(spot);
        }

        var manifest = new BaselineManifest
        {
            Database = databasePath ?? string.Empty,
            HarvesterVersion = _harvesterVersion,
            Reports = entries
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .ToImmutableArray(),
        };

        BaselineManifestWriter.WriteToFile(
            manifest,
            Path.Combine(reportsBaselineRoot, "baseline-manifest.json"));

        if (includePdf)
        {
            var spotManifest = new SpotCheckManifest
            {
                Database = databasePath ?? string.Empty,
                HarvesterVersion = _harvesterVersion,
                Reports = spotEntries
                    .OrderBy(e => e.Name, StringComparer.Ordinal)
                    .ToImmutableArray(),
            };
            Directory.CreateDirectory(spotCheckRoot!);
            SpotCheckManifestWriter.WriteToFile(
                spotManifest,
                Path.Combine(spotCheckRoot!, "spot-check-manifest.json"));
        }

        return manifest;
    }

    private (BaselineManifestEntry Gated, SpotCheckManifestEntry? Spot) CaptureOne(
        string reportName,
        string reportsBaselineRoot,
        string? spotCheckRoot,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? parametersByReport,
        bool includePdf)
    {
        var reportDir = Path.Combine(reportsBaselineRoot, reportName);
        // Recreate the per-report dir from scratch each capture so a
        // re-run cannot leave a stale aggregates.json behind when the
        // current run's outcome differs (Captured -> Failed,
        // Captured -> Skipped, etc.). PDFs live in spotCheckRoot, not here.
        if (Directory.Exists(reportDir))
            Directory.Delete(reportDir, recursive: true);
        Directory.CreateDirectory(reportDir);

        ReportLiveSpec spec;
        try
        {
            spec = _source.ReadSpec(reportName);
        }
        catch (Exception ex)
        {
            return (
                new BaselineManifestEntry
                {
                    Name = reportName,
                    Status = "Failed",
                    Paths = ImmutableSortedDictionary<string, string>.Empty,
                    Reason = $"ReadSpecFailed: {ex.Message}",
                },
                SkippedSpot(reportName, includePdf, $"ReadSpecFailed: {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(spec.RecordSource))
        {
            return (
                new BaselineManifestEntry
                {
                    Name = reportName,
                    Status = "Skipped",
                    Paths = ImmutableSortedDictionary<string, string>.Empty,
                    Reason = "UnboundReport",
                },
                SkippedSpot(reportName, includePdf, "UnboundReport"));
        }

        var pathsBuilder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        IReadOnlyList<KeyValuePair<string, object?>> orderedParams =
            parametersByReport != null
                && parametersByReport.TryGetValue(reportName, out var p)
                    ? p
                    : Array.Empty<KeyValuePair<string, object?>>();

        string? aggregateFailure = null;
        try
        {
            // Name-keyed dictionary for provenance (the values used are
            // recorded into aggregates.json by name); the ordered list is
            // the source of truth for positional binding.
            var nameKeyed = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in orderedParams)
                nameKeyed[kv.Key] = kv.Value;

            var input = new ReportAggregateInput
            {
                ReportName = reportName,
                RecordSource = spec.RecordSource!,
                SavedQuerySql = spec.SavedQuerySql,
                GroupLevels = spec.GroupLevels,
                FooterAggregates = spec.FooterAggregates,
                ParametersUsed = nameKeyed,
            };

            // Positional binding comes from the ORDERED list explicitly,
            // not from any dictionary enumeration. The CLI --params shape
            // requires the caller to declare parameters in the SQL's
            // placeholder order.
            var positional = orderedParams.Select(kv => kv.Value).ToImmutableArray();

            _service.CaptureAndWrite(input, reportDir, positional);
            pathsBuilder["aggregates"] = RelativeForward(
                Path.Combine(reportDir, "aggregates.json"), reportsBaselineRoot);
        }
        catch (Exception ex)
        {
            aggregateFailure = $"AggregateFailed: {ex.Message}";
        }

        if (aggregateFailure is not null)
        {
            return (
                new BaselineManifestEntry
                {
                    Name = reportName,
                    Status = "Failed",
                    Paths = pathsBuilder.ToImmutable(),
                    Reason = aggregateFailure,
                },
                SkippedSpot(reportName, includePdf, aggregateFailure));
        }

        // Aggregate capture (the gate) succeeded. The PDF visual spot-check is
        // opt-in (--include-pdf), non-deterministic, and non-blocking: it is
        // rendered into spotCheckRoot — NEVER the gated corpus — and its
        // outcome is recorded only on the spot-check manifest, never on the
        // gated entry. So a render failure leaves the report Captured.
        var gatedEntry = new BaselineManifestEntry
        {
            Name = reportName,
            Status = "Captured",
            Paths = pathsBuilder.ToImmutable(),
            Reason = null,
        };

        if (!includePdf)
            return (gatedEntry, null);

        var spotReportDir = Path.Combine(spotCheckRoot!, reportName);
        if (Directory.Exists(spotReportDir))
            Directory.Delete(spotReportDir, recursive: true);
        Directory.CreateDirectory(spotReportDir);

        string pdfPath = Path.Combine(spotReportDir, "output.pdf");
        try
        {
            _renderer.Render(reportName, pdfPath);
            return (
                gatedEntry,
                new SpotCheckManifestEntry
                {
                    Name = reportName,
                    Status = "Rendered",
                    Path = RelativeForward(pdfPath, spotCheckRoot!),
                    Reason = null,
                });
        }
        catch (Exception ex)
        {
            return (
                gatedEntry,
                new SpotCheckManifestEntry
                {
                    Name = reportName,
                    Status = "Failed",
                    Path = null,
                    Reason = $"RenderFailed: {ex.Message}",
                });
        }
    }

    /// <summary>
    /// Build the spot-check entry for a report whose PDF was not attempted
    /// because its deterministic aggregate capture did not succeed (unbound,
    /// aggregate failure, or spec-read failure). Returns <c>null</c> when PDFs
    /// were not requested so the spot-check manifest is omitted entirely.
    /// </summary>
    private static SpotCheckManifestEntry? SkippedSpot(string reportName, bool includePdf, string reason)
        => includePdf
            ? new SpotCheckManifestEntry
            {
                Name = reportName,
                Status = "Skipped",
                Path = null,
                Reason = reason,
            }
            : null;

    /// <summary>
    /// Returns the path of <paramref name="absolute"/> relative to
    /// <paramref name="root"/> with forward slashes, so the manifest is
    /// byte-identical regardless of OS path separator.
    /// </summary>
    private static string RelativeForward(string absolute, string root)
        => Path.GetRelativePath(root, absolute).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// True when <paramref name="a"/> and <paramref name="b"/> resolve to the
    /// same directory, or one is nested under the other. Compared on
    /// canonicalized full paths with a trailing separator so a shared name
    /// prefix (e.g. <c>reports-baseline</c> vs <c>reports-baseline-spot-check</c>)
    /// is NOT treated as an overlap. Ordinal-ignore-case to match Windows path
    /// semantics (the only platform the COM capture runs on).
    /// </summary>
    private static bool PathsOverlap(string a, string b)
    {
        static string Normalize(string p)
            => Path.GetFullPath(p)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
               + Path.DirectorySeparatorChar;

        string an = Normalize(a);
        string bn = Normalize(b);
        return an.StartsWith(bn, StringComparison.OrdinalIgnoreCase)
            || bn.StartsWith(an, StringComparison.OrdinalIgnoreCase);
    }
}
