using System.IO;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using AccessHarvester.Vba.Resolver.Types;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.Types;

// Session 4 cross-cut: run TypeInferencePass over the anchor fixtures
// (02, 03, 09, 11) and assert that the inferred-type map reflects each
// module's actual Variant content. All four anchor fixtures declare
// only concretely-typed locals and module-level variables — no Variant
// declarations appear — so the resulting InferredTypeMap must be empty.
//
// This is a negative-space correctness check: it confirms that the
// inference pass does not accidentally treat typed declarations as
// Variant candidates, and does not silently produce stale entries for
// declarations whose declared type is already concrete.
public sealed class TypeInferenceFixtureTests
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

    private static InferredTypeMap RunInferenceOnFixture(
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
        return TypeInferencePass.Run(resolved);
    }

    // ── Fixture 02: cascading combo ──────────────────────────────────
    // No local variables declared; event handler only uses Me/bang refs.

    [Fact]
    public void Fixture02_TypeInferencePass_ProducesEmptyMap()
    {
        var owning = MakeFormClassDecl("frmCaseManagement");
        var map = RunInferenceOnFixture(
            "02-cascading-combo-subform.bas", "frmCaseManagement",
            ModuleKind.Form, owning);

        Assert.Equal(0, map.Count);
    }

    // ── Fixture 03: With-block shadowing ─────────────────────────────
    // db As DAO.Database, rs As DAO.Recordset, sStatus As String —
    // all concretely typed; none are Variant candidates.

    [Fact]
    public void Fixture03_TypeInferencePass_ProducesEmptyMap()
    {
        var owning = MakeFormClassDecl("frmPermitApplication");
        var map = RunInferenceOnFixture(
            "03-with-block-shadowing.bas", "frmPermitApplication",
            ModuleKind.Form, owning);

        Assert.Equal(0, map.Count);
    }

    // ── Fixture 09: report grouping events ───────────────────────────
    // m_GrandTotal As Currency, m_GroupTotal As Currency,
    // m_GroupCount As Long — all concretely typed.

    [Fact]
    public void Fixture09_TypeInferencePass_ProducesEmptyMap()
    {
        var map = RunInferenceOnFixture(
            "09-report-grouping-events.bas", "rptSummary",
            ModuleKind.Report);

        Assert.Equal(0, map.Count);
    }

    // ── Fixture 11: AutoExec startup chain ───────────────────────────
    // 6 public Long/String/Integer/Date/Boolean module-level vars;
    // local String and DAO.Database/DAO.Recordset — none are Variant.

    [Fact]
    public void Fixture11_TypeInferencePass_ProducesEmptyMap()
    {
        var map = RunInferenceOnFixture(
            "11-autoexec-startup-chain.bas", "modStartup",
            ModuleKind.Standard);

        Assert.Equal(0, map.Count);
    }

    // ── Positive-inference smoke: inline Variant source ───────────────
    // Proves the TypeInferencePass actually produces entries when Variant
    // declarations exist, closing the gap where all four anchor fixtures
    // are all-typed and would pass even if the pass were broken.
    //
    // The anchor fixtures are correctly all-typed (no Variant locals);
    // the positive coverage already lives in TypeInferencePassTests.cs.
    // This smoke test keeps the fixture-level infrastructure honest by
    // exercising the pass with a Variant declaration via the same
    // RunInferenceOnFixture helper pipeline.

    [Fact]
    public void InlineVariantSource_TypeInferencePass_ProducesNonEmptyMap()
    {
        const string src = @"
Sub Demo()
    Dim x As Variant
    x = ""hello""
End Sub";
        var parseResult = VbaParserPipeline.Parse(src);
        var ast = parseResult.BuildAst("inline-smoke.bas");
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, "modSmoke", ModuleKind.Standard);
        var resolved = new ReferenceResolver().Resolve(scope, ast);
        var map = TypeInferencePass.Run(resolved);

        Assert.True(map.Count > 0,
            "Expected a non-empty InferredTypeMap for a module with Variant declarations.");
        var entries = map.All.Values;
        Assert.Contains(entries, t => t.TypeName == "String");
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
