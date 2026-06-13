using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Set 008 Session 2: SymbolDbBuilder Build Pass 2 gained a per-module try/catch
// mirroring Pipeline.cs's per-file catch (006 review F-3-2). Previously a single
// module that threw during parse/scope-build failed the entire Build; now it
// degrades that one module (kept in Modules, contributes no symbols) and is
// recorded on BuildResult.ParseFailedModuleFqns, while sibling modules still
// extract normally.
public sealed class S2PerModuleResilienceTests
{
    private static readonly RawModule GoodModule = new(
        "modGood", "Module", "/synthetic/modGood.bas", "ascii",
        """
        Option Compare Database
        Public Function Greet(ByVal personName As String) As String
            Greet = "Hi " & personName
        End Function
        """);

    // A module whose source is null forces a throw inside Pass 2's
    // LoadAndParse — a deterministic stand-in for any module that fails to
    // decode / parse / scope-build at build time.
    private static readonly RawModule BadModule = new(
        "modBad", "Module", "/synthetic/modBad.bas", "ascii", null!);

    [Fact]
    public void OneFailingModule_DegradesAlone_SiblingsSurvive()
    {
        var result = new SymbolDbBuilder().Build(new[] { GoodModule, BadModule });   // must not throw

        // The good module still produced its symbols.
        Assert.Contains(result.Symbols, s => s.Fqn.StartsWith("modGood", System.StringComparison.Ordinal));

        // The bad module is recorded as parse-failed, explicitly — not silently dropped.
        Assert.NotNull(result.ParseFailedModuleFqns);
        Assert.Contains("modBad", result.ParseFailedModuleFqns!);
        Assert.DoesNotContain("modGood", result.ParseFailedModuleFqns!);
    }

    [Fact]
    public void CleanBuild_HasEmptyParseFailedList()
    {
        var result = new SymbolDbBuilder().Build(new[] { GoodModule });
        Assert.NotNull(result.ParseFailedModuleFqns);
        Assert.Empty(result.ParseFailedModuleFqns!);
    }
}
