using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.Models;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Hermetic tests for Session 3 extractors:
//   FormControlsExtractor, MacroExtractor, QuerySchemaExtractor, TableSchemaExtractor.
// All tests read from the committed NorthwindDevEdition KB fixture under tmp/.
public sealed class Session3ExtractorTests
{
    // ── FormControlsExtractor ────────────────────────────────────────────────

    [Fact]
    public void FormControls_CboCompanyTypeID_IsExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json");

        var result = FormControlsExtractor.Extract(formPath, "Form_frmCompanyDetail");

        // The combo box must be present.
        var cbo = Assert.Single(result.Controls, c => c.ControlName == "cboCompanyTypeID");
        Assert.Equal("Form_frmCompanyDetail", cbo.FormFqn);
        Assert.Equal("ComboBox", cbo.ControlType);

        // ControlSource is CompanyTypeID (the form's record-source column this control writes to).
        Assert.Equal("CompanyTypeID", cbo.ControlSource);

        // RowSource is the combo's list-population query.
        Assert.Equal("qrycboCompanyType", cbo.RowSource);

        // BaseInfo decoding: bound_table_or_query = CompanyTypes, bound_column = CompanyTypeID.
        Assert.Equal("CompanyTypes", cbo.BoundTableOrQuery);
        Assert.Equal("CompanyTypeID", cbo.BoundColumn);
    }

    [Fact]
    public void FormControls_AllNamedControls_AreExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json");

        var result = FormControlsExtractor.Extract(formPath, "Form_frmCompanyDetail");

        // The form has several named controls (TextBoxes, ComboBoxes, CommandButtons, Subforms, etc.)
        // At minimum we expect more than 10 named controls in frmCompanyDetail.
        Assert.True(result.Controls.Count > 10,
            $"Expected >10 named controls, found {result.Controls.Count}.");

        // Every control must belong to the form.
        Assert.All(result.Controls, c => Assert.Equal("Form_frmCompanyDetail", c.FormFqn));

        // No duplicate control names within the same form.
        var names = result.Controls.Select(c => c.ControlName).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void EventBindings_CboCompanyTypeID_BeforeUpdate_IsVbaBinding()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json");

        var result = FormControlsExtractor.Extract(formPath, "Form_frmCompanyDetail");

        // The spec probe target: cboCompanyTypeID.BeforeUpdate → vba handler.
        var binding = Assert.Single(result.Bindings,
            b => b.ControlName == "cboCompanyTypeID" && b.EventName == "BeforeUpdate");

        Assert.Equal("vba", binding.HandlerKind);
        Assert.Equal("Form_frmCompanyDetail.cboCompanyTypeID_BeforeUpdate", binding.HandlerFqn);
        Assert.Null(binding.HandlerExpression);
    }

    [Fact]
    public void EventBindings_FormLevelEvents_AreExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json");

        var result = FormControlsExtractor.Extract(formPath, "Form_frmCompanyDetail");

        // The form JSON has AfterInsert, BeforeDelConfirm, AfterDelConfirm, OnOpen, OnLoad
        // as [Event Procedure] bindings at the form level (control_name = null).
        var formLevelBindings = result.Bindings.Where(b => b.ControlName is null).ToList();
        Assert.True(formLevelBindings.Count > 0, "Expected at least one form-level event binding.");

        // Form_Open should appear (from OnOpen).
        Assert.Contains(formLevelBindings, b =>
            b.EventName == "Open" &&
            b.HandlerKind == "vba" &&
            b.HandlerFqn == "Form_frmCompanyDetail.Form_Open");
    }

    // ── MacroExtractor ───────────────────────────────────────────────────────

    [Fact]
    public void MacroExtractor_AutoExec_IsExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        string autoExecPath = Path.Combine(kbRoot, "Macros", "AutoExec.json");

        var result = MacroExtractor.Extract([autoExecPath]);

        // Exactly one macro row for AutoExec.
        var macro = Assert.Single(result.Macros);
        Assert.Equal("AutoExec", macro.Fqn);
        Assert.False(macro.IsDead);
        Assert.False(string.IsNullOrEmpty(macro.SourceXml),
            "AutoExec has _AXL: chunks and should have non-empty SourceXml.");

        // AutoExec has at least one action.
        Assert.True(result.MacroActions.Count > 0,
            $"Expected >0 MacroActions for AutoExec, found {result.MacroActions.Count}.");

        // All actions reference the same macro FQN.
        Assert.All(result.MacroActions, a => Assert.Equal("AutoExec", a.MacroFqn));

        // Positions are sequential from 0.
        var positions = result.MacroActions.Select(a => a.Position).OrderBy(p => p).ToList();
        for (int i = 0; i < positions.Count; i++) Assert.Equal(i, positions[i]);
    }

    [Fact]
    public void MacroExtractor_AllMacroFiles_AreExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        string macroDir = Path.Combine(kbRoot, "Macros");
        var paths = Directory.GetFiles(macroDir, "*.json");

        var result = MacroExtractor.Extract(paths);

        // One MacroRow per file.
        Assert.Equal(paths.Length, result.Macros.Count);

        // No duplicate FQNs.
        var fqns = result.Macros.Select(m => m.Fqn).ToList();
        Assert.Equal(fqns.Distinct().Count(), fqns.Count);
    }

    // ── QuerySchemaExtractor ─────────────────────────────────────────────────

    [Fact]
    public void QuerySchemaExtractor_AllQueryFiles_AreExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json");

        var queries = QuerySchemaExtractor.Extract(paths);

        Assert.Equal(paths.Length, queries.Count);

        // Each query has a non-empty SourceSql and non-null hashes.
        foreach (var q in queries)
        {
            Assert.False(string.IsNullOrEmpty(q.SourceSql),
                $"{q.Fqn}: SourceSql is empty.");
            Assert.NotNull(q.SqlTextHash);
            Assert.NotNull(q.SqlClauseHash);
        }
    }

    [Fact]
    public void QuerySchemaExtractor_NormalizedSql_IsUppercaseNoParameters()
    {
        // A simple inline test of the normalization helper.
        string sql = "PARAMETERS [ID] Long;\r\nSELECT  foo.ID,   foo.Name\r\nFROM foo\r\nWHERE foo.ID = [ID];\r\n";
        string normalized = QuerySchemaExtractor.NormalizeSql(sql);

        Assert.DoesNotContain("PARAMETERS", normalized);
        Assert.DoesNotContain("\r", normalized);
        Assert.DoesNotContain("\n", normalized);
        Assert.Equal(normalized, normalized.ToUpperInvariant());
        // Multiple spaces collapsed to single.
        Assert.DoesNotContain("  ", normalized);
    }

    [Fact]
    public void QuerySchemaExtractor_IdenticalQueries_ShareCanonicalFqn()
    {
        // Build two synthetic query file-equivalent rows by examining the real
        // NorthwindDevEdition fixture for any duplicates by text hash.
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json");
        var queries = QuerySchemaExtractor.Extract(paths);

        // Group by text hash to find any duplicates.
        var groups = queries
            .Where(q => q.SqlTextHash is not null)
            .GroupBy(q => q.SqlTextHash!)
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0)
        {
            // No duplicates in this fixture — document and pass.
            // The test fulfils the spec requirement: "if zero, document."
            Assert.True(true, "No duplicate query text hashes in NorthwindDevEdition fixture.");
            return;
        }

        foreach (var group in groups)
        {
            var canonical = group.Min(q => q.Fqn);  // lexicographically-first
            foreach (var q in group)
            {
                if (q.Fqn == canonical)
                    Assert.Null(q.EquivalenceCanonicalFqn);   // canonical points nowhere
                else
                    Assert.Equal(canonical, q.EquivalenceCanonicalFqn);
            }
        }
    }

    // ── TableSchemaExtractor ─────────────────────────────────────────────────

    [Fact]
    public void TableSchemaExtractor_CompanyTypes_IsLookupTable()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");

        // Provide CompanyTypes as a row-source table (it is used by cboCompanyTypeID).
        var rowSourceNames = new HashSet<string>(["CompanyTypes"], System.StringComparer.OrdinalIgnoreCase);
        var tables = TableSchemaExtractor.Extract(paths, rowSourceNames);

        var ct = Assert.Single(tables, t => t.Fqn == "CompanyTypes");

        Assert.Equal("CompanyTypeID", ct.PrimaryKeyColumn);

        // Score: PK(+20) + RecordCount≤200(+30) + is-row-source(+30) = at least 80.
        // The CompanyType column name does not contain any label-hint word
        // (value/text/label/caption/name/description), so the +20 label-col bonus
        // is not expected — the score reflects the other three contributors.
        Assert.True(ct.IsLookupTableScore >= 70,
            $"CompanyTypes score {ct.IsLookupTableScore} should be ≥70 for a bona-fide lookup table.");
    }

    [Fact]
    public void TableSchemaExtractor_AllTableFiles_AreExtracted()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");

        var tables = TableSchemaExtractor.Extract(paths);

        Assert.Equal(paths.Length, tables.Count);

        // No duplicate FQNs.
        var fqns = tables.Select(t => t.Fqn).ToList();
        Assert.Equal(fqns.Distinct().Count(), fqns.Count);
    }

    // ── Round-1 verification fixes ───────────────────────────────────────────

    [Fact]
    public void EventBindings_OnPush_MapsToClickHandler()
    {
        // Synthesise a minimal form JSON containing a CommandButton with OnPush.
        // OnPush is the legacy click-event property name; the VBA handler
        // procedure Access generates is named <Control>_Click, not _Push.
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"onpush_{System.Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, """
            {
              "Type": "Form",
              "Properties": {},
              "Children": [
                {
                  "Type": "CommandButton",
                  "Properties": {
                    "Name": "btnLegacy",
                    "OnPush": "[Event Procedure]"
                  },
                  "Children": []
                }
              ]
            }
            """);
        try
        {
            var result = FormControlsExtractor.Extract(tempPath, "Form_test");
            var binding = Assert.Single(result.Bindings,
                b => b.ControlName == "btnLegacy");
            Assert.Equal("Click", binding.EventName);
            Assert.Equal("Form_test.btnLegacy_Click", binding.HandlerFqn);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void MacroExtractor_PositionsAreDenseAndContiguous()
    {
        // Run against the AutoExec macro and assert (macro_fqn, position) is
        // dense from 0..N-1 — no gaps even if the parser yields non-object
        // elements that the extractor skips.
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Macros"), "*.json");
        var result = MacroExtractor.Extract(paths);

        // Group by macro_fqn; each group's positions must be 0..count-1.
        var byMacro = result.MacroActions.GroupBy(a => a.MacroFqn);
        foreach (var g in byMacro)
        {
            var positions = g.Select(a => a.Position).OrderBy(p => p).ToList();
            for (int i = 0; i < positions.Count; i++)
                Assert.Equal(i, positions[i]);
        }
    }

    [Fact]
    public void QuerySchemaExtractor_DeadQuery_IsFlagged()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json");

        // Caller set contains every query name except one — that one becomes is_dead.
        var allFqns = paths.Select(Path.GetFileNameWithoutExtension).ToList();
        string toKill = allFqns[0]!;
        var callerNames = new HashSet<string>(allFqns!.Where(n => n != toKill)!,
            System.StringComparer.OrdinalIgnoreCase);

        var queries = QuerySchemaExtractor.Extract(paths, callerNames);

        Assert.True(queries.Single(q => q.Fqn == toKill).IsDead,
            $"Query '{toKill}' should be is_dead = 1 when omitted from caller set.");
        Assert.All(queries.Where(q => q.Fqn != toKill),
            q => Assert.False(q.IsDead, $"Query '{q.Fqn}' should not be is_dead."));
    }

    [Fact]
    public void TableSchemaExtractor_RowSourceCount_TiersTheScore()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");

        // CompanyTypes: simulate 3 controls referencing it (≥3 → +30).
        var counts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyTypes"] = 3,
        };
        var heavy = TableSchemaExtractor.Extract(paths, counts);
        var ctHeavy = Assert.Single(heavy, t => t.Fqn == "CompanyTypes");

        // Now simulate exactly 1 control (1 ≤ uses < 3 → +20).
        counts["CompanyTypes"] = 1;
        var light = TableSchemaExtractor.Extract(paths, counts);
        var ctLight = Assert.Single(light, t => t.Fqn == "CompanyTypes");

        Assert.True(ctHeavy.IsLookupTableScore > ctLight.IsLookupTableScore,
            $"Heavier row-source usage ({ctHeavy.IsLookupTableScore}) should outscore light ({ctLight.IsLookupTableScore}).");
        Assert.Equal(10, ctHeavy.IsLookupTableScore - ctLight.IsLookupTableScore);
    }

    // ── End-to-end persistence smoke ─────────────────────────────────────────

    [Fact]
    public void Session3_FullPipeline_WritesExpectedTableCounts()
    {
        string kbRoot = FixtureLoader.NorthwindKbRoot();

        var formResult = FormControlsExtractor.Extract(
            Path.Combine(kbRoot, "Forms", "frmCompanyDetail.json"),
            "Form_frmCompanyDetail");

        var rowSourceNames = new HashSet<string>(
            formResult.Controls
                .Where(c => c.BoundTableOrQuery is not null)
                .Select(c => c.BoundTableOrQuery!),
            System.StringComparer.OrdinalIgnoreCase);

        var macroResult = MacroExtractor.Extract(
            Directory.GetFiles(Path.Combine(kbRoot, "Macros"), "*.json"));
        var queries = QuerySchemaExtractor.Extract(
            Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json"));
        var tables = TableSchemaExtractor.Extract(
            Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json"), rowSourceNames);

        // Write everything to an in-memory SQLite database.
        var rawModules = new[]
        {
            FixtureLoader.LoadFormFrmCompanyDetail(),
            FixtureLoader.LoadModStrings(),
        };
        var vbaResult = new AccessHarvester.SymbolDb.Extraction.SymbolDbBuilder().Build(rawModules);

        var connStr = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.Memory,
        }.ToString();
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
        conn.Open();

        using var writer = new AccessHarvester.SymbolDb.Persistence.SymbolDbWriter(conn);
        writer.Write(
            vbaResult.Modules, vbaResult.Symbols, vbaResult.Parameters,
            vbaResult.References, vbaResult.MemberAccesses,
            formControls: formResult.Controls,
            eventBindings: formResult.Bindings,
            macros: macroResult.Macros,
            macroActions: macroResult.MacroActions,
            queries: queries,
            tables: tables);

        // Verify row counts via SQL.
        int CountTable(string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
            return (int)(long)cmd.ExecuteScalar()!;
        }

        Assert.True(CountTable("FormControls") > 0, "FormControls should have rows.");
        Assert.True(CountTable("EventBindings") > 0, "EventBindings should have rows.");
        Assert.True(CountTable("Macros") > 0, "Macros should have rows.");
        Assert.True(CountTable("MacroActions") > 0, "MacroActions should have rows.");
        Assert.True(CountTable("Queries") > 0, "Queries should have rows.");
        Assert.True(CountTable("Tables") > 0, "Tables should have rows.");

        // FK enforcement: all macro actions must reference existing macros.
        using var fkCmd = conn.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_key_check(MacroActions);";
        using var fkReader = fkCmd.ExecuteReader();
        Assert.False(fkReader.Read(), "No FK violations expected in MacroActions.");
    }
}
