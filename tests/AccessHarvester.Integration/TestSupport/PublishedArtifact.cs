using System;
using System.IO;

namespace AccessHarvester.Integration.TestSupport;

/// <summary>
/// Resolves the path to the self-contained published CLI artifact produced
/// by <c>tools/publish/publish.ps1</c>. The publish script writes the
/// single-file exe to <c>&lt;repo-root&gt;/publish/&lt;rid&gt;/AccessHarvester.exe</c>;
/// this helper walks up from the test bin directory to find the repo root,
/// then probes the conventional output location.
/// </summary>
/// <remarks>
/// Tests that consume this helper should be <c>[SkippableFact]</c>-gated
/// so they skip cleanly when the publish step has not been run. The
/// developer (or CI) runs <c>pwsh tools/publish/publish.ps1</c> before
/// invoking these tests.
/// </remarks>
internal static class PublishedArtifact
{
    private const string DefaultRuntimeIdentifier = "win-x64";
    private const string ExeName = "AccessHarvester.exe";

    /// <summary>
    /// Resolves the absolute path to the published exe for the default
    /// <c>win-x64</c> runtime, or <c>null</c> if no artifact is present.
    /// </summary>
    public static string? TryLocate(string runtimeIdentifier = DefaultRuntimeIdentifier)
    {
        string repoRoot = RepoPaths.LocateDirectoryContaining("AccessHarvester.sln");
        string candidate = Path.Combine(repoRoot, "publish", runtimeIdentifier, ExeName);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Returns the absolute path to the published exe, or throws if the
    /// artifact is absent. Tests should prefer <see cref="TryLocate"/> +
    /// <c>Skip.IfNot(...)</c> over calling this directly.
    /// </summary>
    public static string Locate(string runtimeIdentifier = DefaultRuntimeIdentifier)
        => TryLocate(runtimeIdentifier)
           ?? throw new FileNotFoundException(
               $"Published AccessHarvester artifact not found for runtime '{runtimeIdentifier}'. " +
               $"Run `pwsh tools/publish/publish.ps1` from the repo root to produce it.");
}
