using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.Vba.Parser;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// End-to-end regression for the vba-attribute-header-strip session set:
// a previously-failing HHC code-behind module now parses cleanly via
// the standard pipeline-consumer path (`VbaModuleBodyLoader.LoadAndParse`),
// and the parsed `Module.Header` carries the migration-relevant
// `VB_PredeclaredId` / `VB_Exposed` / `VB_Creatable` / `VB_GlobalNameSpace`
// fields.
//
// Pre-strip behavior: HHC's 32-module parse-success report reported
// `parseOk: 3, parseFailed: 29` — every leading-`Attribute VB_*` block
// failed the parser. Form_frmReportParam_FY is one of those 29.
public sealed class VbaAttributeHeaderStripRegressionTests
{
    [Fact]
    public void HHC_Form_frmReportParam_FY_parses_cleanly_with_header_metadata_attached()
    {
        var repoRoot = RepoRoot();
        var yamlPath = Path.Combine(
            repoRoot, "samples", "DatabaseYaml", "HomeHealthCare.accdb",
            "Modules", "Form_frmReportParam_FY.yaml");
        Assert.True(File.Exists(yamlPath),
            $"Expected HHC YAML mirror at '{yamlPath}'.");

        var body = ExtractDefinition(yamlPath);
        Assert.NotNull(body);

        var (ast, result) = VbaModuleBodyLoader.LoadAndParse(
            body!,
            new ParseOptions(ParseMode.Recovering, LossyFallbackErrorThreshold: 100, SourcePath: yamlPath),
            yamlPath);

        // The whole point of the strip: no Error-severity diagnostics
        // on this previously-failing form module.
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Code} @ L{d.Line}: {d.Message}")
            .ToList();
        Assert.Empty(errors);

        // Header metadata reflects the form's declared identity.
        Assert.NotNull(ast.Header);
        Assert.False(ast.Header!.VbGlobalNameSpace);
        Assert.True(ast.Header.VbCreatable);
        Assert.True(ast.Header.VbPredeclaredId);
        Assert.False(ast.Header.VbExposed);

        // And the body still carries the procedures the consumer
        // expects — at least one ModuleItem from the post-header
        // source (Option Compare / Option Explicit / etc.).
        Assert.NotEmpty(ast.Items);
    }

    // Local copy of the YAML block-scalar extraction used by
    // ParseSuccessReportGenerator. Tailored to the SaveAsText YAML
    // mirror's `Definition: |` shape — does NOT pretend to be a full
    // YAML parser.
    private static string? ExtractDefinition(string path)
    {
        var text = File.ReadAllText(path);
        var lines = text.Split('\n');
        int headerIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Equals("Definition: |", StringComparison.Ordinal) ||
                line.StartsWith("Definition: |", StringComparison.Ordinal))
            {
                headerIdx = i;
                break;
            }
        }
        if (headerIdx < 0) return null;

        var body = new List<string>();
        for (int i = headerIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0) { body.Add(string.Empty); continue; }
            if (line.StartsWith("  ", StringComparison.Ordinal))
                body.Add(line.Substring(2).TrimEnd('\r'));
            else if (string.IsNullOrWhiteSpace(line.TrimEnd('\r')))
                body.Add(string.Empty);
            else
                break;
        }
        return string.Join("\n", body);
    }

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null)
        {
            var samples = Path.Combine(here.FullName, "samples");
            var sln = Path.Combine(here.FullName, "AccessHarvester.sln");
            if (Directory.Exists(samples) && File.Exists(sln))
                return here.FullName;
            here = here.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate repo root above " + AppContext.BaseDirectory);
    }
}
