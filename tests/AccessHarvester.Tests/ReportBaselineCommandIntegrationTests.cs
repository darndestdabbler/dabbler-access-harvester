using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using AccessHarvester.Cli.Commands;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Test collection that serializes Access-COM-driving tests against the
/// committed <c>HomeHealthCare.accdb</c> fixture. xUnit runs test classes
/// in parallel within an assembly; two classes that each open
/// <c>HomeHealthCare.accdb</c> via the live Access Application would race
/// on the ACE OLE DB file-level lock (per
/// <c>docs/planning/project-guidance.md</c> Conventions → Testing
/// Conventions). This collection serializes them so the per-test 60-second
/// budget is not exhausted by lock contention.
/// </summary>
[CollectionDefinition("HomeHealthCareCom")]
public class HomeHealthCareComCollection { }

/// <summary>
/// <see cref="SkippableFact"/> integration test for the
/// <c>report-baseline</c> CLI subcommand. Drives the command end-to-end
/// (in-process via <see cref="ReportBaselineCommand.Run"/>) against the
/// committed <c>HomeHealthCare.accdb</c> fixture: opens a real Access COM
/// session, renders PDFs, computes aggregates, and emits the
/// <c>baseline-manifest.json</c> index.
///
/// <para>
/// Scoped via <c>--reports</c> to four <c>srpt*</c> sub-reports because
/// the eight top-level <c>rpt*</c> reports in HomeHealthCare reference
/// parameters via <c>Forms!</c>/<c>Reports!</c> bindings or explicit
/// <c>PARAMETERS</c> clauses, and Access's <c>DoCmd.OutputTo</c> pops a
/// modal parameter prompt when those are not provided. Surfacing required-
/// parameter reports through the spec's <c>identify-usage</c> escalation
/// is explicit Session 3 scope; until then the integration test scopes
/// down so it can run unattended.
/// </para>
///
/// <para>
/// The scoped set deliberately includes both <c>Captured</c> reports
/// (srptClientBudgets, srptClientCurrentNeeds — bound, parameter-free at
/// the OLE DB layer) and <c>Failed</c> reports (srptServicePlanEntries,
/// srptServiceShortfall — render PDF with empty data because the parent-
/// context binding is unresolved, then fail at the aggregate OLE DB query
/// with <c>"No value given for one or more required parameters."</c>).
/// Both Status branches of the runner's <c>CaptureOne</c> path are
/// exercised, so the integration test catches regressions in either.
/// Exit code 3 is expected (some Failed entries).
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
[Collection("HomeHealthCareCom")]
public class ReportBaselineCommandIntegrationTests
{
    // Sub-reports only; see class-level remarks. Two are expected to Capture
    // cleanly, two to Fail at the OLE DB aggregate step without popping a
    // modal in DoCmd.OutputTo.
    private static readonly string[] ScopedReports =
    {
        "srptClientBudgets",
        "srptClientCurrentNeeds",
        "srptServicePlanEntries",
        "srptServiceShortfall",
    };

    [SkippableFact]
    public void ReportBaselineCommand_CapturesPdfAndAggregates_AgainstCommittedFixture()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM is Windows-only.");
        Skip.If(Type.GetTypeFromProgID("Access.Application") is null,
            "Microsoft Access is not installed (Access.Application ProgID not registered).");

        var db = ResolveSampleDb("HomeHealthCare.accdb");
        Skip.If(db is null, "samples/HomeHealthCare.accdb not found relative to test bin path.");

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-baseline-{Guid.NewGuid():N}");
        try
        {
            int exit = ReportBaselineCommand.Run(new[]
            {
                "--db", db!,
                "--out", outRoot,
                "--reports", string.Join(",", ScopedReports),
                "--json-status",
            });

            string baselineDir = Path.Combine(outRoot, "AccessMetaData", "HomeHealthCare", "reports-baseline");
            string manifestPath = Path.Combine(baselineDir, "baseline-manifest.json");
            Assert.True(File.Exists(manifestPath), $"Expected baseline-manifest.json at {manifestPath}.");

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            Assert.Equal(db, root.GetProperty("Database").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("HarvesterVersion").GetString()));

            var reports = root.GetProperty("Reports");
            Assert.Equal(ScopedReports.Length, reports.GetArrayLength());

            int captured = 0;
            int failed = 0;
            for (int i = 0; i < reports.GetArrayLength(); i++)
            {
                var entry = reports[i];
                string name = entry.GetProperty("Name").GetString()!;
                string status = entry.GetProperty("Status").GetString()!;

                if (status == "Captured")
                {
                    captured++;
                    var paths = entry.GetProperty("Paths");
                    string pdfRel = paths.GetProperty("pdf").GetString()!;
                    string aggRel = paths.GetProperty("aggregates").GetString()!;

                    string pdfPath = Path.Combine(baselineDir, pdfRel.Replace('/', Path.DirectorySeparatorChar));
                    string aggPath = Path.Combine(baselineDir, aggRel.Replace('/', Path.DirectorySeparatorChar));

                    Assert.True(File.Exists(pdfPath), $"PDF not found on disk for {name}: {pdfPath}");
                    Assert.True(File.Exists(aggPath), $"aggregates.json not found on disk for {name}: {aggPath}");
                    Assert.True(new FileInfo(pdfPath).Length > 0, $"PDF for {name} was empty.");

                    // aggregates.json must parse as JSON with the expected provenance contract.
                    using var aggDoc = JsonDocument.Parse(File.ReadAllText(aggPath));
                    Assert.Equal(name, aggDoc.RootElement.GetProperty("ReportName").GetString());
                    Assert.Equal("RecordSourceResultSet",
                        aggDoc.RootElement.GetProperty("Provenance").GetProperty("AggregateContract").GetString());
                }
                else if (status == "Failed")
                {
                    failed++;
                    // The scoped Failed reports fail at the OLE DB aggregate
                    // step (parent-context bindings unresolved); the PDF
                    // should still render with empty data.
                    var reason = entry.GetProperty("Reason").GetString();
                    Assert.NotNull(reason);
                    Assert.Contains("AggregateFailed", reason);
                }
                else
                {
                    Assert.Fail($"Unexpected Status '{status}' for {name}; the scoped set should produce only Captured or Failed.");
                }
            }

            // Both Status branches of the runner's CaptureOne path must fire
            // against the scoped set, or the integration test isn't actually
            // covering both pipelines.
            Assert.True(captured >= 1, "At least one Captured report expected against the scoped set.");
            Assert.True(failed >= 1, "At least one Failed report expected against the scoped set (proves the failure path is exercised).");
            Assert.Equal(3, exit); // Mixed Captured+Failed → exit 3 per the run-mirroring policy.
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    private static string? ResolveSampleDb(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
