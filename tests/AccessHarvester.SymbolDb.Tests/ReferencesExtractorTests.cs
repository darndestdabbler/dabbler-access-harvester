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

// References + MemberAccesses extraction and persistence against the committed
// synthetic HomeHealthCare kb fixture (frmProviderSearch + basListHelpers +
// basUtilities). Set 005: re-based off the proprietary NorthwindDevEdition
// fixture; runs live on every clone.
public sealed class ReferencesExtractorTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────

    private static BuildResult BuildHhc()
    {
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmProviderSearch(),
            FixtureLoader.LoadBasListHelpers(),
            FixtureLoader.LoadBasUtilities(),
        };
        return new SymbolDbBuilder().Build(rawModules);
    }

    // ── References populated ──────────────────────────────────────────────

    [SkippableFact]
    public void Build_ProducesReferenceRows()
    {
        var result = BuildHhc();
        Assert.NotEmpty(result.References);
    }

    [SkippableFact]
    public void Build_ProducesMemberAccessRows()
    {
        var result = BuildHhc();
        Assert.NotEmpty(result.MemberAccesses);
    }

    // ── Probe target reference set ────────────────────────────────────────

    // A rich form event handler and the identifiers it directly uses.
    private const string ProbeTargetFqn = "Form_frmProviderSearch.btnSearch_Click";

    private static IReadOnlyList<ReferenceRow> ProbeTargetRefs(BuildResult result) =>
        result.References.Where(r => r.FromFqn == ProbeTargetFqn).ToList();

    [SkippableFact]
    public void ProbeTarget_HasReferenceRows()
    {
        var refs = ProbeTargetRefs(BuildHhc());
        Assert.NotEmpty(refs);
    }

    [SkippableTheory]
    [InlineData("PopulateProviderList")]
    [InlineData("StatusEnum")]
    [InlineData("i")]
    [InlineData("where")]
    public void ProbeTarget_ContainsExpectedIdentifiers(string identifier)
    {
        var refs = ProbeTargetRefs(BuildHhc());
        Assert.Contains(refs, r => string.Equals(r.Identifier, identifier,
            StringComparison.OrdinalIgnoreCase));
    }

    // ── Caller-kind annotation ────────────────────────────────────────────

    [SkippableFact]
    public void ProbeTarget_AllRefRows_HaveCallerKind_UiEventHandler()
    {
        // btnSearch_Click is in a Form module and its name contains '_'.
        var refs = ProbeTargetRefs(BuildHhc());
        Assert.NotEmpty(refs);
        Assert.All(refs, r => Assert.Equal("ui-event-handler", r.CallerKind));
    }

    [SkippableFact]
    public void StandardModule_ProcRefs_HaveCallerKind_NonUi()
    {
        var result = BuildHhc();
        // Functions in the standard modules are non-ui callers.
        var refs = result.References
            .Where(r => r.FromFqn.StartsWith("basListHelpers.", StringComparison.OrdinalIgnoreCase)
                     || r.FromFqn.StartsWith("basUtilities.", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(refs);
        Assert.All(refs, r => Assert.Equal("non-ui", r.CallerKind));
    }

    // ── Reverse-lookup: PopulateProviderList has a caller ──────────────────

    [SkippableFact]
    public void PopulateProviderList_HasAtLeastOneCaller()
    {
        var result = BuildHhc();
        bool hasCaller = result.References.Any(r =>
            r.ToFqn is not null &&
            r.ToFqn.EndsWith(".PopulateProviderList", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasCaller, "Expected at least one reference to PopulateProviderList.");
    }

    // ── Resolution status variety ─────────────────────────────────────────

    [SkippableFact]
    public void References_ContainAtLeastOneResolvedRow()
    {
        var result = BuildHhc();
        Assert.Contains(result.References, r => r.ResolutionStatus == "resolved");
    }

    [SkippableFact]
    public void References_ContainMeIdentifierRows()
    {
        // Me references are unresolved (me-in-standard-module when the form's
        // class context is not wired in the resolver). Either way the identifier
        // must appear in the references list.
        var result = BuildHhc();
        Assert.Contains(result.References, r => r.Identifier == "Me");
    }

    // ── MemberAccesses: Me.X rows present ─────────────────────────────────

    [SkippableFact]
    public void MemberAccesses_ContainMeReceiverRow()
    {
        var result = BuildHhc();
        Assert.Contains(result.MemberAccesses, ma => ma.Receiver == "Me");
    }

    // ── Persistence round-trip ────────────────────────────────────────────

    [SkippableFact]
    public void Persist_ReferencesAndMemberAccesses_RoundTrip()
    {
        var result = BuildHhc();
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-refs-test-{Guid.NewGuid():N}.sqlite");
        try
        {
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
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [SkippableFact]
    public void Persist_ProbeTargetCallerKind_IsUiEventHandlerInDb()
    {
        var result = BuildHhc();
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"symbol-db-refs-test-{Guid.NewGuid():N}.sqlite");
        try
        {
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
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ── Kind classification ───────────────────────────────────────────────

    [SkippableFact]
    public void References_ContainCallKindRows()
    {
        var result = BuildHhc();
        Assert.Contains(result.References, r => r.Kind == "call");
    }

    [SkippableFact]
    public void References_ContainVariableReadKindRows()
    {
        var result = BuildHhc();
        Assert.Contains(result.References, r => r.Kind == "variable_read");
    }

    // ── MemberAccesses shape invariant ────────────────────────────────────

    [SkippableFact]
    public void MemberAccesses_AllHaveNonEmptyReceiver()
    {
        // The form uses dot-notation control access (Me.txtFilterName etc.);
        // every member-access row must carry a non-empty receiver.
        var result = BuildHhc();
        Assert.NotEmpty(result.MemberAccesses);
        Assert.All(result.MemberAccesses, ma => Assert.False(string.IsNullOrEmpty(ma.Receiver)));
    }

    // ── Local variable to_fqn includes procedure segment ─────────────────

    [SkippableFact]
    public void LocalVariable_ToFqn_IncludesProcedureSegment()
    {
        // 'where' is a local Dim in btnSearch_Click. Its to_fqn must be
        // Form_frmProviderSearch.btnSearch_Click.where, not
        // Form_frmProviderSearch.where (which would confuse it with a module var).
        var result = BuildHhc();
        var localRef = result.References.First(r =>
            r.FromFqn == ProbeTargetFqn &&
            string.Equals(r.Identifier, "where", StringComparison.OrdinalIgnoreCase) &&
            r.ToFqn is not null);

        Assert.Contains(".btnSearch_Click.", localRef.ToFqn!,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Form_frmProviderSearch.", localRef.ToFqn!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── control_access / enum_value_use: present and well-formed ──────────

    [SkippableFact]
    public void References_ControlAccessRows_ArePresent_AndHaveNonEmptyIdentifier()
    {
        // The form's dot-notation control references (Me.txtFilterName etc.)
        // produce control_access reference rows.
        var result = BuildHhc();
        var controlAccess = result.References.Where(r => r.Kind == "control_access").ToList();
        Assert.NotEmpty(controlAccess);
        Assert.All(controlAccess, r => Assert.False(string.IsNullOrEmpty(r.Identifier)));
    }

    [SkippableFact]
    public void References_EnumValueUseRows_ArePresent_AndResolveToTheEnum()
    {
        // StatusEnum.* uses in the form produce enum_value_use reference rows
        // that resolve to the form's StatusEnum declaration.
        var result = BuildHhc();
        var enumUses = result.References.Where(r => r.Kind == "enum_value_use").ToList();
        Assert.NotEmpty(enumUses);
        Assert.All(enumUses, r => Assert.False(string.IsNullOrEmpty(r.Identifier)));
        Assert.Contains(enumUses, r =>
            r.ResolutionStatus == "resolved" &&
            r.ToFqn == "Form_frmProviderSearch.StatusEnum");
    }
}
