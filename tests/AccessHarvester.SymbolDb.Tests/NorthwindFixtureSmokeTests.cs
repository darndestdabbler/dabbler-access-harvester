using System.Collections.Generic;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using Xunit;
using Xunit.Abstractions;

namespace AccessHarvester.SymbolDb.Tests;

// Tier-1 smoke test: runs the builder against the canonical NorthwindDevEdition
// Form_frmCompanyDetail + modStrings input and asserts on counts and known
// probe-target shapes. Output is also dumped via ITestOutputHelper so the
// numbers are visible in dotnet test logs without rerunning the extractor.
public sealed class NorthwindFixtureSmokeTests
{
    private readonly ITestOutputHelper _output;

    public NorthwindFixtureSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Build_FormFrmCompanyDetail_AndModStrings_ProducesExpectedShape()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmCompanyDetail(),
            FixtureLoader.LoadModStrings(),
        };

        var builder = new SymbolDbBuilder();
        var result = builder.Build(rawModules);

        // --- Always-true sanity ---
        Assert.Equal(2, result.Modules.Count);
        Assert.False(result.Modules.Any(m => m.IsDeferred),
            "NorthwindDevEdition has no Old-companion modules in this two-module fixture set.");

        var formModule = Assert.Single(result.Modules.Where(m => m.Fqn == "Form_frmCompanyDetail"));
        Assert.Equal("Form", formModule.Kind);
        Assert.Equal("utf-16-le", formModule.Encoding);

        var modStringsModule = Assert.Single(result.Modules.Where(m => m.Fqn == "modStrings"));
        Assert.Equal("Module", modStringsModule.Kind);
        Assert.Equal("ascii", modStringsModule.Encoding);

        // --- Probe target: cboCompanyTypeID_BeforeUpdate parameters ---
        AssertProcedureSignature(result.Symbols, result.Parameters,
            "Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate",
            expectedKind: "sub",
            expectedParameters: new[]
            {
                ("Cancel", "Integer", "ByRef", false, false, false),
            });

        // --- modStrings.GetString ---
        AssertProcedureSignature(result.Symbols, result.Parameters,
            "modStrings.GetString",
            expectedKind: "function",
            expectedParameters: new[]
            {
                ("ID",     "enumStrings", "ByVal", false, false, false),
                ("params", "Variant",     "ByRef", false, true,  true),
            });

        // --- The exact seven m_lng* / m_str* module variables in frmCompanyDetail ---
        var moduleVars = result.Symbols
            .Where(s => s.ModuleFqn == "Form_frmCompanyDetail" && s.Kind == "module_var" && s.DeclaredInFqn is null)
            .Select(s => s.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[]
            {
                "m_lngCalledFromID",
                "m_lngCompanyContactCount",
                "m_lngCustomerOrderCount",
                "m_lngShipperOrderCount",
                "m_lngVendorProductCount",
                "m_lngVendorPurchaseOrderCount",
                "m_strMsgCaption",
            },
            moduleVars);

        // --- The exact two local Dims inside cboCompanyTypeID_BeforeUpdate ---
        var locals = result.Symbols
            .Where(s => s.DeclaredInFqn == "Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate")
            .Select(s => s.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "strDlgMsg", "strOpenArgs" }, locals);

        // --- modStrings.enumStrings has many values, including the named ones ---
        var enumValues = result.Symbols
            .Where(s => s.Kind == "enum_value" && s.DeclaredInFqn == "modStrings.enumStrings")
            .Select(s => s.Name).ToList();
        Assert.Contains("sCompanyDelete_CompanyTypeChange", enumValues);
        Assert.True(enumValues.Count >= 30,
            $"expected enumStrings to carry at least 30 values; got {enumValues.Count}");

        // --- modStrings has the 4 public functions ---
        var modStringsFunctions = result.Symbols
            .Where(s => s.ModuleFqn == "modStrings" && s.Kind == "function")
            .Select(s => s.Name).ToList();
        modStringsFunctions.Sort();
        Assert.Equal(
            new[] { "GetString", "StringFormat", "StringFormatSQL", "StringToDictionary" },
            modStringsFunctions);

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
