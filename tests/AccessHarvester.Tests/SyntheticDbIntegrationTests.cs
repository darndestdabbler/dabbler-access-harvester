using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using AccessHarvester.Core;
using Xunit;
// Disambiguate the Pipeline class from the AccessHarvester.Pipeline
// namespace now that Core transitively references the Pipeline assembly
// (added in Session 2 of harvester-finish-canonical-golden-output).
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

/// <summary>
/// Integration tests that drive the full Pipeline (Extract → Translate → Enrich)
/// against a committed synthetic .accdb. Requires Microsoft Access installed.
/// If the Access.Application COM ProgID is not registered on the host, all tests
/// in this class are skipped via <see cref="SkipIfNoAccess"/> — so CI on Linux /
/// dev machines without Access stays green.
/// </summary>
[SupportedOSPlatform("windows")]
public class SyntheticDbIntegrationTests
{
    private static readonly string ProjectRoot = ResolveProjectRoot();
    private static readonly string SyntheticDbDir = Path.Combine(ProjectRoot, "SyntheticDbs");

    [SkippableFact]
    public void GeometryBasic_EndToEnd_ProducesExpectedGeometryForEachControl()
    {
        SkipIfNoAccess();

        var db = Path.Combine(SyntheticDbDir, "geometry_basic.accdb");
        Skip.IfNot(File.Exists(db), $"Synthetic DB not present at {db}. Run test-builder/build_synthetic.py first.");

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-test-{Guid.NewGuid():N}");
        try
        {
            var pipeline = CorePipeline.Default().WithCategories(new[] { "Forms" });
            var result = pipeline.Run(db, outRoot);

            Assert.All(result.Extract, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
            Assert.All(result.Translate, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
            Assert.All(result.Enrich, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

            var irFile = Path.Combine(result.IrDir, "Forms", "frmGeometryBasic.ir.json");
            Assert.True(File.Exists(irFile), $"IR file not produced: {irFile}");

            var form = JsonNode.Parse(File.ReadAllText(irFile))!.AsObject();

            // Form width: 10080 twips → 7 inches.
            Assert.Equal(7.0, form["GeometryIn"]!["W"]!.GetValue<double>());

            // Collect all TextBox controls by Name, keyed by the spec's tag intent.
            var controls = CollectControlsByName(form, "TextBox");

            AssertGeometry(controls, "txtOriginWholeInch", expectedH: 0.25);
            AssertGeometry(controls, "txtOffsetWholeInches", expectedX: 2.0, expectedW: 2.0, expectedH: 0.25);
            AssertGeometry(controls, "txtFractional", expectedX: 0.25, expectedY: 0.5, expectedW: 0.5, expectedH: 0.25);
            AssertGeometry(controls, "txtNonRound", expectedX: 1.042, expectedY: 0.5, expectedW: 1.042, expectedH: 0.25);
            AssertGeometry(controls, "txtWide", expectedY: 1.0, expectedW: 6.0, expectedH: 0.375);
        }
        finally
        {
            if (Directory.Exists(outRoot))
                Directory.Delete(outRoot, recursive: true);
        }
    }

    private static void AssertGeometry(
        Dictionary<string, JsonObject> controls,
        string name,
        double? expectedX = null,
        double? expectedY = null,
        double? expectedW = null,
        double? expectedH = null)
    {
        Assert.True(controls.ContainsKey(name), $"Control '{name}' not found in IR output. Found: {string.Join(", ", controls.Keys)}");
        var geom = controls[name]["GeometryIn"]?.AsObject();
        Assert.NotNull(geom);

        AssertAxis(geom, "X", expectedX, name);
        AssertAxis(geom, "Y", expectedY, name);
        AssertAxis(geom, "W", expectedW, name);
        AssertAxis(geom, "H", expectedH, name);
    }

    private static void AssertAxis(JsonObject geom, string axis, double? expected, string controlName)
    {
        if (expected is null)
        {
            Assert.False(geom.ContainsKey(axis),
                $"{controlName}: expected GeometryIn.{axis} to be absent but got {geom[axis]}");
        }
        else
        {
            Assert.True(geom.ContainsKey(axis), $"{controlName}: expected GeometryIn.{axis} = {expected} but key was absent");
            Assert.Equal(expected.Value, geom[axis]!.GetValue<double>());
        }
    }

    private static Dictionary<string, JsonObject> CollectControlsByName(JsonObject root, string type)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        Walk(root);
        return result;

        void Walk(JsonObject node)
        {
            if (node["Type"]?.GetValue<string>() == type)
            {
                var name = node["Properties"]?["Name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(name))
                    result[name] = node;
            }
            if (node["Children"] is JsonArray children)
            {
                foreach (var c in children)
                {
                    if (c is JsonObject o) Walk(o);
                }
            }
        }
    }

    // ============================================================
    // Phase B label-resolution integration tests
    // ============================================================

    [SkippableFact]
    public void LabelsAttached_EndToEnd_AssignsAttachedCaptionToEachControl()
    {
        var form = RunPipelineAndOpenForm("labels_attached", "frmLabelsAttached");

        AssertLabel(form, "txtFirstName", source: "Attached", text: "First Name");
        AssertLabel(form, "cmbCountry", source: "Attached", text: "Country");
        AssertLabel(form, "chkActive", source: "Attached", text: "Active?");
        // Attached caption beats Round 4 binding: ControlSource=ShouldBeIgnored is
        // present but its synthesis ("Should Be Ignored") must NOT win.
        AssertLabel(form, "txtAttachedBeatsBinding", source: "Attached", text: "Custom Label");
    }

    [SkippableFact]
    public void LabelsFromName_EndToEnd_SplitsNonGenericNamesAndLeavesGenericsUnresolved()
    {
        var form = RunPipelineAndOpenForm("labels_from_name", "frmLabelsFromName");

        AssertLabel(form, "CustomerFirstName", source: "FromName", text: "Customer First Name");
        AssertLabel(form, "XMLPayload", source: "FromName", text: "XML Payload");
        AssertLabel(form, "OrderTotal", source: "FromName", text: "Order Total");
        AssertLabel(form, "Text99", source: "Unresolved", text: null);
    }

    [SkippableFact]
    public void LabelsBinding_EndToEnd_SplitsIdentifierControlSourcesAndSkipsExpressions()
    {
        var form = RunPipelineAndOpenForm("labels_binding", "frmLabelsBinding");

        AssertLabel(form, "Text1", source: "FromBinding", text: "First Name");
        AssertLabel(form, "Text2", source: "FromBinding", text: "Order ID");
        AssertLabel(form, "Text3", source: "Unresolved", text: null);   // expression rejected
        AssertLabel(form, "Text4", source: "Unresolved", text: null);   // no ControlSource
    }

    [SkippableFact]
    public void LabelsProximity_EndToEnd_PipelineRunsAndProducesOutput()
    {
        var form = RunPipelineAndOpenForm("labels_proximity", "frmLabelsProximity");

        // Source assertions (text values for proximity matches are deferred to
        // a future label-quality session set — UAT machinery is out of scope here).
        AssertLabelSource(form, "Text1", "Proximity");
        AssertLabelSource(form, "Text2", "Proximity");
        AssertLabelSource(form, "Text3", "Unresolved");
        AssertLabelSource(form, "Text4", "Unresolved");
        AssertLabelSource(form, "Text5", "Unresolved");
        AssertLabelSource(form, "Text6", "Unresolved");
        AssertLabelSource(form, "Text7", "Proximity");
    }

    // ----- helpers shared by the four label-resolution tests -----

    private static JsonObject RunPipelineAndOpenForm(string specName, string formName)
    {
        SkipIfNoAccess();
        var db = Path.Combine(SyntheticDbDir, specName + ".accdb");
        Skip.IfNot(File.Exists(db), $"Synthetic DB not present at {db}. Run test-builder/build_synthetic.py {specName} first.");

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-test-{Guid.NewGuid():N}");
        try
        {
            var pipeline = CorePipeline.Default().WithCategories(new[] { "Forms" });
            var result = pipeline.Run(db, outRoot);

            Assert.All(result.Extract, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
            Assert.All(result.Translate, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));
            Assert.All(result.Enrich, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

            var irFile = Path.Combine(result.IrDir, "Forms", formName + ".ir.json");
            Assert.True(File.Exists(irFile), $"IR file not produced: {irFile}");

            // The pipeline writes to outRoot; copy the IR into memory before we
            // delete the temp dir in the finally below so the returned JsonObject
            // is fully detached.
            return JsonNode.Parse(File.ReadAllText(irFile))!.AsObject();
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    private static void AssertLabel(JsonObject form, string controlName, string source, string? text)
    {
        var ctrl = FindControlByName(form, controlName);
        Assert.True(ctrl is not null, $"Control '{controlName}' not found in IR output.");
        Assert.True(ctrl!["Label"] is JsonObject, $"Control '{controlName}' has no Label key.");
        var label = ctrl["Label"]!.AsObject();
        Assert.Equal(source, label["source"]?.GetValue<string>());
        if (text is null)
            Assert.False(label.ContainsKey("text"), $"{controlName}: expected no text but got '{label["text"]}'");
        else
            Assert.Equal(text, label["text"]?.GetValue<string>());
    }

    private static void AssertLabelSource(JsonObject form, string controlName, string source)
    {
        var ctrl = FindControlByName(form, controlName);
        Assert.True(ctrl is not null, $"Control '{controlName}' not found in IR output.");
        Assert.Equal(source, ctrl!["Label"]?["source"]?.GetValue<string>());
    }

    private static JsonObject? FindControlByName(JsonObject root, string name)
    {
        JsonObject? result = null;
        Walk(root);
        return result;

        void Walk(JsonObject n)
        {
            if (result is not null) return;
            // Match by Name property AND skip Label nodes (the spec's peer labels
            // are also named, but they're not the controls we want to inspect).
            if (n["Type"]?.GetValue<string>() != "Label"
                && n["Properties"] is JsonObject p
                && p["Name"]?.GetValue<string>() == name)
            {
                result = n;
                return;
            }
            if (n["Children"] is JsonArray arr)
            {
                foreach (var c in arr)
                {
                    if (c is JsonObject co) Walk(co);
                }
            }
        }
    }

    private static void SkipIfNoAccess()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Access COM is Windows-only.");
        Skip.If(Type.GetTypeFromProgID("Access.Application") is null,
            "Microsoft Access is not installed (Access.Application ProgID not registered).");
    }

    private static string ResolveProjectRoot()
    {
        // AppContext.BaseDirectory points at bin/Debug/netXX/. Walk up to the test
        // project folder (where SyntheticDbs/ is copied-to-output via csproj).
        return AppContext.BaseDirectory;
    }
}
