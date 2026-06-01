using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

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
    private const string MaxOrderDateSql = "SELECT Max([OrderDate]) AS [MaxOrderDate] FROM [Orders]";
    private static readonly Regex ReportParameterStartDateFunction = new(
        @"\breportParameterStartDate\s*\(\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReportParameterEndDateFunction = new(
        @"\breportParameterEndDate\s*\(\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        var resolved = RecordSourceResolver.Resolve(input.RecordSource, input.SavedQuerySql);
        var effectiveInput = input;

        var derivedDefaults = ResolveSupportedDefaultParameters(resolved.SqlToExecute);
        if (derivedDefaults.Count > 0)
        {
            resolved = resolved with
            {
                SqlToExecute = InlineSupportedDefaultParameters(resolved.SqlToExecute, derivedDefaults),
            };
            effectiveInput = effectiveInput with
            {
                ParametersUsed = MergeParameters(input.ParametersUsed, derivedDefaults),
            };
        }

        var table = _executor.Execute(resolved.SqlToExecute, positionalParameters);
        return ReportAggregateComputer.Compute(effectiveInput, resolved, table, _harvesterVersion);
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

    private IReadOnlyDictionary<string, object?> ResolveSupportedDefaultParameters(string sql)
    {
        bool needsStart = ReportParameterStartDateFunction.IsMatch(sql);
        bool needsEnd = ReportParameterEndDateFunction.IsMatch(sql);
        if (!needsStart && !needsEnd)
            return ImmutableSortedDictionary<string, object?>.Empty;

        DateTime endDate = ReadMaxOrderDate().Date;
        DateTime startDate = endDate.AddMonths(-3);

        var builder = ImmutableSortedDictionary.CreateBuilder<string, object?>(StringComparer.Ordinal);
        if (needsStart)
            builder["reportParameterStartDate"] = startDate;
        if (needsEnd)
            builder["reportParameterEndDate"] = endDate;
        return builder.ToImmutable();
    }

    private DateTime ReadMaxOrderDate()
    {
        var table = _executor.Execute(MaxOrderDateSql, positionalParameters: null);
        if (table.Rows.Count == 0 || table.Columns.Count == 0)
        {
            throw new InvalidOperationException(
                "Could not derive reportParameterStartDate/reportParameterEndDate defaults: " +
                "MAX(OrderDate) returned no rows.");
        }

        object? value = table.Rows[0][0];
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException(
                "Could not derive reportParameterStartDate/reportParameterEndDate defaults: " +
                "MAX(OrderDate) returned null.");
        }

        if (value is DateTime dt)
            return dt;

        return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
    }

    private static string InlineSupportedDefaultParameters(
        string sql,
        IReadOnlyDictionary<string, object?> derivedDefaults)
    {
        if (derivedDefaults.TryGetValue("reportParameterStartDate", out var startObj)
            && startObj is DateTime startDate)
        {
            sql = ReportParameterStartDateFunction.Replace(sql, AccessDateLiteral(startDate));
        }

        if (derivedDefaults.TryGetValue("reportParameterEndDate", out var endObj)
            && endObj is DateTime endDate)
        {
            sql = ReportParameterEndDateFunction.Replace(sql, AccessDateLiteral(endDate));
        }

        return sql;
    }

    private static IReadOnlyDictionary<string, object?> MergeParameters(
        IReadOnlyDictionary<string, object?> existing,
        IReadOnlyDictionary<string, object?> derivedDefaults)
    {
        if (derivedDefaults.Count == 0)
            return existing;

        var builder = ImmutableSortedDictionary.CreateBuilder<string, object?>(StringComparer.Ordinal);
        foreach (var kv in existing)
            builder[kv.Key] = kv.Value;
        foreach (var kv in derivedDefaults)
            builder.TryAdd(kv.Key, kv.Value);
        return builder.ToImmutable();
    }

    private static string AccessDateLiteral(DateTime date)
        => $"#{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}#";
}
