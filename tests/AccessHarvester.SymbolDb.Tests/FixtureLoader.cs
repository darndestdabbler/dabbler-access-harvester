using System;
using System.IO;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.SourceLoading;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Locates the canonical fixture root (tmp/comprehension-ir/AccessMetaData/
// NorthwindDevEdition/) by walking up from the test assembly. The path is
// the same one the existing pipeline tests use; centralising the lookup
// keeps each test class small.
//
// Set 004 (northwind-symbol-db-sqlite-residue): the NorthwindDevEdition KB
// fixture is proprietary and local-only — it lives under the gitignored tmp/
// tree and is never committed, so it is ABSENT on a fresh clone. This loader
// is the single chokepoint that decides fixture availability: NorthwindKbRoot()
// and the Load* helpers all funnel through RequireNorthwindFixture(), which
// raises a SkippableFact skip (rather than a hard failure) when the fixture is
// missing. Every symbol-db-data-dependent test is a [SkippableFact]/[SkippableTheory]
// so it skips cleanly when absent and runs every live assertion when present.
internal static class FixtureLoader
{
    private const string FixtureSkipReason =
        "NorthwindDevEdition symbol-db fixture " +
        "(tmp/comprehension-ir/AccessMetaData/NorthwindDevEdition/kb) is proprietary " +
        "and local-only; absent on a fresh clone. See " +
        "docs/session-sets/004-northwind-symbol-db-sqlite-residue/.";

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
    // below can use it without recursing through RequireNorthwindFixture().
    private static string KbRootPath()
    {
        return Path.Combine(LocateRepoRoot(),
            "tmp", "comprehension-ir", "AccessMetaData", "NorthwindDevEdition", "kb");
    }

    // True when the proprietary, local-only NorthwindDevEdition KB fixture is
    // present on disk. The two module-source files behind BuildNorthwind() are
    // the canonical presence signal for the whole kb tree.
    public static bool NorthwindFixtureAvailable
    {
        get
        {
            string kb = KbRootPath();
            return File.Exists(Path.Combine(kb, "Forms", "frmCompanyDetail.json"))
                && File.Exists(Path.Combine(kb, "ModulesAndClasses", "modStrings.json"));
        }
    }

    // Skip-when-absent guard. Tests funnel through this (directly, or via
    // NorthwindKbRoot()/Load*) so a missing proprietary fixture skips the test
    // instead of failing it. Mirrors the 003 CanonicalGoldenRegressionTests pattern.
    public static void RequireNorthwindFixture()
    {
        Skip.IfNot(NorthwindFixtureAvailable, FixtureSkipReason);
    }

    public static string NorthwindKbRoot()
    {
        RequireNorthwindFixture();
        return KbRootPath();
    }

    public static RawModule LoadFormFrmCompanyDetail()
    {
        RequireNorthwindFixture();
        string path = Path.Combine(KbRootPath(), "Forms", "frmCompanyDetail.json");
        return KbVbaSourceLoader.FromKbJson(path, "Form_frmCompanyDetail", "Form")
            ?? throw new InvalidOperationException(
                $"VbaCode missing from {path}; cannot run Session 1 fixture tests without it.");
    }

    public static RawModule LoadModStrings()
    {
        RequireNorthwindFixture();
        string path = Path.Combine(KbRootPath(), "ModulesAndClasses", "modStrings.json");
        return KbVbaSourceLoader.FromKbJson(path, "modStrings", "Module")
            ?? throw new InvalidOperationException(
                $"VbaCode missing from {path}; cannot run Session 1 fixture tests without it.");
    }
}
