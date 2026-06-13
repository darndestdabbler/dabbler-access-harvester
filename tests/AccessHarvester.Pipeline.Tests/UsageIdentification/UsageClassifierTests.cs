using System;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

/// <summary>
/// Unit tests for UsageClassifier classification rules.
/// Each test builds a minimal IrDatabase + SignalIndex to exercise
/// a specific rule from the strategy doc §4.
/// </summary>
public class UsageClassifierTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IrDatabase EmptyDb(string name = "TestDb") => new()
    {
        Name = name,
        Forms = [], Reports = [], Queries = [], Tables = [],
        Relationships = [], Macros = [], Modules = [],
    };

    private static IrFormOrReport Form(string name, string? recordSource = null,
        string? vbaCode = null, bool beforeUpdate = false, bool afterUpdate = false,
        bool onDelete = false, IrMacroAction[]? macroActions = null) => new()
    {
        Name = name,
        ObjectType = "Form",
        RecordSource = recordSource,
        VbaCode = vbaCode,
        Controls = [],
        HasBeforeUpdate = beforeUpdate,
        HasAfterUpdate = afterUpdate,
        HasOnDelete = onDelete,
        MacroActions = macroActions ?? [],
    };

    private static IrFormOrReport Report(string name, string? recordSource = null) => new()
    {
        Name = name,
        ObjectType = "Report",
        RecordSource = recordSource,
        VbaCode = null,
        Controls = [],
        MacroActions = [],
    };

    private static IrQuery Query(string name, int typeCode, string? sql = null) =>
        new() { Name = name, QueryTypeCode = typeCode, Sql = sql };

    private static IrTable Table(string name, int colCount) =>
        new() { Name = name, ColumnCount = colCount };

    private static IrRelationship Rel(string name, string parent, string child) =>
        new() { Name = name, ParentTable = parent, ChildTable = child };

    private static UsageFinding Classify(IrDatabase db, string objectName)
    {
        var index = SignalIndex.Build(db);
        var findings = UsageClassifier.Classify(db, index);
        return findings.First(f => f.ObjectName == objectName);
    }

    // ── Form rules ─────────────────────────────────────────────────────────────

    [Fact]
    public void Form_F1_TransactionalTable_With_CRUD_Is_MainDataEntry_High()
    {
        // tblClients has FK children (it's a parent in rel_Client_Orders)
        var db = EmptyDb() with
        {
            Forms = [Form("frmClients", "tblClients", beforeUpdate: true)],
            Tables = [Table("tblClients", 10), Table("tblOrders", 5)],
            Relationships = [Rel("rel_Client_Orders", "tblClients", "tblOrders")],
        };
        var f = Classify(db, "frmClients");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
        Assert.True(f.Confidence >= 0.8);
    }

    [Fact]
    public void Form_F2_TransactionalTable_No_CRUD_Is_MainDataEntry_Medium()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmClients", "tblClients")],
            Tables = [Table("tblClients", 10), Table("tblOrders", 5)],
            Relationships = [Rel("rel_Client_Orders", "tblClients", "tblOrders")],
        };
        var f = Classify(db, "frmClients");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
        Assert.True(f.Confidence >= 0.5 && f.Confidence < 0.8);
    }

    [Fact]
    public void Form_F3_LookupTable_RecordSource_Is_LookupReference()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmStates", "tlkpStates")],
            Tables = [Table("tlkpStates", 2)],
        };
        var f = Classify(db, "frmStates");
        Assert.Equal(UsageCategory.LookupReference, f.Classification);
    }

    [Fact]
    public void Form_F5_NoRecordSource_With_NavEdges_Is_NavigationShell()
    {
        var db = EmptyDb() with
        {
            Forms = [
                Form("frmSwitchboard", macroActions:
                [
                    new IrMacroAction { Action = "OpenForm", Arguments = ["frmClients"] },
                ]),
                Form("frmClients"),
            ],
        };
        var f = Classify(db, "frmSwitchboard");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
    }

    [Fact]
    public void Form_F6_NoRecordSource_NoNav_WithVba_Is_Unknown()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmMystery", vbaCode: "Sub Foo()\nEnd Sub")],
        };
        var f = Classify(db, "frmMystery");
        Assert.Equal(UsageCategory.Unknown, f.Classification);
    }

    [Fact]
    public void Form_F7_NoRecordSource_NoNav_NoVba_Is_PossiblyUnused()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmEmpty")],
        };
        var f = Classify(db, "frmEmpty");
        Assert.Equal(UsageCategory.PossiblyUnused, f.Classification);
    }

    [Fact]
    public void Form_F7b_NavigationTarget_NoOtherSignals_Is_Unknown()
    {
        // A form that is a navigation target but has no record source, no nav edges out,
        // and no VBA should be Unknown (not PossiblyUnused) — it's reachable but unclassifiable.
        var db = EmptyDb() with
        {
            Forms = [
                Form("frmAbout"),
                Form("frmMain", macroActions: [new IrMacroAction { Action = "OpenForm", Arguments = ["frmAbout"] }]),
            ],
        };
        var f = Classify(db, "frmAbout");
        Assert.Equal(UsageCategory.Unknown, f.Classification);
    }

    // ── Report rules ───────────────────────────────────────────────────────────

    [Fact]
    public void Report_R1_HasRecordSource_Is_Reporting()
    {
        var db = EmptyDb() with
        {
            Reports = [Report("rptClients", "tblClients")],
        };
        var f = Classify(db, "rptClients");
        Assert.Equal(UsageCategory.Reporting, f.Classification);
        Assert.True(f.Confidence >= 0.8);
    }

    [Fact]
    public void Report_R3_NoRecordSource_NotOpened_Is_PossiblyUnused()
    {
        var db = EmptyDb() with
        {
            Reports = [Report("rptOrphaned")],
        };
        var f = Classify(db, "rptOrphaned");
        Assert.Equal(UsageCategory.PossiblyUnused, f.Classification);
    }

    // ── Query rules ────────────────────────────────────────────────────────────

    [Fact]
    public void Query_Q1_Crosstab_Is_Reporting()
    {
        var db = EmptyDb() with
        {
            Queries = [Query("qxtBySvc", typeCode: 16)],  // dbQCrosstab = 16 (006 F-2-1)
        };
        var f = Classify(db, "qxtBySvc");
        Assert.Equal(UsageCategory.Reporting, f.Classification);
    }

    [Fact]
    public void Query_Q2_Select_UsedAsFormRecordSource_Is_MainDataEntry()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmList", "qryClientList")],
            Queries = [Query("qryClientList", typeCode: 0, sql: "SELECT * FROM tblClients")],
            Tables = [Table("tblClients", 10)],
        };
        var f = Classify(db, "qryClientList");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
    }

    [Fact]
    public void Query_Q3_Select_UsedAsReportRecordSource_Is_Reporting()
    {
        var db = EmptyDb() with
        {
            Reports = [Report("rptList", "qryClientList")],
            Queries = [Query("qryClientList", typeCode: 0, sql: "SELECT * FROM tblClients")],
            Tables = [Table("tblClients", 10)],
        };
        var f = Classify(db, "qryClientList");
        Assert.Equal(UsageCategory.Reporting, f.Classification);
    }

    [Fact]
    public void Query_Q8_ActionQuery_OpenedByNav_Is_StandaloneUtility()
    {
        var db = EmptyDb() with
        {
            Queries = [Query("qryDeleteOld", typeCode: 32)],  // dbQDelete = 32 (006 F-2-1)
            Macros = [new IrMacro
            {
                Name = "mcrMaintenance",
                Actions = [new IrMacroAction { Action = "OpenQuery", Arguments = ["qryDeleteOld"] }],
            }],
        };
        var f = Classify(db, "qryDeleteOld");
        Assert.Equal(UsageCategory.StandaloneUtility, f.Classification);
    }

    [Fact]
    public void Query_Q9_ActionQuery_Unreferenced_Is_PossiblyUnused()
    {
        var db = EmptyDb() with
        {
            Queries = [Query("qryDeleteStale", typeCode: 32)],  // dbQDelete = 32 (006 F-2-1)
        };
        var f = Classify(db, "qryDeleteStale");
        Assert.Equal(UsageCategory.PossiblyUnused, f.Classification);
    }

    [Fact]
    public void Query_Q11_PassThrough_Is_StandaloneUtility()
    {
        var db = EmptyDb() with
        {
            Queries = [Query("qryPassThrough", typeCode: 112)],  // dbQSQLPassThrough = 112 (006 F-2-1)
        };
        var f = Classify(db, "qryPassThrough");
        Assert.Equal(UsageCategory.StandaloneUtility, f.Classification);
    }

    [Fact]
    public void Query_Q11_PassThroughBulk_Is_StandaloneUtility()  // 006 F-2-1 (dbQSPTBulk = 144)
    {
        var db = EmptyDb() with
        {
            Queries = [Query("qrySptBulk", typeCode: 144)],  // dbQSPTBulk = 144
        };
        var f = Classify(db, "qrySptBulk");
        Assert.Equal(UsageCategory.StandaloneUtility, f.Classification);
    }

    [Fact]
    public void Query_MakeTable_Unreferenced_Is_PossiblyUnused()  // 006 F-2-1 (decode-shift guard)
    {
        // dbQMakeTable = 80 is an action query (Q9 → PossiblyUnused when
        // unreferenced). Before the F-2-1 fix, 80 decoded as "Union" and took the
        // Select path, so this pins the corrected high end of the shifted table.
        var db = EmptyDb() with
        {
            Queries = [Query("qryMakeTbl", typeCode: 80)],
        };
        var f = Classify(db, "qryMakeTbl");
        Assert.Equal(UsageCategory.PossiblyUnused, f.Classification);
    }

    // ── Table rules ────────────────────────────────────────────────────────────

    [Fact]
    public void Table_T1_FkParent_UsedAsRecordSource_Is_MainDataEntry_High()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmClients", "tblClients")],
            Tables = [Table("tblClients", 10), Table("tblOrders", 5)],
            Relationships = [Rel("rel_Client_Orders", "tblClients", "tblOrders")],
        };
        var f = Classify(db, "tblClients");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
        Assert.True(f.Confidence >= 0.8);
    }

    [Fact]
    public void Table_T3_FkChildOnly_SmallColumns_Is_LookupReference()
    {
        var db = EmptyDb() with
        {
            Tables = [Table("tblStates", 2), Table("tblClients", 10)],
            Relationships = [Rel("rel_State_Client", "tblStates", "tblClients")],
        };
        // tblStates is a parent (has FK children), but with 2 cols → T3 doesn't apply
        // tblClients is a child (has FK parents) with 10 cols → not T3 either
        // Actually T3: hasFkParents && !hasFkChildren && columnCount <= 4
        // tblStates: has FK children (tblClients) → T2 (FK parent)
        // tblClients: has FK parents (tblStates) && 10 cols → not T3
        // Let's use a proper T3 scenario: tblStatus has parent tblCategory, and has 3 cols, no children
        var db2 = EmptyDb() with
        {
            Tables = [Table("tblCategory", 5), Table("tblStatus", 3), Table("tblOrders", 8)],
            Relationships = [
                Rel("rel_Cat_Status", "tblCategory", "tblStatus"),
                Rel("rel_Cat_Orders", "tblCategory", "tblOrders"),
            ],
        };
        var f = Classify(db2, "tblStatus");
        Assert.Equal(UsageCategory.LookupReference, f.Classification);
    }

    [Fact]
    public void Table_T4_LookupNamed_Is_LookupReference()
    {
        var db = EmptyDb() with
        {
            Tables = [Table("tlkpPlanStatuses", 3)],
        };
        var f = Classify(db, "tlkpPlanStatuses");
        Assert.Equal(UsageCategory.LookupReference, f.Classification);
    }

    [Fact]
    public void Table_T4b_FkParent_UsedAsRecordSource_Is_MainDataEntry()
    {
        // A table that is a FK child (has FK parents) and is used as a form RecordSource
        // should be MainDataEntry — it's a transactional data-entry table on the child side.
        var db = EmptyDb() with
        {
            Forms = [Form("frmBudgets", "tblClientBudgets")],
            Tables = [Table("tblClients", 10), Table("tblClientBudgets", 8)],
            Relationships = [Rel("rel_Client_Budgets", "tblClients", "tblClientBudgets")],
        };
        var f = Classify(db, "tblClientBudgets");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
    }

    [Fact]
    public void Table_T6_NoSignals_Is_PossiblyUnused()
    {
        var db = EmptyDb() with
        {
            Tables = [Table("tblOrphan", 5)],
        };
        var f = Classify(db, "tblOrphan");
        Assert.Equal(UsageCategory.PossiblyUnused, f.Classification);
    }

    [Fact]
    public void Table_System_Tables_Are_Excluded()
    {
        var db = EmptyDb() with
        {
            Tables = [Table("MSysObjects", 10), Table("tblClients", 8)],
        };
        var index = SignalIndex.Build(db);
        var findings = UsageClassifier.Classify(db, index);
        Assert.DoesNotContain(findings, f => f.ObjectName == "MSysObjects");
        Assert.Contains(findings, f => f.ObjectName == "tblClients");
    }

    // ── Exclusion ──────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_Excluded_Objects_Are_Not_In_Findings()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmOld"), Form("frmActive")],
        };
        var index = SignalIndex.Build(db);
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "frmOld" };
        var findings = UsageClassifier.Classify(db, index, excluded);
        Assert.DoesNotContain(findings, f => f.ObjectName == "frmOld");
        Assert.Contains(findings, f => f.ObjectName == "frmActive");
    }

    // ── F4 boundary — form bound to plain table with no FK or lookup signals ─────

    [Fact]
    public void Form_PlainTable_RecordSource_No_FK_Is_MainDataEntry()
    {
        // tblNotes has no FK relationships and no lookup naming — not transactional
        // and not lookup. Should NOT be classified as Reporting (the old F4 overreach).
        var db = EmptyDb() with
        {
            Forms = [Form("frmNotes", "tblNotes")],
            Tables = [Table("tblNotes", 5)],
        };
        var f = Classify(db, "frmNotes");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
    }

    [Fact]
    public void Form_InlineSql_RecordSource_Is_MainDataEntry()
    {
        // An inline SQL RecordSource means the form IS bound (not unbound),
        // but the SQL string is not a named table/query object.
        // Pre-fix: the form was treated as having NO RecordSource -> PossiblyUnused/Unknown
        // Post-fix: the form is classified as MainDataEntry (bound, inline SQL)
        var db = EmptyDb() with
        {
            Forms = [Form("frmInlineSql", "SELECT * FROM tblClients")],
        };
        var f = Classify(db, "frmInlineSql");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
    }

    [Fact]
    public void Form_Query_RecordSource_Is_Reporting()
    {
        // Form bound to a SELECT query (no FK signal, not lookup)
        var db = EmptyDb() with
        {
            Forms = [Form("frmList", "qryClientList")],
            Queries = [Query("qryClientList", typeCode: 0, sql: "SELECT * FROM tblClients")],
            Tables = [Table("tblClients", 10)],
        };
        var f = Classify(db, "frmList");
        // F4: RecordSource is a known query → Reporting
        Assert.Equal(UsageCategory.Reporting, f.Classification);
    }

    [Fact]
    public void Query_Q3_UsedInReportInlineSql_Is_Reporting()
    {
        // A query referenced inside an inline-SQL RecordSource of a report
        // (e.g., "SELECT * FROM qryClientList WHERE ClientID=1")
        // should be classified as Reporting, not PossiblyUnused.
        var db = EmptyDb() with
        {
            Reports = [Report("srptDetail", "SELECT * FROM qryClientList WHERE ClientID=1")],
            Queries = [Query("qryClientList", typeCode: 0, sql: "SELECT * FROM tblClients")],
            Tables = [Table("tblClients", 10)],
        };
        var f = Classify(db, "qryClientList");
        Assert.Equal(UsageCategory.Reporting, f.Classification);
    }

    // ── Q6 references-no-tables (006 F-2-4 — was dead code) ──────────────────────

    [Fact]
    public void Query_Q6_ReferencesOnlyQueries_Is_Unknown()  // 006 F-2-4
    {
        // A query whose FROM/JOIN targets are all OTHER queries (no base table) is a
        // pure intermediate-computation step — strategy doc Q6 → Unknown 0.40 plus the
        // QueryReferencesNoTables clarifying question. Before F-2-4 the QueryRefsNoTables
        // helper unconditionally returned false, so Q6 never fired.
        var db = EmptyDb() with
        {
            Queries =
            [
                Query("qryBase", typeCode: 0, sql: "SELECT * FROM tblClients"),
                Query("qryDerived", typeCode: 0, sql: "SELECT * FROM qryBase"),
            ],
            Tables = [Table("tblClients", 10)],
        };
        var f = Classify(db, "qryDerived");
        Assert.Equal(UsageCategory.Unknown, f.Classification);
        Assert.Contains(f.Questions, q => q.UncertaintySignal == UncertaintySignal.QueryReferencesNoTables);
    }

    [Fact]
    public void Query_ReferencesBaseTable_DoesNot_FireQ6()  // 006 F-2-4 (guard)
    {
        // A query whose FROM/JOIN includes a base table must NOT fire Q6, even when it
        // also references a query — Q6 requires that EVERY target be a query.
        var db = EmptyDb() with
        {
            Queries =
            [
                Query("qryBase", typeCode: 0, sql: "SELECT * FROM tblClients"),
                Query("qryMixed", typeCode: 0,
                      sql: "SELECT * FROM qryBase INNER JOIN tblOrders ON qryBase.ID = tblOrders.ClientID"),
            ],
            Tables = [Table("tblClients", 10), Table("tblOrders", 5)],
        };
        var f = Classify(db, "qryMixed");
        Assert.DoesNotContain(f.Questions, q => q.UncertaintySignal == UncertaintySignal.QueryReferencesNoTables);
    }

    // ── Inline-SQL RecordSource table credit (006 F-2-5) ─────────────────────────

    [Fact]
    public void Table_BoundOnlyViaInlineSqlRecordSource_EarnsRecordSourceCredit()  // 006 F-2-5
    {
        // A table named only inside a form's inline-SQL RecordSource
        // ("SELECT * FROM tblRates …") must earn used-as-RecordSource credit. Before
        // F-2-5 the phase-2 inline-SQL scan credited only query names, so the table
        // got no RecordSource signal and could read as unreferenced.
        var db = EmptyDb() with
        {
            Forms = [Form("frmRates", "SELECT * FROM tblRates WHERE Active = True")],
            Tables = [Table("tblRates", 8)],
        };
        var f = Classify(db, "tblRates");
        Assert.Contains(f.Signals, s => s.SignalType == UsageSignalType.RecordSource && s.Fired);
    }

    [Fact]
    public void Table_NotInCatalog_NotCredited_FromInlineSql()  // 006 F-2-5 (catalog guard)
    {
        // An inline-SQL FROM name that is NOT a catalog object (alias / unknown) must
        // not earn credit — only names in db.Tables ∪ db.Queries are credited.
        var db = EmptyDb() with
        {
            Forms = [Form("frmRates", "SELECT * FROM tblRates WHERE Active = True")],
            Tables = [Table("tblRates", 8), Table("tblOther", 6)],
        };
        var f = Classify(db, "tblOther");
        Assert.DoesNotContain(f.Signals, s => s.SignalType == UsageSignalType.RecordSource && s.Fired);
    }

    // ── Q10 Union handling ─────────────────────────────────────────────────────

    [Fact]
    public void Query_Q10_Union_UsedAsFormRecordSource_Is_MainDataEntry()
    {
        var db = EmptyDb() with
        {
            Forms = [Form("frmUnion", "qryUnionAll")],
            Queries = [Query("qryUnionAll", typeCode: 128, sql: "SELECT * FROM tblA UNION ALL SELECT * FROM tblB")],  // dbQSetOperation = 128 (006 F-2-1)
        };
        var f = Classify(db, "qryUnionAll");
        // Union queries follow Q2 path when used as form RecordSource
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
    }

    [Fact]
    public void Query_Q10_Union_Unreferenced_Is_PossiblyUnused()
    {
        var db = EmptyDb() with
        {
            Queries = [Query("qryUnionOrphan", typeCode: 128, sql: "SELECT * FROM tblA UNION ALL SELECT * FROM tblB")],  // dbQSetOperation = 128 (006 F-2-1)
        };
        var f = Classify(db, "qryUnionOrphan");
        // Q7 applies to unreferenced Select/Union queries
        Assert.Equal(UsageCategory.PossiblyUnused, f.Classification);
    }

    // ── Module / Macro findings ────────────────────────────────────────────────

    [Fact]
    public void Module_With_VbaCode_Is_StandaloneUtility()
    {
        var db = EmptyDb() with
        {
            Modules = [new IrModule { Name = "basUtilities", VbaCode = "Sub Foo()\nEnd Sub" }],
        };
        var f = Classify(db, "basUtilities");
        Assert.Equal("Module", f.ObjectType);
        Assert.Equal(UsageCategory.StandaloneUtility, f.Classification);
    }

    [Fact]
    public void Macro_WithNavEdges_Is_NavigationShell()
    {
        var db = EmptyDb() with
        {
            Macros = [new IrMacro
            {
                Name = "mcrSwitchboard",
                Actions = [new IrMacroAction { Action = "OpenForm", Arguments = ["frmClients"] }],
            }],
            Forms = [Form("frmClients")],
        };
        var f = Classify(db, "mcrSwitchboard");
        Assert.Equal("Macro", f.ObjectType);
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
    }

    [Fact]
    public void Macro_NoNavEdges_Is_StandaloneUtility()
    {
        var db = EmptyDb() with
        {
            Macros = [new IrMacro
            {
                Name = "mcrValidation",
                Actions = [new IrMacroAction { Action = "SetValue", Arguments = ["[Field]", "Value"] }],
            }],
        };
        var f = Classify(db, "mcrValidation");
        Assert.Equal("Macro", f.ObjectType);
        Assert.Equal(UsageCategory.StandaloneUtility, f.Classification);
    }
}
