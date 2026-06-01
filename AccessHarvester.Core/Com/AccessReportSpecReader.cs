using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AccessHarvester.Core.ReportBaseline;

namespace AccessHarvester.Core.Com;

/// <summary>
/// Reads <see cref="ReportLiveSpec"/> for an Access report by opening it
/// in hidden design view, reading its <c>RecordSource</c> + group-level
/// keys off the live <c>Reports</c> collection, and closing without
/// saving. Enumerates the database's reports via
/// <c>CurrentProject.AllReports</c>. Windows-only.
///
/// <para>
/// Design view is the only view in which the <c>GroupLevel</c> collection
/// is reliably accessible — Print Preview / Layout view throw on
/// indexed access. Open is hidden (<c>acHidden = 1</c>) so the harvester
/// never pops a window; close is <c>acSaveNo = 2</c> because property
/// reads do not mutate the report.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AccessReportSpecReader : IReportSource
{
    // Access AcView / AcObjectType / AcWindowMode / AcCloseSave constants.
    private const int AcViewDesign = 1;
    private const int AcReport = 3;
    private const int AcHidden = 1;
    private const int AcSaveNo = 2;

    // AcControlType: a bound/unbound text box (the only control type carrying
    // both ControlSource and RunningSum).
    private const int AcTextBox = 109;

    private readonly AccessComSession _session;

    public AccessReportSpecReader(AccessComSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public IReadOnlyList<string> EnumerateAll()
    {
        var names = new List<string>();
        dynamic? currentProject = null;
        dynamic? allReports = null;
        try
        {
            currentProject = _session.Application.CurrentProject;
            allReports = currentProject.AllReports;
            foreach (dynamic report in allReports)
            {
                try
                {
                    names.Add((string)report.Name);
                }
                finally
                {
                    ReleaseRcw(report);
                }
            }
        }
        finally
        {
            ReleaseRcw(allReports);
            ReleaseRcw(currentProject);
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public ReportLiveSpec ReadSpec(string reportName)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));

        _session.Application.DoCmd.OpenReport(
            reportName,
            AcViewDesign,
            Type.Missing,
            Type.Missing,
            AcHidden);

        dynamic? report = null;
        try
        {
            report = _session.Application.Reports[reportName];

            string? recordSource = TryReadString(() => (string?)report.RecordSource);
            string? savedQuerySql = TryReadSavedQuerySql(recordSource);

            var groupLevels = ImmutableArray.CreateBuilder<ReportGroupLevelKey>();
            // GroupLevel is a sparse collection — iterating Count is the
            // documented access pattern. Wrapped because reports with no
            // grouping can throw on the Count read on some Access versions.
            int count = TryReadInt(() => (int)report.GroupLevel.Count) ?? 0;
            for (int i = 0; i < count; i++)
            {
                dynamic? groupLevel = null;
                string? controlSource;
                try
                {
                    groupLevel = report.GroupLevel(i);
                    controlSource = TryReadString(() => (string?)groupLevel.ControlSource);
                }
                finally
                {
                    ReleaseRcw(groupLevel);
                }

                if (string.IsNullOrEmpty(controlSource)) continue;
                groupLevels.Add(new ReportGroupLevelKey
                {
                    Level = i,
                    GroupOn = controlSource,
                });
            }

            return new ReportLiveSpec
            {
                ReportName = reportName,
                RecordSource = string.IsNullOrEmpty(recordSource) ? null : recordSource,
                SavedQuerySql = string.IsNullOrEmpty(savedQuerySql) ? null : savedQuerySql,
                GroupLevels = groupLevels.ToImmutable(),
                FooterAggregates = ReadFooterAggregates(report),
            };
        }
        finally
        {
            try { _session.Application.DoCmd.Close(AcReport, reportName, AcSaveNo); }
            catch
            {
                // Best-effort close; the caller's Application.Quit will
                // discard any lingering state on session dispose.
            }
            ReleaseRcw(report);
        }
    }

    /// <summary>
    /// Enumerate the report's controls (the report is already open in hidden
    /// Design view) and capture every footer-section text box whose
    /// <c>ControlSource</c> is an Access expression (begins with <c>=</c>).
    /// Report footer and group footers are captured; page footers are not
    /// (their controls are page furniture, not display totals). The
    /// classification of which captured controls are derivable display totals
    /// happens downstream in <see cref="ReportAggregateComputer"/>; the reader
    /// only mechanically lifts the candidates. The returned list is sorted by
    /// (Section, ControlName) ordinal so the capture is deterministic across
    /// runs regardless of the COM collection's enumeration order.
    ///
    /// <para>
    /// A failure to enumerate <c>Controls</c> is deliberately <em>not</em>
    /// swallowed: it propagates so <see cref="ReadSpec"/> fails and the
    /// runner records the report as <c>Failed</c> rather than silently
    /// emitting an empty footer set — a derivable display total must never
    /// disappear from both <see cref="ReportAggregates.DisplayTotals"/> and
    /// <see cref="ReportAggregates.Skipped"/> with no surfaced reason. The
    /// per-property <c>TryRead*</c> helpers stay soft: a control whose
    /// individual properties cannot be read is, by definition, not a usable
    /// footer-aggregate candidate.
    /// </para>
    /// </summary>
    private static IReadOnlyList<FooterAggregateControl> ReadFooterAggregates(dynamic report)
    {
        var captured = new List<FooterAggregateControl>();
        dynamic? controls = null;
        try
        {
            controls = report.Controls;
            foreach (dynamic control in controls)
            {
                try
                {
                    int? controlType = TryReadInt(() => (int)control.ControlType);
                    if (controlType != AcTextBox) continue;

                    int? section = TryReadInt(() => (int)control.Section);
                    if (section is null || !ReportFooterSection.IsFooterSection(section.Value)) continue;

                    string? controlSource = TryReadString(() => (string?)control.ControlSource);
                    if (string.IsNullOrEmpty(controlSource) || !controlSource.TrimStart().StartsWith('='))
                        continue;

                    string? name = TryReadString(() => (string?)control.Name);
                    int runningSum = TryReadInt(() => (int)control.RunningSum) ?? 0;

                    captured.Add(new FooterAggregateControl
                    {
                        ControlName = name ?? string.Empty,
                        Section = ReportFooterSection.Label(section.Value),
                        IsReportFooter = ReportFooterSection.IsReportFooter(section.Value),
                        Expression = controlSource,
                        RunningSum = runningSum != 0,
                    });
                }
                finally
                {
                    ReleaseRcw(control);
                }
            }
        }
        finally
        {
            ReleaseRcw(controls);
        }

        captured.Sort(static (a, b) =>
        {
            int bySection = string.CompareOrdinal(a.Section, b.Section);
            return bySection != 0 ? bySection : string.CompareOrdinal(a.ControlName, b.ControlName);
        });
        return captured.ToImmutableArray();
    }

    private static string? TryReadString(Func<string?> read)
    {
        try { return read(); }
        catch { return null; }
    }

    private static int? TryReadInt(Func<int> read)
    {
        try { return read(); }
        catch { return null; }
    }

    private string? TryReadSavedQuerySql(string? recordSource)
    {
        if (string.IsNullOrWhiteSpace(recordSource))
            return null;

        dynamic? currentDb = null;
        dynamic? queryDefs = null;
        dynamic? queryDef = null;
        try
        {
            currentDb = _session.Application.CurrentDb();
            queryDefs = currentDb.QueryDefs;
            queryDef = queryDefs[recordSource];
            return TryReadString(() => (string?)queryDef.SQL);
        }
        catch
        {
            return null;
        }
        finally
        {
            ReleaseRcw(queryDef);
            ReleaseRcw(queryDefs);
            ReleaseRcw(currentDb);
        }
    }

    private static void ReleaseRcw(object? rcw)
    {
        if (rcw is null) return;
        try { Marshal.FinalReleaseComObject(rcw); } catch { /* RCW already released */ }
    }
}
