using System;
using System.IO;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

using ResolverProcedureDeclaration = AccessHarvester.Vba.Resolver.Declarations.ProcedureDeclaration;

namespace AccessHarvester.Vba.Resolver.Tests;

// End-to-end fixture tests: parse the 6 anchor inputs from
// docs/test-cases/ through VbaParserPipeline -> AstBuilder ->
// ScopeBuildingVisitor and assert scope-tree shape and declaration
// coverage.
public sealed class ScopeBuildingFixtureTests
{
    // ── Fixture loading ──────────────────────────────────────────────

    private static string LocateTestCasesRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = Path.Combine(dir, "docs", "test-cases");
            if (Directory.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate docs/test-cases walking up from '{AppContext.BaseDirectory}'.");
    }

    private static (ProjectScope project, ModuleScope module) BuildFromFixture(
        string fileName,
        string moduleName,
        ModuleKind kind = ModuleKind.Standard)
    {
        string root = LocateTestCasesRoot();
        string source = File.ReadAllText(Path.Combine(root, fileName));
        var parseResult = VbaParserPipeline.Parse(source);
        Module ast = parseResult.BuildAst(fileName);

        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        ModuleScope module = visitor.BuildModuleScope(project, ast, moduleName, kind);
        return (project, module);
    }

    // ── Fixture 01: simple validation handler ───────────────────────
    // 1 procedure (Form_BeforeUpdate), no module-level vars.

    [Fact]
    public void Fixture01_ProducesOneProcedureScopeAndOneDeclaration()
    {
        var (_, module) = BuildFromFixture(
            "01-simple-validation.bas", "frmCustomers", ModuleKind.Form);

        Assert.Single(module.Children);
        var proc = Assert.IsType<ProcedureScope>(module.Children[0]);
        Assert.Equal("Form_BeforeUpdate", proc.Procedure.Name);
        Assert.Empty(proc.Children);

        var decl = Assert.IsType<SubDeclaration>(Assert.Single(module.Declarations));
        Assert.Equal("Form_BeforeUpdate", decl.Name);
        Assert.Equal(AccessLevel.Private, decl.Access);
    }

    [Fact]
    public void Fixture01_ParameterIsRegisteredInProcedureScope()
    {
        var (_, module) = BuildFromFixture(
            "01-simple-validation.bas", "frmCustomers", ModuleKind.Form);

        var procScope = (ProcedureScope)module.Children[0];
        var param = procScope.Declarations.OfType<ParameterDeclaration>().Single();
        Assert.Equal("Cancel", param.Name);
        Assert.Same(procScope, param.Scope);
    }

    // ── Fixture 02: cascading combo with subform filtering ───────────
    // 1 procedure (cboCaseworker_AfterUpdate), no module-level vars.

    [Fact]
    public void Fixture02_ProducesOneProcedureScopeAndOneDeclaration()
    {
        var (_, module) = BuildFromFixture(
            "02-cascading-combo-subform.bas", "frmCaseManagement", ModuleKind.Form);

        Assert.Single(module.Children);
        var proc = Assert.IsType<ProcedureScope>(module.Children[0]);
        Assert.Equal("cboCaseworker_AfterUpdate", proc.Procedure.Name);
        var decl02 = Assert.IsType<SubDeclaration>(Assert.Single(
            module.Declarations.OfType<ResolverProcedureDeclaration>()));
        Assert.Equal("cboCaseworker_AfterUpdate", decl02.Name);
        Assert.Empty(module.Declarations.Where(d => !(d is ResolverProcedureDeclaration)));
    }

    // ── Fixture 09: report grouping events ──────────────────────────
    // 3 private module-level vars (running totals) + 6 procedures.
    // No For Each / With inside procedures.

    [Fact]
    public void Fixture09_ProducesSixProcedureScopes()
    {
        var (_, module) = BuildFromFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        Assert.Equal(6, module.Children.Count);
        Assert.All(module.Children, c => Assert.IsType<ProcedureScope>(c));
        Assert.All(module.Children.Cast<ProcedureScope>(), p => Assert.Empty(p.Children));
    }

    [Fact]
    public void Fixture09_AllProceduresHaveDeclarations()
    {
        var (_, module) = BuildFromFixture(
            "09-report-grouping-events.bas", "rptSummary", ModuleKind.Report);

        string[] expectedNames = new[]
        {
            "Report_Open", "GroupHeader0_Format", "Detail_Format",
            "GroupFooter0_Format", "ReportFooter_Format", "Detail_Retreat",
        };
        var actualNames = module.Declarations
            .OfType<ResolverProcedureDeclaration>()
            .Select(d => d.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(
            expectedNames.OrderBy(n => n).ToArray(),
            actualNames);
        // Three private module-level running-total variables (state for Format events).
        var moduleVars = module.Declarations.OfType<VariableDeclaration>()
            .Where(v => v.DeclarationScope == VariableScope.Module)
            .Select(v => v.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(
            new[] { "m_GrandTotal", "m_GroupCount", "m_GroupTotal" },
            moduleVars);
        Assert.Empty(module.Declarations.Where(
            d => d is not ResolverProcedureDeclaration and not VariableDeclaration));
    }

    // ── Fixture 10: class module with events ────────────────────────
    // 4 private module-level vars (one WithEvents), 2 property pairs
    // (Get+Let each) + 1 interface property = 5 property declarations,
    // 9 Subs total, 14 procedure scopes. Two Subs contain For Each loops.

    [Fact]
    public void Fixture10_ClassModule_HasExpectedProcedureInventory()
    {
        var (_, module) = BuildFromFixture(
            "10-class-with-events.cls", "clsAuditLogger",
            ModuleKind.Class);

        Assert.All(module.Children, c => Assert.IsType<ProcedureScope>(c));

        // Assert exact non-property sub/function names.
        string[] expectedSubs = new[]
        {
            "AttachToForm", "CaptureSnapshot", "Class_Initialize", "Class_Terminate",
            "IAuditLogger_LogEvent", "LogChanges",
            "m_Form_AfterUpdate", "m_Form_BeforeUpdate", "m_Form_Close",
        };
        var actualSubs = module.Children.OfType<ProcedureScope>()
            .Where(p => p.Procedure is not PropertyDeclaration)
            .Select(p => p.Procedure.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(expectedSubs, actualSubs);

        // Assert exact property (name, kind) pairs.
        (string Name, PropertyKind Kind)[] expectedProps = new[]
        {
            ("Enabled", PropertyKind.Get), ("Enabled", PropertyKind.Let),
            ("EntityName", PropertyKind.Get), ("EntityName", PropertyKind.Let),
            ("IAuditLogger_Enabled", PropertyKind.Get),
        };
        var actualProps = module.Children.OfType<ProcedureScope>()
            .Where(p => p.Procedure is PropertyDeclaration)
            .Select(p => (((PropertyDeclaration)p.Procedure).Name, ((PropertyDeclaration)p.Procedure).Kind))
            .OrderBy(t => t.Name).ThenBy(t => t.Kind)
            .ToArray();
        Assert.Equal(expectedProps.OrderBy(t => t.Name).ThenBy(t => t.Kind).ToArray(), actualProps);
    }

    [Fact]
    public void Fixture10_ModuleLevelVariables_AreRegistered()
    {
        var (_, module) = BuildFromFixture(
            "10-class-with-events.cls", "clsAuditLogger",
            ModuleKind.Class);

        var vars = module.Declarations.OfType<VariableDeclaration>()
            .Where(v => v.DeclarationScope == VariableScope.Module)
            .ToList();
        Assert.Equal(4, vars.Count);

        var names = vars.Select(v => v.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "m_AuditEnabled", "m_AuditEntityName", "m_Form", "m_LastSavedSnapshot" },
            names);

        var withEventsVar = vars.Single(v => v.IsWithEvents);
        Assert.Equal("m_Form", withEventsVar.Name);
    }

    [Fact]
    public void Fixture10_ForEachBlocks_ProduceForEachScopesUnderProcedure()
    {
        var (_, module) = BuildFromFixture(
            "10-class-with-events.cls", "clsAuditLogger",
            ModuleKind.Class);

        // CaptureSnapshot and LogChanges each contain exactly one For Each.
        var captureScope = module.Children.OfType<ProcedureScope>()
            .Single(p => p.Procedure.Name == "CaptureSnapshot");
        var logScope = module.Children.OfType<ProcedureScope>()
            .Single(p => p.Procedure.Name == "LogChanges");

        Assert.Single(captureScope.Children.OfType<ForEachScope>());
        Assert.Equal(1, captureScope.Children.Count);
        Assert.Single(logScope.Children.OfType<ForEachScope>());
        Assert.Equal(1, logScope.Children.Count);

        // Every other procedure scope has no nested scopes.
        var withNestedChildren = module.Children.OfType<ProcedureScope>()
            .Where(p => p.Procedure.Name != "CaptureSnapshot"
                     && p.Procedure.Name != "LogChanges"
                     && p.Children.Count > 0)
            .ToList();
        Assert.Empty(withNestedChildren);
    }

    [Fact]
    public void Fixture10_PropertyDeclarations_AreInModuleDeclarations()
    {
        var (_, module) = BuildFromFixture(
            "10-class-with-events.cls", "clsAuditLogger",
            ModuleKind.Class);

        var properties = module.Declarations.OfType<PropertyDeclaration>().ToList();
        // EntityName Get + Let, Enabled Get + Let, IAuditLogger_Enabled Get = 5
        Assert.Equal(5, properties.Count);
    }

    // ── Fixture 11: AutoExec startup chain ──────────────────────────
    // 6 public module-level vars + 2 functions (Initialize, GetAppVersion).

    [Fact]
    public void Fixture11_ProducesTwoProcedureScopes()
    {
        var (_, module) = BuildFromFixture(
            "11-autoexec-startup-chain.bas", "modStartup");

        Assert.Equal(2, module.Children.Count);
        Assert.All(module.Children, c => Assert.IsType<ProcedureScope>(c));
        var names = module.Children.OfType<ProcedureScope>()
            .Select(p => p.Procedure.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(new[] { "GetAppVersion", "Initialize" }, names);
        // Both are Functions (not Subs).
        Assert.All(module.Declarations.OfType<ResolverProcedureDeclaration>(),
            d => Assert.IsType<FunctionDeclaration>(d));
    }

    [Fact]
    public void Fixture11_SixPublicModuleVariables_AreRegistered()
    {
        var (_, module) = BuildFromFixture(
            "11-autoexec-startup-chain.bas", "modStartup");

        var moduleVars = module.Declarations.OfType<VariableDeclaration>()
            .Where(v => v.DeclarationScope == VariableScope.Module)
            .ToList();
        Assert.Equal(6, moduleVars.Count);
        Assert.All(moduleVars, v => Assert.Equal(AccessLevel.Public, v.Access));
        var varNames = moduleVars.Select(v => v.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "g_AppStartTime", "g_CurrentUserID", "g_CurrentUserName",
                    "g_DebugMode", "g_FiscalYear", "g_UserRole" },
            varNames);
    }

    // ── Fixture 12: switchboard navigation ──────────────────────────
    // Option Compare Database + Option Explicit; 12 procedures.

    [Fact]
    public void Fixture12_ProducesExactTwelveProcedures()
    {
        var (_, module) = BuildFromFixture(
            "12-switchboard-navigation.bas", "frmMainSwitchboard", ModuleKind.Form);

        Assert.All(module.Children, c => Assert.IsType<ProcedureScope>(c));
        string[] expected = new[]
        {
            "btnAdminTools_Click", "btnAuditLog_Click", "btnExit_Click",
            "btnHelp_Click", "btnMyOpenCases_Click", "btnNewCase_Click",
            "btnQuickCaseload_Click", "btnRefresh_Click", "btnReports_Click",
            "btnSearchCases_Click", "btnUserManagement_Click", "Form_Load",
        };
        var actual = module.Children.OfType<ProcedureScope>()
            .Select(p => p.Procedure.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Fixture12_OptionExplicitAndOptionCompareDatabaseAreCaptured()
    {
        var (_, module) = BuildFromFixture(
            "12-switchboard-navigation.bas", "frmMainSwitchboard", ModuleKind.Form);

        Assert.True(module.IsOptionExplicit);
        Assert.Equal(OptionCompare.Database, module.OptionCompare);
    }

    // ── Cross-fixture: module metadata ──────────────────────────────

    [Theory]
    [InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Form)]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Report)]
    [InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
    [InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
    [InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Form)]
    public void AllFixtures_ModuleNameAndKindArePropagated(
        string fileName, string moduleName, ModuleKind kind)
    {
        var (project, module) = BuildFromFixture(fileName, moduleName, kind);

        Assert.Equal(moduleName, module.ModuleName);
        Assert.Equal(kind, module.Kind);
        Assert.Same(project, module.Parent);
    }

    // ── Cross-fixture: declaration/scope bidirectional invariant ─────
    // Every ProcedureDeclaration in module.Declarations has a matching
    // ProcedureScope in module.Children, and vice versa.

    [Theory]
    [InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Form)]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Report)]
    [InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
    [InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
    [InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Form)]
    public void AllFixtures_ProcedureDeclarationAndScopeCountsMatch(
        string fileName, string moduleName, ModuleKind kind)
    {
        var (_, module) = BuildFromFixture(fileName, moduleName, kind);

        int declCount = module.Declarations.OfType<ResolverProcedureDeclaration>().Count();
        int scopeCount = module.Children.OfType<ProcedureScope>().Count();
        Assert.Equal(declCount, scopeCount);
    }

    [Theory]
    [InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Form)]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Report)]
    [InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
    [InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
    [InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Form)]
    public void AllFixtures_EachProcedureScopeHasDeclarationInModule(
        string fileName, string moduleName, ModuleKind kind)
    {
        var (_, module) = BuildFromFixture(fileName, moduleName, kind);

        // Forward: every ProcedureScope has a matching declaration.
        foreach (var procScope in module.Children.OfType<ProcedureScope>())
        {
            // Use (name, kind) key for Property declarations to distinguish
            // Get/Let/Set that share a name.
            var procedure = procScope.Procedure;
            if (procedure is PropertyDeclaration prop)
            {
                Assert.Contains(
                    module.Declarations.OfType<PropertyDeclaration>(),
                    d => Declaration.NameComparer.Equals(d.Name, prop.Name) && d.Kind == prop.Kind);
            }
            else
            {
                Assert.Contains(
                    module.Declarations.OfType<ResolverProcedureDeclaration>(),
                    d => Declaration.NameComparer.Equals(d.Name, procedure.Name));
            }
        }

        // Reverse: every ProcedureDeclaration has a matching ProcedureScope.
        foreach (var decl in module.Declarations.OfType<ResolverProcedureDeclaration>())
        {
            if (decl is PropertyDeclaration propDecl)
            {
                Assert.Contains(
                    module.Children.OfType<ProcedureScope>(),
                    s => s.Procedure is PropertyDeclaration pd
                         && Declaration.NameComparer.Equals(pd.Name, propDecl.Name)
                         && pd.Kind == propDecl.Kind);
            }
            else
            {
                Assert.Contains(
                    module.Children.OfType<ProcedureScope>(),
                    s => Declaration.NameComparer.Equals(s.Procedure.Name, decl.Name));
            }
        }
    }

    [Theory]
    [InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Form)]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Report)]
    [InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
    [InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
    [InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Form)]
    public void AllFixtures_TryFindLocal_FindsAllModuleLevelDeclarations(
        string fileName, string moduleName, ModuleKind kind)
    {
        var (_, module) = BuildFromFixture(fileName, moduleName, kind);

        // TryFindLocal is name-only (kind-specific lookup belongs to #8).
        // Iterate distinct names so Property Get/Let pairs sharing a name
        // don't falsely appear to double-validate.
        foreach (var name in module.Declarations
            .Select(d => d.Name)
            .Distinct(Declaration.NameComparer))
        {
            Assert.NotNull(module.TryFindLocal(name));
        }
    }

    [Theory]
    [InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Form)]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Report)]
    [InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
    [InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
    [InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Form)]
    public void AllFixtures_ModuleDeclarations_ContainNoLocalOrParameterDeclarations(
        string fileName, string moduleName, ModuleKind kind)
    {
        var (_, module) = BuildFromFixture(fileName, moduleName, kind);

        // ParameterDeclarations must never appear in module.Declarations —
        // they belong in ProcedureScope.Declarations.
        Assert.Empty(module.Declarations.OfType<ParameterDeclaration>());

        // VariableDeclarations in module.Declarations must all be module-scope,
        // not local or parameter-scope (which would indicate a visitor leak).
        Assert.All(
            module.Declarations.OfType<VariableDeclaration>(),
            v => Assert.Equal(VariableScope.Module, v.DeclarationScope));
    }

    [Theory]
    [InlineData("01-simple-validation.bas",      "frmCustomers",       ModuleKind.Form)]
    [InlineData("02-cascading-combo-subform.bas", "frmCaseManagement",  ModuleKind.Form)]
    [InlineData("09-report-grouping-events.bas",  "rptSummary",         ModuleKind.Report)]
    [InlineData("10-class-with-events.cls",       "clsAuditLogger",     ModuleKind.Class)]
    [InlineData("11-autoexec-startup-chain.bas",  "modStartup",         ModuleKind.Standard)]
    [InlineData("12-switchboard-navigation.bas",  "frmMainSwitchboard", ModuleKind.Form)]
    public void AllFixtures_AllNonRootScopesHaveNonNullRange(
        string fileName, string moduleName, ModuleKind kind)
    {
        var (_, module) = BuildFromFixture(fileName, moduleName, kind);

        Assert.NotNull(module.Range);
        foreach (var procScope in module.Children.OfType<ProcedureScope>())
        {
            Assert.NotNull(procScope.Range);
            foreach (var child in procScope.Children)
                Assert.NotNull(child.Range);
        }
    }
}
