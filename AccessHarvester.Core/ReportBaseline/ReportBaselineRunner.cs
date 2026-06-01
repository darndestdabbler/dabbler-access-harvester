using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Composes <see cref="IReportSource"/> + <see cref="IReportPdfRenderer"/>
/// + <see cref="ReportBaselineService"/> to capture baselines for a batch
/// of reports. Writes per-report <c>aggregates.json</c> (always) and
/// <c>output.pdf</c> (only when <c>includePdf</c> is set — see
/// <see cref="Run"/>), plus a top-level <c>baseline-manifest.json</c>.
/// Returns the manifest so the CLI can emit its summary into
/// <c>--json-status</c>.
///
/// <para>
/// All file writes go under <c>reportsBaselineRoot</c>; the per-report
/// directory is <c>&lt;root&gt;/&lt;reportName&gt;/</c>. Paths recorded on
/// the manifest are forward-slashed relative paths so the JSON is
/// byte-identical across OS path separators.
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
    /// skipped entirely. Aggregates are the RecordSource-result-set
    /// parity ground truth; the rendered PDF is a non-deterministic
    /// binary whose only consumer is human visual review, so it is
    /// opt-in. When <c>true</c>, each captured report additionally
    /// renders <c>output.pdf</c> via <see cref="IReportPdfRenderer"/>. Aggregate capture remains the
    /// gate: if the report's RecordSource cannot run (for example,
    /// required parameters were not supplied), the runner records the
    /// aggregate failure and skips PDF rendering so the batch does not
    /// enter a modal COM render path for a report that already failed.
    /// The CLI surfaces this as the <c>--include-pdf</c> flag.
    /// </param>
    public BaselineManifest Run(
        string databasePath,
        IReadOnlyList<string> reportNames,
        string reportsBaselineRoot,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? parametersByReport = null,
        bool cleanRoot = true,
        bool includePdf = false)
    {
        if (reportNames is null) throw new ArgumentNullException(nameof(reportNames));
        if (string.IsNullOrWhiteSpace(reportsBaselineRoot))
            throw new ArgumentException("reportsBaselineRoot is empty.", nameof(reportsBaselineRoot));

        if (cleanRoot && Directory.Exists(reportsBaselineRoot))
            Directory.Delete(reportsBaselineRoot, recursive: true);
        Directory.CreateDirectory(reportsBaselineRoot);

        var entries = new List<BaselineManifestEntry>(reportNames.Count);
        foreach (var name in reportNames)
        {
            entries.Add(CaptureOne(name, reportsBaselineRoot, parametersByReport, includePdf));
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

        return manifest;
    }

    private BaselineManifestEntry CaptureOne(
        string reportName,
        string reportsBaselineRoot,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? parametersByReport,
        bool includePdf)
    {
        var reportDir = Path.Combine(reportsBaselineRoot, reportName);
        // Recreate the per-report dir from scratch each capture so a
        // re-run cannot leave stale output.pdf / aggregates.json behind
        // when the current run's outcome differs (Captured -> Failed,
        // Captured -> Skipped, partial-Captured this run that did not
        // emit one of the two artifacts the prior run did, etc.).
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
            return new BaselineManifestEntry
            {
                Name = reportName,
                Status = "Failed",
                Paths = ImmutableSortedDictionary<string, string>.Empty,
                Reason = $"ReadSpecFailed: {ex.Message}",
            };
        }

        if (string.IsNullOrWhiteSpace(spec.RecordSource))
        {
            return new BaselineManifestEntry
            {
                Name = reportName,
                Status = "Skipped",
                Paths = ImmutableSortedDictionary<string, string>.Empty,
                Reason = "UnboundReport",
            };
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

        // PDF render is opt-in (--include-pdf). When off, the renderer is
        // never invoked: the default capture is the deterministic
        // aggregates.json only. Aggregate capture is the gate so reports
        // that already failed deterministically do not enter the COM
        // render path.
        string? renderFailure = null;
        if (aggregateFailure is null && includePdf)
        {
            string pdfPath = Path.Combine(reportDir, "output.pdf");
            try
            {
                _renderer.Render(reportName, pdfPath);
                pathsBuilder["pdf"] = RelativeForward(pdfPath, reportsBaselineRoot);
            }
            catch (Exception ex)
            {
                renderFailure = $"RenderFailed: {ex.Message}";
            }
        }

        if (renderFailure is null && aggregateFailure is null)
        {
            return new BaselineManifestEntry
            {
                Name = reportName,
                Status = "Captured",
                Paths = pathsBuilder.ToImmutable(),
                Reason = null,
            };
        }

        string reason = (renderFailure, aggregateFailure) switch
        {
            (not null, not null) => $"{renderFailure}; {aggregateFailure}",
            (not null, null) => renderFailure!,
            (null, not null) => aggregateFailure!,
            _ => "",
        };

        return new BaselineManifestEntry
        {
            Name = reportName,
            Status = "Failed",
            Paths = pathsBuilder.ToImmutable(),
            Reason = reason,
        };
    }

    /// <summary>
    /// Returns the path of <paramref name="absolute"/> relative to
    /// <paramref name="root"/> with forward slashes, so the manifest is
    /// byte-identical regardless of OS path separator.
    /// </summary>
    private static string RelativeForward(string absolute, string root)
        => Path.GetRelativePath(root, absolute).Replace(Path.DirectorySeparatorChar, '/');
}
