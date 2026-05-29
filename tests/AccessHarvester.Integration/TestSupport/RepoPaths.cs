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
        => Path.Combine(TestCases(), "harvester-schema.v1.1.json");

    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> until it finds
    /// a directory containing <paramref name="sentinel"/> (a file name or
    /// relative path that uniquely identifies the target directory) and
    /// returns that directory's absolute path. Useful for resolving the
    /// repo root from a sentinel like <c>AccessHarvester.sln</c>.
    /// </summary>
    public static string LocateDirectoryContaining(string sentinel)
    {
        string match = Locate(sentinel);
        string? parent = Path.GetDirectoryName(match);
        if (string.IsNullOrEmpty(parent))
            throw new DirectoryNotFoundException(
                $"Sentinel '{sentinel}' resolved to '{match}' but has no parent directory.");
        return parent;
    }

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
