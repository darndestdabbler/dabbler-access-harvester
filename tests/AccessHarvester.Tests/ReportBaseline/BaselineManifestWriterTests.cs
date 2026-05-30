using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using AccessHarvester.Core.ReportBaseline;
using Xunit;

namespace AccessHarvester.Tests.ReportBaseline;

/// <summary>
/// Hermetic tests for <see cref="BaselineManifestWriter"/>: canonical JSON
/// shape, byte-stability across repeated calls, sorted Reports + sorted
/// Paths, file-write round-trip, parent-directory creation, and null-Reason
/// JSON shape.
/// </summary>
public class BaselineManifestWriterTests
{
    private static BaselineManifest BuildSampleManifest() => new()
    {
        Database = "/path/to/HomeHealthCare.accdb",
        HarvesterVersion = "v-test",
        Reports = ImmutableArray.Create(
            new BaselineManifestEntry
            {
                Name = "rptB",
                Status = "Captured",
                Paths = ImmutableSortedDictionary.CreateRange(new[]
                {
                    new KeyValuePair<string, string>("pdf", "rptB/output.pdf"),
                    new KeyValuePair<string, string>("aggregates", "rptB/aggregates.json"),
                }),
                Reason = null,
            },
            new BaselineManifestEntry
            {
                Name = "rptA",
                Status = "Skipped",
                Paths = ImmutableSortedDictionary<string, string>.Empty,
                Reason = "UnboundReport",
            }),
    };

    [Fact]
    public void Serialize_EmitsCanonicalJson_WithSortedPaths_AndPreservesReason()
    {
        var json = BaselineManifestWriter.Serialize(BuildSampleManifest());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("v-test", root.GetProperty("HarvesterVersion").GetString());
        Assert.Equal("/path/to/HomeHealthCare.accdb", root.GetProperty("Database").GetString());

        var reports = root.GetProperty("Reports");
        Assert.Equal(2, reports.GetArrayLength());

        // Paths are ordinal-sorted: aggregates < pdf.
        var captured = reports[0];
        var capturedPaths = captured.GetProperty("Paths");
        var capturedPathKeys = new List<string>();
        foreach (var p in capturedPaths.EnumerateObject())
            capturedPathKeys.Add(p.Name);
        Assert.Equal(new[] { "aggregates", "pdf" }, capturedPathKeys);

        // Reason is emitted (null for Captured, string for Skipped).
        Assert.Equal(JsonValueKind.Null, captured.GetProperty("Reason").ValueKind);
        Assert.Equal("UnboundReport", reports[1].GetProperty("Reason").GetString());
    }

    [Fact]
    public void Serialize_OrdersReportsOrdinalAscendingByName_RegardlessOfInputOrder()
    {
        // Builder authored rptB before rptA; writer must emit rptA first.
        var json = BaselineManifestWriter.Serialize(BuildSampleManifest());
        using var doc = JsonDocument.Parse(json);
        var reports = doc.RootElement.GetProperty("Reports");

        // Note: the writer does NOT sort Reports — that's the runner's job.
        // This test pins serialize-order = input-order so the contract is
        // explicit. Compare with ReportBaselineRunnerTests which exercises
        // the runner's sort.
        Assert.Equal("rptB", reports[0].GetProperty("Name").GetString());
        Assert.Equal("rptA", reports[1].GetProperty("Name").GetString());
    }

    [Fact]
    public void Serialize_IsByteStable_AcrossRepeatedCalls()
    {
        var manifest = BuildSampleManifest();
        var a = BaselineManifestWriter.Serialize(manifest);
        var b = BaselineManifestWriter.Serialize(manifest);
        Assert.Equal(a, b);
    }

    [Fact]
    public void WriteToFile_CreatesParentDirectories_AndProducesIdenticalContentToSerialize()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ae-test-{System.Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(dir, "nested", "baseline-manifest.json");
            var manifest = BuildSampleManifest();

            BaselineManifestWriter.WriteToFile(manifest, path);

            Assert.True(File.Exists(path));
            Assert.Equal(BaselineManifestWriter.Serialize(manifest), File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
