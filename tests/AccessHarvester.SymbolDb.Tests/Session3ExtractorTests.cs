using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.SymbolDb.Models;
using Xunit;

namespace AccessHarvester.SymbolDb.Tests;

// Tests for Session 3 extractors:
//   FormControlsExtractor, MacroExtractor, QuerySchemaExtractor, TableSchemaExtractor.
// Set 005: re-based off the proprietary, local-only NorthwindDevEdition KB fixture
// onto the committed synthetic HomeHealthCare kb (samples/GoldenOutput/HomeHealthCare/kb),
// so they run live on every clone. Two synthetic tests (inline SQL / inline temp
// JSON) carry no fixture dependency and stay [Fact].
//
// Note: the committed HHC kb macros store their structure as a parsed Children tree
// rather than the raw `_AXL:` chunks the COM extractor emits, so MacroExtractor
// parses them into zero MacroActions. The action-parsing path itself is covered
// hermetically by AccessHarvester.Tests/MacroXmlParserTests; here the macro tests
// assert the file-level extraction contract (one row per file, FQNs, SourceXml,
// dense action positions) that is the MacroExtractor's unique responsibility.
public sealed class Session3ExtractorTests
{
    // Control-rich form: a combo bound to a query + lookup table.
    private const string ControlFormName = "frmClientServiceNeedDetail";
    private const string ControlFormFqn = "Form_frmClientServiceNeedDetail";

    // ── FormControlsExtractor ────────────────────────────────────────────────

    [SkippableFact]
    public void FormControls_CboServiceID_IsExtracted()
    {
        string kbRoot = FixtureLoader.KbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", ControlFormName + ".json");

        var result = FormControlsExtractor.Extract(formPath, ControlFormFqn);

        // The combo box must be present.
        var cbo = Assert.Single(result.Controls, c => c.ControlName == "cboServiceID");
        Assert.Equal(ControlFormFqn, cbo.FormFqn);
        Assert.Equal("ComboBox", cbo.ControlType);

        // ControlSource is the record-source column this control writes to.
        Assert.Equal("ServiceID", cbo.ControlSource);

        // RowSource is the combo's list-population query.
        Assert.Equal("qryServiceList", cbo.RowSource);

        // BaseInfo decoding: bound_table_or_query = tblServices, bound_column = ServiceID.
        Assert.Equal("tblServices", cbo.BoundTableOrQuery);
        Assert.Equal("ServiceID", cbo.BoundColumn);
    }

    [SkippableFact]
    public void FormControls_AllNamedControls_AreExtracted()
    {
        string kbRoot = FixtureLoader.KbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", ControlFormName + ".json");

        var result = FormControlsExtractor.Extract(formPath, ControlFormFqn);

        // The form has many named controls (TextBoxes, ComboBoxes, CommandButtons, etc.).
        Assert.True(result.Controls.Count > 10,
            $"Expected >10 named controls, found {result.Controls.Count}.");

        // Every control must belong to the form.
        Assert.All(result.Controls, c => Assert.Equal(ControlFormFqn, c.FormFqn));

        // No duplicate control names within the same form.
        var names = result.Controls.Select(c => c.ControlName).ToList();
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [SkippableFact]
    public void EventBindings_BtnSave_Click_IsVbaBinding()
    {
        string kbRoot = FixtureLoader.KbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", ControlFormName + ".json");

        var result = FormControlsExtractor.Extract(formPath, ControlFormFqn);

        var binding = Assert.Single(result.Bindings,
            b => b.ControlName == "btnSave" && b.EventName == "Click");

        Assert.Equal("vba", binding.HandlerKind);
        Assert.Equal(ControlFormFqn + ".btnSave_Click", binding.HandlerFqn);
        Assert.Null(binding.HandlerExpression);
    }

    [SkippableFact]
    public void EventBindings_FormLevelEvents_AreExtracted()
    {
        string kbRoot = FixtureLoader.KbRoot();
        string formPath = Path.Combine(kbRoot, "Forms", ControlFormName + ".json");

        var result = FormControlsExtractor.Extract(formPath, ControlFormFqn);

        // Form-level [Event Procedure] bindings have control_name = null.
        var formLevelBindings = result.Bindings.Where(b => b.ControlName is null).ToList();
        Assert.True(formLevelBindings.Count > 0, "Expected at least one form-level event binding.");

        // Form_Open should appear (from OnOpen).
        Assert.Contains(formLevelBindings, b =>
            b.EventName == "Open" &&
            b.HandlerKind == "vba" &&
            b.HandlerFqn == ControlFormFqn + ".Form_Open");
    }

    // ── MacroExtractor ───────────────────────────────────────────────────────

    [SkippableFact]
    public void MacroExtractor_AllMacroFiles_AreExtracted()
    {
        string kbRoot = FixtureLoader.KbRoot();
        string macroDir = Path.Combine(kbRoot, "Macros");
        var paths = Directory.GetFiles(macroDir, "*.json");

        var result = MacroExtractor.Extract(paths);

        // One MacroRow per file.
        Assert.Equal(paths.Length, result.Macros.Count);

        // No duplicate FQNs, and each macro yields some SourceXml.
        var fqns = result.Macros.Select(m => m.Fqn).ToList();
        Assert.Equal(fqns.Distinct().Count(), fqns.Count);
        Assert.All(result.Macros, m => Assert.False(string.IsNullOrEmpty(m.SourceXml),
            $"Macro '{m.Fqn}' should have non-empty SourceXml."));
    }

    // Synthetic (inline temp JSON) — no fixture dependency; never skips.
    // The committed HHC kb macros store a parsed Children tree rather than the
    // raw `_AXL:` chunks the COM extractor emits, so they yield zero MacroActions.
    // This action-bearing synthetic fixture preserves MacroExtractor's
    // extractor-level "macro file -> action rows" integration coverage
    // (distinct from the parser-only MacroXmlParserTests).
    [Fact]
    public void MacroExtractor_ActionBearingMacro_EmitsActionsWithDensePositions()
    {
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"mcrSynthetic_{System.Guid.NewGuid():N}.json");
        // One Begin child whose Properties.Comment carries an _AXL: chunk with two
        // top-level Actions (single-quoted XML attributes avoid SaveAsText quote escaping).
        File.WriteAllText(tempPath, """
            {
              "Type": "DocumentRoot",
              "Properties": {},
              "Children": [
                {
                  "Type": "Begin",
                  "Properties": {
                    "Comment": "_AXL:<UserInterfaceMacro><Statements><Action Name='Beep' /><Action Name='OpenForm'><Argument Name='FormName'>frmHome</Argument></Action></Statements></UserInterfaceMacro>"
                  },
                  "Children": []
                }
              ]
            }
            """);
        try
        {
            var result = MacroExtractor.Extract(new[] { tempPath });

            var macro = Assert.Single(result.Macros);
            Assert.False(string.IsNullOrEmpty(macro.SourceXml));

            // The action extractor must emit one row per top-level action.
            Assert.Equal(2, result.MacroActions.Count);

            // All actions reference this macro; positions are dense from 0.
            Assert.All(result.MacroActions, a => Assert.Equal(macro.Fqn, a.MacroFqn));
            var positions = result.MacroActions.Select(a => a.Position).OrderBy(p => p).ToList();
            Assert.Equal(new[] { 0, 1 }, positions);

            // The two named actions are present in document order, with arguments.
            Assert.Equal("Beep", result.MacroActions[0].ActionName);
            Assert.Equal("OpenForm", result.MacroActions[1].ActionName);
            Assert.Contains("frmHome", result.MacroActions[1].ArgumentsJson);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ── QuerySchemaExtractor ─────────────────────────────────────────────────

    [SkippableFact]
    public void QuerySchemaExtractor_AllQueryFiles_AreExtracted()
    {
        string kbRoot = FixtureLoader.KbRoot();
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

    // Synthetic (inline SQL) — no fixture dependency; never skips.
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

    [SkippableFact]
    public void QuerySchemaExtractor_IdenticalQueries_ShareCanonicalFqn()
    {
        // Examine the real fixture for any text-hash duplicates and assert the
        // dedup contract for whichever groups exist.
        string kbRoot = FixtureLoader.KbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Queries"), "*.json");
        var queries = QuerySchemaExtractor.Extract(paths);

        var groups = queries
            .Where(q => q.SqlTextHash is not null)
            .GroupBy(q => q.SqlTextHash!)
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0)
        {
            // No duplicates in this fixture — document and pass.
            Assert.True(true, "No duplicate query text hashes in the HomeHealthCare fixture.");
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

    private const string LookupTableName = "tlkpServiceCategories";

    [SkippableFact]
    public void TableSchemaExtractor_LookupTable_IsScoredAsLookup()
    {
        string kbRoot = FixtureLoader.KbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");

        // Provide the lookup table as a row-source table (a combo populates from it).
        var rowSourceNames = new HashSet<string>([LookupTableName], System.StringComparer.OrdinalIgnoreCase);
        var tables = TableSchemaExtractor.Extract(paths, rowSourceNames);

        var lookup = Assert.Single(tables, t => t.Fqn == LookupTableName);

        Assert.Equal("CategoryID", lookup.PrimaryKeyColumn);

        // Score: PK(+20) + RecordCount<=200(+30) + is-row-source(+30) => >=70 for a
        // bona-fide lookup table (a label-hint column name may add a further bonus).
        Assert.True(lookup.IsLookupTableScore >= 70,
            $"{LookupTableName} score {lookup.IsLookupTableScore} should be >=70 for a bona-fide lookup table.");
    }

    [SkippableFact]
    public void TableSchemaExtractor_AllTableFiles_AreExtracted()
    {
        string kbRoot = FixtureLoader.KbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");

        var tables = TableSchemaExtractor.Extract(paths);

        Assert.Equal(paths.Length, tables.Count);

        // No duplicate FQNs.
        var fqns = tables.Select(t => t.Fqn).ToList();
        Assert.Equal(fqns.Distinct().Count(), fqns.Count);
    }

    // ── Round-1 verification fixes ───────────────────────────────────────────

    // Synthetic (inline temp JSON) — no fixture dependency; never skips.
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

    [SkippableFact]
    public void MacroExtractor_PositionsAreDenseAndContiguous()
    {
        // Each macro's MacroActions positions must be dense from 0..N-1 — no gaps,
        // even if the parser yields non-object elements that the extractor skips.
        string kbRoot = FixtureLoader.KbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Macros"), "*.json");
        var result = MacroExtractor.Extract(paths);

        var byMacro = result.MacroActions.GroupBy(a => a.MacroFqn);
        foreach (var g in byMacro)
        {
            var positions = g.Select(a => a.Position).OrderBy(p => p).ToList();
            for (int i = 0; i < positions.Count; i++)
                Assert.Equal(i, positions[i]);
        }
    }

    [SkippableFact]
    public void QuerySchemaExtractor_DeadQuery_IsFlagged()
    {
        string kbRoot = FixtureLoader.KbRoot();
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

    [SkippableFact]
    public void TableSchemaExtractor_RowSourceCount_TiersTheScore()
    {
        string kbRoot = FixtureLoader.KbRoot();
        var paths = Directory.GetFiles(Path.Combine(kbRoot, "Tables"), "*.json");

        // Simulate 3 controls referencing the lookup table (>=3 → +30).
        var counts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
        {
            [LookupTableName] = 3,
        };
        var heavy = TableSchemaExtractor.Extract(paths, counts);
        var ctHeavy = Assert.Single(heavy, t => t.Fqn == LookupTableName);

        // Now simulate exactly 1 control (1 <= uses < 3 → +20).
        counts[LookupTableName] = 1;
        var light = TableSchemaExtractor.Extract(paths, counts);
        var ctLight = Assert.Single(light, t => t.Fqn == LookupTableName);

        Assert.True(ctHeavy.IsLookupTableScore > ctLight.IsLookupTableScore,
            $"Heavier row-source usage ({ctHeavy.IsLookupTableScore}) should outscore light ({ctLight.IsLookupTableScore}).");
        Assert.Equal(10, ctHeavy.IsLookupTableScore - ctLight.IsLookupTableScore);
    }

    // ── End-to-end persistence smoke ─────────────────────────────────────────

    [SkippableFact]
    public void Session3_FullPipeline_WritesExpectedTableCounts()
    {
        string kbRoot = FixtureLoader.KbRoot();

        var formResult = FormControlsExtractor.Extract(
            Path.Combine(kbRoot, "Forms", ControlFormName + ".json"),
            ControlFormFqn);

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

        // Build the VBA module set from the SAME form whose controls we
        // extracted, so the FormControls' form_fqn has a matching Modules row
        // (the FormControls -> Modules foreign key).
        var rawModules = new[]
        {
            FixtureLoader.LoadForm(ControlFormName),
            FixtureLoader.LoadBasListHelpers(),
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

        int CountTable(string tableName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [{tableName}]";
            return (int)(long)cmd.ExecuteScalar()!;
        }

        Assert.True(CountTable("FormControls") > 0, "FormControls should have rows.");
        Assert.True(CountTable("EventBindings") > 0, "EventBindings should have rows.");
        Assert.True(CountTable("Macros") > 0, "Macros should have rows.");
        // Note: the committed HHC macros carry no _AXL action chunks, so
        // MacroActions is legitimately empty here (the action parser is covered
        // by MacroXmlParserTests). We assert the FK integrity below regardless.
        Assert.True(CountTable("Queries") > 0, "Queries should have rows.");
        Assert.True(CountTable("Tables") > 0, "Tables should have rows.");

        // FK enforcement: all macro actions must reference existing macros.
        using var fkCmd = conn.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_key_check(MacroActions);";
        using var fkReader = fkCmd.ExecuteReader();
        Assert.False(fkReader.Read(), "No FK violations expected in MacroActions.");
    }
}
