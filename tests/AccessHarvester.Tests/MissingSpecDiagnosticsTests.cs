using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Manifest;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

/// <summary>
/// Pins the F-1-4 FORM_SPEC_MISSING diagnostic seam, including the verifier-R1
/// edge case: a parsed module with no matching ir spec must be reported even when
/// the entire ir/&lt;category&gt; directory is absent (irNamesSeen empty).
/// </summary>
public class MissingSpecDiagnosticsTests
{
    [Fact]
    public void Reports_every_parsed_module_when_no_ir_specs_were_seen()
    {
        var diagnostics = new List<HarvesterDiagnostic>();
        // irNamesSeen empty models the missing-ir-directory case.
        CorePipeline.AddMissingSpecDiagnostics(
            parserOutputNames: ["frmZebra", "frmAlpha"],
            irNamesSeen: new HashSet<string>(),
            category: "Forms",
            diagnostics: diagnostics);

        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("FORM_SPEC_MISSING", d.Code));
        Assert.All(diagnostics, d => Assert.Equal(HarvesterDiagnosticSeverity.Warning, d.Severity));
        // Deterministic Ordinal order.
        Assert.Equal(new[] { "frmAlpha", "frmZebra" },
            diagnostics.Select(d => d.Message.Contains("frmAlpha") ? "frmAlpha" : "frmZebra").ToArray());
    }

    [Fact]
    public void Reports_only_the_modules_without_a_matching_spec()
    {
        var diagnostics = new List<HarvesterDiagnostic>();
        CorePipeline.AddMissingSpecDiagnostics(
            parserOutputNames: ["frmA", "frmB"],
            irNamesSeen: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "frmA" },
            category: "Forms",
            diagnostics: diagnostics);

        var d = Assert.Single(diagnostics);
        Assert.Equal("FORM_SPEC_MISSING", d.Code);
        Assert.Contains("frmB", d.Message);
    }

    [Fact]
    public void Emits_nothing_when_every_module_has_a_spec()
    {
        var diagnostics = new List<HarvesterDiagnostic>();
        CorePipeline.AddMissingSpecDiagnostics(
            parserOutputNames: ["frmA"],
            irNamesSeen: new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "frmA" },
            category: "Forms",
            diagnostics: diagnostics);

        Assert.Empty(diagnostics);
    }
}
