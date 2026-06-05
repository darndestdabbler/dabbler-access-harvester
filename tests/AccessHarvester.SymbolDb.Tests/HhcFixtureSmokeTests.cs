using System.Collections.Generic;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using Xunit;
using Xunit.Abstractions;

namespace AccessHarvester.SymbolDb.Tests;

// Tier-1 smoke test: runs the builder against the committed synthetic
// HomeHealthCare Form_frmProviderSearch + basListHelpers input and asserts on
// counts and known shapes. Output is also dumped via ITestOutputHelper so the
// numbers are visible in dotnet test logs without rerunning the extractor.
//
// Set 005: re-based off the proprietary NorthwindDevEdition fixture onto the
// committed HHC kb so the test runs live on every clone.
public sealed class HhcFixtureSmokeTests
{
    private readonly ITestOutputHelper _output;

    public HhcFixtureSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public void Build_FormFrmProviderSearch_AndBasListHelpers_ProducesExpectedShape()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmProviderSearch(),
            FixtureLoader.LoadBasListHelpers(),
        };

        var builder = new SymbolDbBuilder();
        var result = builder.Build(rawModules);

        // --- Always-true sanity ---
        Assert.Equal(2, result.Modules.Count);
        Assert.False(result.Modules.Any(m => m.IsDeferred),
            "This two-module fixture set has no Old-companion deferred modules.");

        var formModule = Assert.Single(result.Modules.Where(m => m.Fqn == "Form_frmProviderSearch"));
        Assert.Equal("Form", formModule.Kind);
        Assert.Equal("utf-16-le", formModule.Encoding);

        var listHelpersModule = Assert.Single(result.Modules.Where(m => m.Fqn == "basListHelpers"));
        Assert.Equal("Module", listHelpersModule.Kind);
        Assert.Equal("ascii", listHelpersModule.Encoding);

        // --- Probe target: lstProviderList_DblClick(Cancel As Integer) ---
        AssertProcedureSignature(result.Symbols, result.Parameters,
            "Form_frmProviderSearch.lstProviderList_DblClick",
            expectedKind: "sub",
            expectedParameters: new[]
            {
                ("Cancel", "Integer", "ByRef", false, false, false),
            });

        // --- basListHelpers.BuildContextArgs(ParamArray pairs() As Variant) ---
        // The ParamArray probe: a function whose sole parameter is a
        // ByRef ParamArray array of Variant.
        AssertProcedureSignature(result.Symbols, result.Parameters,
            "basListHelpers.BuildContextArgs",
            expectedKind: "function",
            expectedParameters: new[]
            {
                ("pairs", "Variant", "ByRef", false, true, true),
            });

        // --- The form's StatusEnum has exactly its three named values ---
        var enumValues = result.Symbols
            .Where(s => s.Kind == "enum_value" && s.DeclaredInFqn == "Form_frmProviderSearch.StatusEnum")
            .Select(s => s.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[] { "STATUS_ACTIVE", "STATUS_ALL", "STATUS_INACTIVE" },
            enumValues);

        // --- The exact two local Dims inside btnSearch_Click ---
        var locals = result.Symbols
            .Where(s => s.DeclaredInFqn == "Form_frmProviderSearch.btnSearch_Click")
            .Select(s => s.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "i", "where" }, locals);

        // --- basListHelpers exposes its public helper functions ---
        var listHelperFunctions = result.Symbols
            .Where(s => s.ModuleFqn == "basListHelpers" && s.Kind == "function")
            .Select(s => s.Name).ToList();
        Assert.Contains("BuildAddableRowSource", listHelperFunctions);
        Assert.Contains("BuildContextArgs", listHelperFunctions);
        Assert.Contains("GetContextString", listHelperFunctions);
        Assert.Contains("IsAddMode", listHelperFunctions);

        // --- Dump full counts so any future change to the fixture surfaces ---
        var stats = result.Statistics;
        _output.WriteLine($"Modules total = {stats.ModuleCount}, deferred = {stats.DeferredModuleCount}");
        _output.WriteLine($"Symbols total = {stats.SymbolCount}, dead = {stats.DeadSymbolCount}");
        _output.WriteLine(
            $"Subs={stats.ProcedureCount}, Functions={stats.FunctionCount}, " +
            $"Properties={stats.PropertyCount}, ModuleVars={stats.ModuleVarCount}, " +
            $"EnumDecls={stats.EnumDeclCount}, EnumValues={stats.EnumValueCount}");
    }

    private static void AssertProcedureSignature(
        IReadOnlyList<AccessHarvester.SymbolDb.Models.SymbolRow> symbols,
        IReadOnlyList<AccessHarvester.SymbolDb.Models.ParameterRow> parameters,
        string fqn,
        string expectedKind,
        IReadOnlyList<(string Name, string DataType, string Passing, bool IsOptional, bool IsParamArray, bool IsArray)> expectedParameters)
    {
        var symbol = Assert.Single(symbols.Where(s => s.Fqn == fqn));
        Assert.Equal(expectedKind, symbol.Kind);
        Assert.NotNull(symbol.SignatureString);
        Assert.NotNull(symbol.BodyHashMd5);

        var actual = parameters.Where(p => p.ProcedureFqn == fqn).OrderBy(p => p.Position).ToList();
        Assert.Equal(expectedParameters.Count, actual.Count);
        for (int i = 0; i < expectedParameters.Count; i++)
        {
            var (name, dataType, passing, isOpt, isParamArray, isArray) = expectedParameters[i];
            Assert.Equal(name, actual[i].Name);
            Assert.Equal(dataType, actual[i].DataType);
            Assert.Equal(passing, actual[i].Passing);
            Assert.Equal(isOpt, actual[i].IsOptional);
            Assert.Equal(isParamArray, actual[i].IsParamArray);
            Assert.Equal(isArray, actual[i].IsArray);
        }
    }
}
