using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="ReportMetadataExtractor"/>. Covers
/// the shared form/report shape (RecordSource / Filter / OrderBy / DefaultView /
/// Name promotion) plus report-specific features: <see cref="GroupLevel"/>
/// construction and the PageSetup deferral stub.
/// </summary>
public class ReportMetadataExtractorTests
{
    // ── RecordSource / Filter / OrderBy exact-value lifts ─────────────────────
    // (mirrors FormMetadataExtractorTests — reports use the same shared path)

    [Fact]
    public void Extract_LiftsRecordSource_WhenPresent()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "RecordSource": "qryPatientSummary" }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rptPatients", irJson);

        Assert.Equal("qryPatientSummary", meta.RecordSource);
    }

    [Fact]
    public void Extract_RecordSourceAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Report", "Properties": {} }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.RecordSource);
    }

    [Fact]
    public void Extract_LiftsFilter_WhenPresent()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "Filter": "[Status] = 'Active'" }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Equal("[Status] = 'Active'", meta.Filter);
    }

    [Fact]
    public void Extract_FilterAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Report", "Properties": { "Caption": "My Report" } }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.Filter);
    }

    [Fact]
    public void Extract_LiftsOrderBy_WhenPresent()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "OrderBy": "PatientId DESC" }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Equal("PatientId DESC", meta.OrderBy);
    }

    [Fact]
    public void Extract_OrderByAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Report", "Properties": { "Caption": "My Report" } }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.OrderBy);
    }

    [Fact]
    public void Extract_RemovesPromotedKeysFromPropertiesBag()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {
            "RecordSource": "qry",
            "Filter": "[X] = 1",
            "OrderBy": "Y",
            "Caption": "Report"
          }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.Properties);
        Assert.False(meta.Properties!.ContainsKey("RecordSource"));
        Assert.False(meta.Properties.ContainsKey("Filter"));
        Assert.False(meta.Properties.ContainsKey("OrderBy"));
        Assert.True(meta.Properties.ContainsKey("Caption"));
    }

    [Fact]
    public void Extract_UnknownPropertiesPassThroughFaithfully()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GridX": "24", "GridY": "24" }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.Properties);
        Assert.Equal("24", meta.Properties!["GridX"].GetString());
        Assert.Equal("24", meta.Properties["GridY"].GetString());
    }

    // ── DefaultView decoded lift — report-specific codes ──────────────────────

    [Theory]
    [InlineData("0", "Report View")]
    [InlineData("1", "Print Preview")]
    [InlineData("2", "Layout View")]
    public void Extract_DefaultViewDecoding_AllReportCodes(string code, string expectedLabel)
    {
        string irJson = $$"""{ "Type": "Report", "Properties": { "DefaultView": "{{code}}" } }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Equal(expectedLabel, meta.DefaultView);
    }

    [Fact]
    public void Extract_DefaultViewCode1_IsNotContinuousForms()
    {
        // Reports and forms share the same code "1" but must decode differently:
        // Forms: "Continuous Forms", Reports: "Print Preview"
        const string irJson = """{ "Type": "Report", "Properties": { "DefaultView": "1" } }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Equal("Print Preview", meta.DefaultView);
        Assert.NotEqual("Continuous Forms", meta.DefaultView);
    }

    [Fact]
    public void Extract_DefaultViewRawCodeRemainsInProperties()
    {
        const string irJson = """{ "Type": "Report", "Properties": { "DefaultView": "1" } }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Equal("Print Preview", meta.DefaultView);
        Assert.True(meta.Properties?.ContainsKey("DefaultView") == true, "Raw DefaultView code must remain in Properties bag.");
        Assert.Equal("1", meta.Properties!["DefaultView"].GetString());
    }

    [Fact]
    public void Extract_DefaultViewAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Report", "Properties": {} }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.DefaultView);
    }

    // ── Name promotion on AccessControl ───────────────────────────────────────

    [Fact]
    public void Extract_NamePromotedOnControl_RemovedFromControlPropertiesBag()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "Children": [
            {
              "Type": "PageHeader",
              "Properties": { "Name": "PageHeader", "Height": "720" }
            }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        var section = meta.Children![0];
        Assert.Equal("PageHeader", section.Name);
        if (section.Properties is not null)
            Assert.False(section.Properties.ContainsKey("Name"));
    }

    // ── GroupLevels construction ───────────────────────────────────────────────

    [Fact]
    public void Extract_NoGroupLevels_GroupLevelsIsNull()
    {
        // No GroupLevel<N>Header/Footer sections and no GroupLevel<N> properties.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "RecordSource": "tblOrders" },
          "Children": [
            { "Type": "PageHeader", "Properties": { "Name": "PageHeader" } },
            { "Type": "Detail",     "Properties": { "Name": "Detail" } },
            { "Type": "PageFooter", "Properties": { "Name": "PageFooter" } }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.GroupLevels);
    }

    [Fact]
    public void Extract_SingleGroupLevel_HeaderOnly()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GroupLevel0": "CustomerName" },
          "Children": [
            { "Type": "GroupLevel0Header", "Properties": { "Name": "GroupLevel0Header" } },
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GroupLevels);
        Assert.Single(meta.GroupLevels!);
        var gl = meta.GroupLevels![0];
        Assert.Equal(0, gl.Level);
        Assert.Equal("CustomerName", gl.GroupOn);
        Assert.True(gl.HasHeader);
        Assert.False(gl.HasFooter);
    }

    [Fact]
    public void Extract_SingleGroupLevel_FooterOnly()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GroupLevel0": "Region" },
          "Children": [
            { "Type": "GroupLevel0Footer", "Properties": { "Name": "GroupLevel0Footer" } },
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GroupLevels);
        var gl = meta.GroupLevels![0];
        Assert.Equal(0, gl.Level);
        Assert.Equal("Region", gl.GroupOn);
        Assert.False(gl.HasHeader);
        Assert.True(gl.HasFooter);
    }

    [Fact]
    public void Extract_SingleGroupLevel_BothHeaderAndFooter()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GroupLevel0": "OrderDate" },
          "Children": [
            { "Type": "GroupLevel0Header", "Properties": { "Name": "GroupLevel0Header" } },
            { "Type": "Detail", "Properties": {} },
            { "Type": "GroupLevel0Footer", "Properties": { "Name": "GroupLevel0Footer" } }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GroupLevels);
        var gl = meta.GroupLevels![0];
        Assert.Equal(0, gl.Level);
        Assert.Equal("OrderDate", gl.GroupOn);
        Assert.True(gl.HasHeader);
        Assert.True(gl.HasFooter);
    }

    [Fact]
    public void Extract_MultipleGroupLevels_OrderedByLevelAscending()
    {
        // Two groups: outermost = 0 (Customer), inner = 1 (Year).
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {
            "GroupLevel0": "CustomerName",
            "GroupLevel1": "=Year([OrderDate])"
          },
          "Children": [
            { "Type": "GroupLevel0Header", "Properties": {} },
            { "Type": "GroupLevel0Footer", "Properties": {} },
            { "Type": "GroupLevel1Header", "Properties": {} },
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GroupLevels);
        Assert.Equal(2, meta.GroupLevels!.Count);

        var g0 = meta.GroupLevels[0];
        Assert.Equal(0, g0.Level);
        Assert.Equal("CustomerName", g0.GroupOn);
        Assert.True(g0.HasHeader);
        Assert.True(g0.HasFooter);

        var g1 = meta.GroupLevels[1];
        Assert.Equal(1, g1.Level);
        Assert.Equal("=Year([OrderDate])", g1.GroupOn);
        Assert.True(g1.HasHeader);
        Assert.False(g1.HasFooter);
    }

    [Fact]
    public void Extract_GroupLevelSectionWithoutProperty_HasNullGroupOn()
    {
        // A GroupLevel<N>Header section exists but no GroupLevel<N> property.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel0Header", "Properties": {} },
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GroupLevels);
        var gl = meta.GroupLevels![0];
        Assert.Equal(0, gl.Level);
        Assert.Null(gl.GroupOn);
        Assert.True(gl.HasHeader);
        Assert.False(gl.HasFooter);
    }

    [Fact]
    public void Extract_GroupLevelKeysRemovedFromPropertiesBag()
    {
        // GroupLevel<N> keys are exact-value lifts and must not appear in the Properties bag.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {
            "GroupLevel0": "Region",
            "Caption": "My Report"
          },
          "Children": [
            { "Type": "GroupLevel0Header", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.Properties);
        Assert.False(meta.Properties!.ContainsKey("GroupLevel0"),
            "GroupLevel<N> keys must be removed from Properties bag after promotion.");
        Assert.True(meta.Properties.ContainsKey("Caption"));
    }

    [Fact]
    public void Extract_GroupLevelKeyWithEmptyValue_NotIncludedInGroupLevels()
    {
        // A GroupLevel<N> key with an empty/whitespace value should not produce a GroupLevel
        // entry unless a matching section node exists.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GroupLevel0": "" },
          "Children": [
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        // Empty GroupOn string — no section node either — should yield null GroupLevels.
        Assert.Null(meta.GroupLevels);
    }

    // ── KeepTogether / GroupingMode / SortOrder — deferred, always null ────────

    [Fact]
    public void Extract_KeepTogether_IsNullDeferredDecoding()
    {
        // GroupingMode, GroupInterval, SortOrder, KeepTogether decoding is deferred
        // per the spec. This test pins the deferral — when Session N implements it,
        // this test will need to be updated to assert the decoded value.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GroupLevel0": "Region" },
          "Children": [
            { "Type": "GroupLevel0Header", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        var gl = meta.GroupLevels![0];
        Assert.Null(gl.KeepTogether);     // deferred
        Assert.Null(gl.GroupingMode);     // deferred
        Assert.Null(gl.GroupInterval);    // deferred
        Assert.Null(gl.SortOrder);        // deferred
    }

    // ── PageSetup stub (deferred) ──────────────────────────────────────────────

    [Fact]
    public void Extract_PageSetup_IsNullDeferredDecoding()
    {
        // PrtMip / PrtDevMode binary-blob decoding is deferred. This test pins the
        // deferral — when a follow-on session implements it this test must be updated.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "PrtMip": "some-binary", "PrtDevMode": "other-binary" }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.PageSetup);
    }

    // ── Geometry, Label, NonDefaultProperties, VbaCode (shared shape) ─────────

    [Fact]
    public void Extract_GeometryIn_ReadFromRootNode()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "GeometryIn": { "W": 7.5 }
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GeometryIn);
        Assert.Equal(7.5, meta.GeometryIn!.W);
    }

    [Fact]
    public void Extract_NonDefaultProperties_PassedThrough()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "NonDefaultProperties": ["GrpKeepTogether"]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.NonDefaultProperties);
        Assert.Contains("GrpKeepTogether", meta.NonDefaultProperties!);
    }

    [Fact]
    public void Extract_VbaCodeAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Report", "Properties": {} }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.VbaCode);
    }

    [Fact]
    public void Extract_ModuleName_IsNullDeferredForSession3()
    {
        const string irJson = """{ "Type": "Report", "Properties": {} }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.ModuleName);
    }

    // ── Report name field ──────────────────────────────────────────────────────

    [Fact]
    public void Extract_ReportName_SetFromArgument()
    {
        const string irJson = """{ "Type": "Report", "Properties": {} }""";

        var meta = ReportMetadataExtractor.Extract("rptWeekly", irJson);

        Assert.Equal("rptWeekly", meta.Name);
    }

    // ── Guard conditions ───────────────────────────────────────────────────────

    [Fact]
    public void Extract_NullName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ReportMetadataExtractor.Extract(string.Empty, """{ "Type": "Report", "Properties": {} }"""));
    }

    [Fact]
    public void Extract_NullJson_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ReportMetadataExtractor.Extract("rpt", (string)null!));
    }

    [Fact]
    public void Extract_RootArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ReportMetadataExtractor.Extract("rpt", "[]"));
    }
}
