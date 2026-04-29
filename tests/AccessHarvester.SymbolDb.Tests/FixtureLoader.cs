using System;
using System.IO;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.SourceLoading;

namespace AccessHarvester.SymbolDb.Tests;

// Locates the canonical fixture root (tmp/comprehension-ir/AccessMetaData/
// NorthwindDevEdition/) by walking up from the test assembly. The path is
// the same one the existing pipeline tests use; centralising the lookup
// keeps each test class small.
internal static class FixtureLoader
{
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

    public static string NorthwindKbRoot()
    {
        return Path.Combine(LocateRepoRoot(),
            "tmp", "comprehension-ir", "AccessMetaData", "NorthwindDevEdition", "kb");
    }

    public static RawModule LoadFormFrmCompanyDetail()
    {
        string path = Path.Combine(NorthwindKbRoot(), "Forms", "frmCompanyDetail.json");
        return KbVbaSourceLoader.FromKbJson(path, "Form_frmCompanyDetail", "Form")
            ?? throw new InvalidOperationException(
                $"VbaCode missing from {path}; cannot run Session 1 fixture tests without it.");
    }

    public static RawModule LoadModStrings()
    {
        string path = Path.Combine(NorthwindKbRoot(), "ModulesAndClasses", "modStrings.json");
        return KbVbaSourceLoader.FromKbJson(path, "modStrings", "Module")
            ?? throw new InvalidOperationException(
                $"VbaCode missing from {path}; cannot run Session 1 fixture tests without it.");
    }
}
