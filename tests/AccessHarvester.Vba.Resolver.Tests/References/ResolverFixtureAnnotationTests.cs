using System.IO;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.References;

// Session 4 cross-cut tests: run the full resolver over the canonical
// fixtures (02, 03, 08, 09) and assert resolver-level annotations —
// unresolved provenance on 08, With-block flattening on 03, and
// resolution coverage on 02 and 09.
public sealed class ResolverFixtureAnnotationTests
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

    private static ResolvedModule ResolveFixture(
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
        return new ReferenceResolver().Resolve(scope, ast);
    }

    // ── Fixture 08: Unresolvable — provenance assertions ─────────────
    //
    // Session 4 spec: "Confirm unresolved provenance is correct on 08."
    // dbOpenSnapshot is not in the built-in registry or the scope chain
    // → NotInScope. Member access on `db` and `rs` (declared but typed
    // as DAO objects for which the resolver has no member registry) goes
    // through the default-member-not-yet-resolved path.

    [Fact]
    public void Fixture08_LocalVariables_AreResolved()
    {
        var rm = ResolveFixture("08-unresolvable.bas", "modConfigDriven");

        foreach (var localName in new[]
            { "sConfigKey", "sFormName", "sFilter", "db", "rs", "iStart", "iEnd", "sExpr", "sCallback" })
        {
            var hits = rm.References
                .Where(r => r.Expression is IdentifierExpression id
                            && string.Equals(id.Name, localName,
                                System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(hits.Count > 0,
                $"Expected at least one reference to '{localName}'.");
            // Every use of each listed local must resolve — partial regressions
            // (where some uses are Resolved and others are not) are defects.
            Assert.All(hits, r => Assert.IsType<ResolutionOutcome.Resolved>(r.Outcome));
        }
    }

    [Fact]
    public void Fixture08_dbOpenSnapshot_IsUnresolved_NotInScope()
    {
        var rm = ResolveFixture("08-unresolvable.bas", "modConfigDriven");

        var hits = rm.References
            .Where(r => r.Expression is IdentifierExpression id
                        && string.Equals(id.Name, "dbOpenSnapshot",
                            System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(hits.Count > 0, "Expected at least one reference to dbOpenSnapshot.");
        Assert.All(hits, r => Assert.Equal(
            UnresolvedReasons.NotInScope,
            Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
    }

    [Fact]
    public void Fixture08_MemberAccessOnDb_IsDefaultMemberNotYetResolved()
    {
        var rm = ResolveFixture("08-unresolvable.bas", "modConfigDriven");

        var openRecordset = rm.References
            .Where(r => r.Expression is MemberAccessExpression m
                        && string.Equals(m.MemberName, "OpenRecordset",
                            System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(openRecordset.Count > 0,
            "Expected at least one reference to OpenRecordset member access.");
        Assert.All(openRecordset, r => Assert.Equal(
            UnresolvedReasons.DefaultMemberNotYetResolved,
            Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
    }

    [Fact]
    public void Fixture08_BangAccessOnRs_IsDefaultMemberNotYetResolved()
    {
        var rm = ResolveFixture("08-unresolvable.bas", "modConfigDriven");

        var bangs = rm.References
            .Where(r => r.Expression is BangAccessExpression)
            .ToList();
        Assert.True(bangs.Count > 0,
            "Expected at least one bang-access reference on rs.");
        Assert.All(bangs, r => Assert.Equal(
            UnresolvedReasons.DefaultMemberNotYetResolved,
            Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
    }

    [Fact]
    public void Fixture08_VbaBuiltInsAndAccessSpecific_AreResolvedToBuiltIn()
    {
        // These names are registered in BuiltInFunctionRegistry as
        // BuiltInCategory.AccessSpecific and always resolve via the
        // built-in registry step (step 3) when no IAccessObjectModelRegistry
        // is provided. DoCmd and Application are also registered there
        // because #11 (the real Access object-model registry) is not
        // yet integrated; once #11 lands these two will move to
        // ResolvedToObjectModel. The remaining names (CurrentUser, DLookup,
        // Eval, MsgBox, CurrentDb) are expected to stay in the built-in
        // registry long-term.
        var rm = ResolveFixture("08-unresolvable.bas", "modConfigDriven");

        foreach (var builtInName in new[]
            { "CurrentDb", "CurrentUser", "DLookup", "DoCmd", "Application", "Eval", "MsgBox" })
        {
            var hits = rm.References
                .Where(r => r.Expression is IdentifierExpression id
                            && string.Equals(id.Name, builtInName,
                                System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(hits.Count > 0,
                $"Expected at least one reference to '{builtInName}'.");
            Assert.All(hits, r => Assert.IsType<ResolutionOutcome.ResolvedToBuiltIn>(r.Outcome));
        }
    }

    // ── Fixture 03: With-block flattening ────────────────────────────

    [Fact]
    public void Fixture03_ImplicitWithMembers_AreResolvedViaWithFlattening()
    {
        var owning = MakeFormClassDecl("frmPermitApplication");
        var rm = ResolveFixture(
            "03-with-block-shadowing.bas", "frmPermitApplication",
            ModuleKind.Form, owning);

        var withFlattened = rm.References
            .Where(r => r.Outcome is ResolutionOutcome.ResolvedViaWithFlattening)
            .ToList();
        Assert.True(withFlattened.Count > 0,
            "Expected ResolvedViaWithFlattening outcomes for implicit-with members in fixture 03.");

        // Session-2 synthesis §3 single-wrap invariant: InnerOutcome is
        // never itself ResolvedViaWithFlattening.
        Assert.All(withFlattened, r =>
            Assert.IsNotType<ResolutionOutcome.ResolvedViaWithFlattening>(
                ((ResolutionOutcome.ResolvedViaWithFlattening)r.Outcome).InnerOutcome));
    }

    [Fact]
    public void Fixture03_ImplicitWithBangAccess_IsResolvedViaWithFlattening()
    {
        var owning = MakeFormClassDecl("frmPermitApplication");
        var rm = ResolveFixture(
            "03-with-block-shadowing.bas", "frmPermitApplication",
            ModuleKind.Form, owning);

        var bangFlattened = rm.References
            .Where(r => r.Expression is ImplicitWithBangExpression
                        && r.Outcome is ResolutionOutcome.ResolvedViaWithFlattening)
            .ToList();
        Assert.True(bangFlattened.Count > 0,
            "Expected at least one ImplicitWithBangExpression resolved via with-flattening.");
    }

    [Fact]
    public void Fixture03_NoOutcome_HasReason_WithBlockFlatteningDeferred()
    {
        var owning = MakeFormClassDecl("frmPermitApplication");
        var rm = ResolveFixture(
            "03-with-block-shadowing.bas", "frmPermitApplication",
            ModuleKind.Form, owning);

        var deferred = rm.References
            .Where(r => r.Outcome is ResolutionOutcome.Unresolved u
                        && u.Reason == UnresolvedReasons.WithBlockFlatteningDeferred)
            .ToList();
        Assert.Empty(deferred);
    }

    [Fact]
    public void Fixture03_dbOpenDynaset_IsUnresolved_NotInScope()
    {
        // dbOpenDynaset is a DAO constant not in the curated built-in
        // registry, mirroring the dbOpenSnapshot check for fixture 08.
        var owning = MakeFormClassDecl("frmPermitApplication");
        var rm = ResolveFixture(
            "03-with-block-shadowing.bas", "frmPermitApplication",
            ModuleKind.Form, owning);

        var hits = rm.References
            .Where(r => r.Expression is IdentifierExpression id
                        && string.Equals(id.Name, "dbOpenDynaset",
                            System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(hits.Count > 0, "Expected at least one reference to dbOpenDynaset.");
        Assert.All(hits, r => Assert.Equal(
            UnresolvedReasons.NotInScope,
            Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
    }

    // ── Fixture 02: Bang operator and Me access ───────────────────────

    [Fact]
    public void Fixture02_Me_IsResolvedToMe()
    {
        var owning = MakeFormClassDecl("frmCaseManagement");
        var rm = ResolveFixture(
            "02-cascading-combo-subform.bas", "frmCaseManagement",
            ModuleKind.Form, owning);

        var meRefs = rm.References
            .Where(r => r.Expression is MeExpression
                        && r.Outcome is ResolutionOutcome.ResolvedToMe)
            .ToList();
        Assert.True(meRefs.Count > 0, "Expected at least one Me reference resolving to ResolvedToMe.");
    }

    [Fact]
    public void Fixture02_HasResolvedToMeAndUnresolvedOutcomes()
    {
        // Content-bearing smoke: fixture 02 contains Me references (ResolvedToMe)
        // and unresolved names (RecalculateCaseloadTotals, subform navigation
        // members). Fixture 02 does not call any BuiltInFunctionRegistry names
        // so ResolvedToBuiltIn is not asserted here.
        var owning = MakeFormClassDecl("frmCaseManagement");
        var rm = ResolveFixture(
            "02-cascading-combo-subform.bas", "frmCaseManagement",
            ModuleKind.Form, owning);

        Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.ResolvedToMe);
        Assert.Contains(rm.References, r => r.Outcome is ResolutionOutcome.Unresolved);
        Assert.NotEmpty(rm.References);
    }

    // ── Fixture 09: Report grouping events — module-level vars ────────

    [Fact]
    public void Fixture09_ModuleLevelVariables_AreResolved()
    {
        var rm = ResolveFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        foreach (var varName in new[] { "m_GrandTotal", "m_GroupTotal", "m_GroupCount" })
        {
            var uses = rm.References
                .Where(r => r.Expression is IdentifierExpression id
                            && string.Equals(id.Name, varName,
                                System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(uses.Count > 0,
                $"Expected at least one reference to '{varName}'.");
            Assert.All(uses, r => Assert.IsType<ResolutionOutcome.Resolved>(r.Outcome));
        }
    }

    [Fact]
    public void Fixture09_DiagnosticsCount_MatchesUnresolvedCount()
    {
        var rm = ResolveFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        Assert.Equal(rm.UnresolvedReferences.Count, rm.Diagnostics.Count);
    }

    // ── Fixture 11: AutoExec startup chain ───────────────────────────
    //
    // Session 4 cross-cut: standard module with 6 public module-level
    // vars, 2 functions (Initialize, GetAppVersion), bang access on
    // DAO.Recordset (`rs!UserID` etc.), and dbOpenSnapshot (unresolved).

    [Fact]
    public void Fixture11_GlobalVars_AreResolved()
    {
        var rm = ResolveFixture("11-autoexec-startup-chain.bas", "modStartup");

        foreach (var varName in new[]
            { "g_CurrentUserID", "g_CurrentUserName", "g_UserRole",
              "g_FiscalYear", "g_AppStartTime", "g_DebugMode" })
        {
            var uses = rm.References
                .Where(r => r.Expression is IdentifierExpression id
                            && string.Equals(id.Name, varName,
                                System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(uses.Count > 0,
                $"Expected at least one reference to '{varName}'.");
            Assert.All(uses, r => Assert.IsType<ResolutionOutcome.Resolved>(r.Outcome));
        }
    }

    [Fact]
    public void Fixture11_BangAccessOnRs_IsDefaultMemberNotYetResolved()
    {
        var rm = ResolveFixture("11-autoexec-startup-chain.bas", "modStartup");

        var bangs = rm.References
            .Where(r => r.Expression is BangAccessExpression b
                        && string.Equals(b.Target is IdentifierExpression id ? id.Name : "",
                            "rs", System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(bangs.Count > 0,
            "Expected at least one bang-access reference on rs (rs!UserID etc.).");
        Assert.All(bangs, r => Assert.Equal(
            UnresolvedReasons.DefaultMemberNotYetResolved,
            Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
    }

    [Fact]
    public void Fixture11_dbOpenSnapshot_IsUnresolved_NotInScope()
    {
        var rm = ResolveFixture("11-autoexec-startup-chain.bas", "modStartup");

        var hits = rm.References
            .Where(r => r.Expression is IdentifierExpression id
                        && string.Equals(id.Name, "dbOpenSnapshot",
                            System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(hits.Count > 0, "Expected at least one reference to dbOpenSnapshot.");
        Assert.All(hits, r => Assert.Equal(
            UnresolvedReasons.NotInScope,
            Assert.IsType<ResolutionOutcome.Unresolved>(r.Outcome).Reason));
    }

    [Fact]
    public void Fixture11_BuiltIns_AreResolvedToBuiltIn()
    {
        var rm = ResolveFixture("11-autoexec-startup-chain.bas", "modStartup");

        foreach (var builtInName in new[]
            { "CurrentDb", "MsgBox", "Application", "Now", "IIf",
              "InStr", "UCase", "Month", "Year" })
        {
            var hits = rm.References
                .Where(r => r.Expression is IdentifierExpression id
                            && string.Equals(id.Name, builtInName,
                                System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(hits.Count > 0,
                $"Expected at least one reference to '{builtInName}'.");
            Assert.All(hits, r => Assert.IsType<ResolutionOutcome.ResolvedToBuiltIn>(r.Outcome));
        }
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static ClassDeclaration MakeFormClassDecl(string formName)
        => new ClassDeclaration(
            formName,
            new SourceDeclarationProvenance(
                new SourceRange(
                    new SourceLocation("", 0, 0),
                    new SourceLocation("", 0, 1))),
            ClassDeclarationKind.FormModule,
            System.Array.Empty<TypeReference>());
}
