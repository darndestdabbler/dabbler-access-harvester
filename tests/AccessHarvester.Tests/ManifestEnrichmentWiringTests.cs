using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

// Hermetic pins for the F-2-2 navigation & enrichment wiring (006 review;
// session set 011 Session 2). EmitManifestArtifacts is pure file IO (no
// Access COM), so these stage a minimal raw/kb/ir tree and read
// manifest.json + unresolved-items.json.
//
// Before this fix the Phase 6 enrichment substrate had ZERO production call
// sites: Pipeline.cs built `resolvedModules` + `crossRefs` for
// FunctionClassificationBuilder but never called NavigationGraphBuilder,
// UnresolvedItemAggregator, or EnrichmentPipelineRunner. So manifest.json
// had no NavigationGraph, `unresolved-items.json` was permanently `[]`, and
// the EventHandlerClassifications / ValidationRules / RowSourceSpecs /
// FunctionClassifications arrays were all empty.
//
// These pins prove each substrate component now fires through the production
// manifest path. (The whole-corpus proof is the regenerated HomeHealthCare
// golden; these isolate each newly-wired seam.)
//
// SCOPE NOTE (DW-9 / DW-10): NavigationGraphBuilder is called with
// inputs=null, so the graph carries the VBA-side edges only — the switchboard
// / ribbon / macro top-level surfaces and the R2-1/R2-2 consumer chains are
// deferred to the P4 set (their inputs are not harvested today). See
// docs/planning/deferred-work.md.
public sealed class ManifestEnrichmentWiringTests
{
    [Fact]
    public void NavigationGraph_PopulatedWithLiteralOpenFormEdge()
    {
        // A form whose code-behind issues DoCmd.OpenForm "frmTarget" must
        // surface a literal OpenForm navigation edge in manifest.NavigationGraph.
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmNav",
            "CodeBehindForm\r\n"
            + "Private Sub btnOpen_Click()\r\n"
            + "    DoCmd.OpenForm \"frmTarget\"\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmNav");

        var manifest = tree.EmitAndReadManifest();

        Assert.True(manifest.RootElement.TryGetProperty("NavigationGraph", out var nav),
            "manifest.json must now carry a NavigationGraph (was absent pre-F-2-2).");
        var edge = nav.GetProperty("Edges").EnumerateArray().Single(e =>
            e.GetProperty("Action").GetString() == "OpenForm"
            && e.GetProperty("TargetName").GetString() == "frmTarget");
        Assert.Equal("frmNav", edge.GetProperty("SourceModule").GetString());
        Assert.Equal("btnOpen_Click", edge.GetProperty("SourceProcedure").GetString());
        Assert.Equal("Form", edge.GetProperty("TargetKind").GetString());
    }

    [Fact]
    public void UnresolvedItems_PopulatedWithNonLiteralNavigationTarget()
    {
        // A non-literal dispatch target (DoCmd.OpenForm sFormName) must
        // surface as an UnresolvedItem in BOTH manifest.UnresolvedItems and
        // the unresolved-items.json side file — which was permanently [].
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmDyn",
            "CodeBehindForm\r\n"
            + "Private Sub btnGo_Click()\r\n"
            + "    Dim sFormName As String\r\n"
            + "    DoCmd.OpenForm sFormName\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmDyn");

        var manifest = tree.EmitAndReadManifest();

        var item = manifest.RootElement.GetProperty("UnresolvedItems").EnumerateArray().Single(u =>
            u.GetProperty("Source").GetString() == "NavigationTarget"
            && u.GetProperty("Reason").GetString() == "non-literal-target");
        Assert.Equal("frmDyn", item.GetProperty("Location").GetProperty("ModuleName").GetString());

        // The side file is the same aggregation, no longer empty.
        var sideFile = JsonNode.Parse(tree.ReadUnresolvedItemsJson())!.AsArray();
        Assert.Contains(sideFile, n =>
            n!["Source"]!.GetValue<string>() == "NavigationTarget"
            && n["Reason"]!.GetValue<string>() == "non-literal-target");
    }

    [Fact]
    public void EventHandlerClassification_PopulatedAndThreadedToFormSpec()
    {
        // A control event procedure wired via [Event Procedure] + matching
        // code-behind must (a) surface in manifest.EventHandlerClassifications
        // and (b) thread its category back onto the form spec's
        // EventHandlerReference.Classification slot (was always null).
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmEvents",
            "CodeBehindForm\r\n"
            + "Private Sub btnOpen_Click()\r\n"
            + "    DoCmd.OpenForm \"frmTarget\"\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmEvents", controls: new[]
        {
            new StagedControl("CommandButton", "btnOpen",
                new() { ["OnClick"] = "[Event Procedure]" }),
        });

        var manifest = tree.EmitAndReadManifest();

        var classification = manifest.RootElement.GetProperty("EventHandlerClassifications")
            .EnumerateArray()
            .Single(c => c.GetProperty("ProcedureName").GetString() == "btnOpen_Click");
        Assert.NotEqual(JsonValueKind.Null, classification.GetProperty("Categories").ValueKind);

        // The per-form reference now carries a non-null Classification.
        var form = manifest.RootElement.GetProperty("Forms").EnumerateArray()
            .Single(f => f.GetProperty("Spec").GetProperty("SourceFormName").GetString() == "frmEvents");
        var handlerRef = form.GetProperty("Spec").GetProperty("EventHandlers").EnumerateArray()
            .Single(h => h.GetProperty("VbaProcedureName").GetString() == "btnOpen_Click");
        Assert.Equal(JsonValueKind.String, handlerRef.GetProperty("Classification").ValueKind);
    }

    [Fact]
    public void TableFieldValidationRule_SurfacesInManifestValidationRules()
    {
        // ValidationRuleExtractor.ExtractFromTable now has a production call
        // site: a table field carrying a ValidationRule surfaces in the
        // run-wide manifest.ValidationRules array.
        using var tree = StagedTree.Create();
        tree.AddIrTable("tblThing", new[]
        {
            new StagedField("Amount", validationRule: "> 0", validationText: "Must be positive"),
        });

        var manifest = tree.EmitAndReadManifest();

        var rule = manifest.RootElement.GetProperty("ValidationRules").EnumerateArray()
            .Single(r => r.GetProperty("SourceObject").GetString() == "tblThing"
                && r.TryGetProperty("FieldOrControlName", out var f)
                && f.GetString() == "Amount");
        Assert.Equal("> 0", rule.GetProperty("Expression").GetString());
    }

    [Fact]
    public void RowSourceSpecs_PopulatedForComboWithRowSource()
    {
        // RowSourceSpecBuilder now has a production call site: a combo box
        // carrying a RowSource surfaces a RowSourceSpec in the run-wide
        // manifest.RowSourceSpecs array. (Closes the verifier's S2 R1 note
        // that RowSourceSpecs lacked a dedicated hermetic pin.)
        using var tree = StagedTree.Create();
        tree.AddIrForm("Forms", "frmCombo", controls: new[]
        {
            new StagedControl("ComboBox", "cboState", new()
            {
                ["RowSource"] = "SELECT StateCode FROM tlkpStates",
                ["RowSourceType"] = "Table/Query",
            }),
        });

        var manifest = tree.EmitAndReadManifest();

        var spec = manifest.RootElement.GetProperty("RowSourceSpecs").EnumerateArray()
            .Single(s => s.GetProperty("SourceFormName").GetString() == "frmCombo"
                && s.GetProperty("ControlName").GetString() == "cboState");
        Assert.Equal("ComboBox", spec.GetProperty("ControlType").GetString());
    }

    [Fact]
    public void FunctionClassifications_PopulatedInManifest()
    {
        // The run-wide FunctionClassifications array (separate from the
        // per-object ParserOutput projection) is now passed through to the
        // manifest.
        using var tree = StagedTree.Create();
        tree.AddRawModule("modMath",
            "Public Function Add2(ByVal x As Long) As Long\r\n"
            + "    Add2 = x + 2\r\n"
            + "End Function\r\n");

        var manifest = tree.EmitAndReadManifest();

        Assert.Contains(manifest.RootElement.GetProperty("FunctionClassifications").EnumerateArray(),
            c => c.GetProperty("ProcedureName").GetString() == "Add2");
    }

    // ── Staged output tree (forms + tables) ─────────────────────────────

    private sealed record StagedControl(
        string Type, string Name, Dictionary<string, string> ExtraProperties);

    private sealed record StagedField(
        string Name, string? validationRule = null, string? validationText = null);

    private sealed class StagedTree : System.IDisposable
    {
        public string BaseDir { get; }
        public string RawDir => Path.Combine(BaseDir, "raw");
        public string KbDir => Path.Combine(BaseDir, "kb");
        public string IrDir => Path.Combine(BaseDir, "ir");

        private StagedTree(string baseDir) => BaseDir = baseDir;

        public static StagedTree Create()
        {
            string dir = Path.Combine(
                Path.GetTempPath(), $"ah-enrichment-wiring-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return new StagedTree(dir);
        }

        public void AddRawModule(string name, string vbaText)
        {
            string dir = Path.Combine(RawDir, "ModulesAndClasses");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name + ".txt"), vbaText);
        }

        public void AddKbForm(string category, string name, string vbaCode)
        {
            string dir = Path.Combine(KbDir, category);
            Directory.CreateDirectory(dir);
            var node = new JsonObject
            {
                ["Type"] = category == "Forms" ? "Form" : "Report",
                ["Properties"] = new JsonObject(),
                ["VbaCode"] = vbaCode,
            };
            File.WriteAllText(Path.Combine(dir, name + ".json"), node.ToJsonString());
        }

        public void AddIrForm(string category, string name,
            StagedControl[]? controls = null)
        {
            string dir = Path.Combine(IrDir, category);
            Directory.CreateDirectory(dir);
            var controlNodes = new JsonArray();
            foreach (var c in controls ?? System.Array.Empty<StagedControl>())
            {
                var props = new JsonObject { ["Name"] = c.Name };
                foreach (var (k, v) in c.ExtraProperties) props[k] = v;
                controlNodes.Add(new JsonObject
                {
                    ["Type"] = c.Type,
                    ["Properties"] = props,
                });
            }
            var node = new JsonObject
            {
                ["Type"] = category == "Forms" ? "Form" : "Report",
                ["Properties"] = new JsonObject(),
                ["Children"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["Type"] = "Section",
                        ["Properties"] = new JsonObject { ["Name"] = "Detail" },
                        ["Children"] = controlNodes,
                    },
                },
            };
            File.WriteAllText(Path.Combine(dir, name + ".ir.json"), node.ToJsonString());
        }

        // ir/Tables/<name>.ir.json deserialises directly to TableMetadata
        // (the kb→ir pipeline is a JsonPassthrough with no enricher).
        public void AddIrTable(string name, StagedField[] fields)
        {
            string dir = Path.Combine(IrDir, "Tables");
            Directory.CreateDirectory(dir);
            var fieldNodes = new JsonArray();
            int ordinal = 0;
            foreach (var f in fields)
            {
                fieldNodes.Add(new JsonObject
                {
                    ["Name"] = f.Name,
                    ["TypeName"] = "Long",
                    ["ValidationRule"] = f.validationRule ?? "",
                    ["ValidationText"] = f.validationText ?? "",
                    ["OrdinalPosition"] = ordinal++,
                });
            }
            var node = new JsonObject
            {
                ["Name"] = name,
                ["Type"] = "Table",
                ["Fields"] = fieldNodes,
            };
            File.WriteAllText(Path.Combine(dir, name + ".ir.json"), node.ToJsonString());
        }

        public JsonDocument EmitAndReadManifest()
        {
            CorePipeline.EmitManifestArtifacts(
                BaseDir,
                databasePath: Path.Combine(BaseDir, "Staged.accdb"),
                rawDir: RawDir,
                kbDir: KbDir,
                irDir: IrDir);
            return JsonDocument.Parse(
                File.ReadAllText(Path.Combine(BaseDir, "manifest.json")));
        }

        public string ReadUnresolvedItemsJson()
            => File.ReadAllText(Path.Combine(BaseDir, "unresolved-items.json"));

        public void Dispose()
        {
            if (Directory.Exists(BaseDir))
                Directory.Delete(BaseDir, recursive: true);
        }
    }
}
