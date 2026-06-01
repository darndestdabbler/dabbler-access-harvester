namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// A footer-section text box whose <c>ControlSource</c> is an Access
/// expression (begins with <c>=</c>), captured off a live report so the
/// aggregate computer can decide whether the displayed total is derivable
/// from the RecordSource result set.
///
/// <para>
/// Captured for both the report footer and group footers; page footers are
/// not captured (their controls — <c>=Now()</c>, <c>="Page " &amp; [Page]</c> —
/// are page furniture, not display totals). The capture is deliberately
/// permissive: every <c>=</c>-prefixed footer text box is recorded here,
/// regardless of whether its expression is a supported aggregate, so that
/// <see cref="ReportAggregateComputer"/> can route the un-derivable ones to
/// <see cref="ReportAggregates.Skipped"/> with a grounded reason rather than
/// dropping them silently.
/// </para>
/// </summary>
public sealed record FooterAggregateControl
{
    /// <summary>The control's name as Access stores it.</summary>
    public required string ControlName { get; init; }

    /// <summary>
    /// The owning section label, for output: <c>ReportFooter</c> for the
    /// report-level grand footer, or <c>GroupFooter{n}</c> (1-based) for a
    /// group footer.
    /// </summary>
    public required string Section { get; init; }

    /// <summary>
    /// <c>true</c> when the control lives in the report footer (a
    /// whole-result-set grand total). <c>false</c> for group footers, whose
    /// aggregates evaluate per group at report runtime and are not part of
    /// the whole-result-set DisplayTotal contract.
    /// </summary>
    public required bool IsReportFooter { get; init; }

    /// <summary>
    /// The raw <c>ControlSource</c> string, including the leading <c>=</c>
    /// (e.g. <c>=Sum((1-[Discount])*[Quantity]*[UnitPrice])</c>).
    /// </summary>
    public required string Expression { get; init; }

    /// <summary>
    /// <c>true</c> when the control's <c>RunningSum</c> property is on
    /// (Over Group or Over All). A running-sum total is a report-runtime
    /// accumulation, not a pure function of the flat result set, so it is
    /// routed to <see cref="ReportAggregates.Skipped"/>.
    /// </summary>
    public bool RunningSum { get; init; }
}
