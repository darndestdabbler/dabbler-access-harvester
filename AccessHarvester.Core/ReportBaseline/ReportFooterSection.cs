namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Pure classification of an Access <c>Control.Section</c> index for footer
/// display-total capture. Extracted from the (Windows-only, COM-driven)
/// <c>AccessReportSpecReader</c> so the section logic is hermetically
/// testable without an Access process.
///
/// <para>
/// Access numbers sections: report footer is <c>2</c>; page footer (<c>4</c>)
/// is excluded — its controls are page furniture, not display totals; group
/// footers are the even indices <c>6, 8, 10, ...</c>
/// (<c>acGroupLevel1Footer = 6</c>, <c>acGroupLevel2Footer = 8</c>, ...).
/// </para>
/// </summary>
public static class ReportFooterSection
{
    /// <summary>Access <c>acFooter</c> — the report-level footer section.</summary>
    public const int ReportFooter = 2;

    /// <summary>Access <c>acGroupLevel1Footer</c> — the first group footer.</summary>
    public const int FirstGroupFooter = 6;

    /// <summary>
    /// <c>true</c> for the report footer (2) or any group footer (even index
    /// &gt;= 6); <c>false</c> for detail, headers, and the page footer (4).
    /// </summary>
    public static bool IsFooterSection(int section) =>
        section == ReportFooter
        || (section >= FirstGroupFooter && section % 2 == 0);

    /// <summary>
    /// <c>true</c> when the section is the report-level footer (a
    /// whole-result-set grand total); <c>false</c> for group footers.
    /// </summary>
    public static bool IsReportFooter(int section) => section == ReportFooter;

    /// <summary>
    /// The output section label: <c>ReportFooter</c> for the report footer,
    /// or <c>GroupFooter{n}</c> (1-based) for a group footer. Only meaningful
    /// for sections where <see cref="IsFooterSection"/> is <c>true</c>.
    /// </summary>
    public static string Label(int section) =>
        section == ReportFooter
            ? "ReportFooter"
            : $"GroupFooter{(section - FirstGroupFooter) / 2 + 1}";
}
