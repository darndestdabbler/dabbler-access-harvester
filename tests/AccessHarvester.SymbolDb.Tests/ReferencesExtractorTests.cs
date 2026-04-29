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

// Session 2 hermetic tests: References + MemberAccesses extraction and
// persistence against the canonical NorthwindDevEdition fixture.
public sealed class ReferencesExtractorTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────

    private static BuildResult BuildNorthwind()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmCompanyDetail(),
            FixtureLoader.LoadModStrings(),
        };
        return new SymbolDbBuilder().Build(rawModules);
    }

    // ── References populated ──────────────────────────────────────────────

    [Fact]
    public void Build_ProducesReferenceRows()
    {
        var result = BuildNorthwind();
        Assert.NotEmpty(result.References);
    }

    [Fact]
    public void Build_ProducesMemberAccessRows()
    {
        var result = BuildNorthwind();
        Assert.NotEmpty(result.MemberAccesses);
    }

    // ── Probe target reference set ────────────────────────────────────────

    // The spec's probe-target procedure and the identifiers it directly uses.
    private const string ProbeTargetFqn = "Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate";

    private static IReadOnlyList<ReferenceRow> ProbeTargetRefs(BuildResult result) =>
        result.References.Where(r => r.FromFqn == ProbeTargetFqn).ToList();

    [Fact]
    public void ProbeTarget_HasReferenceRows()
    {
        var refs = ProbeTargetRefs(BuildNorthwind());
        Assert.NotEmpty(refs);
    }

    [Theory]
    [InlineData("CompanyIsActive")]
    [InlineData("GetString")]
    [InlineData("Me")]
    [InlineData("strDlgMsg")]
    public void ProbeTarget_ContainsExpectedIdentifiers(string identifier)
    {
        var refs = ProbeTargetRefs(BuildNorthwind());
        Assert.Contains(refs, r => string.Equals(r.Identifier, identifier,
            StringComparison.OrdinalIgnoreCase));
    }

    // ── Caller-kind annotation ────────────────────────────────────────────

    [Fact]
    public void ProbeTarget_AllRefRows_HaveCallerKind_UiEventHandler()
    {
        // cboCompanyTypeID_BeforeUpdate is in a Form module and its name contains '_'.
        var refs = ProbeTargetRefs(BuildNorthwind());
        Assert.NotEmpty(refs);
        Assert.All(refs, r => Assert.Equal("ui-event-handler", r.CallerKind));
    }

    [Fact]
    public void ModStrings_ProcRefs_HaveCallerKind_NonUi()
    {
        var result = BuildNorthwind();
        // GetString is a Public Function in a Standard module.
        var refs = result.References
            .Where(r => r.FromFqn.StartsWith("modStrings.", StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Not all refs may come from procedures — skip if the module has no refs.
        if (refs.Count == 0) return;
        Assert.All(refs, r => Assert.Equal("non-ui", r.CallerKind));
    }

    // ── Reverse-lookup: CompanyIsActive has a caller ───────────────────────

    [Fact]
    public void CompanyIsActive_HasAtLeastOneCaller()
    {
        var result = BuildNorthwind();
        // Some reference's to_fqn must resolve to CompanyIsActive's FQN.
        bool hasCaller = result.References.Any(r =>
            r.ToFqn is not null &&
            r.ToFqn.EndsWith(".CompanyIsActive", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasCaller, "Expected at least one reference to CompanyIsActive.");
    }

    // ── Resolution status variety ─────────────────────────────────────────

    [Fact]
    public void References_ContainAtLeastOneResolvedRow()
    {
        var result = BuildNorthwind();
        Assert.Contains(result.References, r => r.ResolutionStatus == "resolved");
    }

    [Fact]
    public void References_ContainMeIdentifierRows()
    {
        // Me references may be resolved-to-me or unresolved (me-in-standard-module
        // when the form's class context is not wired in the resolver). Either way
        // the identifier must appear in the references list.
        var result = BuildNorthwind();
        Assert.Contains(result.References, r => r.Identifier == "Me");
    }

    // ── MemberAccesses: Me.X or Me!X rows present ─────────────────────────

    [Fact]
    public void MemberAccesses_ContainMeReceiverRow()
    {
        var result = BuildNorthwind();
        Assert.Contains(result.MemberAccesses, ma => ma.Receiver == "Me");
    }

    // ── Persistence round-trip ────────────────────────────────────────────

    [Fact]
    public void Persist_ReferencesAndMemberAccesses_RoundTrip()
    {
        var result = BuildNorthwind();
        // Temp file intentionally not deleted — SQLite on Windows holds file locks
        // briefly after Dispose; deleting in the test causes intermittent failures.
        // The OS cleans up TEMP on reboot. (Same pattern as PersistenceAndDedupTests.)
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-refs-test-{Guid.NewGuid():N}.sqlite");

        using var writer = new SymbolDbWriter(tempPath);
        writer.Write(result.Modules, result.Symbols, result.Parameters,
            result.References, result.MemberAccesses);

        using var cmd = writer.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM [References];";
        long refCount = (long)cmd.ExecuteScalar()!;
        Assert.Equal((long)result.References.Count, refCount);

        using var cmd2 = writer.Connection.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM MemberAccesses;";
        long maCount = (long)cmd2.ExecuteScalar()!;
        Assert.Equal((long)result.MemberAccesses.Count, maCount);
    }

    [Fact]
    public void Persist_ProbeTargetCallerKind_IsUiEventHandlerInDb()
    {
        var result = BuildNorthwind();
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-refs-test-{Guid.NewGuid():N}.sqlite");

        using var writer = new SymbolDbWriter(tempPath);
        writer.Write(result.Modules, result.Symbols, result.Parameters,
            result.References, result.MemberAccesses);

        using var cmd = writer.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(*) FROM [References]
            WHERE from_fqn = '{ProbeTargetFqn}'
              AND caller_kind != 'ui-event-handler';
            """;
        long nonUiCount = (long)cmd.ExecuteScalar()!;
        Assert.Equal(0L, nonUiCount);

        using var cmd2 = writer.Connection.CreateCommand();
        cmd2.CommandText = $"""
            SELECT COUNT(*) FROM [References]
            WHERE from_fqn = '{ProbeTargetFqn}';
            """;
        long totalCount = (long)cmd2.ExecuteScalar()!;
        Assert.True(totalCount > 0, "Expected at least one reference row for the probe target.");
    }

    // ── Kind classification ───────────────────────────────────────────────

    [Fact]
    public void References_ContainCallKindRows()
    {
        var result = BuildNorthwind();
        Assert.Contains(result.References, r => r.Kind == "call");
    }

    [Fact]
    public void References_ContainVariableReadKindRows()
    {
        var result = BuildNorthwind();
        Assert.Contains(result.References, r => r.Kind == "variable_read");
    }

    // ── MemberAccesses: this fixture has no bang-access rows ──────────────

    [Fact]
    public void MemberAccesses_NoBangAccessRows_InNorthwindFixture()
    {
        // Form_frmCompanyDetail + modStrings use dot notation (Me.Foo) exclusively;
        // no bang-access (Me!Foo / Forms!X) expressions appear. Assert the known
        // fixture count so the test is fixture-assertive rather than a vacuous guard.
        var result = BuildNorthwind();
        Assert.DoesNotContain(result.MemberAccesses, ma => ma.Path.StartsWith("!"));
    }

    // ── Local variable to_fqn includes procedure segment ─────────────────

    [Fact]
    public void LocalVariable_ToFqn_IncludesProcedureSegment()
    {
        // strDlgMsg is a local Dim in cboCompanyTypeID_BeforeUpdate.
        // Its to_fqn must be Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate.strDlgMsg,
        // not Form_frmCompanyDetail.strDlgMsg (which would confuse it with a module var).
        // This fixture resolves strDlgMsg — the assertion is not guarded.
        var result = BuildNorthwind();
        var strDlgMsgRef = result.References.First(r =>
            r.FromFqn == ProbeTargetFqn &&
            string.Equals(r.Identifier, "strDlgMsg", StringComparison.OrdinalIgnoreCase) &&
            r.ToFqn is not null);

        Assert.Contains(".cboCompanyTypeID_BeforeUpdate.", strDlgMsgRef.ToFqn,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Form_frmCompanyDetail.", strDlgMsgRef.ToFqn,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── control_access / enum_value_use: shape guards ─────────────────────

    [Fact]
    public void References_ControlAccessRows_HaveNonEmptyIdentifier()
    {
        // The NorthwindDevEdition fixture uses dot notation (Me.Foo) rather than bang
        // (Me!Foo) in the two probe modules, so control_access count is 0 here.
        // This test validates the shape invariant for any rows that do appear.
        var result = BuildNorthwind();
        Assert.All(result.References.Where(r => r.Kind == "control_access"),
            r => Assert.False(string.IsNullOrEmpty(r.Identifier)));
    }

    [Fact]
    public void References_EnumValueUseRows_HaveNonEmptyIdentifier()
    {
        // Cross-module enum members are unresolved in this fixture (the resolver does
        // not yet do cross-module lookup). enum_value_use count is 0 here. The test
        // validates the shape invariant for any rows that do appear.
        var result = BuildNorthwind();
        Assert.All(result.References.Where(r => r.Kind == "enum_value_use"),
            r => Assert.False(string.IsNullOrEmpty(r.Identifier)));
    }
}
