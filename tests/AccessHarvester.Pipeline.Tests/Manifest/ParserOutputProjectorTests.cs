using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Pipeline.Manifest;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;
using ManifestModuleKind = AccessHarvester.Pipeline.Manifest.ModuleKind;
using ResolverModuleKind = AccessHarvester.Vba.Resolver.Scopes.ModuleKind;

namespace AccessHarvester.Pipeline.Tests.Manifest;

// Hermetic unit tests for ParserOutputProjector.
// Each test parses a minimal VBA snippet, resolves it, and asserts the
// projected JSON has the expected shape. No I/O or COM required.
public sealed class ParserOutputProjectorTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static ResolvedModule Resolve(
        string source,
        string moduleName = "modTest",
        ResolverModuleKind kind = ResolverModuleKind.Standard)
    {
        var parseResult = VbaParserPipeline.Parse(source);
        var ast = parseResult.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, moduleName, kind);
        return new ReferenceResolver().Resolve(scope, ast);
    }

    private static JsonNode Project(
        ResolvedModule resolved,
        string moduleName = "modTest",
        ManifestModuleKind kind = ManifestModuleKind.Standard)
    {
        var crossRefs = CrossReferenceIndex.Build(resolved);
        var classifications = FunctionClassificationBuilder.BuildAll(
            new[] { resolved }, crossRefs);
        return ParserOutputProjector.Project(resolved, classifications, moduleName, kind);
    }

    private static JsonArray Items(JsonNode root)
        => root["Items"]!.AsArray();

    private static JsonNode? Item(JsonNode root, int index)
        => root["Items"]!.AsArray()[index];

    // ── Tests: event binding ──────────────────────────────────────────

    [Fact]
    public void Form_BeforeUpdate_ProducesEventBinding()
    {
        var resolved = Resolve(
            "Private Sub Form_BeforeUpdate(Cancel As Integer)\nEnd Sub",
            moduleName: "Form_frmTest",
            kind: ResolverModuleKind.Form);

        var root = Project(resolved, "Form_frmTest", ManifestModuleKind.ClassModule);
        var item = Item(root, 0)!;

        Assert.Equal("EventHandler", item["ProcedureKind"]!.GetValue<string>());
        Assert.Equal("Form", item["EventBinding"]!["ControlName"]!.GetValue<string>());
        Assert.Equal("BeforeUpdate", item["EventBinding"]!["EventName"]!.GetValue<string>());
    }

    [Fact]
    public void btnSave_Click_ProducesEventBinding()
    {
        var resolved = Resolve(
            "Private Sub btnSave_Click()\nEnd Sub",
            kind: ResolverModuleKind.Form);

        var root = Project(resolved, "Form_frmTest", ManifestModuleKind.ClassModule);
        var item = Item(root, 0)!;

        Assert.Equal("EventHandler", item["ProcedureKind"]!.GetValue<string>());
        Assert.Equal("btnSave", item["EventBinding"]!["ControlName"]!.GetValue<string>());
        Assert.Equal("Click", item["EventBinding"]!["EventName"]!.GetValue<string>());
    }

    [Fact]
    public void NonEventHandler_EventBindingIsAbsent()
    {
        var resolved = Resolve("Public Sub DoSomething()\nEnd Sub");

        var root = Project(resolved);
        var item = Item(root, 0)!;

        Assert.Equal("Sub", item["ProcedureKind"]!.GetValue<string>());
        Assert.Null(item["EventBinding"]);
    }

    // ── Tests: procedure kind mapping ─────────────────────────────────

    [Fact]
    public void FunctionProcedure_MapsToFunction()
    {
        var resolved = Resolve("Public Function GetName() As String\nGetName = \"x\"\nEnd Function");

        var item = Item(Project(resolved), 0)!;
        Assert.Equal("Function", item["ProcedureKind"]!.GetValue<string>());
    }

    [Fact]
    public void PropertyGet_MapsToPropertyGet()
    {
        var resolved = Resolve("Public Property Get MyProp() As String\nEnd Property");

        var item = Item(Project(resolved), 0)!;
        Assert.Equal("PropertyGet", item["ProcedureKind"]!.GetValue<string>());
    }

    // ── Tests: body.References ────────────────────────────────────────

    [Fact]
    public void ResolvedReference_ProjectsToBodyReferencesWithCorrectShape()
    {
        // A procedure that calls a built-in so the resolver produces a
        // ResolvedToBuiltIn outcome, which maps to ResolutionStatus=Resolved.
        const string source = @"
Public Sub DoSomething()
    Dim s As String
    s = Format(Now(), ""yyyy-mm-dd"")
End Sub";
        var resolved = Resolve(source);
        var root = Project(resolved);
        var item = Item(root, 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        // There should be at least one reference (Format and/or Now).
        Assert.True(refs.Count > 0, "Expected at least one reference in Body.References");

        // Every reference must have OriginalText, Location, and ResolutionStatus.
        foreach (var r in refs)
        {
            Assert.NotNull(r!["OriginalText"]);
            Assert.NotNull(r!["Location"]);
            Assert.NotNull(r!["ResolutionStatus"]);
        }
    }

    [Fact]
    public void UnresolvedReference_ProjectsAsUnresolved()
    {
        const string source = @"
Option Explicit
Public Sub DoSomething()
    DoUnknownThing
End Sub";
        var resolved = Resolve(source);
        var root = Project(resolved);
        var item = Item(root, 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        var unresolved = refs.Where(r =>
            r!["ResolutionStatus"]?.GetValue<string>() == "Unresolved").ToList();

        Assert.True(unresolved.Count > 0,
            "Expected at least one Unresolved reference for DoUnknownThing");
    }

    // ── Tests: F-3-6 ResolutionReason surfacing ──────────────────────

    [Fact]
    public void UnresolvedReference_CarriesResolutionReason_NotInScope()
    {
        // A bare unknown identifier walks the scope chain without a match →
        // the resolver's NotInScope reason, which the projector now surfaces.
        const string source = @"
Option Explicit
Public Sub DoSomething()
    DoUnknownThing
End Sub";
        var resolved = Resolve(source);
        var item = Item(Project(resolved), 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        var unresolved = refs.First(r =>
            r!["ResolutionStatus"]?.GetValue<string>() == "Unresolved")!;

        Assert.Equal("not-in-scope", unresolved["ResolutionReason"]!.GetValue<string>());
    }

    [Fact]
    public void MeInStandardModule_CarriesResolutionReason_MeInStandardModule()
    {
        // `Me` has no class context in a standard module → the resolver's
        // MeInStandardModule reason. Distinct from not-in-scope, so this
        // proves the reason is the resolver's actual value, not a constant.
        const string source = @"
Option Explicit
Public Sub DoSomething()
    Me.Requery
End Sub";
        var resolved = Resolve(source, kind: ResolverModuleKind.Standard);
        var item = Item(Project(resolved), 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        var meRef = refs.FirstOrDefault(r =>
            r!["ResolutionReason"]?.GetValue<string>() == "me-in-standard-module");

        Assert.NotNull(meRef);
    }

    [Fact]
    public void ResolvedReference_HasNoResolutionReason()
    {
        // A resolved reference (built-in) must NOT carry a reason — the
        // field is unresolved-only, by omission semantics.
        const string source = @"
Public Sub DoSomething()
    Dim s As String
    s = Format(Now(), ""yyyy-mm-dd"")
End Sub";
        var resolved = Resolve(source);
        var item = Item(Project(resolved), 0)!;
        var refs = item["Body"]!["References"]!.AsArray();

        foreach (var r in refs.Where(r =>
            r!["ResolutionStatus"]?.GetValue<string>() == "Resolved"))
        {
            Assert.Null(r!["ResolutionReason"]);
        }
    }

    // ── Tests: Body.Calls ─────────────────────────────────────────────

    [Fact]
    public void CallToUserDefinedProcedure_AppearsInBodyCalls()
    {
        const string source = @"
Public Sub Caller()
    Helper
End Sub
Public Sub Helper()
End Sub";
        var resolved = Resolve(source);
        var root = Project(resolved);

        // Caller is the first item (source order).
        var callerItem = Items(root).First(n =>
            n!["Name"]!.GetValue<string>() == "Caller")!;
        var calls = callerItem["Body"]!["Calls"]!.AsArray();

        Assert.True(calls.Count > 0,
            "Expected at least one call in Body.Calls for Caller");
        Assert.NotNull(calls[0]!["CalleeText"]);
        Assert.NotNull(calls[0]!["Location"]);
    }

    // ── Tests: Body.SqlStrings ────────────────────────────────────────

    [Fact]
    public void SqlStrings_AlwaysEmptyArrayInSessionOne()
    {
        var resolved = Resolve("Public Sub DoSomething()\nEnd Sub");
        var item = Item(Project(resolved), 0)!;
        var sqlStrings = item["Body"]!["SqlStrings"]!.AsArray();

        Assert.Empty(sqlStrings);
    }

    // ── Tests: schema envelope ────────────────────────────────────────

    [Fact]
    public void ProjectedOutput_HasSchemaVersionAndSourceFields()
    {
        var resolved = Resolve("Public Sub DoSomething()\nEnd Sub",
            moduleName: "modFoo");
        var root = Project(resolved, "modFoo");

        // 1.1: F-3-6 added the optional ResolutionReason on unresolved
        // references; the ParserOutput sub-document version is bumped to
        // signal the new field (the schema validates it as a free pattern,
        // not a const, so no manifest-envelope churn).
        Assert.Equal("1.1", root["SchemaVersion"]!.GetValue<string>());
        Assert.NotNull(root["Source"]!["Kind"]);
        Assert.NotNull(root["Source"]!["Location"]);
        // Standard module → Kind = "Procedure"
        Assert.Equal("Procedure", root["Source"]!["Kind"]!.GetValue<string>());
    }

    // ── Tests: TryExtractEventBinding internal helper ─────────────────

    [Fact]
    public void TryExtractEventBinding_SplitsOnLastUnderscore()
    {
        var result = ParserOutputProjector.TryExtractEventBinding("Form_BeforeUpdate");
        Assert.NotNull(result);
        Assert.Equal("Form", result!.Value.Control);
        Assert.Equal("BeforeUpdate", result!.Value.Event);
    }

    [Fact]
    public void TryExtractEventBinding_MultipleUnderscores_SplitsOnLast()
    {
        // Control names can theoretically contain underscores.
        var result = ParserOutputProjector.TryExtractEventBinding("My_Control_Click");
        Assert.NotNull(result);
        Assert.Equal("My_Control", result!.Value.Control);
        Assert.Equal("Click", result!.Value.Event);
    }

    [Fact]
    public void TryExtractEventBinding_NoUnderscore_ReturnsNull()
    {
        var result = ParserOutputProjector.TryExtractEventBinding("DoSomething");
        Assert.Null(result);
    }

    [Fact]
    public void TryExtractEventBinding_LeadingUnderscore_ReturnsNull()
    {
        var result = ParserOutputProjector.TryExtractEventBinding("_Click");
        Assert.Null(result);
    }

    // ── Tests: parameter pass mode (006 F-3-5) ────────────────────────

    [Fact]
    public void Parameter_ExplicitPassModes_EmitPassingMechanism()
    {
        // Explicit ByVal and ByRef each emit the schema-declared PassingMechanism
        // enum; an unspecified pass mode is omitted (omission convention → the
        // consumer reads the VBA default, ByRef). The deprecated bare "ByRef"
        // boolean is no longer emitted.
        var resolved = Resolve(
            "Public Sub Copy(ByVal src As Long, ByRef dst As Long, plain As String)\nEnd Sub");
        var item = Item(Project(resolved), 0)!;
        var ps = item["Signature"]!["Parameters"]!.AsArray();

        Assert.Equal("ByVal", ps[0]!["PassingMechanism"]!.GetValue<string>());
        Assert.Equal("ByRef", ps[1]!["PassingMechanism"]!.GetValue<string>());
        Assert.Null(ps[2]!["PassingMechanism"]);
        Assert.Null(ps[1]!["ByRef"]);  // old boolean form retired
    }
}
