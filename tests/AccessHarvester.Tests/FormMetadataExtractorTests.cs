using System.Text.Json;
using AccessHarvester.Access;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for <see cref="FormMetadataExtractor"/>. Each test
/// fabricates a minimal IR JSON string that exercises exactly one behaviour of
/// the extractor so failures are maximally localised.
/// </summary>
public class FormMetadataExtractorTests
{
    // ── RecordSource / Filter / OrderBy exact-value lifts ─────────────────────

    [Fact]
    public void Extract_LiftsRecordSource_WhenPresent()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": { "RecordSource": "tblPatients", "SomeOther": "X" }
        }
        """;

        var meta = FormMetadataExtractor.Extract("frmPatients", irJson);

        Assert.Equal("tblPatients", meta.RecordSource);
    }

    [Fact]
    public void Extract_RecordSourceAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Form", "Properties": { "Caption": "My Form" } }""";

        var meta = FormMetadataExtractor.Extract("frmTest", irJson);

        Assert.Null(meta.RecordSource);
    }

    [Fact]
    public void Extract_LiftsFilter_WhenPresent()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": { "Filter": "[Active] = True" }
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Equal("[Active] = True", meta.Filter);
    }

    [Fact]
    public void Extract_FilterAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Form", "Properties": { "Caption": "My Form" } }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.Filter);
    }

    [Fact]
    public void Extract_LiftsOrderBy_WhenPresent()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": { "OrderBy": "LastName ASC" }
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Equal("LastName ASC", meta.OrderBy);
    }

    [Fact]
    public void Extract_OrderByAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Form", "Properties": { "Caption": "My Form" } }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.OrderBy);
    }

    [Fact]
    public void Extract_RemovesPromotedKeysFromPropertiesBag()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {
            "RecordSource": "tblPatients",
            "Filter": "[Active] = True",
            "OrderBy": "LastName ASC",
            "Caption": "Patients",
            "DefaultView": "0"
          }
        }
        """;

        var meta = FormMetadataExtractor.Extract("frmPatients", irJson);

        Assert.NotNull(meta.Properties);
        Assert.False(meta.Properties!.ContainsKey("RecordSource"), "RecordSource must be removed from bag after exact-value lift.");
        Assert.False(meta.Properties.ContainsKey("Filter"), "Filter must be removed from bag after exact-value lift.");
        Assert.False(meta.Properties.ContainsKey("OrderBy"), "OrderBy must be removed from bag after exact-value lift.");
        // Non-promoted keys remain.
        Assert.True(meta.Properties.ContainsKey("Caption"));
        Assert.True(meta.Properties.ContainsKey("DefaultView"));
    }

    [Fact]
    public void Extract_UnknownPropertiesPassThroughFaithfully()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {
            "SomeCustomProp": "value1",
            "AnotherProp": "42"
          }
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.NotNull(meta.Properties);
        Assert.Equal("value1", meta.Properties!["SomeCustomProp"].GetString());
        Assert.Equal("42", meta.Properties["AnotherProp"].GetString());
    }

    [Fact]
    public void Extract_EmptyPropertiesBag_PropertiesIsNull()
    {
        // After removing promoted keys, if nothing remains the bag should be null.
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {
            "RecordSource": "tbl",
            "Filter": null,
            "OrderBy": null
          }
        }
        """;

        // Filter/OrderBy are null-valued — ReadString returns null but they are still
        // technically present in the raw bag. The extractor excludes them from the bag
        // regardless of value (key-based exclusion, not value-based).
        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.Properties);
    }

    // ── DefaultView decoded lift ───────────────────────────────────────────────

    [Theory]
    [InlineData("0", "Single Form")]
    [InlineData("1", "Continuous Forms")]
    [InlineData("2", "Datasheet")]
    [InlineData("3", "PivotTable")]
    [InlineData("4", "PivotChart")]
    [InlineData("5", "Split Form")]
    public void Extract_DefaultViewDecoding_AllCodes(string code, string expectedLabel)
    {
        string irJson = $$"""{ "Type": "Form", "Properties": { "DefaultView": "{{code}}" } }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Equal(expectedLabel, meta.DefaultView);
    }

    [Fact]
    public void Extract_DefaultViewRawCodeRemainsInProperties()
    {
        // Decoded lift: the label is on the typed field; the raw code stays in Properties.
        const string irJson = """{ "Type": "Form", "Properties": { "DefaultView": "2" } }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Equal("Datasheet", meta.DefaultView);
        Assert.NotNull(meta.Properties);
        Assert.True(meta.Properties!.ContainsKey("DefaultView"), "Raw DefaultView code must remain in Properties bag.");
        Assert.Equal("2", meta.Properties["DefaultView"].GetString());
    }

    [Fact]
    public void Extract_DefaultViewAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Form", "Properties": {} }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.DefaultView);
    }

    [Fact]
    public void Extract_DefaultViewUnknownCode_IsNull()
    {
        // Access only emits 0-5; an unknown code should not crash — just return null.
        const string irJson = """{ "Type": "Form", "Properties": { "DefaultView": "99" } }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.DefaultView);
    }

    // ── Name promotion on AccessControl ───────────────────────────────────────

    [Fact]
    public void Extract_NamePromotedOnControl_RemovedFromControlPropertiesBag()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {},
          "Children": [
            {
              "Type": "Section",
              "Properties": { "Name": "Detail" }
            }
          ]
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.NotNull(meta.Children);
        var section = meta.Children![0];
        Assert.Equal("Detail", section.Name);
        // Name must be removed from the control's Properties bag.
        if (section.Properties is not null)
            Assert.False(section.Properties.ContainsKey("Name"), "Name must not appear in the control Properties bag after promotion.");
    }

    [Fact]
    public void Extract_ControlWithoutName_NameIsNull()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {},
          "Children": [
            { "Type": "Begin", "Properties": {} }
          ]
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.NotNull(meta.Children);
        Assert.Null(meta.Children![0].Name);
    }

    [Fact]
    public void Extract_NestedChildrenAreRecursivelyDeserialized()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {},
          "Children": [
            {
              "Type": "FormHeader",
              "Properties": { "Name": "FormHeader" },
              "Children": [
                {
                  "Type": "TextBox",
                  "Properties": { "Name": "txtTitle", "ControlSource": "LastName" }
                }
              ]
            }
          ]
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.NotNull(meta.Children);
        var header = meta.Children![0];
        Assert.Equal("FormHeader", header.Name);
        Assert.NotNull(header.Children);
        var txt = header.Children![0];
        Assert.Equal("TextBox", txt.Type);
        Assert.Equal("txtTitle", txt.Name);
        Assert.NotNull(txt.Properties);
        Assert.True(txt.Properties!.ContainsKey("ControlSource"));
    }

    // ── Geometry, Label, NonDefaultProperties round-trips ─────────────────────

    [Fact]
    public void Extract_GeometryIn_ReadFromRootNode()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {},
          "GeometryIn": { "W": 6.5, "H": 4.0 }
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.NotNull(meta.GeometryIn);
        Assert.Equal(6.5, meta.GeometryIn!.W);
        Assert.Equal(4.0, meta.GeometryIn.H);
        Assert.Null(meta.GeometryIn.X);
        Assert.Null(meta.GeometryIn.Y);
    }

    [Fact]
    public void Extract_Label_CaseInsensitiveSourceAndText()
    {
        // LabelResolver writes lowercase { source, text }; extractor must accept both casings.
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {},
          "Children": [
            {
              "Type": "TextBox",
              "Properties": { "Name": "txtFirst" },
              "Label": { "source": "attached", "text": "First Name" }
            }
          ]
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        var ctrl = meta.Children![0];
        Assert.NotNull(ctrl.Label);
        Assert.Equal("attached", ctrl.Label!.Source);
        Assert.Equal("First Name", ctrl.Label.Text);
    }

    [Fact]
    public void Extract_NonDefaultProperties_PassedThrough()
    {
        const string irJson = """
        {
          "Type": "Form",
          "Properties": {},
          "NonDefaultProperties": ["AllowEdits", "AllowDeletions"]
        }
        """;

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.NotNull(meta.NonDefaultProperties);
        Assert.Contains("AllowEdits", meta.NonDefaultProperties!);
        Assert.Contains("AllowDeletions", meta.NonDefaultProperties);
    }

    [Fact]
    public void Extract_VbaCode_PassedThrough()
    {
        const string code = "Option Compare Database\r\nPrivate Sub Form_Load()\r\nEnd Sub";
        string irJson = $$"""{ "Type": "Form", "Properties": {}, "VbaCode": "{{code.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")}}" }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Equal(code, meta.VbaCode);
    }

    [Fact]
    public void Extract_VbaCodeAbsent_IsNull()
    {
        const string irJson = """{ "Type": "Form", "Properties": {} }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.VbaCode);
    }

    // ── ModuleName is always null (deferred) ──────────────────────────────────

    [Fact]
    public void Extract_ModuleName_IsNullDeferredForSession3()
    {
        const string irJson = """{ "Type": "Form", "Properties": {} }""";

        var meta = FormMetadataExtractor.Extract("frm", irJson);

        Assert.Null(meta.ModuleName);
    }

    // ── Name field on FormMetadata ─────────────────────────────────────────────

    [Fact]
    public void Extract_FormName_SetFromArgument()
    {
        const string irJson = """{ "Type": "Form", "Properties": {} }""";

        var meta = FormMetadataExtractor.Extract("frmPatients", irJson);

        Assert.Equal("frmPatients", meta.Name);
    }

    // ── Guard conditions ───────────────────────────────────────────────────────

    [Fact]
    public void Extract_NullName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FormMetadataExtractor.Extract(string.Empty, """{ "Type": "Form", "Properties": {} }"""));
    }

    [Fact]
    public void Extract_NullJson_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FormMetadataExtractor.Extract("frm", (string)null!));
    }

    [Fact]
    public void Extract_RootArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            FormMetadataExtractor.Extract("frm", "[]"));
    }
}
