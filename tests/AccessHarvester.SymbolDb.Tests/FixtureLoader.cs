using System;
using System.IO;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.SourceLoading;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Locates the committed synthetic HomeHealthCare kb fixture root
// (samples/GoldenOutput/HomeHealthCare/kb/) by walking up from the test
// assembly. Centralising the lookup keeps each test class small.
//
// Set 005 (northwind-embedded-residue-remediation): these tests were
// re-based off the proprietary, local-only NorthwindDevEdition kb fixture
// onto the synthetic HomeHealthCare kb, which IS committed to the repo
// (samples/GoldenOutput/HomeHealthCare/kb/...). So the symbol-db tests now
// run LIVE on every clone rather than skipping. The skip guard is retained
// as a belt-and-suspenders fallback: if the committed fixture is ever moved
// or a partial checkout omits it, the data-dependent tests skip cleanly
// (mirroring the 004 pattern) instead of hard-failing.
internal static class FixtureLoader
{
    private const string FixtureSkipReason =
        "HomeHealthCare symbol-db kb fixture " +
        "(samples/GoldenOutput/HomeHealthCare/kb) is committed and should be " +
        "present; absent only on a partial checkout.";

    // The form module re-base target: a synthetic provider-search form with
    // a private Enum, several event handlers, same-module and cross-module
    // calls, and control access via dot notation.
    public const string FormFqn = "Form_frmProviderSearch";

    public static string LocateRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string solution = Path.Combine(dir, "AccessHarvester.sln");
            if (File.Exists(solution)) return dir;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"AccessHarvester.sln not found walking up from {AppContext.BaseDirectory}.");
    }

    // Path-only computation — never raises a skip, so the availability probe
    // below can use it without recursing through RequireFixture().
    private static string KbRootPath()
    {
        return Path.Combine(LocateRepoRoot(),
            "samples", "GoldenOutput", "HomeHealthCare", "kb");
    }

    // True when the committed synthetic HomeHealthCare kb fixture is present.
    // The form + standard-module files behind the loaders below are the
    // canonical presence signal for the whole kb tree.
    public static bool FixtureAvailable
    {
        get
        {
            string kb = KbRootPath();
            return File.Exists(Path.Combine(kb, "Forms", "frmProviderSearch.json"))
                && File.Exists(Path.Combine(kb, "ModulesAndClasses", "basListHelpers.json"));
        }
    }

    // Skip-when-absent guard. Tests funnel through this (directly, or via
    // KbRoot()/Load*) so a missing fixture skips the test instead of failing.
    public static void RequireFixture()
    {
        Skip.IfNot(FixtureAvailable, FixtureSkipReason);
    }

    public static string KbRoot()
    {
        RequireFixture();
        return KbRootPath();
    }

    // Standard-module loader: kb/ModulesAndClasses/<name>.json, module FQN == name.
    public static RawModule LoadStandardModule(string name)
    {
        RequireFixture();
        string path = Path.Combine(KbRootPath(), "ModulesAndClasses", name + ".json");
        return KbVbaSourceLoader.FromKbJson(path, name, "Module")
            ?? throw new InvalidOperationException(
                $"VbaCode missing from {path}; cannot run symbol-db fixture tests without it.");
    }

    // Form loader: kb/Forms/<name>.json, module FQN == "Form_<name>".
    public static RawModule LoadForm(string name)
    {
        RequireFixture();
        string path = Path.Combine(KbRootPath(), "Forms", name + ".json");
        return KbVbaSourceLoader.FromKbJson(path, "Form_" + name, "Form")
            ?? throw new InvalidOperationException(
                $"VbaCode missing from {path}; cannot run symbol-db fixture tests without it.");
    }

    // The canonical re-base form: frmProviderSearch (has event handlers,
    // same-module calls, qualified cross-module calls, a private Enum, and
    // dot-notation control access).
    public static RawModule LoadFormFrmProviderSearch() => LoadForm("frmProviderSearch");

    // basListHelpers: a standard module of reusable list-box helpers; its
    // BuildContextArgs(ParamArray pairs() As Variant) is the ParamArray probe.
    public static RawModule LoadBasListHelpers() => LoadStandardModule("basListHelpers");

    // basUtilities: a standard module the search form calls into (qualified
    // cross-module references resolve to its public functions).
    public static RawModule LoadBasUtilities() => LoadStandardModule("basUtilities");
}
