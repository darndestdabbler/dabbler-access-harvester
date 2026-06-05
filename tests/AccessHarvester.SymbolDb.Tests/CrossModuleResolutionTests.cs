using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.Models;
using AccessHarvester.SymbolDb.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Regression tests for cross-module reference resolution: bare-name uses in a
// Form module of Public symbols declared in a standard module (procedures and
// Public-enum members) must bind across modules, while same-module references
// keep binding and out-of-scope identifiers stay unresolved.
//
// Set 005: re-based off the proprietary NorthwindDevEdition fixture. HHC's
// committed forms qualify their cross-module calls (basModule.Member), which the
// resolver treats as member access on an unresolved module name — so they do not
// exercise the *bare-name* cross-module binding this regression guards. The
// bare-name semantic is reproduced here with a hermetic two-module synthetic
// fixture (a committed, in-source fixture per the re-base guidance), preserving
// the exact resolution claims the NorthwindDevEdition probe asserted.
public sealed class CrossModuleResolutionTests
{
    private const string ProbeTargetFqn = "Form_frmConsumer.btnGo_BeforeUpdate";

    private static readonly RawModule StandardModule = new(
        "modHelpers", "Module", "/synthetic/modHelpers.bas", "ascii",
        """
        Option Compare Database
        Public Enum ColorEnum
            clrRed = 1
            clrGreen = 2
        End Enum
        Public Function Greet(ByVal personName As String) As String
            Greet = "Hi " & personName
        End Function
        Public Function Farewell() As String
            Farewell = "Bye"
        End Function
        """);

    private static readonly RawModule FormModule = new(
        "Form_frmConsumer", "Form", "/synthetic/frmConsumer.cls", "utf-16-le",
        """
        Option Compare Database
        Private Sub btnGo_BeforeUpdate(Cancel As Integer)
            Dim msg As String
            msg = Greet("x")
            msg = Farewell()
            If clrRed = 1 Then
                LocalHelper
            End If
            UnknownThing
        End Sub
        Private Function LocalHelper() As Boolean
            LocalHelper = True
        End Function
        """);

    private static BuildResult Build() =>
        new SymbolDbBuilder().Build(new[] { FormModule, StandardModule });

    private static IReadOnlyList<ReferenceRow> ProbeTargetRefs(BuildResult result) =>
        result.References.Where(r => r.FromFqn == ProbeTargetFqn).ToList();

    // Returns the first reference row for an identifier (existence + first match);
    // the per-identifier resolution claim is what each test asserts, not row uniqueness.
    private static ReferenceRow FirstRef(IReadOnlyList<ReferenceRow> refs, string identifier)
    {
        var matches = refs.Where(r => string.Equals(r.Identifier, identifier,
            StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(matches.Count > 0,
            $"No reference row found for identifier '{identifier}' in probe target.");
        return matches[0];
    }

    [Fact]
    public void Greet_BindsToModHelpersGreet()
    {
        var row = FirstRef(ProbeTargetRefs(Build()), "Greet");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("modHelpers.Greet", row.ToFqn);
    }

    [Fact]
    public void Farewell_BindsToModHelpersFarewell()
    {
        var row = FirstRef(ProbeTargetRefs(Build()), "Farewell");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("modHelpers.Farewell", row.ToFqn);
    }

    [Fact]
    public void EnumMember_clrRed_BindsAcrossModules()
    {
        // Bare reference to a Public-enum member declared in modHelpers.
        // VBA spills Public enum members into the module's Public namespace,
        // so use sites can name them without enum qualification.
        var row = FirstRef(ProbeTargetRefs(Build()), "clrRed");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("modHelpers.ColorEnum.clrRed", row.ToFqn);
    }

    [Fact]
    public void WithinModule_LocalHelper_StillBinds()
    {
        // Same-module reference must continue to bind after the cross-module
        // fix; this is the "no regression on the in-scope path" check.
        var row = FirstRef(ProbeTargetRefs(Build()), "LocalHelper");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("Form_frmConsumer.LocalHelper", row.ToFqn);
    }

    [Fact]
    public void RebuiltSymbolDb_PersistsResolvedCrossModuleReferences()
    {
        // End-to-end persistence check: write to a temp DB and confirm the
        // resolved cross-module / enum-member references survive the round-trip.
        var result = Build();
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-cross-module-{Guid.NewGuid():N}.sqlite");
        try
        {
            using (var writer = new SymbolDbWriter(tempPath))
            {
                writer.Write(
                    result.Modules,
                    result.Symbols,
                    result.Parameters,
                    result.References,
                    result.MemberAccesses);
            }

            var rows = QueryProbeTargetReferences(tempPath, "Greet", "Farewell", "clrRed");

            Assert.Equal("resolved", rows["Greet"].Status);
            Assert.Equal("modHelpers.Greet", rows["Greet"].ToFqn);

            Assert.Equal("resolved", rows["Farewell"].Status);
            Assert.Equal("modHelpers.Farewell", rows["Farewell"].ToFqn);

            Assert.Equal("resolved", rows["clrRed"].Status);
            Assert.Equal("modHelpers.ColorEnum.clrRed", rows["clrRed"].ToFqn);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static IDictionary<string, (string Status, string? ToFqn)> QueryProbeTargetReferences(
        string dbPath, params string[] identifiers)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();
        using var con = new SqliteConnection(cs);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT identifier, resolution_status, to_fqn
            FROM "References"
            WHERE from_fqn = $fromFqn AND identifier = $identifier
            LIMIT 1;
            """;
        var fromParam = cmd.CreateParameter();
        fromParam.ParameterName = "$fromFqn";
        fromParam.Value = ProbeTargetFqn;
        cmd.Parameters.Add(fromParam);
        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "$identifier";
        cmd.Parameters.Add(idParam);

        var result = new Dictionary<string, (string, string?)>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in identifiers)
        {
            idParam.Value = id;
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read(),
                $"No persisted [References] row for '{id}' from {ProbeTargetFqn}.");
            string status = reader.GetString(1);
            string? toFqn = reader.IsDBNull(2) ? null : reader.GetString(2);
            result[id] = (status, toFqn);
        }
        return result;
    }

    [Fact]
    public void OutOfScope_UnknownThing_RemainsUnresolved()
    {
        // UnknownThing is declared in no module in this fixture. It must
        // resolve as 'unresolved' with reason 'not-in-scope' — the cross-module
        // fix must not leak out-of-scope identifiers into spurious bindings.
        var row = FirstRef(ProbeTargetRefs(Build()), "UnknownThing");
        Assert.Equal("unresolved", row.ResolutionStatus);
        Assert.Equal("not-in-scope", row.ResolutionReason);
        Assert.Null(row.ToFqn);
    }
}
