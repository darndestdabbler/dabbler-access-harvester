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

// Regression tests for the cross-module reference-resolution gap surfaced
// by the vba-symbol-db Session 7 comprehension probe and addressed by
// Session 1 of vba-symbol-db-path-b-probe. Bare-name uses in
// Form_frmCompanyDetail of Public symbols declared in modStrings
// (procedures and Public-enum members) must bind across modules.
public sealed class CrossModuleResolutionTests
{
    private const string ProbeTargetFqn = "Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate";

    private static BuildResult BuildNorthwind()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmCompanyDetail(),
            FixtureLoader.LoadModStrings(),
        };
        return new SymbolDbBuilder().Build(rawModules);
    }

    private static IReadOnlyList<ReferenceRow> ProbeTargetRefs(BuildResult result) =>
        result.References.Where(r => r.FromFqn == ProbeTargetFqn).ToList();

    private static ReferenceRow Single(IReadOnlyList<ReferenceRow> refs, string identifier)
    {
        var matches = refs.Where(r => string.Equals(r.Identifier, identifier,
            StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(matches.Count > 0,
            $"No reference row found for identifier '{identifier}' in probe target.");
        return matches[0];
    }

    [Fact]
    public void GetString_BindsToModStringsGetString()
    {
        var refs = ProbeTargetRefs(BuildNorthwind());
        var row = Single(refs, "GetString");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("modStrings.GetString", row.ToFqn);
    }

    [Fact]
    public void StringFormat_BindsToModStringsStringFormat()
    {
        var refs = ProbeTargetRefs(BuildNorthwind());
        var row = Single(refs, "StringFormat");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("modStrings.StringFormat", row.ToFqn);
    }

    [Fact]
    public void EnumMember_sCompanyDelete_BindsAcrossModules()
    {
        // Bare reference to a Public-enum member declared in modStrings.
        // VBA spills Public enum members into the module's Public namespace,
        // so use sites can name them without enum qualification.
        var refs = ProbeTargetRefs(BuildNorthwind());
        var row = Single(refs, "sCompanyDelete_CompanyTypeChange");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal(
            "modStrings.enumStrings.sCompanyDelete_CompanyTypeChange",
            row.ToFqn);
    }

    [Fact]
    public void WithinModule_CompanyIsActive_StillBinds()
    {
        // Same-module reference must continue to bind after the cross-module
        // fix; this is the "no regression on the in-scope path" check.
        var refs = ProbeTargetRefs(BuildNorthwind());
        var row = Single(refs, "CompanyIsActive");
        Assert.Equal("resolved", row.ResolutionStatus);
        Assert.Equal("Form_frmCompanyDetail.CompanyIsActive", row.ToFqn);
    }

    [Fact]
    public void RebuiltSymbolDb_PersistsResolvedCrossModuleReferences()
    {
        // End-to-end persistence check that mirrors the spec's Step 5
        // ("Re-run the symbol DB build [...] confirm with a one-symbol
        // query that GetString-call sites now bind"), but writes to a
        // temp DB so the committed tools/symbol-db/symbol-db.sqlite —
        // which carries vba-symbol-db Sessions 4-7 interpretation
        // columns — is not overwritten. The committed DB's structural
        // rows can be regenerated explicitly when the human is ready.
        var result = BuildNorthwind();
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

            var rows = QueryProbeTargetReferences(tempPath, "GetString",
                "StringFormat", "sCompanyDelete_CompanyTypeChange");

            Assert.Equal("resolved", rows["GetString"].Status);
            Assert.Equal("modStrings.GetString", rows["GetString"].ToFqn);

            Assert.Equal("resolved", rows["StringFormat"].Status);
            Assert.Equal("modStrings.StringFormat", rows["StringFormat"].ToFqn);

            Assert.Equal("resolved", rows["sCompanyDelete_CompanyTypeChange"].Status);
            Assert.Equal(
                "modStrings.enumStrings.sCompanyDelete_CompanyTypeChange",
                rows["sCompanyDelete_CompanyTypeChange"].ToFqn);
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
    public void OutOfFixtureScope_IsFormOpen_RemainsUnresolved()
    {
        // IsFormOpen lives in a module not part of this two-module fixture.
        // It must continue to resolve as 'unresolved' with reason
        // 'not-in-scope' — the fix must not leak out-of-scope identifiers
        // into spurious bindings.
        var refs = ProbeTargetRefs(BuildNorthwind());
        var row = Single(refs, "IsFormOpen");
        Assert.Equal("unresolved", row.ResolutionStatus);
        Assert.Equal("not-in-scope", row.ResolutionReason);
        Assert.Null(row.ToFqn);
    }
}
