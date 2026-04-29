using System.IO;
using AccessHarvester.SymbolDb.Cli;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Regenerates tools/symbol-db/symbol-db.sqlite from the canonical fixture
// when the file is missing or has not yet been populated by the Session 4+
// AI interpretation pipeline. The C# builder is destructive — it deletes
// and recreates the database — so once Session 4 has written
// Interpretation1 columns to the existing artifact, regenerating would
// silently wipe that work. Skipping the regeneration in that case keeps
// `dotnet test` a sanity check rather than a destructive rebuild.
//
// Run `python -m interpret --db tools/symbol-db/symbol-db.sqlite ...` to
// regenerate the structural rows and re-run interpretations explicitly
// when the upstream C# pipeline shape changes.
public sealed class GenerateArtifacts
{
    [Fact]
    public void Regenerate_NorthwindFixture_ArtifactsLandUnderToolsSymbolDb()
    {
        string repoRoot = FixtureLoader.LocateRepoRoot();
        string dbPath = Path.Combine(repoRoot, "tools", "symbol-db", "symbol-db.sqlite");

        if (HasInterpretationsPopulated(dbPath))
        {
            // Session 4+ has written downstream-only AI interpretation
            // columns. The builder would overwrite them — skip the
            // regeneration and rely on the standalone runner.
            return;
        }

        int exit = BuildArtifacts.RunForNorthwind(repoRoot);
        Assert.Equal(0, exit);
    }

    private static bool HasInterpretationsPopulated(string dbPath)
    {
        if (!File.Exists(dbPath)) return false;
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();
        try
        {
            using var con = new SqliteConnection(cs);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM Symbols WHERE Interpretation1 IS NOT NULL";
            var n = (long)(cmd.ExecuteScalar() ?? 0L);
            return n > 0;
        }
        catch (SqliteException)
        {
            // Schema mismatch (e.g., older DB without Interpretation1) —
            // safe to regenerate.
            return false;
        }
    }
}
