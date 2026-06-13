using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

// Hermetic pins for the F-3-4 resolver-wiring fix (006 review; session
// set 011 Session 1). EmitManifestArtifacts is pure file IO (no Access
// COM), so these stage a minimal raw/kb/ir tree and read manifest.json.
//
// Before this fix the manifest parse loop built a FRESH per-module
// ProjectScope and a registry-less, owningClass-less ReferenceResolver,
// so three differentiating resolver capabilities could never fire in
// production output:
//
//   1. cross-module Public lookup (every module was an only child of its
//      own ProjectScope, so a bare call to another module's Public proc
//      stayed Unresolved "not-in-scope");
//   2. `Me` binding in a form/report module (no owningClass, so `Me`
//      resolved Unresolved "me-in-standard-module");
//   3. `Me.<control>` / bare-control binding (no Access registry, so a
//      control member access stayed "member-access-on-unresolved-target").
//
// The fix shares one ProjectScope across the whole loop, builds the
// AccessObjectModelRegistry from the enriched ir/ metadata, and passes a
// Form/Report owningClass. These pins prove each capability now fires
// through the production manifest path.
public sealed class ManifestResolverWiringTests
{
    [Fact]
    public void CrossModulePublicCall_ResolvesAcrossModules()
    {
        // modHelpers exposes a Public function; modConsumer calls it bare.
        // With one shared ProjectScope the consumer's call resolves to the
        // helper's declaration (cross-module Public lookup, Step 1.5).
        using var tree = StagedTree.Create();
        tree.AddRawModule("modHelpers",
            "Public Function GetString() As String\r\n"
            + "    GetString = \"x\"\r\n"
            + "End Function\r\n");
        tree.AddRawModule("modConsumer",
            "Public Sub UseHelper()\r\n"
            + "    Dim s As String\r\n"
            + "    s = GetString()\r\n"
            + "End Sub\r\n");

        var manifest = tree.EmitAndReadManifest();

        var reference = FindReference(manifest, module: "modConsumer", originalText: "GetString");
        Assert.Equal("Resolved", reference.GetProperty("ResolutionStatus").GetString());
        Assert.Equal("Procedure", reference.GetProperty("ReferenceKind").GetString());
    }

    [Fact]
    public void MeInFormModule_ResolvesToFormViaOwningClass()
    {
        // A form module now carries a FormModule owningClass, so `Me`
        // resolves to the form rather than Unresolved("me-in-standard-module").
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmWidget",
            "CodeBehindForm\r\n"
            + "Private Sub Form_Load()\r\n"
            + "    Me.Requery\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmWidget");

        var manifest = tree.EmitAndReadManifest();

        var reference = FindReference(manifest, module: "frmWidget", originalText: "Me");
        Assert.Equal("Resolved", reference.GetProperty("ResolutionStatus").GetString());
        Assert.Equal("Form", reference.GetProperty("ReferenceKind").GetString());
    }

    [Fact]
    public void MeControlInFormModule_ResolvesToControlViaRegistry()
    {
        // The Access registry, built from the ir/ form metadata, resolves
        // `Me.txtName` to the synthetic control declaration — the
        // member-chain capability that had no production call site before.
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmEntry",
            "CodeBehindForm\r\n"
            + "Private Sub Form_Load()\r\n"
            + "    Me.txtName = \"\"\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmEntry", new[] { ("TextBox", "txtName") });

        var manifest = tree.EmitAndReadManifest();

        var reference = FindReference(manifest, module: "frmEntry", originalText: "Me.txtName");
        Assert.Equal("Resolved", reference.GetProperty("ResolutionStatus").GetString());
        Assert.Equal("ObjectModel", reference.GetProperty("ReferenceKind").GetString());
    }

    [Fact]
    public void MeControl_ResolvesViaByNameIndex_WhenModuleNameDivergesFromFormName()
    {
        // Verifier (011 S1 R1) Minor B: owningClass.Name = the module/file
        // name = FormMetadata.Name, and the registry resolves controls via
        // its by-Name index. This pin proves that holds even when the
        // form's ModuleName (Access's `Form_<x>` class-module name, here a
        // deliberately divergent "Form_TotallyDifferent") differs from the
        // form name — the by-ModuleName index misses and the by-Name index
        // (keyed by the file name = owningClass.Name) carries the lookup.
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmDiverge",
            "CodeBehindForm\r\n"
            + "Private Sub Form_Load()\r\n"
            + "    Me.txtName = \"\"\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmDiverge",
            controls: new[] { ("TextBox", "txtName") },
            moduleName: "Form_TotallyDifferent");

        var manifest = tree.EmitAndReadManifest();

        var reference = FindReference(manifest, module: "frmDiverge", originalText: "Me.txtName");
        Assert.Equal("Resolved", reference.GetProperty("ResolutionStatus").GetString());
        Assert.Equal("ObjectModel", reference.GetProperty("ReferenceKind").GetString());
    }

    [Fact]
    public void BareControlInFormModule_ResolvesToControlViaRegistry()
    {
        // VBA allows a bare control identifier in a form module
        // (`txtName = ""` ≡ `Me.txtName = ""`). ResolveIdentifier's
        // form-context step now fires because owningClass + registry are
        // wired through the production loop.
        using var tree = StagedTree.Create();
        tree.AddKbForm("Forms", "frmBare",
            "CodeBehindForm\r\n"
            + "Private Sub Form_Load()\r\n"
            + "    txtName = \"\"\r\n"
            + "End Sub\r\n");
        tree.AddIrForm("Forms", "frmBare", new[] { ("TextBox", "txtName") });

        var manifest = tree.EmitAndReadManifest();

        var reference = FindReference(manifest, module: "frmBare", originalText: "txtName");
        Assert.Equal("Resolved", reference.GetProperty("ResolutionStatus").GetString());
        Assert.Equal("ObjectModel", reference.GetProperty("ReferenceKind").GetString());
    }

    // Finds the first Body.References entry whose OriginalText matches
    // `originalText`, across every procedure of the named module.
    private static JsonElement FindReference(
        JsonDocument manifest, string module, string originalText)
    {
        var moduleEl = manifest.RootElement.GetProperty("Modules").EnumerateArray()
            .Concat(manifest.RootElement.GetProperty("Forms").EnumerateArray())
            .Concat(manifest.RootElement.GetProperty("Reports").EnumerateArray())
            .FirstOrDefault(m =>
            {
                if (!m.TryGetProperty("ParserOutput", out var po)) return false;
                return po.GetProperty("Source").GetProperty("Location")
                         .GetProperty("Module").GetString() == module;
            });
        Assert.True(moduleEl.ValueKind == JsonValueKind.Object,
            $"Module '{module}' not found in manifest with a ParserOutput.");

        foreach (var item in moduleEl.GetProperty("ParserOutput").GetProperty("Items").EnumerateArray())
        {
            if (!item.TryGetProperty("Body", out var body)) continue;
            if (!body.TryGetProperty("References", out var refs)) continue;
            foreach (var r in refs.EnumerateArray())
            {
                if (r.GetProperty("OriginalText").GetString() == originalText)
                    return r;
            }
        }

        Assert.Fail($"Reference '{originalText}' not found in module '{module}'.");
        return default; // unreachable
    }

    // Minimal staged output tree mirroring ManifestVbaIngestionTests'
    // harness, extended with raw standalone modules and ir forms carrying
    // controls (needed for the registry-backed resolution pins).
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
                Path.GetTempPath(), $"ah-resolver-wiring-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return new StagedTree(dir);
        }

        // Standalone module: raw/ModulesAndClasses/<name>.txt. No
        // "VERSION 1.0 CLASS" header → parsed as a Standard module, which
        // is what cross-module Public lookup requires.
        public void AddRawModule(string name, string vbaText)
        {
            string dir = Path.Combine(RawDir, "ModulesAndClasses");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name + ".txt"), vbaText);
        }

        // Form/report code-behind sources from the kb tier's VbaCode field
        // (the F-3-1 ingestion path).
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

        // ir form/report carrying optional controls in a Detail section.
        // FormMetadataExtractor reads control Name from Properties.Name and
        // Type from the node's Type — the AccessObjectModelRegistry indexes
        // the controls so Me.<control> / bare-control resolve.
        public void AddIrForm(string category, string name,
            (string Type, string Name)[]? controls = null,
            string? moduleName = null)
        {
            string dir = Path.Combine(IrDir, category);
            Directory.CreateDirectory(dir);
            var controlNodes = new JsonArray();
            foreach (var (type, ctlName) in controls ?? System.Array.Empty<(string, string)>())
            {
                controlNodes.Add(new JsonObject
                {
                    ["Type"] = type,
                    ["Properties"] = new JsonObject { ["Name"] = ctlName },
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
            // FormMetadataExtractor reads ModuleName from the ir VbaCode's
            // `Attribute VB_Name` line. Inject one only when a test needs a
            // ModuleName that diverges from the form/file name.
            if (moduleName is not null)
                node["VbaCode"] = $"Attribute VB_Name = \"{moduleName}\"\r\n";
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

        public void Dispose()
        {
            if (Directory.Exists(BaseDir))
                Directory.Delete(BaseDir, recursive: true);
        }
    }
}
