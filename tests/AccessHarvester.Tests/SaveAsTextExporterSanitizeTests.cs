using System.Linq;
using AccessHarvester.Core.Extraction;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic pins for SaveAsText raw-export object-name sanitisation (006 F-1-2).
/// Access permits path separators in object names (proven by DAO probe:
/// <c>CreateQueryDef</c> stored <c>probe/slash</c> and <c>probe\bs</c> verbatim);
/// an unsanitised <c>Path.Combine(outDir, name + ".txt")</c> then targets a
/// non-existent subdirectory and fails that object's export (exit 3) on an
/// otherwise-valid database. These exercise the pure planning logic that drives
/// the COM export loop.
/// </summary>
public class SaveAsTextExporterSanitizeTests
{
    [Theory]
    [InlineData("probe/slash", "probe_slash.txt")]
    [InlineData("probe\\bs", "probe_bs.txt")]
    [InlineData("a/b\\c", "a_b_c.txt")]
    public void PlanOutputFiles_SeparatorBearingName_SanitisesToFlatFileName(string name, string expected)
    {
        var plan = SaveAsTextExporter.PlanOutputFiles(new[] { name });

        var entry = Assert.Single(plan);
        Assert.Equal(name, entry.Name);
        Assert.Null(entry.CollisionError);
        Assert.Equal(expected, entry.FileName);
    }

    [Fact]
    public void PlanOutputFiles_OrdinaryName_IsUnchanged()
    {
        // The HomeHealthCare corpus is all Hungarian-named (frmFoo, rptBar) with no
        // invalid characters, so sanitisation is identity there — this is the
        // golden-neutrality guarantee (no regen needed) made mechanical.
        var plan = SaveAsTextExporter.PlanOutputFiles(new[] { "frmClientList" });

        Assert.Equal("frmClientList.txt", Assert.Single(plan).FileName);
    }

    [Fact]
    public void PlanOutputFiles_TwoDistinctNamesThatCollide_ReportsCollisionNotSilentOverwrite()
    {
        // "a/b" and "a\b" both sanitise to "a_b.txt": the second is reported as a
        // collision (a reportable failure) rather than silently overwriting the
        // first object's exported file.
        var plan = SaveAsTextExporter.PlanOutputFiles(new[] { "a/b", "a\\b" });

        Assert.Equal(2, plan.Count);
        Assert.Equal("a_b.txt", plan[0].FileName);
        Assert.Null(plan[0].CollisionError);

        Assert.Null(plan[1].FileName);
        Assert.NotNull(plan[1].CollisionError);
        Assert.Contains("a_b.txt", plan[1].CollisionError!);
        Assert.Contains("a/b", plan[1].CollisionError!); // names the already-exported object
    }

    [Fact]
    public void PlanOutputFiles_DistinctNonCollidingNames_AllExport()
    {
        var plan = SaveAsTextExporter.PlanOutputFiles(new[] { "frmA", "frmB", "rptC" });

        Assert.All(plan, e => Assert.Null(e.CollisionError));
        Assert.Equal(new[] { "frmA.txt", "frmB.txt", "rptC.txt" }, plan.Select(e => e.FileName).ToArray());
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("frmClient")]
    [InlineData("rpt_Profile_2024")]
    public void SanitizeFileName_OrdinaryName_IsIdentity(string name)
    {
        Assert.Equal(name, SaveAsTextExporter.SanitizeFileName(name));
    }

    [Fact]
    public void SanitizeFileName_ReplacesPathSeparators()
    {
        Assert.Equal("a_b", SaveAsTextExporter.SanitizeFileName("a/b"));
        Assert.Equal("a_b", SaveAsTextExporter.SanitizeFileName("a\\b"));
    }
}
