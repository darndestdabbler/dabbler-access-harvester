using System.IO;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.SymbolDb.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Persistence smoke + MD5 dedup tests. Spins the builder against the committed
// synthetic HomeHealthCare kb fixture, writes the rows to a transient SQLite db,
// and checks that the dedup pass populates equivalence_canonical_fqn for every
// hash-equivalent cluster. No external assertions on cluster membership (the
// fixture's twin set may evolve); the contract is "if there are clusters, the
// canonical points are correct."
//
// Set 005: the fixture-coupled tests were re-based off the proprietary
// NorthwindDevEdition fixture onto the committed HHC kb. The hermetic [Fact]
// tests below already use inline synthetic VBA and are unchanged.
public sealed class PersistenceAndDedupTests
{
    [SkippableFact]
    public void Build_Persist_AndDedup_RoundTrip_PopulatesCanonicalsOnTwins()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmProviderSearch(),
            FixtureLoader.LoadBasListHelpers(),
        };
        var result = new SymbolDbBuilder().Build(rawModules);

        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-tests-{System.Guid.NewGuid():N}.sqlite");
        try
        {
            using var writer = new SymbolDbWriter(tempPath);
            writer.Write(result.Modules, result.Symbols, result.Parameters);

            var dedup = new Md5Deduper();
            var report = dedup.Run(writer.Connection);

            // The cluster count on this fixture is data-driven; assert only
            // that whichever clusters exist were tagged consistently.
            using var cmd = writer.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM Symbols WHERE equivalence_canonical_fqn IS NOT NULL;
                """;
            long taggedCount = (long)cmd.ExecuteScalar()!;
            Assert.Equal((long)report.SiblingsTagged, taggedCount);

            // Every tagged row's canonical FQN must itself exist in Symbols
            // and must NOT have a canonical pointer (canonicals point to
            // themselves implicitly by leaving the column null).
            using var cmd2 = writer.Connection.CreateCommand();
            cmd2.CommandText = """
                SELECT s.equivalence_canonical_fqn
                FROM Symbols s
                WHERE s.equivalence_canonical_fqn IS NOT NULL;
                """;
            using var reader = cmd2.ExecuteReader();
            var canonicals = new System.Collections.Generic.List<string>();
            while (reader.Read()) canonicals.Add(reader.GetString(0));

            foreach (var canon in canonicals.Distinct())
            {
                using var c = writer.Connection.CreateCommand();
                c.CommandText = "SELECT equivalence_canonical_fqn FROM Symbols WHERE fqn = $fqn;";
                var p = c.CreateParameter(); p.ParameterName = "$fqn"; p.Value = canon; c.Parameters.Add(p);
                var pointer = c.ExecuteScalar();
                Assert.True(pointer is null or System.DBNull,
                    $"Canonical {canon} must have a NULL equivalence_canonical_fqn (canonicals point to themselves implicitly).");
            }
        }
        finally
        {
            // SQLite on Windows occasionally keeps a file handle until GC; the
            // try/finally ensures the using block flushes the connection.
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [SkippableFact]
    public void Build_Persist_RoundTrip_RowCountsMatchInMemory()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmProviderSearch(),
            FixtureLoader.LoadBasListHelpers(),
        };
        var result = new SymbolDbBuilder().Build(rawModules);

        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-tests-{System.Guid.NewGuid():N}.sqlite");
        try
        {
            using (var writer = new SymbolDbWriter(tempPath))
            {
                writer.Write(result.Modules, result.Symbols, result.Parameters);

                Assert.Equal((long)result.Modules.Count, ScalarLong(writer.Connection, "SELECT COUNT(*) FROM Modules;"));
                Assert.Equal((long)result.Symbols.Count, ScalarLong(writer.Connection, "SELECT COUNT(*) FROM Symbols;"));
                Assert.Equal((long)result.Parameters.Count, ScalarLong(writer.Connection, "SELECT COUNT(*) FROM Parameters;"));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Persistence_ForeignKeyEnforcement_RejectsOrphanSymbol()
    {
        // Round-1 verification flagged that PRAGMA foreign_keys was never
        // enabled. This test pins the fix: inserting a Symbols row whose
        // module_fqn does not exist in Modules must throw.
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-tests-{System.Guid.NewGuid():N}.sqlite");
        try
        {
            using var writer = new SymbolDbWriter(tempPath);
            using var cmd = writer.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Symbols (
                  fqn, module_fqn, name, kind, is_dead, human_review_required
                ) VALUES (
                  'orphan.foo', 'no-such-module', 'foo', 'sub', 0, 0
                );
                """;
            var ex = Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
            Assert.Contains("FOREIGN KEY", ex.Message, System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Build_DeferredModule_IsRecordedButNotParsed()
    {
        // Deferred modules must produce a Modules row with is_deferred=1
        // and zero Symbols rows tied to that module. The companion module
        // (the live one) gets fully extracted as usual.
        var raw = new[]
        {
            new RawModule("frmFoo", "Form", "/synthetic/frmFoo.cls", "utf-16-le",
                """
                Option Compare Database
                Public Sub Hello()
                    Debug.Print "live"
                End Sub
                """),
            new RawModule("frmFoo_Old", "Form", "/synthetic/frmFoo_Old.cls", "utf-16-le",
                """
                Option Compare Database
                Public Sub Hello_Outdated_Should_Not_Be_Parsed()
                    Debug.Print "deferred"
                End Sub
                """),
        };

        var result = new SymbolDbBuilder().Build(raw);

        var liveModule = Assert.Single(result.Modules.Where(m => m.Fqn == "frmFoo"));
        Assert.False(liveModule.IsDeferred);

        var deferredModule = Assert.Single(result.Modules.Where(m => m.Fqn == "frmFoo_Old"));
        Assert.True(deferredModule.IsDeferred);
        Assert.Equal("frmFoo", deferredModule.DeferredCompanionFqn);

        // The deferred module's procedure must not appear in Symbols at all.
        Assert.Empty(result.Symbols.Where(s => s.ModuleFqn == "frmFoo_Old"));
        Assert.Empty(result.Symbols.Where(s => s.Name == "Hello_Outdated_Should_Not_Be_Parsed"));

        // The live module's procedure does appear.
        Assert.Single(result.Symbols.Where(s => s.Fqn == "frmFoo.Hello" && s.Kind == "sub"));
    }

    [Fact]
    public void Build_PropertyTriplet_PersistsAccessorsUnderDistinctFqns()
    {
        // Round-2 verification flagged that Property Get/Let/Set with the
        // same VBA name were collapsing onto the same Symbols.fqn primary
        // key. The fix encodes accessor kind into the FQN with `#get`/
        // `#let`/`#set`. This test pins the new shape and proves writes
        // succeed without a primary-key conflict.
        var raw = new[]
        {
            new RawModule("clsBag", "ClassModule", "/synthetic/clsBag.cls", "ascii",
                """
                Public Property Get Item() As Long
                    Item = 1
                End Property

                Public Property Let Item(ByVal v As Long)
                    Debug.Print v
                End Property

                Public Property Set Item(ByVal v As Object)
                    Debug.Print v
                End Property
                """),
        };

        var result = new SymbolDbBuilder().Build(raw);

        var fqns = result.Symbols
            .Where(s => s.Name == "Item")
            .Select(s => (s.Fqn, s.Kind))
            .OrderBy(t => t.Fqn, System.StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            new[]
            {
                ("clsBag.Item#get", "property_get"),
                ("clsBag.Item#let", "property_let"),
                ("clsBag.Item#set", "property_set"),
            },
            fqns);

        // Round-trip through SQLite to prove the unique-FQN PK constraint
        // is satisfied (the collision used to throw on INSERT).
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-tests-{System.Guid.NewGuid():N}.sqlite");
        try
        {
            using var writer = new SymbolDbWriter(tempPath);
            writer.Write(result.Modules, result.Symbols, result.Parameters);
            Assert.Equal(3L, ScalarLong(writer.Connection,
                "SELECT COUNT(*) FROM Symbols WHERE name = 'Item';"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Statistics_ModuleVarCount_ExcludesProcedureLocals()
    {
        // Round-2 verification flagged that BuildStatistics.ModuleVarCount
        // was overcounting because every kind='module_var' row was added
        // regardless of declared_in_fqn. Pin the corrected behaviour: only
        // module-level rows (declared_in_fqn IS NULL) count.
        var raw = new[]
        {
            new RawModule("modX", "Module", "/synthetic/modX.bas", "ascii",
                """
                Private m_count As Long          ' module-level: should count
                Public Sub Foo()
                    Dim local1 As Long           ' procedure-local: should not
                    Dim local2 As String         ' procedure-local: should not
                End Sub
                """),
        };

        var result = new SymbolDbBuilder().Build(raw);

        Assert.Equal(1, result.Statistics.ModuleVarCount);
        // Sanity: the locals are still in Symbols, just not in the
        // module-variable headline statistic.
        Assert.Equal(2, result.Symbols.Count(s =>
            s.Kind == "module_var" && s.DeclaredInFqn == "modX.Foo"));
    }

    [SkippableFact]
    public void SignatureFormatter_BuildContextArgs_ProducesExpectedSignatureText()
    {
        // basListHelpers.BuildContextArgs(ParamArray pairs() As Variant) As String
        // is the ParamArray probe: pin the exact signature_string the formatter
        // produces so future refactors cannot silently shift its shape.
        var rawModules = new[] { FixtureLoader.LoadBasListHelpers() };
        var result = new SymbolDbBuilder().Build(rawModules);

        var fn = Assert.Single(result.Symbols.Where(s => s.Fqn == "basListHelpers.BuildContextArgs"));
        Assert.Equal(
            "Function(pairs:Variant:ByRef:ParamArray:Array):String",
            fn.SignatureString);
    }

    [Fact]
    public void Md5Deduper_TwoIdenticalProcedures_TagSiblingPointAtAlphabeticallyFirstFqn()
    {
        // Synthetic fixture: two modules with byte-identical procedure
        // bodies. The dedup pass must pick the alphabetically-earlier FQN
        // as the canonical and tag the other as a sibling.
        var raw = new[]
        {
            new RawModule("aaa", "Module", "/synthetic/aaa.bas", "ascii",
                """
                Public Sub Twin()
                    Debug.Print "same"
                End Sub
                """),
            new RawModule("bbb", "Module", "/synthetic/bbb.bas", "ascii",
                """
                Public Sub Twin()
                    Debug.Print "same"
                End Sub
                """),
        };

        var result = new SymbolDbBuilder().Build(raw);

        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-tests-{System.Guid.NewGuid():N}.sqlite");
        try
        {
            using var writer = new SymbolDbWriter(tempPath);
            writer.Write(result.Modules, result.Symbols, result.Parameters);
            var report = new Md5Deduper().Run(writer.Connection);

            Assert.Equal(1, report.ClustersFound);
            Assert.Equal(1, report.SiblingsTagged);

            using var cmd = writer.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT fqn, equivalence_canonical_fqn FROM Symbols
                WHERE name = 'Twin' ORDER BY fqn;
                """;
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("aaa.Twin", reader.GetString(0));
            Assert.True(reader.IsDBNull(1));   // aaa.Twin is the canonical
            Assert.True(reader.Read());
            Assert.Equal("bbb.Twin", reader.GetString(0));
            Assert.Equal("aaa.Twin", reader.GetString(1));   // points to canonical
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return (long)cmd.ExecuteScalar()!;
    }
}
