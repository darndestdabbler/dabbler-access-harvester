using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// End-to-end orchestrator for capturing a report's aggregate baseline.
/// Composes the S1 building blocks — <see cref="RecordSourceResolver"/>,
/// an <see cref="IReportQueryExecutor"/> (typically the Windows-only
/// OLE DB implementation), <see cref="ReportAggregateComputer"/>, and
/// <see cref="ReportAggregatesWriter"/> — so S2 wires a single service
/// from CLI code instead of re-assembling the pipeline.
///
/// <para>
/// The service is purely functional given a fixed
/// <see cref="IReportQueryExecutor"/>: hermetic tests pass a fake
/// executor over a hand-built <see cref="System.Data.DataTable"/> and
/// exercise the full resolve → execute → compute path without OLE DB.
/// </para>
/// </summary>
public sealed class ReportBaselineService
{
    private readonly IReportQueryExecutor _executor;
    private readonly string _harvesterVersion;

    public ReportBaselineService(IReportQueryExecutor executor, string harvesterVersion)
    {
        if (executor is null) throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(harvesterVersion))
            throw new ArgumentException("HarvesterVersion is required.", nameof(harvesterVersion));

        _executor = executor;
        _harvesterVersion = harvesterVersion;
    }

    /// <summary>
    /// Resolve, execute, and compute aggregates for a single report.
    /// Returns the canonical <see cref="ReportAggregates"/> document
    /// without writing to disk — callers that need the JSON file should
    /// use <see cref="CaptureAndWrite"/>.
    /// </summary>
    /// <param name="input">
    /// Report identity, raw RecordSource, group levels, and the name-
    /// keyed parameter dictionary recorded into provenance.
    /// </param>
    /// <param name="positionalParameters">
    /// Values bound to the SQL's <c>?</c> placeholders in order. Must
    /// align with the report's placeholder list; pass <c>null</c> when
    /// the RecordSource has no placeholders. Distinct from
    /// <see cref="ReportAggregateInput.ParametersUsed"/>, which is the
    /// name-keyed provenance record of the same values.
    /// </param>
    public ReportAggregates Capture(
        ReportAggregateInput input,
        IReadOnlyList<object?>? positionalParameters = null)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var resolved = RecordSourceResolver.Resolve(input.RecordSource);
        var table = _executor.Execute(resolved.SqlToExecute, positionalParameters);
        return ReportAggregateComputer.Compute(input, resolved, table, _harvesterVersion);
    }

    /// <summary>
    /// Capture aggregates and write <c>aggregates.json</c> to
    /// <paramref name="reportBaselineDirectory"/>. Creates the directory
    /// if it does not exist. Returns the full path of the written file.
    /// </summary>
    /// <remarks>
    /// The on-disk layout per the spec is
    /// <c>&lt;out&gt;/AccessMetaData/&lt;dbName&gt;/reports-baseline/&lt;report&gt;/aggregates.json</c>;
    /// this method writes one report's <c>aggregates.json</c> at the
    /// directory the caller supplies. The CLI command in S2 is
    /// responsible for the directory structure above
    /// <paramref name="reportBaselineDirectory"/>.
    /// </remarks>
    public string CaptureAndWrite(
        ReportAggregateInput input,
        string reportBaselineDirectory,
        IReadOnlyList<object?>? positionalParameters = null)
    {
        if (string.IsNullOrWhiteSpace(reportBaselineDirectory))
            throw new ArgumentException("Directory is empty.", nameof(reportBaselineDirectory));

        var aggregates = Capture(input, positionalParameters);
        string path = Path.Combine(reportBaselineDirectory, "aggregates.json");
        ReportAggregatesWriter.WriteToFile(aggregates, path);
        return path;
    }
}
