using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

/// <summary>
/// Hermetic pins for <see cref="RibbonStructureBuilder"/> — the pure
/// artifact-to-structure step that feeds <c>NavigationGraphInputs.Ribbon</c>.
/// </summary>
public class RibbonStructureBuilderTests
{
    private const string OneTabXml = """
<customUI xmlns="http://schemas.microsoft.com/office/2006/01/customui">
  <ribbon><tabs><tab id="t1" label="Home">
    <group id="g1" label="G"><button id="b1" label="B" onAction="B_OnAction"/></group>
  </tab></tabs></ribbon>
</customUI>
""";

    private static RibbonXmlArtifact Artifact(params RibbonXmlRow[] rows) =>
        new() { DetectedTables = rows.Select(r => r.Table).Distinct().ToArray(), Rows = rows };

    [Fact]
    public void NullOrEmptyArtifact_YieldsNullRibbon()
    {
        Assert.Null(RibbonStructureBuilder.Build(null).Ribbon);
        Assert.Null(RibbonStructureBuilder.Build(new RibbonXmlArtifact()).Ribbon);
    }

    [Fact]
    public void ShapeGatedRow_ProducesPopulatedRibbon_NamedFromRow()
    {
        var outcome = RibbonStructureBuilder.Build(Artifact(
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Main", RibbonXml = OneTabXml }));

        Assert.NotNull(outcome.Ribbon);
        Assert.Equal("Main", outcome.Ribbon!.Name);
        Assert.Equal("t1", outcome.Ribbon.Tabs.Single().Id);
        Assert.Equal("B_OnAction",
            outcome.Ribbon.Tabs.Single().Groups.Single().Controls.Single().OnAction);
        Assert.Equal(0, outcome.AdditionalRibbonCount);
        Assert.Empty(outcome.MalformedRows);
    }

    [Fact]
    public void EmptyXmlRow_YieldsNullRibbon()
    {
        var outcome = RibbonStructureBuilder.Build(Artifact(
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Blank", RibbonXml = "   " }));
        Assert.Null(outcome.Ribbon);
    }

    [Fact]
    public void MultipleRibbonRows_SurfaceOneDeterministically_AndReportTheRest()
    {
        // Two parseable ribbons; the first by RibbonName (ordinal) is surfaced,
        // the other is COUNTED (preserved in raw), never silently dropped.
        var outcome = RibbonStructureBuilder.Build(Artifact(
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Zeta", RibbonXml = OneTabXml },
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Alpha", RibbonXml = OneTabXml }));

        Assert.Equal("Alpha", outcome.Ribbon!.Name); // ordinal-first
        Assert.Equal(1, outcome.AdditionalRibbonCount);
    }

    [Fact]
    public void DuplicateNameAndTable_SelectsDeterministically_ByXmlTiebreaker()
    {
        // Same RibbonName + Table on both rows (RibbonName is not unique); the
        // surfaced ribbon must NOT depend on row order — RibbonXml breaks the tie.
        const string xmlA = """<customUI xmlns="x"><ribbon startFromScratch="false"><tabs><tab id="aaa" label="A"/></tabs></ribbon></customUI>""";
        const string xmlB = """<customUI xmlns="x"><ribbon startFromScratch="false"><tabs><tab id="bbb" label="B"/></tabs></ribbon></customUI>""";
        var forward = RibbonStructureBuilder.Build(Artifact(
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Dup", RibbonXml = xmlA },
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Dup", RibbonXml = xmlB }));
        var reversed = RibbonStructureBuilder.Build(Artifact(
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Dup", RibbonXml = xmlB },
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Dup", RibbonXml = xmlA }));

        // xmlA sorts before xmlB ordinally -> "aaa" surfaced regardless of input order.
        Assert.Equal("aaa", forward.Ribbon!.Tabs.Single().Id);
        Assert.Equal("aaa", reversed.Ribbon!.Tabs.Single().Id);
    }

    [Fact]
    public void MalformedRow_IsRecorded_AndDoesNotBlockAGoodRow()
    {
        var outcome = RibbonStructureBuilder.Build(Artifact(
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Bad", RibbonXml = "<customUI><ribbon>" },
            new RibbonXmlRow { Table = "USysRibbons", RibbonName = "Good", RibbonXml = OneTabXml }));

        Assert.Equal("Good", outcome.Ribbon!.Name);
        Assert.Contains("USysRibbons/Bad", outcome.MalformedRows);
    }
}
