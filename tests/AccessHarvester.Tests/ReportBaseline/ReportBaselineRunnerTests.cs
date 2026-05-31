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

    private static ReportLiveSpec BoundSpec(string name, string rs) => new()
    {
        ReportName = name,
        RecordSource = rs,
        GroupLevels = ImmutableArray<ReportGroupLevelKey>.Empty,
    };

    [Fact]
    public void Run_WritesPdf_Aggregates_AndManifest_ForCapturedReport()
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
            var manifest = runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true);

            Assert.True(File.Exists(Path.Combine(root, "rptA", "output.pdf")));
            Assert.True(File.Exists(Path.Combine(root, "rptA", "aggregates.json")));
            Assert.True(File.Exists(Path.Combine(root, "baseline-manifest.json")));

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("rptA", entry.Name);
            Assert.Equal("Captured", entry.Status);
            Assert.Null(entry.Reason);
            Assert.Equal("rptA/output.pdf", entry.Paths["pdf"]);
            Assert.Equal("rptA/aggregates.json", entry.Paths["aggregates"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Run_WithoutIncludePdf_WritesAggregatesOnly_AndNeverCallsRenderer()
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
            // aggregates.json written; output.pdf is NOT — and the
            // renderer was never invoked at all.
            Assert.Equal("rptA/aggregates.json", entry.Paths["aggregates"]);
            Assert.False(entry.Paths.ContainsKey("pdf"));
            Assert.True(File.Exists(Path.Combine(root, "rptA", "aggregates.json")));
            Assert.False(File.Exists(Path.Combine(root, "rptA", "output.pdf")));
            Assert.Empty(renderer.Calls);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptUnbound" }, root);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Skipped", entry.Status);
            Assert.Equal("UnboundReport", entry.Reason);
            Assert.Empty(entry.Paths);
            Assert.Empty(renderer.Calls);
            Assert.Empty(executor.Calls);
            // Per-report dir is created up-front; that's fine, but the
            // PDF + aggregates files must not exist.
            Assert.False(File.Exists(Path.Combine(root, "rptUnbound", "output.pdf")));
            Assert.False(File.Exists(Path.Combine(root, "rptUnbound", "aggregates.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Run_RecordsRenderFailure_AsFailed_ButStillWritesAggregates()
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
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptA" }, root, includePdf: true);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.NotNull(entry.Reason);
            Assert.Contains("RenderFailed", entry.Reason);
            // No PDF on render failure; aggregates still written.
            Assert.False(entry.Paths.ContainsKey("pdf"));
            Assert.Equal("rptA/aggregates.json", entry.Paths["aggregates"]);
            Assert.True(File.Exists(Path.Combine(root, "rptA", "aggregates.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Run_RecordsAggregateFailure_AsFailed_ButStillWritesPdf()
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
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptBad" }, root, includePdf: true);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.NotNull(entry.Reason);
            Assert.Contains("AggregateFailed", entry.Reason);
            Assert.Equal("rptBad/output.pdf", entry.Paths["pdf"]);
            Assert.False(entry.Paths.ContainsKey("aggregates"));
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "aggregates.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Run_WithBothRenderAndAggregateFailure_RecordsFailedWithJoinedReasonAndEmptyPaths()
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
        try
        {
            var manifest = runner.Run("/some.accdb", new[] { "rptBad" }, root, includePdf: true);

            var entry = Assert.Single(manifest.Reports);
            Assert.Equal("Failed", entry.Status);
            Assert.Empty(entry.Paths);
            Assert.NotNull(entry.Reason);
            Assert.Contains("RenderFailed", entry.Reason);
            Assert.Contains("AggregateFailed", entry.Reason);
            // The joined-reason separator is "; ".
            Assert.Contains("; ", entry.Reason);
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "output.pdf")));
            Assert.False(File.Exists(Path.Combine(root, "rptBad", "aggregates.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Run_DefaultClean_RemovesStaleArtifactsFromReportsNoLongerProcessed()
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
        try
        {
            // First run: capture both (with PDF so the stale-PDF clearing
            // assertion below has an artifact to clear).
            runner.Run("/some.accdb", new[] { "rptStale", "rptKeep" }, root, includePdf: true);
            Assert.True(File.Exists(Path.Combine(root, "rptStale", "output.pdf")));
            Assert.True(File.Exists(Path.Combine(root, "rptKeep", "output.pdf")));

            // Second run scoped to rptKeep only — rptStale's stale dir + PDF
            // must be cleared. Default cleanRoot=true is the policy that
            // delivers this.
            runner.Run("/some.accdb", new[] { "rptKeep" }, root, includePdf: true);
            Assert.False(Directory.Exists(Path.Combine(root, "rptStale")),
                "Stale per-report dir should be removed when default cleanRoot=true.");
            Assert.True(File.Exists(Path.Combine(root, "rptKeep", "output.pdf")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Run_NoCleanRoot_PreservesUnprocessedReports_ButRecreatesProcessedReportDirs()
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
        try
        {
            // First run: capture both (with PDF).
            runner.Run("/some.accdb", new[] { "rptStale", "rptKeep" }, root, includePdf: true);
            Assert.True(File.Exists(Path.Combine(root, "rptStale", "output.pdf")));
            Assert.True(File.Exists(Path.Combine(root, "rptKeep", "output.pdf")));

            // Plant a sentinel file inside rptKeep's dir; it must be removed
            // by the per-report directory recreate (which always runs, even
            // under cleanRoot:false, to keep this-run-Captured artifacts
            // uncontaminated).
            var sentinel = Path.Combine(root, "rptKeep", "stale.txt");
            File.WriteAllText(sentinel, "stale-from-prior-run");

            // Second run scoped to rptKeep with cleanRoot=false — rptStale
            // is preserved as-is; rptKeep's sentinel is removed.
            runner.Run("/some.accdb", new[] { "rptKeep" }, root, cleanRoot: false, includePdf: true);
            Assert.True(Directory.Exists(Path.Combine(root, "rptStale")),
                "Unprocessed report's prior dir should be preserved when cleanRoot=false.");
            Assert.True(File.Exists(Path.Combine(root, "rptStale", "output.pdf")));
            Assert.False(File.Exists(sentinel),
                "Per-report dir is always recreated for processed reports to keep current-run outputs clean.");
            Assert.True(File.Exists(Path.Combine(root, "rptKeep", "output.pdf")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
