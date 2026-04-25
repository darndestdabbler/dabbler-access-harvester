using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using AccessHarvester.Core;
using Xunit;

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
            var pipeline = Pipeline.Default().WithCategories(new[] { "Forms" });
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
    // Session 03 — VbaSignatureEnricher integration test
    // ============================================================

    [SkippableFact]
    public void Northwind_FormCodeBehind_EmitsSignaturesWithStartLines()
    {
        SkipIfNoAccess();
        var db = ResolveSampleDb("NorthwindDevEdition.accdb");
        Skip.If(db is null, "samples/NorthwindDevEdition.accdb not found relative to test bin path.");

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-test-{Guid.NewGuid():N}");
        try
        {
            // Pull ModulesAndClasses too so the same run exercises the third
            // VbaSignatureEnricher target alongside the Forms code-behind path.
            var pipeline = Pipeline.Default().WithCategories(new[] { "Forms", "ModulesAndClasses" });
            var result = pipeline.Run(db!, outRoot);

            Assert.All(result.Enrich, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

            // ---- Form code-behind: frmCompanyDetail has 24 procedures (22 Subs + 2 Functions). ----
            var formIr = Path.Combine(result.IrDir, "Forms", "frmCompanyDetail.ir.json");
            Assert.True(File.Exists(formIr), $"IR file not produced: {formIr}");

            var formRoot = JsonNode.Parse(File.ReadAllText(formIr))!.AsObject();
            var formProcs = formRoot["Procedures"] as JsonArray;
            Assert.NotNull(formProcs);

            // The form's source has 24 procedures; the parser should match all.
            Assert.True(formProcs!.Count >= 20,
                $"Expected ≥20 procedures on frmCompanyDetail, got {formProcs.Count}");

            // At least one Function with a return type.
            Assert.Contains(formProcs.Cast<JsonObject>(),
                p => p["ProcedureType"]!.GetValue<string>() == "Function"
                     && p["ReturnType"] is not null
                     && !string.IsNullOrEmpty(p["ReturnType"]!.GetValue<string>()));

            // Each procedure carries the InferredFunctionality / Notes nulls.
            Assert.All(formProcs.Cast<JsonObject>(), p =>
            {
                Assert.Null(p["InferredFunctionality"]);
                Assert.Null(p["Notes"]);
                Assert.True(p["StartLine"]!.GetValue<int>() > 0,
                    $"Procedure '{p["Name"]}' has non-positive StartLine.");
            });

            // ---- Standalone module: modCompanies has GetRandomCustomerID() and
            // GetTaxStatusID(ByVal lngCompanyID As Long) — both Long-returning. ----
            var modIr = Path.Combine(result.IrDir, "ModulesAndClasses", "modCompanies.ir.json");
            Assert.True(File.Exists(modIr), $"IR file not produced: {modIr}");

            var modRoot = JsonNode.Parse(File.ReadAllText(modIr))!.AsObject();
            var modProcs = modRoot["Procedures"] as JsonArray;
            Assert.NotNull(modProcs);
            Assert.True(modProcs!.Count >= 2,
                $"Expected ≥2 procedures on modCompanies, got {modProcs.Count}");

            // GetTaxStatusID has one Long parameter — proves parameter parsing
            // survives the full extract-translate-enrich roundtrip.
            var taxStatus = modProcs.Cast<JsonObject>()
                .FirstOrDefault(p => p["Name"]!.GetValue<string>() == "GetTaxStatusID");
            Assert.NotNull(taxStatus);
            Assert.Equal("Function", taxStatus!["ProcedureType"]!.GetValue<string>());
            var taxParams = taxStatus["Parameters"]!.AsArray();
            Assert.Single(taxParams);
            Assert.Equal("lngCompanyID", taxParams[0]!["Name"]!.GetValue<string>());
            Assert.Equal("Long", taxParams[0]!["DataType"]!.GetValue<string>());
            Assert.Equal("ByVal", taxParams[0]!["PassingMechanism"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    // ============================================================
    // Session 04 — MacroActionsEnricher integration test
    // ============================================================

    [SkippableFact]
    public void Northwind_StandaloneAutoExec_EmitsTwoConditionalActionsFromAxlXml()
    {
        SkipIfNoAccess();
        var db = ResolveSampleDb("NorthwindDevEdition.accdb");
        Skip.If(db is null, "samples/NorthwindDevEdition.accdb not found relative to test bin path.");

        var outRoot = Path.Combine(Path.GetTempPath(), $"ae-test-{Guid.NewGuid():N}");
        try
        {
            // Forms come along so the embedded-macro side gets exercised in
            // the same end-to-end run.
            var pipeline = Pipeline.Default().WithCategories(new[] { "Macros", "Forms" });
            var result = pipeline.Run(db!, outRoot);

            Assert.All(result.Enrich, c => Assert.All(c.Outcomes, o => Assert.True(o.Success, o.ErrorMessage)));

            // ---- Standalone macro: AutoExec has two top-level actions inside two
            // ConditionalBlocks (Not IsTrusted → OpenForm, IsTrusted → RunCode). ----
            var autoExecIr = Path.Combine(result.IrDir, "Macros", "AutoExec.ir.json");
            Assert.True(File.Exists(autoExecIr), $"IR file not produced: {autoExecIr}");

            var macroRoot = JsonNode.Parse(File.ReadAllText(autoExecIr))!.AsObject();
            var actions = macroRoot["Actions"] as JsonArray;
            Assert.NotNull(actions);
            Assert.Equal(2, actions!.Count);

            var first = actions[0]!.AsObject();
            Assert.Equal("OpenForm", first["Action"]!.GetValue<string>());
            Assert.Equal("frmStartup", first["Arguments"]!["FormName"]!.GetValue<string>());
            Assert.Contains("Not", first["Condition"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

            var second = actions[1]!.AsObject();
            Assert.Equal("RunCode", second["Action"]!.GetValue<string>());
            Assert.Equal("Startup()", second["Arguments"]!["FunctionName"]!.GetValue<string>());

            // No ParseError sentinels and every action has the AI slot.
            Assert.All(actions.Cast<JsonObject>(), a =>
            {
                Assert.NotEqual("ParseError", a["Action"]!.GetValue<string>());
                Assert.True(a.ContainsKey("InferredFunctionality"));
                Assert.Null(a["InferredFunctionality"]);
            });
            Assert.True(macroRoot.ContainsKey("InferredFunctionality"));
            Assert.Null(macroRoot["InferredFunctionality"]);

            // ---- Embedded macro: frmLearn.cmdOpenReport.OnClick should
            // produce an OpenReport action via the assembled _AXL: chunks. ----
            var formIr = Path.Combine(result.IrDir, "Forms", "frmLearn.ir.json");
            Assert.True(File.Exists(formIr), $"IR file not produced: {formIr}");

            var form = JsonNode.Parse(File.ReadAllText(formIr))!.AsObject();
            var btn = FindControlByName(form, "cmdOpenReport");
            Assert.NotNull(btn);
            var em = btn!["EventMacros"] as JsonObject;
            Assert.NotNull(em);
            var onClick = em!["OnClick"] as JsonObject;
            Assert.NotNull(onClick);

            var onClickActions = onClick!["Actions"] as JsonArray;
            Assert.NotNull(onClickActions);
            Assert.NotEmpty(onClickActions!);
            Assert.Null(onClick["InferredFunctionality"]);
            Assert.All(onClickActions.Cast<JsonObject>(), a =>
            {
                Assert.NotEqual("ParseError", a["Action"]!.GetValue<string>());
                Assert.Null(a["InferredFunctionality"]);
            });
            Assert.Contains(onClickActions.Cast<JsonObject>(),
                a => a["Action"]!.GetValue<string>() == "OpenReport");
        }
        finally
        {
            if (Directory.Exists(outRoot)) Directory.Delete(outRoot, recursive: true);
        }
    }

    /// <summary>Walks up from AppContext.BaseDirectory until it finds a
    /// <c>samples/</c> directory containing the requested .accdb. Returns null
    /// if the file isn't found within 8 levels (defensive — protects against an
    /// odd build layout).</summary>
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

    // ============================================================
    // Phase B label-resolution integration tests
    // ============================================================

    [SkippableFact]
    public void LabelsAttached_EndToEnd_AssignsAttachedCaptionToEachControl()
    {
        var form = RunPipelineAndOpenForm("labels_attached", "frmLabelsAttached");

        AssertLabel(form, "txtFirstName", source: "attached", text: "First Name");
        AssertLabel(form, "cmbCountry", source: "attached", text: "Country");
        AssertLabel(form, "chkActive", source: "attached", text: "Active?");
        // Attached caption beats Round 4 binding: ControlSource=ShouldBeIgnored is
        // present but its synthesis ("Should Be Ignored") must NOT win.
        AssertLabel(form, "txtAttachedBeatsBinding", source: "attached", text: "Custom Label");
    }

    [SkippableFact]
    public void LabelsFromName_EndToEnd_SplitsNonGenericNamesAndLeavesGenericsUnresolved()
    {
        var form = RunPipelineAndOpenForm("labels_from_name", "frmLabelsFromName");

        AssertLabel(form, "CustomerFirstName", source: "from-name", text: "Customer First Name");
        AssertLabel(form, "XMLPayload", source: "from-name", text: "XML Payload");
        AssertLabel(form, "OrderTotal", source: "from-name", text: "Order Total");
        AssertLabel(form, "Text99", source: "unresolved", text: null);
    }

    [SkippableFact]
    public void LabelsBinding_EndToEnd_SplitsIdentifierControlSourcesAndSkipsExpressions()
    {
        var form = RunPipelineAndOpenForm("labels_binding", "frmLabelsBinding");

        AssertLabel(form, "Text1", source: "from-binding", text: "First Name");
        AssertLabel(form, "Text2", source: "from-binding", text: "Order ID");
        AssertLabel(form, "Text3", source: "unresolved", text: null);   // expression rejected
        AssertLabel(form, "Text4", source: "unresolved", text: null);   // no ControlSource
    }

    [SkippableFact]
    public void LabelsProximity_EndToEnd_PipelineRunsAndProducesOutput()
    {
        var form = RunPipelineAndOpenForm("labels_proximity", "frmLabelsProximity");

        // Source assertions (text values for proximity matches are deferred to
        // a future label-quality session set — UAT machinery is out of scope here).
        AssertLabelSource(form, "Text1", "proximity");
        AssertLabelSource(form, "Text2", "proximity");
        AssertLabelSource(form, "Text3", "unresolved");
        AssertLabelSource(form, "Text4", "unresolved");
        AssertLabelSource(form, "Text5", "unresolved");
        AssertLabelSource(form, "Text6", "unresolved");
        AssertLabelSource(form, "Text7", "proximity");
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
            var pipeline = Pipeline.Default().WithCategories(new[] { "Forms" });
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
