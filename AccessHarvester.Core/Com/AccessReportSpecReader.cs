using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

    private readonly AccessComSession _session;

    public AccessReportSpecReader(AccessComSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public IReadOnlyList<string> EnumerateAll()
    {
        var names = new List<string>();
        dynamic allReports = _session.Application.CurrentProject.AllReports;
        foreach (dynamic report in allReports)
        {
            names.Add((string)report.Name);
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

        try
        {
            dynamic report = _session.Application.Reports[reportName];

            string? recordSource = TryReadString(() => (string?)report.RecordSource);
            string? savedQuerySql = TryReadSavedQuerySql(recordSource);

            var groupLevels = ImmutableArray.CreateBuilder<ReportGroupLevelKey>();
            // GroupLevel is a sparse collection — iterating Count is the
            // documented access pattern. Wrapped because reports with no
            // grouping can throw on the Count read on some Access versions.
            int count = TryReadInt(() => (int)report.GroupLevel.Count) ?? 0;
            for (int i = 0; i < count; i++)
            {
                string? controlSource = TryReadString(() => (string?)report.GroupLevel(i).ControlSource);
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
        }
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

        try
        {
            dynamic currentDb = _session.Application.CurrentDb();
            dynamic queryDef = currentDb.QueryDefs[recordSource];
            return TryReadString(() => (string?)queryDef.SQL);
        }
        catch
        {
            return null;
        }
    }
}
