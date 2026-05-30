using System.Collections.Generic;

namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// The data side of <c>report-baseline</c> capture: enumerate the
/// database's reports and read each report's RecordSource + group-level
/// keys. The production implementation is the live COM-driven
/// <c>AccessHarvester.Core.Com.AccessReportSpecReader</c>; hermetic tests
/// pass a fake.
/// </summary>
public interface IReportSource
{
    /// <summary>
    /// Returns the ordinal-sorted list of every report defined in the
    /// database, regardless of whether it is bound or has a RecordSource.
    /// The CLI applies any <c>--reports</c> filter against this list.
    /// </summary>
    IReadOnlyList<string> EnumerateAll();

    /// <summary>
    /// Reads RecordSource + group-level keys for the named report. Throws
    /// when the report cannot be opened; returns a spec with a null
    /// <see cref="ReportLiveSpec.RecordSource"/> for unbound reports.
    /// </summary>
    ReportLiveSpec ReadSpec(string reportName);
}
