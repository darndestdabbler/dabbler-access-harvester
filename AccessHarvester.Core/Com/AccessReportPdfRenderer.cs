using System;
using System.IO;
using System.Runtime.Versioning;
using AccessHarvester.Core.ReportBaseline;

namespace AccessHarvester.Core.Com;

/// <summary>
/// Renders Access reports to PDF via
/// <c>DoCmd.OutputTo acOutputReport, &lt;name&gt;, acFormatPDF, &lt;path&gt;</c>
/// over a live COM session. Windows-only.
///
/// <para>
/// The PDF binary is not byte-deterministic — Access stamps a creation
/// timestamp inside the file — so callers must exclude PDF content from
/// the determinism gate's byte comparison (per the spec's Session 2
/// determinism note). Aggregate / manifest JSON output remains
/// byte-stable.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AccessReportPdfRenderer : IReportPdfRenderer
{
    // AcOutputObjectType.acOutputReport = 3; OutputFormat is the documented
    // string Access exposes for PDF output (the AcFormat enum stringifies
    // to this exact value at the COM boundary).
    private const int AcOutputReport = 3;
    private const string AcFormatPdf = "PDF Format (*.pdf)";

    private readonly AccessComSession _session;

    public AccessReportPdfRenderer(AccessComSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Render(string reportName, string outputPdfPath)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));
        if (string.IsNullOrWhiteSpace(outputPdfPath))
            throw new ArgumentException("Output PDF path is required.", nameof(outputPdfPath));

        var dir = Path.GetDirectoryName(outputPdfPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // DoCmd.OutputTo(ObjectType, ObjectName, OutputFormat, OutputFile,
        //                AutoStart, TemplateFile, Encoding, OutputQuality)
        _session.Application.DoCmd.OutputTo(
            AcOutputReport,
            reportName,
            AcFormatPdf,
            outputPdfPath,
            /* AutoStart: */ false,
            Type.Missing,
            Type.Missing,
            Type.Missing);
    }
}
