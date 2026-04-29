using AccessHarvester.SymbolDb.Cli;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Always regenerates tools/symbol-db/symbol-db.sqlite and
// measurement-northwind.md from the canonical fixture. The builder is
// deterministic, so re-running on every `dotnet test` produces byte-
// identical output (checked into git as the Session 1 build artifact).
// Sessions 2+ will append rows; this regeneration test is therefore
// transient and will be replaced once those sessions are runnable.
public sealed class GenerateArtifacts
{
    [Fact]
    public void Regenerate_NorthwindFixture_ArtifactsLandUnderToolsSymbolDb()
    {
        string repoRoot = FixtureLoader.LocateRepoRoot();
        int exit = BuildArtifacts.RunForNorthwind(repoRoot);
        Assert.Equal(0, exit);
    }
}
