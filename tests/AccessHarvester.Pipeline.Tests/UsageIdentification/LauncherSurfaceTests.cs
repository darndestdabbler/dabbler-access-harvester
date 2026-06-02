using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

/// <summary>
/// Regression tests for launcher-surface recognition and out-of-band entry
/// uncertainty (set 001 Session 1). Covers the HomeHealthCare-shaped cases the
/// upstream feedback called out: a switchboard form that delegates its opens to a
/// helper macro, the switchboard helper macro and table, a modal report-menu, a
/// startup-opened form, and the negative guard that a stripped-chrome parameter
/// dialog is NOT mistaken for a launcher.
/// </summary>
public class LauncherSurfaceTests
{
    // ── Builders ───────────────────────────────────────────────────────────────

    private static IrDatabase Db(
        IrFormOrReport[]? forms = null,
        IrFormOrReport[]? reports = null,
        IrQuery[]? queries = null,
        IrTable[]? tables = null,
        IrMacro[]? macros = null,
        IrRelationship[]? relationships = null)
        => new()
        {
            Name = "TestDb",
            Forms = forms ?? [],
            Reports = reports ?? [],
            Queries = queries ?? [],
            Tables = tables ?? [],
            Relationships = relationships ?? [],
            Macros = macros ?? [],
            Modules = [],
        };

    private static IrRelationship Rel(string name, string parent, string child) =>
        new() { Name = name, ParentTable = parent, ChildTable = child };

    private static IrFormOrReport Form(string name, string? recordSource = null,
        string? vbaCode = null, Dictionary<string, string>? properties = null) => new()
    {
        Name = name,
        ObjectType = "Form",
        RecordSource = recordSource,
        VbaCode = vbaCode,
        Controls = [],
        MacroActions = [],
        Properties = properties ?? new Dictionary<string, string>(),
    };

    private static IrMacro OpenFormMacro(string name, params string[] targets) => new()
    {
        Name = name,
        Actions = targets
            .Select(t => new IrMacroAction { Action = "OpenForm", Arguments = [t] })
            .ToList(),
    };

    private static IrTable Table(string name, int cols = 4) =>
        new() { Name = name, ColumnCount = cols };

    private static IrQuery Query(string name, string? sql = null, int typeCode = 0) =>
        new() { Name = name, QueryTypeCode = typeCode, Sql = sql };

    private static IReadOnlyList<string> LauncherEvidence(UsageFinding f)
        => f.Signals.First(s => s.SignalType == UsageSignalType.LauncherSurface).Evidence;

    private static Dictionary<string, string> LauncherChrome(string caption = "Home") => new()
    {
        ["RecordSelectors"] = "False",
        ["NavigationButtons"] = "False",
        ["Caption"] = caption,
    };

    private static UsageFinding Classify(IrDatabase db, string name)
    {
        var index = SignalIndex.Build(db);
        return UsageClassifier.Classify(db, index).First(f => f.ObjectName == name);
    }

    private static bool Fired(UsageFinding f, string signalType)
        => f.Signals.Any(s => s.SignalType == signalType && s.Fired);

    // ── Switchboard form delegating to a helper macro ────────────────────────────

    [Fact]
    public void Switchboard_Form_Delegating_Opens_To_Macro_Is_NavigationShell()
    {
        // frmSwitchboard emits no navigation edges of its own — the opens live in
        // mcrSwitchboardActions — yet it is the application's launcher home screen.
        var db = Db(
            forms:
            [
                Form("frmSwitchboard", vbaCode: "Private Sub Form_Load()\nEnd Sub"),
                Form("frmClientDetail"),
                Form("frmProviderDetail"),
            ],
            macros: [OpenFormMacro("mcrSwitchboardActions", "frmClientDetail", "frmProviderDetail")],
            tables: [Table("tblSwitchboardItems")]);

        var f = Classify(db, "frmSwitchboard");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
    }

    [Fact]
    public void Switchboard_Helper_Macro_Is_NavigationShell()
    {
        var db = Db(
            forms: [Form("frmClientDetail"), Form("frmProviderDetail")],
            macros: [OpenFormMacro("mcrSwitchboardActions", "frmClientDetail", "frmProviderDetail")]);

        var f = Classify(db, "mcrSwitchboardActions");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
        Assert.Contains("containing navigation actions", f.UsageSummary);
    }

    [Fact]
    public void Name_Only_Launcher_Macro_Without_Edges_Uses_Cautious_Summary()
    {
        // A switchboard/menu-named macro whose actions did NOT parse into navigation
        // edges is still recognized as a launcher helper, but its summary must not
        // claim it "contains navigation actions" — that would overstate the evidence.
        var db = Db(macros: [new IrMacro { Name = "mcrSwitchboardHelper", Actions = [] }]);

        var f = Classify(db, "mcrSwitchboardHelper");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
        Assert.DoesNotContain("containing navigation actions", f.UsageSummary);
        Assert.Contains("no navigation actions parsed", f.UsageSummary);
    }

    [Fact]
    public void Switchboard_Helper_Table_Is_Not_PossiblyUnused()
    {
        // tblSwitchboardItems has no FK and no inbound reference, but it is launcher
        // configuration data — it must read as launcher infrastructure, not as a
        // generic unexplained/unused object.
        var db = Db(tables: [Table("tblSwitchboardItems")]);

        var f = Classify(db, "tblSwitchboardItems");
        Assert.NotEqual(UsageCategory.PossiblyUnused, f.Classification);
        Assert.Equal(UsageCategory.StandaloneUtility, f.Classification);
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
        Assert.Contains("switchboard", f.UsageSummary, System.StringComparison.OrdinalIgnoreCase);
        // No deletion-implying "unreferenced" question for a recognized helper.
        Assert.DoesNotContain(f.Questions, q => q.UncertaintySignal == UncertaintySignal.UnreferencedObject);
    }

    [Fact]
    public void Positively_Used_Launcher_Named_Table_Does_Not_Emit_Launcher_Output()
    {
        // tblMenuItems matches the launcher-data name pattern, but here it is a real
        // data table (FK parent + used as a form RecordSource -> T1 MainDataEntry).
        // Because an earlier rule decided the category, the launcher summary and
        // LauncherSurface signal must NOT fire — they are gated on the T5b decision,
        // not the name.
        var db = Db(
            forms: [Form("frmMenuAdmin", recordSource: "tblMenuItems")],
            tables: [Table("tblMenuItems", 6), Table("tblMenuClicks", 4)],
            relationships: [Rel("rel_MenuItems_Clicks", "tblMenuItems", "tblMenuClicks")]);

        var f = Classify(db, "tblMenuItems");
        Assert.Equal(UsageCategory.MainDataEntry, f.Classification);
        Assert.False(Fired(f, UsageSignalType.LauncherSurface));
        Assert.DoesNotContain("switchboard configuration", f.UsageSummary,
            System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("launcher menu", f.UsageSummary,
            System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Modal report menu ────────────────────────────────────────────────────────

    [Fact]
    public void Modal_ReportMenu_Form_Is_NavigationShell()
    {
        var props = new Dictionary<string, string> { ["PopUp"] = "True", ["Modal"] = "True" };
        var db = Db(forms: [Form("frmReportMenu", vbaCode: "Private Sub Form_Open()\nEnd Sub", properties: props)]);

        var f = Classify(db, "frmReportMenu");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
        // A recognized launcher is explained — it should not be nagged as unknown.
        Assert.DoesNotContain(f.Questions, q => q.UncertaintySignal == UncertaintySignal.NoRecordSource);
        Assert.DoesNotContain(f.Questions, q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
    }

    [Fact]
    public void Switchboard_Form_With_Launcher_Chrome_Is_High_Confidence()
    {
        var db = Db(forms:
        [
            Form("frmSwitchboard",
                 vbaCode: "Private Sub Form_Load()\nEnd Sub",
                 properties: LauncherChrome("Home Health Care Services Tracking")),
        ]);

        var f = Classify(db, "frmSwitchboard");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(f.Confidence >= 0.8);
    }

    // ── Startup-opened form ──────────────────────────────────────────────────────

    [Fact]
    public void Startup_Opened_Form_Is_NavigationShell_With_StartupEntry_Signal()
    {
        // frmDashboard is not launcher-named, but an AutoExec macro opens it on
        // database open — that makes it a first-class startup entry surface.
        var db = Db(
            forms: [Form("frmDashboard", vbaCode: "Private Sub Form_Load()\nEnd Sub")],
            macros: [OpenFormMacro("AutoExec", "frmDashboard")]);

        var f = Classify(db, "frmDashboard");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.StartupEntry));
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
    }

    // ── Negative guard: stripped-chrome parameter dialog is NOT a launcher ────────

    [Fact]
    public void Stripped_Chrome_Parameter_Dialog_Without_Launcher_Name_Stays_Unknown()
    {
        // A report-parameter dialog can carry the same chrome (no selectors / nav
        // buttons) as a switchboard. Chrome alone must NOT classify it as a launcher;
        // that interpretation belongs to set 001 Session 2 (parameter-dialog flows).
        var db = Db(forms:
        [
            Form("frmReportParam_Client",
                 vbaCode: "Private Sub Form_Open()\nEnd Sub",
                 properties: LauncherChrome("Select Client")),
        ]);

        var f = Classify(db, "frmReportParam_Client");
        Assert.NotEqual(UsageCategory.NavigationShell, f.Classification);
        Assert.Equal(UsageCategory.Unknown, f.Classification);
        Assert.False(Fired(f, UsageSignalType.LauncherSurface));
    }

    // ── Boundary guards: bound forms and signal/category agreement (R1 Issue 2/3) ─

    [Fact]
    public void Bound_Launcher_Named_Form_Follows_RecordSource_Branch_Not_F5b()
    {
        // A data form whose name contains "Menu" but which IS bound must classify
        // from its RecordSource, never be forced into NavigationShell — and the
        // LauncherSurface signal must not fire (it would disagree with the category).
        var db = Db(
            forms: [Form("frmClientMenu", recordSource: "tblClients")],
            tables: [Table("tblClients", 10)]);

        var f = Classify(db, "frmClientMenu");
        Assert.NotEqual(UsageCategory.NavigationShell, f.Classification);
        Assert.False(Fired(f, UsageSignalType.LauncherSurface));
    }

    [Fact]
    public void Startup_Opened_Bound_Form_Reports_StartupEntry_But_Not_LauncherSurface()
    {
        // A bound form opened by AutoExec is honestly StartupEntry, but it is NOT a
        // launcher (it has a RecordSource), so LauncherSurface must stay off and the
        // category must come from the data branch.
        var db = Db(
            forms: [Form("frmStartScreen", recordSource: "tblConfig")],
            tables: [Table("tblConfig", 6)],
            macros: [OpenFormMacro("AutoExec", "frmStartScreen")]);

        var f = Classify(db, "frmStartScreen");
        Assert.NotEqual(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.StartupEntry));
        Assert.False(Fired(f, UsageSignalType.LauncherSurface));
    }

    [Fact]
    public void Launcher_Chrome_Is_Recorded_As_LauncherSurface_Evidence()
    {
        var db = Db(forms:
        [
            Form("frmSwitchboard",
                 vbaCode: "Private Sub Form_Load()\nEnd Sub",
                 properties: LauncherChrome("Home")),
        ]);

        var f = Classify(db, "frmSwitchboard");
        Assert.Contains("launcher-chrome", LauncherEvidence(f));
        Assert.Contains("launcher-name-pattern", LauncherEvidence(f));
    }

    // ── Out-of-band: in-graph query dependency suppresses the question (R1 Issue 1) ─

    [Fact]
    public void Query_Consumed_By_Another_Query_Does_Not_Get_OutOfBand_Question()
    {
        // qryInner is reached in-graph because qryOuter's SQL references it. That is a
        // harvested dependency, not an out-of-band entry, so it must NOT be told
        // "no harvested opener was found".
        var db = Db(queries:
        [
            Query("qryOuter", sql: "SELECT * FROM qryInner"),
            Query("qryInner", sql: "SELECT * FROM tblClients"),
        ],
        tables: [Table("tblClients", 10)]);

        // qryInner is referenced (Reporting), in-graph — neither out-of-band nor unused.
        var inner = Classify(db, "qryInner");
        Assert.DoesNotContain(inner.Questions,
            q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
        Assert.DoesNotContain(inner.Questions,
            q => q.UncertaintySignal == UncertaintySignal.UnreferencedObject);

        // qryOuter is referenced by nobody → PossiblyUnused → the reframed
        // UnreferencedObject question, not the out-of-band one.
        var outer = Classify(db, "qryOuter");
        Assert.Equal(UsageCategory.PossiblyUnused, outer.Classification);
        Assert.Contains(outer.Questions,
            q => q.UncertaintySignal == UncertaintySignal.UnreferencedObject);
        Assert.DoesNotContain(outer.Questions,
            q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
    }

    [Fact]
    public void Query_With_No_Harvested_Opener_Gets_OutOfBand_Question()
    {
        // A parameterized select query is Reporting (not PossiblyUnused) yet has no
        // harvested opener — it should get the out-of-band reframing, and the retired
        // NoNavigationEntry signal must stay absent.
        var db = Db(queries:
        [
            Query("qryByFiscalYear", sql: "SELECT * FROM tblPlans WHERE FY = [Enter Fiscal Year]"),
        ],
        tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryByFiscalYear");
        Assert.NotEqual(UsageCategory.PossiblyUnused, f.Classification);
        Assert.Contains(f.Questions,
            q => q.UncertaintySignal == UncertaintySignal.OutOfBandEntryPossible);
        Assert.DoesNotContain(f.Questions,
            q => q.UncertaintySignal == UncertaintySignal.NoNavigationEntry);
    }

    // ── Neutral wording for generic (non-launcher) navigation objects (R3) ────────

    [Fact]
    public void Generic_Nav_Form_Via_Edges_Keeps_Neutral_Summary()
    {
        // frmClientList opens a detail form (F5 via its own edges) but is not a
        // switchboard/menu/startup launcher — its summary must stay neutral and not
        // claim switchboard/menu/launcher traits.
        var frmList = new IrFormOrReport
        {
            Name = "frmClientList", ObjectType = "Form", Controls = [],
            Properties = new Dictionary<string, string>(),
            MacroActions = [new IrMacroAction { Action = "OpenForm", Arguments = ["frmClientDetail"] }],
        };
        var db = Db(forms: [frmList, Form("frmClientDetail")]);

        var f = Classify(db, "frmClientList");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.DoesNotContain("launcher", f.UsageSummary, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("switchboard", f.UsageSummary, System.StringComparison.OrdinalIgnoreCase);
        Assert.False(Fired(f, UsageSignalType.LauncherSurface));
    }

    [Fact]
    public void Generic_Edge_Backed_Macro_Keeps_Neutral_Summary()
    {
        // A macro that opens one detail form, but is not switchboard/menu-named, is a
        // navigation source — its summary should describe navigation actions without
        // overstating a "launcher surface".
        var db = Db(
            forms: [Form("frmClientDetail")],
            macros: [OpenFormMacro("mcrOpenClientDetail", "frmClientDetail")]);

        var f = Classify(db, "mcrOpenClientDetail");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.Contains("containing navigation actions", f.UsageSummary);
        Assert.DoesNotContain("launcher surface", f.UsageSummary);
    }
}
