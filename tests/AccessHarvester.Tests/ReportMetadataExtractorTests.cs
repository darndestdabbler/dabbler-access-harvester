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
    // GroupLevel configuration nodes have Type = "GroupLevel N" (space-separated),
    // produced by AccessTextParser from Access's "Begin GroupLevel N" blocks.
    // The grouping field expression is in Properties["GroupOnCol"].
    // Section bands (GroupLevel0Header / GroupLevel0Footer) are separate child nodes.

    [Fact]
    public void Extract_NoGroupLevels_GroupLevelsIsNull()
    {
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
        // Configuration node ("GroupLevel 0") + section node ("GroupLevel0Header").
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "CustomerName", "GroupOn": "0" } },
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
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "Region", "GroupOn": "0" } },
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
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "OrderDate", "GroupOn": "2", "SortOrder": "0", "KeepTogether": "1" } },
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
        Assert.Equal("Year", gl.GroupingMode);
        Assert.Equal("Ascending", gl.SortOrder);
        Assert.Equal("WholeGroup", gl.KeepTogether);
    }

    [Fact]
    public void Extract_MultipleGroupLevels_OrderedByLevelAscending()
    {
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "CustomerName", "GroupOn": "0", "SortOrder": "0" } },
            { "Type": "GroupLevel 1", "Properties": { "GroupOnCol": "=Year([OrderDate])", "GroupOn": "2" } },
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
        Assert.Equal("EachValue", g0.GroupingMode);
        Assert.Equal("Ascending", g0.SortOrder);
        Assert.True(g0.HasHeader);
        Assert.True(g0.HasFooter);

        var g1 = meta.GroupLevels[1];
        Assert.Equal(1, g1.Level);
        Assert.Equal("=Year([OrderDate])", g1.GroupOn);
        Assert.Equal("Year", g1.GroupingMode);
        Assert.True(g1.HasHeader);
        Assert.False(g1.HasFooter);
    }

    [Fact]
    public void Extract_GroupLevelSectionWithoutConfigNode_HasNullGroupOn()
    {
        // A GroupLevel0Header exists but no "GroupLevel 0" configuration node.
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
    public void Extract_GroupLevelConfigNodes_ExcludedFromChildren()
    {
        // "GroupLevel 0" config nodes are metadata; they must not appear in Children
        // alongside design-band sections (PageHeader, Detail, etc.).
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "Region" } },
            { "Type": "GroupLevel0Header", "Properties": { "Name": "GroupLevel0Header" } },
            { "Type": "Detail", "Properties": { "Name": "Detail" } }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.Children);
        Assert.DoesNotContain(meta.Children!, c => c.Type == "GroupLevel 0");
        Assert.Contains(meta.Children!, c => c.Type == "GroupLevel0Header");
        Assert.Contains(meta.Children!, c => c.Type == "Detail");
    }

    [Fact]
    public void Extract_GroupLevelConfigWithoutSectionNodes_HasFalseHeaderAndFooter()
    {
        // A "GroupLevel 0" config node exists but no Header/Footer section bands.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "Region" } },
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.NotNull(meta.GroupLevels);
        var gl = meta.GroupLevels![0];
        Assert.Equal("Region", gl.GroupOn);
        Assert.False(gl.HasHeader);
        Assert.False(gl.HasFooter);
    }

    [Fact]
    public void Extract_GroupLevelConfigNodeNotInRootProperties()
    {
        // GroupLevel configuration is sourced from child nodes, NOT root Properties.
        // A "GroupLevel0" flat key in root Properties should not produce a GroupLevel entry.
        const string irJson = """
        {
          "Type": "Report",
          "Properties": { "GroupLevel0": "ShouldBeIgnored", "Caption": "My Report" },
          "Children": [
            { "Type": "Detail", "Properties": {} }
          ]
        }
        """;

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        // GroupLevel0 in root Properties is not the source-of-truth; no config child node → null.
        Assert.Null(meta.GroupLevels);
        // The key must remain in the Properties bag (it's not a promoted key).
        Assert.True(meta.Properties?.ContainsKey("GroupLevel0") == true);
    }

    // ── GroupingMode / SortOrder / KeepTogether / GroupInterval decoding ──────

    [Theory]
    [InlineData("0", "EachValue")]
    [InlineData("1", "Prefix")]
    [InlineData("2", "Year")]
    [InlineData("3", "Quarter")]
    [InlineData("4", "Month")]
    [InlineData("5", "Week")]
    [InlineData("6", "Day")]
    [InlineData("7", "Hour")]
    [InlineData("8", "Minute")]
    [InlineData("9", "Interval")]
    public void Extract_GroupingMode_AllCodes(string code, string expected)
    {
        string irJson = $$"""
        {
          "Type": "Report", "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "X", "GroupOn": "{{code}}" } }
          ]
        }
        """;
        var gl = ReportMetadataExtractor.Extract("rpt", irJson).GroupLevels![0];
        Assert.Equal(expected, gl.GroupingMode);
    }

    [Theory]
    [InlineData("0", "Ascending")]
    [InlineData("1", "Descending")]
    public void Extract_SortOrder_AllCodes(string code, string expected)
    {
        string irJson = $$"""
        {
          "Type": "Report", "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "X", "SortOrder": "{{code}}" } }
          ]
        }
        """;
        var gl = ReportMetadataExtractor.Extract("rpt", irJson).GroupLevels![0];
        Assert.Equal(expected, gl.SortOrder);
    }

    [Theory]
    [InlineData("0", "None")]
    [InlineData("1", "WholeGroup")]
    [InlineData("2", "WithFirstDetail")]
    public void Extract_KeepTogether_AllCodes(string code, string expected)
    {
        string irJson = $$"""
        {
          "Type": "Report", "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "X", "KeepTogether": "{{code}}" } }
          ]
        }
        """;
        var gl = ReportMetadataExtractor.Extract("rpt", irJson).GroupLevels![0];
        Assert.Equal(expected, gl.KeepTogether);
    }

    [Fact]
    public void Extract_GroupInterval_ParsedAsInt()
    {
        const string irJson = """
        {
          "Type": "Report", "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "X", "GroupInterval": "3" } }
          ]
        }
        """;
        var gl = ReportMetadataExtractor.Extract("rpt", irJson).GroupLevels![0];
        Assert.Equal(3, gl.GroupInterval);
    }

    [Fact]
    public void Extract_GroupingMode_UnknownCode_IsNull()
    {
        const string irJson = """
        {
          "Type": "Report", "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "X", "GroupOn": "99" } }
          ]
        }
        """;
        var gl = ReportMetadataExtractor.Extract("rpt", irJson).GroupLevels![0];
        Assert.Null(gl.GroupingMode);
    }

    [Fact]
    public void Extract_PerLevelMetadata_AbsentWhenConfigNodeHasNoProperties()
    {
        // Config node present but no per-level property keys → all null.
        const string irJson = """
        {
          "Type": "Report", "Properties": {},
          "Children": [
            { "Type": "GroupLevel 0", "Properties": { "GroupOnCol": "X" } },
            { "Type": "GroupLevel0Header", "Properties": {} }
          ]
        }
        """;
        var gl = ReportMetadataExtractor.Extract("rpt", irJson).GroupLevels![0];
        Assert.Null(gl.GroupingMode);
        Assert.Null(gl.SortOrder);
        Assert.Null(gl.KeepTogether);
        Assert.Null(gl.GroupInterval);
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
    public void Extract_ModuleName_ExtractedFromVbName()
    {
        const string vbaCode = "Attribute VB_Name = \"Report_rptWeekly\"\r\nOption Compare Database";
        string irJson = $$"""{ "Type": "Report", "Properties": {}, "VbaCode": "{{vbaCode.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")}}" }""";

        var meta = ReportMetadataExtractor.Extract("rptWeekly", irJson);

        Assert.Equal("Report_rptWeekly", meta.ModuleName);
    }

    [Fact]
    public void Extract_ModuleName_IsNullWhenVbaCodeAbsent()
    {
        const string irJson = """{ "Type": "Report", "Properties": {} }""";

        var meta = ReportMetadataExtractor.Extract("rpt", irJson);

        Assert.Null(meta.ModuleName);
    }

    [Fact]
    public void Extract_ModuleName_IsNullWhenVbNameLineAbsent()
    {
        // VbaCode present but no Attribute VB_Name line (e.g. synthetic test code).
        const string irJson = """{ "Type": "Report", "Properties": {}, "VbaCode": "Option Compare Database\r\nOption Explicit" }""";

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
