using System;
using System.IO;

namespace AccessHarvester.Integration.TestSupport;

internal static class RepoPaths
{
    private const int MaxAscent = 12;

    public static string TestCases() => Locate(Path.Combine("docs", "test-cases"));
    public static string Examples() => Locate("examples");
    public static string Samples() => Locate("samples");
    public static string SchemaFile()
        => Path.Combine(TestCases(), "harvester-schema.v1.0.json");

    private static string Locate(string relative)
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < MaxAscent; i++)
        {
            string candidate = Path.Combine(dir, relative);
            if (Directory.Exists(candidate) || File.Exists(candidate))
                return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate '{relative}' walking up from '{AppContext.BaseDirectory}'.");
    }
}
