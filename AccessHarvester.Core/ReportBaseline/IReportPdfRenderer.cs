namespace AccessHarvester.Core.ReportBaseline;

/// <summary>
/// Renders an Access report to a PDF file. The production implementation
/// wraps <c>DoCmd.OutputTo acOutputReport, &lt;name&gt;, acFormatPDF,
/// &lt;path&gt;</c> over a live COM session; hermetic tests pass a fake
/// that just touches a stand-in file.
/// </summary>
public interface IReportPdfRenderer
{
    /// <summary>
    /// Render <paramref name="reportName"/> to <paramref name="outputPdfPath"/>.
    /// Implementations create any missing parent directories. Throws on
    /// render failure (caller records it as <c>Failed</c> on the
    /// baseline manifest).
    /// </summary>
    void Render(string reportName, string outputPdfPath);
}
