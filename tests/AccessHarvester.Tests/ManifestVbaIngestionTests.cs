using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using CorePipeline = AccessHarvester.Core.Pipeline;

namespace AccessHarvester.Tests;

// Hermetic pins for the F-3-1 manifest VBA ingestion fix (006 review's
// one Critical; fixed in session set 008 Session 1):
//
//   - Form/report ParserOutput is sourced from the kb tier's VbaCode
//     field — VBE header stripped — rather than the raw SaveAsText
//     dump, so a real event handler produces non-empty parsed Items
//     through the manifest path. Before the fix, whole-file parsing of
//     the layout DSL returned zero items for every form/report,
//     silently.
//
//   - A degraded parse (recovery mode used, or parse diagnostics
//     present) surfaces a VBA_PARSE_DEGRADED manifest diagnostic
//     instead of being discarded at the LoadAndParse call site.
//
//   - A form without code-behind (no VbaCode in kb) has no ParserOutput
//     and emits no diagnostic — omission is the honest signal, not a
//     defect.
//
// EmitManifestArtifacts is pure file IO (no Access COM), so these tests
// stage a minimal kb/ir tree in a temp dir and read back manifest.json.
public sealed class ManifestVbaIngestionTests
{
    // Kb-shaped VbaCode exactly as AccessTextParser captures it: the
    // CodeBehindForm marker plus Attribute lines, then real code with a
    // form event handler (the 006 mechanism-probe shape).
    private const string CleanVbaCode =
        "CodeBehindForm\n"
        + "Attribute VB_GlobalNameSpace = False\n"
        + "Attribute VB_Exposed = False\n"
        + "Option Compare Database\n"
        + "Option Explicit\n"
        + "\n"
        + "Private Sub Form_Load()\n"
        + "    MsgBox \"loaded\"\n"
        + "End Sub\n";

    // In-procedure syntax error (`Dim x As` with no type). Statement-
    // level recovery degrades the statement to a recovered stub without
    // failing the module parse, so this exercises the degraded-but-not-
    // thrown path that used to be silently discarded.
    private const string DegradedVbaCode =
        "CodeBehindForm\n"
        + "Attribute VB_Exposed = False\n"
        + "Option Compare Database\n"
        + "\n"
        + "Private Sub Form_Load()\n"
        + "    Dim x As\n"
        + "End Sub\n";

    [Fact]
    public void FormCodeBehind_FromKbVbaCode_ProducesParserOutputItems()
    {
        using var tree = StagedTree.Create();
        tree.AddKbObject("Forms", "frmPin", CleanVbaCode);
        tree.AddIrObject("Forms", "frmPin");

        var manifest = tree.EmitAndReadManifest();

        var form = Assert.Single(manifest.RootElement.GetProperty("Forms").EnumerateArray());
        var items = form.GetProperty("ParserOutput").GetProperty("Items");
        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal("Form_Load", item.GetProperty("Name").GetString());

        // The header strip + kb sourcing must yield a clean parse — a
        // degraded-mode parse here would mean the layout/header text
        // reached the parser again.
        Assert.DoesNotContain(
            EnumerateDiagnosticCodes(manifest), c => c == "VBA_PARSE_DEGRADED");
        Assert.DoesNotContain(
            EnumerateDiagnosticCodes(manifest), c => c == "VBA_PARSE_FAILURE");
    }

    [Fact]
    public void ReportCodeBehind_FromKbVbaCode_ProducesParserOutputItems()
    {
        using var tree = StagedTree.Create();
        tree.AddKbObject("Reports", "rptPin", CleanVbaCode.Replace("Form_Load", "Report_Open"));
        tree.AddIrObject("Reports", "rptPin");

        var manifest = tree.EmitAndReadManifest();

        var report = Assert.Single(manifest.RootElement.GetProperty("Reports").EnumerateArray());
        var items = report.GetProperty("ParserOutput").GetProperty("Items");
        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal("Report_Open", item.GetProperty("Name").GetString());
    }

    [Fact]
    public void DegradedParse_SurfacesVbaParseDegradedDiagnostic()
    {
        using var tree = StagedTree.Create();
        tree.AddKbObject("Forms", "frmDegraded", DegradedVbaCode);
        tree.AddIrObject("Forms", "frmDegraded");

        var manifest = tree.EmitAndReadManifest();

        // The parse must not be silently reduced: either the degraded
        // diagnostic fires (parse survived in a degraded mode) — the
        // expected outcome for an in-procedure syntax error — or, at
        // absolute minimum, some parse diagnostic names the form.
        var degraded = manifest.RootElement.GetProperty("Diagnostics").EnumerateArray()
            .Where(d => d.GetProperty("Code").GetString() == "VBA_PARSE_DEGRADED")
            .ToList();
        var hit = Assert.Single(degraded);
        Assert.Contains("frmDegraded", hit.GetProperty("Message").GetString());
    }

    [Fact]
    public void FormWithoutCodeBehind_HasNoParserOutput_AndNoDiagnostic()
    {
        using var tree = StagedTree.Create();
        tree.AddKbObject("Forms", "frmNoCode", vbaCode: null);
        tree.AddIrObject("Forms", "frmNoCode");

        var manifest = tree.EmitAndReadManifest();

        var form = Assert.Single(manifest.RootElement.GetProperty("Forms").EnumerateArray());
        Assert.False(form.TryGetProperty("ParserOutput", out _),
            "A form without code-behind must omit ParserOutput (omission semantics), not emit an empty one.");
        Assert.Empty(EnumerateDiagnosticCodes(manifest));
    }

    // F-3-6: a saved query whose SQL fails to parse must surface a
    // QUERY_SQL_PARSE_FAILURE diagnostic (the downgrade to Raw-only Parsed
    // used to be silent — the bare catch in BuildQueryExports swallowed it).
    [Fact]
    public void QuerySqlParseFailure_SurfacesQuerySqlParseFailureDiagnostic()
    {
        using var tree = StagedTree.Create();
        // The recovering SQL parser builds a statement context with no
        // recognized DML body, so AstBuilder throws — exercising the inner
        // catch. (Same unparseable string QuerySqlProjectorTests uses.)
        tree.AddRawQuery("qryBad", "THIS IS NOT VALID SQL AT ALL @@@@", "Select");

        var manifest = tree.EmitAndReadManifest();

        var failures = manifest.RootElement.GetProperty("Diagnostics").EnumerateArray()
            .Where(d => d.GetProperty("Code").GetString() == "QUERY_SQL_PARSE_FAILURE")
            .ToList();
        var hit = Assert.Single(failures);
        Assert.Contains("qryBad", hit.GetProperty("Message").GetString());

        // The query is still shipped (Raw-only Parsed), not dropped.
        var query = Assert.Single(manifest.RootElement.GetProperty("Queries").EnumerateArray());
        Assert.Equal("qryBad", query.GetProperty("Name").GetString());
    }

    // A well-formed query parses without emitting the failure diagnostic —
    // proves the diagnostic is failure-scoped, not unconditional.
    [Fact]
    public void WellFormedQuery_EmitsNoParseFailureDiagnostic()
    {
        using var tree = StagedTree.Create();
        tree.AddRawQuery("qryGood", "SELECT a FROM b", "Select");

        var manifest = tree.EmitAndReadManifest();

        Assert.DoesNotContain(
            EnumerateDiagnosticCodes(manifest), c => c == "QUERY_SQL_PARSE_FAILURE");
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateDiagnosticCodes(
        JsonDocument manifest)
        => manifest.RootElement.GetProperty("Diagnostics").EnumerateArray()
            .Select(d => d.GetProperty("Code").GetString()!)
            .ToList();

    // Minimal staged output tree: base dir with kb/ and ir/ category
    // files; raw/ left absent (EmitManifestArtifacts skips missing
    // category dirs). Mirrors the PipelineCleanTests temp-dir harness.
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
                Path.GetTempPath(), $"ah-manifest-ingest-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return new StagedTree(dir);
        }

        public void AddKbObject(string category, string name, string? vbaCode)
        {
            string categoryDir = Path.Combine(KbDir, category);
            Directory.CreateDirectory(categoryDir);
            var node = new System.Text.Json.Nodes.JsonObject
            {
                ["Type"] = category == "Forms" ? "Form" : "Report",
                ["Properties"] = new System.Text.Json.Nodes.JsonObject(),
            };
            if (vbaCode is not null)
                node["VbaCode"] = vbaCode;
            File.WriteAllText(
                Path.Combine(categoryDir, name + ".json"), node.ToJsonString());
        }

        public void AddRawQuery(string name, string sql, string queryTypeName)
        {
            string categoryDir = Path.Combine(RawDir, "Queries");
            Directory.CreateDirectory(categoryDir);
            var node = new System.Text.Json.Nodes.JsonObject
            {
                ["Name"] = name,
                ["Sql"] = sql,
                ["QueryTypeName"] = queryTypeName,
            };
            File.WriteAllText(
                Path.Combine(categoryDir, name + ".json"), node.ToJsonString());
        }

        public void AddIrObject(string category, string name)
        {
            string categoryDir = Path.Combine(IrDir, category);
            Directory.CreateDirectory(categoryDir);
            string type = category == "Forms" ? "Form" : "Report";
            File.WriteAllText(
                Path.Combine(categoryDir, name + ".ir.json"),
                $$"""{ "Type": "{{type}}", "Properties": {} }""");
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
