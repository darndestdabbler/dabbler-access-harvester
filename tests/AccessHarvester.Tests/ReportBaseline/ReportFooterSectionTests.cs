using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="ReportFooterSection"/> — the pure section-
/// classification logic extracted from the (COM-only) report spec reader so
/// the footer-vs-not and report-footer-vs-group-footer decisions are testable
/// without an Access process.
/// </summary>
public class ReportFooterSectionTests
{
    [Theory]
    [InlineData(0, false)]   // Detail
    [InlineData(1, false)]   // Report header
    [InlineData(2, true)]    // Report footer
    [InlineData(3, false)]   // Page header
    [InlineData(4, false)]   // Page footer — excluded (page furniture)
    [InlineData(5, false)]   // Group level 1 header
    [InlineData(6, true)]    // Group level 1 footer
    [InlineData(7, false)]   // Group level 2 header
    [InlineData(8, true)]    // Group level 2 footer
    [InlineData(10, true)]   // Group level 3 footer
    public void IsFooterSection_ClassifiesReportAndGroupFooters_ExcludesPageFooter(
        int section, bool expected)
    {
        Assert.Equal(expected, ReportFooterSection.IsFooterSection(section));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(6, false)]
    [InlineData(8, false)]
    public void IsReportFooter_IsTrueOnlyForTheReportFooterSection(int section, bool expected)
    {
        Assert.Equal(expected, ReportFooterSection.IsReportFooter(section));
    }

    [Theory]
    [InlineData(2, "ReportFooter")]
    [InlineData(6, "GroupFooter1")]
    [InlineData(8, "GroupFooter2")]
    [InlineData(10, "GroupFooter3")]
    public void Label_MapsSectionIndexToOutputLabel(int section, string expected)
    {
        Assert.Equal(expected, ReportFooterSection.Label(section));
    }
}
