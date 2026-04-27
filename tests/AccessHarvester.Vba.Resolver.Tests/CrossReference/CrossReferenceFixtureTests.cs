using System.IO;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.CrossReference;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.CrossReference;

// Session 4 cross-cut: build the CrossReferenceIndex from the anchor
// fixtures and assert that the inverted map correctly captures multi-
// procedure usage patterns. Fixture 09 (report grouping events) is the
// primary target because its three module-level running-total variables
// (m_GrandTotal, m_GroupTotal, m_GroupCount) are each used across
// multiple named procedures — a natural test of the index's ability to
// aggregate references that span disjoint ProcedureScopes.
public sealed class CrossReferenceFixtureTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static string LocateTestCasesRoot()
    {
        string dir = System.AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate docs/test-cases walking up from '{System.AppContext.BaseDirectory}'.");
    }

    private static (ResolvedModule resolved, CrossReferenceIndex index) BuildFromFixture(
        string fileName,
        string moduleName,
        ModuleKind kind = ModuleKind.Standard,
        ClassDeclaration? owningClass = null)
    {
        string source = File.ReadAllText(Path.Combine(LocateTestCasesRoot(), fileName));
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(fileName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, moduleName, kind, owningClass);
        var resolved = new ReferenceResolver().Resolve(scope, ast);
        var index = CrossReferenceIndex.Build(resolved);
        return (resolved, index);
    }

    // ── Fixture 09: report grouping events ───────────────────────────

    [Fact]
    public void Fixture09_GrandTotal_HasReferencesInAtLeastThreeProcedures()
    {
        // m_GrandTotal is written in Report_Open (= 0) and
        // GroupFooter0_Format (+= m_GroupTotal), and read in
        // ReportFooter_Format (Me.txtGrandTotal = m_GrandTotal).
        // The cross-reference index must return ≥ 3 resolved references
        // to the m_GrandTotal declaration.
        var (resolved, index) = BuildFromFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        var grandTotalDecl = resolved.ModuleScope.Declarations
            .OfType<VariableDeclaration>()
            .Single(v => Declaration.NameComparer.Equals(v.Name, "m_GrandTotal"));

        var refs = index.GetReferencesTo(grandTotalDecl);
        Assert.True(refs.Count >= 3,
            $"Expected >= 3 references to m_GrandTotal; got {refs.Count}.");
    }

    [Fact]
    public void Fixture09_GroupTotal_HasReferencesAcrossMultipleProcedures()
    {
        // m_GroupTotal is reset in GroupHeader0_Format, accumulated in
        // Detail_Format, displayed in GroupFooter0_Format, and also used
        // in Detail_Retreat and GroupFooter0_Format's grand-total add.
        var (resolved, index) = BuildFromFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        var groupTotalDecl = resolved.ModuleScope.Declarations
            .OfType<VariableDeclaration>()
            .Single(v => Declaration.NameComparer.Equals(v.Name, "m_GroupTotal"));

        var refs = index.GetReferencesTo(groupTotalDecl);
        Assert.True(refs.Count >= 4,
            $"Expected >= 4 references to m_GroupTotal; got {refs.Count}.");
    }

    [Fact]
    public void Fixture09_UnknownDeclaration_ReturnsEmptyList()
    {
        // GetReferencesTo for a declaration that was never resolved into
        // the module returns an empty (not null) list. Uses a synthetic
        // declaration that was never inserted into the scope.
        var (_, index) = BuildFromFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        var synthetic = new VariableDeclaration(
            "phantomVar",
            new SourceDeclarationProvenance(
                new SourceRange(
                    new SourceLocation("", 0, 0),
                    new SourceLocation("", 0, 1))),
            declarationScope: VariableScope.Local,
            access: AccessLevel.Private,
            isDefaultedAccess: false,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: System.Array.Empty<ArrayBound>());

        var refs = index.GetReferencesTo(synthetic);
        Assert.Empty(refs);
    }

    // ── Schema-correspondence: resolver output vs. expected.json ─────
    //
    // Session 4 schema-validation: verifies that the resolver's output
    // for fixture 02 carries the fields required by the expected.json
    // schema's resolver-level annotations. The expected.json specifies
    // resolutionStatus ("resolved"/"unresolved"), a resolved reference's
    // location (startLine/endLine via SourceRange), and outcome kind.
    //
    // This is the structural part of the in-conversation schema-validation
    // pass: it confirms that ResolvedReference.Location, Outcome, and the
    // ResolutionOutcome discriminated union are populated correctly for
    // the fixture, without requiring a JSON serialization layer (which is
    // out-of-scope until the #11 object-model registry is in place).

    [Fact]
    public void Fixture02_ResolverOutput_HasLocationAndOutcomeForEveryReference()
    {
        // Every ResolvedReference must carry a non-null Location
        // (maps to expected.json startLine/endLine) and a non-null
        // Outcome (maps to resolutionStatus). A null location or outcome
        // would be a schema-correspondence failure.
        var owning = new ClassDeclaration(
            "frmCaseManagement",
            new SourceDeclarationProvenance(
                new SourceRange(
                    new SourceLocation("", 0, 0),
                    new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            System.Array.Empty<TypeReference>());

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(LocateTestCasesRoot(), "02-cascading-combo-subform.bas"));
        var parseResult = VbaParserPipeline.Parse(source);
        var ast = parseResult.BuildAst("02-cascading-combo-subform.bas");
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, "frmCaseManagement",
            ModuleKind.Form, owning);
        var resolved = new ReferenceResolver().Resolve(scope, ast);

        Assert.NotEmpty(resolved.References);
        Assert.All(resolved.References, r =>
        {
            // Location must be set (maps to startLine/endLine in expected.json).
            Assert.NotNull(r.Location);
            // Outcome must be one of the known discriminated-union variants.
            Assert.True(
                r.Outcome is ResolutionOutcome.Resolved
                    or ResolutionOutcome.ResolvedToMe
                    or ResolutionOutcome.ResolvedToBuiltIn
                    or ResolutionOutcome.ResolvedToObjectModel
                    or ResolutionOutcome.ResolvedViaWithFlattening
                    or ResolutionOutcome.Unresolved,
                $"Unexpected outcome type: {r.Outcome.GetType().Name}");
        });
    }

    // ── Fixture 11: AutoExec startup chain ───────────────────────────

    [Fact]
    public void Fixture11_GlobalVars_AreIndexedByIndex()
    {
        // Each of the 6 public module-level vars is used at least once
        // inside Initialize(), so the index must return ≥ 1 reference
        // for each.
        var (resolved, index) = BuildFromFixture(
            "11-autoexec-startup-chain.bas", "modStartup", ModuleKind.Standard);

        foreach (var varName in new[]
            { "g_CurrentUserID", "g_CurrentUserName", "g_UserRole",
              "g_FiscalYear", "g_AppStartTime", "g_DebugMode" })
        {
            var decl = resolved.ModuleScope.Declarations
                .OfType<VariableDeclaration>()
                .SingleOrDefault(v => Declaration.NameComparer.Equals(v.Name, varName));
            Assert.NotNull(decl);
            var refs = index.GetReferencesTo(decl!);
            Assert.True(refs.Count >= 1,
                $"Expected >= 1 reference to '{varName}' in the cross-reference index.");
        }
    }
}
