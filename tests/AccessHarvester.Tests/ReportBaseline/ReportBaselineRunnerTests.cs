using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="ReportBaselineRunner"/>. Uses fakes for
/// <see cref="IReportSource"/>, <see cref="IReportPdfRenderer"/>, and
/// <see cref="IReportQueryExecutor"/> so the full
/// resolve → render → execute → compute → write → manifest path is
/// exercised without any COM or OLE DB dependency.
///
/// <para>
/// The optional PDF visual spot-check (<c>includePdf</c>) writes PDFs and a
/// <c>spot-check-manifest.json</c> into a SEPARATE <c>spotCheckRoot</c> sibling,
/// never into the gated <c>reports-baseline</c> root. These tests assert that
/// the gated capture is byte-identical regardless of PDF rendering and that a
/// render failure is non-blocking (the report stays <c>Captured</c>).
/// </para>
/// </summary>
public class ReportBaselineRunnerTests
{
    private sealed class FakeSource : IReportSource
    {
        public Dictionary<string, ReportLiveSpec> Specs { get; } = new();
        public Dictionary<string, Exception> Throws { get; } = new();
        public List<string> All { get; } = new();

        public IReadOnlyList<string> EnumerateAll() => All;

        public ReportLiveSpec ReadSpec(string reportName)
        {
            if (Throws.TryGetValue(reportName, out var ex)) throw ex;
            return Specs[reportName];
        }
    }

    private sealed class FakeRenderer : IReportPdfRenderer
    {
        public Dictionary<string, Exception> Throws { get; } = new();
        public List<(string Name, string Path)> Calls { get; } = new();

        public void Render(string reportName, string outputPdfPath)
        {
            Calls.Add((reportName, outputPdfPath));
            if (Throws.TryGetValue(reportName, out var ex)) throw ex;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
            File.WriteAllBytes(outputPdfPath, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        }
    }

    private sealed class FakeExecutor : IReportQueryExecutor
    {
        public Func<string, DataTable>? ResultFor { get; init; }
        public Dictionary<string, Exception> Throws { get; } = new();
        public List<(string Sql, IReadOnlyList<object?>? Pos)> Calls { get; } = new();

        public DataTable Execute(string sql, IReadOnlyList<object?>? positionalParameters)
        {
            Calls.Add((sql, positionalParameters));
            foreach (var kv in Throws)
                if (sql.Contains(kv.Key)) throw kv.Value;
            return (ResultFor ?? (_ => OneRow(1, 10m)))(sql);
        }
    }

    private static DataTable OneRow(int orderId, decimal total)
    {
        var t = new DataTable();
        t.Columns.Add("OrderID", typeof(int));
        t.Columns.Add("Total", typeof(decimal));
        t.Rows.Add(orderId, total);
        return t;
    }

    private static string FreshRoot() => Path.Combine(Path.GetTempPath(), $"ae-runner-{Guid.NewGuid():N}");
    private static string FreshSpotRoot() => Path.Combine(Path.GetTempPath(), $"ae-runner-spot-{Guid.NewGuid():N}");

    private static ReportLiveSpec BoundSpec(string name, string rs) => new()
    {
        ReportName = name,
        RecordSource = rs,
        GroupLevels = ImmutableArray<ReportGroupLevelKey>.Empty,
    };

    private static SpotCheckManifest ReadSpotManifest(string spotRoot)
    {
        string path = Path.Combine(spotRoot, "spot-check-manifest.json");
        Assert.True(File.Exists(path), $"spot-check-manifest.json missing at {path}");
        return JsonSerializer.Deserialize<SpotCheckManifest>(File.ReadAllText(path))!;
    }

    private static void Cleanup(params string[] roots)
    {
        foreach (var r in roots)
            if (Directory.Exists(r)) Directory.Delete(r, recursive: true);
    }

    [Fact]
    public void Run_WithIncludePdf_WritesPdfToSpotCheckSibling_NotGatedTree()
    {
        var source = new FakeSource();
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.All.Add("rptA");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true, spotCheckRoot: spot);

            // Gated tree: aggregates + manifest only, NO pdf anywhere under it.
            Assert.True(File.Exists(Path.Combine(root, "rptA", "aggregates.json")));
            Assert.True(File.Exists(Path.Combine(root, "baseline-manifest.json")));
            Assert.False(File.Exists(Path.Combine(root, "rptA", "output.pdf")));

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("rptA", entry.Name);
            Assert.Equal("Captured", entry.Status);
            Assert.Null(entry.Reason);
            Assert.Equal("rptA/aggregates.json", entry.Paths["aggregates"]);
            Assert.False(entry.Paths.ContainsKey("pdf"));

            // PDF + spot-check manifest live in the sibling root only.
            Assert.True(File.Exists(Path.Combine(spot, "rptA", "output.pdf")));
            var spotEntry = Assert.Single(ReadSpotManifest(spot).Reports);
            Assert.Equal("rptA", spotEntry.Name);
            Assert.Equal("Rendered", spotEntry.Status);
            Assert.Equal("rptA/output.pdf", spotEntry.Path);
            Assert.Null(spotEntry.Reason);
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_GatedTree_IsByteIdentical_WithAndWithoutIncludePdf()
    {
        string Render(bool includePdf)
        {
            var source = new FakeSource();
            source.Specs["rptA"] = BoundSpec("rptA", "Orders");
            source.All.Add("rptA");
            var renderer = new FakeRenderer();
            var executor = new FakeExecutor();
            var service = new ReportBaselineService(executor, "v-test");
            var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

            var root = FreshRoot();
            var spot = FreshSpotRoot();
            try
            {
                runner.Run("/some.accdb", new[] { "rptA" }, root,
                    includePdf: includePdf, spotCheckRoot: includePdf ? spot : null);
                // The gated manifest + aggregates must not change at all.
                return File.ReadAllText(Path.Combine(root, "baseline-manifest.json"))
                       + "\n----\n"
                       + File.ReadAllText(Path.Combine(root, "rptA", "aggregates.json"));
            }
            finally
            {
                Cleanup(root, spot);
            }
        }

        Assert.Equal(Render(includePdf: false), Render(includePdf: true));
    }

    [Fact]
    public void Run_WithoutIncludePdf_WritesAggregatesOnly_AndNeverTouchesSpotCheckRoot()
    {
        var source = new FakeSource();
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.All.Add("rptA");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        try
        {
            // Default includePdf:false — aggregates-only, deterministic.
            var manifest = runner.Run("/some.accdb", new[] { "rptA" }, root);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("rptA", entry.Name);
            Assert.Equal("Captured", entry.Status);
            Assert.Null(entry.Reason);
            // aggregates.json written; the renderer was never invoked and no
            // pdf path was recorded.
            Assert.Equal("rptA/aggregates.json", entry.Paths["aggregates"]);
            Assert.False(entry.Paths.ContainsKey("pdf"));
            Assert.True(File.Exists(Path.Combine(root, "rptA", "aggregates.json")));
            Assert.False(File.Exists(Path.Combine(root, "rptA", "output.pdf")));
            Assert.Empty(renderer.Calls);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Run_IncludePdf_WithoutSpotCheckRoot_Throws()
    {
        var source = new FakeSource();
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.All.Add("rptA");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        try
        {
            Assert.Throws<ArgumentException>(() =>
                runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true, spotCheckRoot: null));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Run_IncludePdf_WithSpotCheckRootOverlappingGatedRoot_Throws()
    {
        var source = new FakeSource();
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.All.Add("rptA");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        try
        {
            // spotCheckRoot equal to the gated root.
            Assert.Throws<ArgumentException>(() =>
                runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true, spotCheckRoot: root));
            // spotCheckRoot nested under the gated root.
            Assert.Throws<ArgumentException>(() =>
                runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true,
                    spotCheckRoot: Path.Combine(root, "spot")));
            // gated root nested under spotCheckRoot (the other direction).
            Assert.Throws<ArgumentException>(() =>
                runner.Run("/some.accdb", new[] { "rptA" }, Path.Combine(root, "gated"),
                    includePdf: true, spotCheckRoot: root));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Run_IncludePdf_WithSiblingSharingNamePrefix_DoesNotTreatAsOverlap()
    {
        // reports-baseline vs reports-baseline-spot-check share a name prefix
        // but are NOT nested; the disjointness guard must allow this (it is the
        // exact layout the CLI produces).
        var source = new FakeSource();
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.All.Add("rptA");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        string baseDir = FreshRoot();
        string root = Path.Combine(baseDir, "reports-baseline");
        string spot = Path.Combine(baseDir, "reports-baseline-spot-check");
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true, spotCheckRoot: spot);
            Assert.Equal("Captured", Assert.Single(manifest.Reports).Status);
            Assert.True(File.Exists(Path.Combine(spot, "rptA", "output.pdf")));
        }
        finally
        {
            Cleanup(baseDir);
        }
    }

    [Fact]
    public void Run_IncludePdf_ReadSpecFailure_GatedFailed_SpotCheckSkippedWithReadSpecReason()
    {
        var source = new FakeSource();
        source.Throws["rptBad"] = new InvalidOperationException("opening rptBad blew up");
        source.All.Add("rptBad");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptBad" }, root, includePdf: true, spotCheckRoot: spot);

            // Gated: Failed with ReadSpecFailed, renderer never reached.
            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.Contains("ReadSpecFailed", entry.Reason);
            Assert.Empty(entry.Paths);
            Assert.Empty(renderer.Calls);

            // Spot-check: the same report is surfaced as Skipped with the same
            // ReadSpecFailed reason — both manifests, no silent drop.
            var spotEntry = Assert.Single(ReadSpotManifest(spot).Reports);
            Assert.Equal("rptBad", spotEntry.Name);
            Assert.Equal("Skipped", spotEntry.Status);
            Assert.Contains("ReadSpecFailed", spotEntry.Reason);
            Assert.Null(spotEntry.Path);
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_RecordsUnboundReport_AsSkipped_WithoutPdfOrAggregates()
    {
        var source = new FakeSource();
        source.Specs["rptUnbound"] = new ReportLiveSpec
        {
            ReportName = "rptUnbound",
            RecordSource = null,
            GroupLevels = ImmutableArray<ReportGroupLevelKey>.Empty,
        };
        source.All.Add("rptUnbound");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptUnbound" }, root, includePdf: true, spotCheckRoot: spot);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Skipped", entry.Status);
            Assert.Equal("UnboundReport", entry.Reason);
            Assert.Empty(entry.Paths);
            Assert.Empty(renderer.Calls);
            Assert.Empty(executor.Calls);
            Assert.False(File.Exists(Path.Combine(root, "rptUnbound", "output.pdf")));
            Assert.False(File.Exists(Path.Combine(root, "rptUnbound", "aggregates.json")));
            Assert.False(File.Exists(Path.Combine(spot, "rptUnbound", "output.pdf")));

            // The unbound report is surfaced on the spot-check manifest as
            // Skipped, never silently dropped.
            var spotEntry = Assert.Single(ReadSpotManifest(spot).Reports);
            Assert.Equal("rptUnbound", spotEntry.Name);
            Assert.Equal("Skipped", spotEntry.Status);
            Assert.Equal("UnboundReport", spotEntry.Reason);
            Assert.Null(spotEntry.Path);
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_RenderFailure_IsNonBlocking_ReportStaysCaptured_SpotCheckRecordsFailure()
    {
        var source = new FakeSource();
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.All.Add("rptA");
        var renderer = new FakeRenderer();
        renderer.Throws["rptA"] = new InvalidOperationException("printer offline");
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true, spotCheckRoot: spot);

            // Render failure must NOT change the gated capture status.
            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Captured", entry.Status);
            Assert.Null(entry.Reason);
            Assert.False(entry.Paths.ContainsKey("pdf"));
            Assert.Equal("rptA/aggregates.json", entry.Paths["aggregates"]);
            Assert.True(File.Exists(Path.Combine(root, "rptA", "aggregates.json")));

            // The render failure is surfaced on the spot-check manifest only.
            var spotEntry = Assert.Single(ReadSpotManifest(spot).Reports);
            Assert.Equal("rptA", spotEntry.Name);
            Assert.Equal("Failed", spotEntry.Status);
            Assert.Contains("RenderFailed", spotEntry.Reason);
            Assert.Null(spotEntry.Path);
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_RecordsAggregateFailure_AsFailed_WithoutInvokingRenderer()
    {
        var source = new FakeSource();
        source.Specs["rptBad"] = BoundSpec("rptBad", "Orders");
        source.All.Add("rptBad");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        executor.Throws["Orders"] = new InvalidOperationException("syntax error");
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptBad" }, root, includePdf: true, spotCheckRoot: spot);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.NotNull(entry.Reason);
            Assert.Contains("AggregateFailed", entry.Reason);
            Assert.Empty(renderer.Calls);
            Assert.False(entry.Paths.ContainsKey("pdf"));
            Assert.False(entry.Paths.ContainsKey("aggregates"));
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "output.pdf")));
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "aggregates.json")));
            Assert.False(File.Exists(Path.Combine(spot, "rptBad", "output.pdf")));

            // Aggregate failure routes the spot-check entry to Skipped (no
            // render attempted for a report that already failed the gate).
            var spotEntry = Assert.Single(ReadSpotManifest(spot).Reports);
            Assert.Equal("Skipped", spotEntry.Status);
            Assert.Contains("AggregateFailed", spotEntry.Reason);
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_RecordsReadSpecFailure_AsFailed_WithReadSpecFailedReason()
    {
        var source = new FakeSource();
        source.Throws["rptBad"] = new InvalidOperationException("opening rptBad blew up");
        source.All.Add("rptBad");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptBad" }, root);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.Contains("ReadSpecFailed", entry.Reason);
            Assert.Empty(entry.Paths);
            Assert.Empty(renderer.Calls);
            Assert.Empty(executor.Calls);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Run_OrdersManifestReportsOrdinalAscending_RegardlessOfInputOrder()
    {
        var source = new FakeSource();
        source.Specs["rptZ"] = BoundSpec("rptZ", "Orders");
        source.Specs["rptA"] = BoundSpec("rptA", "Orders");
        source.Specs["rptM"] = BoundSpec("rptM", "Orders");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptZ", "rptA", "rptM" }, root);
            Assert.Equal(new[] { "rptA", "rptM", "rptZ" }, manifest.Reports.Select(r => r.Name));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Run_ForwardsParameters_InListOrder_AsPositionalToExecutor()
    {
        var source = new FakeSource();
        source.Specs["rptByMin"] = BoundSpec("rptByMin", "PARAMETERS [Min] LONG, [Max] LONG; SELECT * FROM Orders WHERE OrderID >= [Min] AND OrderID <= [Max]");
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        // Explicitly ordered list — NOT a dictionary — so positional
        // binding is reliable regardless of any future change to
        // dictionary enumeration semantics.
        var orderedParams = new List<KeyValuePair<string, object?>>
        {
            new("Min", 5),
            new("Max", 100),
        };
        var byReport = new Dictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>
        {
            ["rptByMin"] = orderedParams,
        };

        var root = FreshRoot();
        try
        {
            runner.Run("/some.accdb", new[] { "rptByMin" }, root, byReport);

            var lastCall = Assert.Single(executor.Calls);
            Assert.NotNull(lastCall.Pos);
            Assert.Equal(new object?[] { 5, 100 }, lastCall.Pos!.ToArray());
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Run_WhenAggregateWouldFail_DoesNotAttemptRender_AndRecordsAggregateFailureOnly()
    {
        var source = new FakeSource();
        source.Specs["rptBad"] = BoundSpec("rptBad", "Orders");
        source.All.Add("rptBad");
        var renderer = new FakeRenderer();
        renderer.Throws["rptBad"] = new InvalidOperationException("printer offline");
        var executor = new FakeExecutor();
        executor.Throws["Orders"] = new InvalidOperationException("syntax error");
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptBad" }, root, includePdf: true, spotCheckRoot: spot);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.Empty(renderer.Calls);
            Assert.Empty(entry.Paths);
            Assert.NotNull(entry.Reason);
            Assert.Contains("AggregateFailed", entry.Reason);
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "output.pdf")));
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "aggregates.json")));
            Assert.False(File.Exists(Path.Combine(spot, "rptBad", "output.pdf")));
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_DefaultClean_RemovesStaleSpotCheckPdfsFromReportsNoLongerProcessed()
    {
        var source = new FakeSource();
        source.Specs["rptStale"] = BoundSpec("rptStale", "Orders");
        source.Specs["rptKeep"] = BoundSpec("rptKeep", "Orders");
        source.All.AddRange(new[] { "rptStale", "rptKeep" });
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            // First run: capture both with PDF so the stale-PDF clearing
            // assertion below has an artifact to clear.
            runner.Run("/some.accdb", new[] { "rptStale", "rptKeep" }, root, includePdf: true, spotCheckRoot: spot);
            Assert.True(File.Exists(Path.Combine(spot, "rptStale", "output.pdf")));
            Assert.True(File.Exists(Path.Combine(spot, "rptKeep", "output.pdf")));

            // Second run scoped to rptKeep only — rptStale's stale spot-check
            // dir + PDF must be cleared (default cleanRoot=true cleans both
            // the gated root and the spot-check sibling).
            runner.Run("/some.accdb", new[] { "rptKeep" }, root, includePdf: true, spotCheckRoot: spot);
            Assert.False(Directory.Exists(Path.Combine(root, "rptStale")),
                "Stale gated per-report dir should be removed when default cleanRoot=true.");
            Assert.False(Directory.Exists(Path.Combine(spot, "rptStale")),
                "Stale spot-check per-report dir should be removed when default cleanRoot=true.");
            Assert.True(File.Exists(Path.Combine(spot, "rptKeep", "output.pdf")));
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_NoCleanRoot_PreservesUnprocessedSpotCheckReports_ButRecreatesProcessedReportDirs()
    {
        var source = new FakeSource();
        source.Specs["rptStale"] = BoundSpec("rptStale", "Orders");
        source.Specs["rptKeep"] = BoundSpec("rptKeep", "Orders");
        source.All.AddRange(new[] { "rptStale", "rptKeep" });
        var renderer = new FakeRenderer();
        var executor = new FakeExecutor();
        var service = new ReportBaselineService(executor, "v-test");
        var runner = new ReportBaselineRunner(source, renderer, service, "v-test");

        var root = FreshRoot();
        var spot = FreshSpotRoot();
        try
        {
            // First run: capture both (with PDF).
            runner.Run("/some.accdb", new[] { "rptStale", "rptKeep" }, root, includePdf: true, spotCheckRoot: spot);
            Assert.True(File.Exists(Path.Combine(spot, "rptStale", "output.pdf")));
            Assert.True(File.Exists(Path.Combine(spot, "rptKeep", "output.pdf")));

            // Plant a sentinel inside rptKeep's spot dir; it must be removed
            // by the per-report directory recreate (which always runs).
            var sentinel = Path.Combine(spot, "rptKeep", "stale.txt");
            File.WriteAllText(sentinel, "stale-from-prior-run");

            // Second run scoped to rptKeep with cleanRoot=false — rptStale is
            // preserved as-is; rptKeep's sentinel is removed.
            runner.Run("/some.accdb", new[] { "rptKeep" }, root, cleanRoot: false, includePdf: true, spotCheckRoot: spot);
            Assert.True(Directory.Exists(Path.Combine(spot, "rptStale")),
                "Unprocessed report's prior spot-check dir should be preserved when cleanRoot=false.");
            Assert.True(File.Exists(Path.Combine(spot, "rptStale", "output.pdf")));
            Assert.False(File.Exists(sentinel),
                "Per-report spot-check dir is recreated for processed reports to keep current-run outputs clean.");
            Assert.True(File.Exists(Path.Combine(spot, "rptKeep", "output.pdf")));
        }
        finally
        {
            Cleanup(root, spot);
        }
    }

    [Fact]
    public void Run_EmitsBaselineManifestJson_WithStableOrdering_RegardlessOfInputOrder()
    {
        var sourceA = new FakeSource();
        sourceA.Specs["rptZ"] = BoundSpec("rptZ", "Orders");
        sourceA.Specs["rptA"] = BoundSpec("rptA", "Orders");
        var sourceB = new FakeSource();
        sourceB.Specs["rptZ"] = BoundSpec("rptZ", "Orders");
        sourceB.Specs["rptA"] = BoundSpec("rptA", "Orders");

        string Render(FakeSource src, string[] input)
        {
            var renderer = new FakeRenderer();
            var executor = new FakeExecutor();
            var service = new ReportBaselineService(executor, "v-test");
            var runner = new ReportBaselineRunner(src, renderer, service, "v-test");
            var root = FreshRoot();
            try
            {
                runner.Run("/some.accdb", input, root);
                return File.ReadAllText(Path.Combine(root, "baseline-manifest.json"));
            }
            finally
            {
                Cleanup(root);
            }
        }

        var a = Render(sourceA, new[] { "rptA", "rptZ" });
        var b = Render(sourceB, new[] { "rptZ", "rptA" });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Constructor_RejectsNullsAndEmptyHarvesterVersion()
    {
        var s = new FakeSource();
        var r = new FakeRenderer();
        var x = new FakeExecutor();
        var svc = new ReportBaselineService(x, "v");

        Assert.Throws<ArgumentNullException>(() => new ReportBaselineRunner(null!, r, svc, "v"));
        Assert.Throws<ArgumentNullException>(() => new ReportBaselineRunner(s, null!, svc, "v"));
        Assert.Throws<ArgumentNullException>(() => new ReportBaselineRunner(s, r, null!, "v"));
        Assert.Throws<ArgumentException>(() => new ReportBaselineRunner(s, r, svc, ""));
        Assert.Throws<ArgumentException>(() => new ReportBaselineRunner(s, r, svc, "   "));
    }
}
