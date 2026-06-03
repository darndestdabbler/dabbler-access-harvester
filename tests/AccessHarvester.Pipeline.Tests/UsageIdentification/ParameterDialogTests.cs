using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.UsageIdentification;
using Xunit;

namespace AccessHarvester.Pipeline.Tests.UsageIdentification;

/// <summary>
/// Regression tests for parameterized-query and parameter-dialog recognition
/// (set 001 Session 2). Covers the HomeHealthCare-shaped cases: declared
/// PARAMETERS queries surfacing a first-class ParameterizedInput signal (even
/// when used as a report RecordSource), and frmReportParam_* dialog forms being
/// recognized as parameter collectors (rule F8) instead of reading as generic
/// VBA-only Unknown forms. Includes the negative guards that a bound *Param*
/// form stays data entry and that a launcher/menu form wins over F8.
/// </summary>
public class ParameterDialogTests
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

    private static IrFormOrReport Report(string name, string? recordSource = null) => new()
    {
        Name = name,
        ObjectType = "Report",
        RecordSource = recordSource,
        Controls = [],
        MacroActions = [],
        Properties = new Dictionary<string, string>(),
    };

    // Mirrors IrLoader: parse the query's parameters the same way the real load
    // path does, so in-memory fixtures carry IrQuery.Parameters faithfully.
    private static IrQuery Query(string name, string? sql = null, int typeCode = 0) =>
        new()
        {
            Name = name,
            QueryTypeCode = typeCode,
            Sql = sql,
            Parameters = IrLoader.ExtractQueryParameters(sql),
        };

    private static IrTable Table(string name, int cols = 4) =>
        new() { Name = name, ColumnCount = cols };

    private static IrMacro OpenFormMacro(string name, params string[] targets) => new()
    {
        Name = name,
        Actions = targets
            .Select(t => new IrMacroAction { Action = "OpenForm", Arguments = [t] })
            .ToList(),
    };

    // Stripped-chrome dialog: no record selectors / nav buttons, like the
    // HomeHealthCare frmReportParam_* forms.
    private static Dictionary<string, string> DialogChrome(string caption = "Select Client") => new()
    {
        ["RecordSelectors"] = "False",
        ["NavigationButtons"] = "False",
        ["Caption"] = caption,
    };

    // VBA shaped like a classic acDialog form: validate + hide on OK, cancel flag.
    private const string DialogVba =
        "Private Sub btnOK_Click()\n    Me.Visible = False\nEnd Sub\n" +
        "Private Sub btnCancel_Click()\n    DoCmd.Close acForm, Me.Name\nEnd Sub\n";

    private static UsageFinding Classify(IrDatabase db, string name)
    {
        var index = SignalIndex.Build(db);
        return UsageClassifier.Classify(db, index).First(f => f.ObjectName == name);
    }

    private static bool Fired(UsageFinding f, string signalType)
        => f.Signals.Any(s => s.SignalType == signalType && s.Fired);

    private static IReadOnlyList<string> ParamEvidence(UsageFinding f)
        => f.Signals.First(s => s.SignalType == UsageSignalType.ParameterizedInput).Evidence;

    private static bool HasQuestion(UsageFinding f, string uncertaintySignal)
        => f.Questions.Any(q => q.UncertaintySignal == uncertaintySignal);

    // ── Parameterized queries ────────────────────────────────────────────────────

    [Fact]
    public void Declared_Parameters_Query_Fires_ParameterizedInput_And_Question()
    {
        // A saved query with a leading PARAMETERS clause expects run-time input.
        var db = Db(
            queries:
            [
                Query("qryPlansByProvider",
                    sql: "PARAMETERS [Enter Provider ID] Long;\nSELECT * FROM tblPlans WHERE ProviderID = [Enter Provider ID]"),
            ],
            tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryPlansByProvider");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains(ParamEvidence(f), e => e.StartsWith("declared-parameter:"));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
    }

    [Fact]
    public void Parameterized_Query_Used_As_Report_RecordSource_Still_Surfaces_The_Signal()
    {
        // qryPlansByClient classifies via Q3 (report RecordSource -> Reporting 0.80),
        // which never reaches the Q5 parameter-marker path. The ParameterizedInput
        // signal is computed independently of the category branch, so it must still
        // fire — this is the core Session 2 gap the upstream feedback called out.
        var db = Db(
            reports: [Report("rptPlansByClient", recordSource: "qryPlansByClient")],
            queries:
            [
                Query("qryPlansByClient",
                    sql: "PARAMETERS [Enter Client ID] Long;\nSELECT * FROM tblPlans WHERE ClientID = [Enter Client ID]"),
            ],
            tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryPlansByClient");
        Assert.Equal(UsageCategory.Reporting, f.Classification);
        Assert.True(f.Confidence >= 0.8);                       // Q3, not Q5
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
    }

    [Fact]
    public void Form_Control_Reference_Query_Fires_ParameterizedInput()
    {
        // A query that reads a value from a control on an open form expects that
        // form to supply the parameter at run time.
        var db = Db(
            queries:
            [
                Query("qryByCriteria",
                    sql: "SELECT * FROM tblPlans WHERE ClientID = [Forms]![frmCriteria]![cboClient]"),
            ],
            tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryByCriteria");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains(ParamEvidence(f), e => e.StartsWith("form-control-reference:"));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
    }

    [Fact]
    public void Report_Control_Reference_Query_Fires_ParameterizedInput()
    {
        // A query that reads a value from a control on an open report expects that
        // report to supply the parameter at run time.
        var db = Db(
            queries:
            [
                Query("qryByReportCtl",
                    sql: "SELECT * FROM tblPlans WHERE FY = [Reports]![rptFiscal]![txtYear]"),
            ],
            tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryByReportCtl");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains(ParamEvidence(f), e => e.StartsWith("report-control-reference:"));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
    }

    [Fact]
    public void Odbc_Marker_Query_Fires_ParameterizedInput()
    {
        // An ODBC '?' parameter marker (pass-through / parameterized action) is
        // run-time input. It fires ParameterizedInput with odbc-parameter-marker
        // evidence; the legacy Q5 category path (which already treats '?' as a
        // marker) is unchanged.
        var db = Db(
            queries: [Query("qryOdbcByClient", sql: "SELECT * FROM tblPlans WHERE ClientID = ?")],
            tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryOdbcByClient");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains("odbc-parameter-marker", ParamEvidence(f));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
    }

    [Fact]
    public void Plain_Select_Query_Does_Not_Fire_ParameterizedInput()
    {
        // A non-parameterized select query carries no parameter evidence.
        var db = Db(
            queries: [Query("qryAllClients", sql: "SELECT * FROM tblClients ORDER BY LastName")],
            tables: [Table("tblClients", 10)]);

        var f = Classify(db, "qryAllClients");
        Assert.False(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.False(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
    }

    [Fact]
    public void Inline_Bracketed_Prompt_Query_Fires_Param_Signal_And_Keeps_OutOfBand()
    {
        // An undeclared inline prompt (no PARAMETERS clause) is still a run-time
        // parameter. It fires ParameterizedInput with bracketed-prompt evidence and,
        // because the two probe different things, ALSO keeps the Session 1
        // out-of-band question (the parameter signal does not suppress it).
        var db = Db(
            queries: [Query("qryByFiscalYear", sql: "SELECT * FROM tblPlans WHERE FY = [Enter Fiscal Year]")],
            tables: [Table("tblPlans", 8)]);

        var f = Classify(db, "qryByFiscalYear");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains("bracketed-prompt", ParamEvidence(f));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterizedQuery));
        Assert.True(HasQuestion(f, UncertaintySignal.OutOfBandEntryPossible));
    }

    // ── Parameter-dialog forms (rule F8) ─────────────────────────────────────────

    [Fact]
    public void Param_Dialog_Form_Is_Recognized_Not_Generic_Unknown()
    {
        // frmReportParam_Client: unbound, dialog chrome, OK/Cancel VBA. It must be
        // recognized as a parameter collector — ParameterizedInput fired, the
        // specific ParameterDialogFlow question, a parameter-dialog summary — and
        // must NOT get the generic NoRecordSource / UnrecognizedVbaPattern questions.
        var db = Db(forms:
        [
            Form("frmReportParam_Client", vbaCode: DialogVba, properties: DialogChrome()),
        ]);

        var f = Classify(db, "frmReportParam_Client");
        Assert.Equal(UsageCategory.Unknown, f.Classification);     // F8 keeps Unknown (contract)
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains("param-dialog-name-pattern", ParamEvidence(f));
        Assert.Contains("dialog-chrome", ParamEvidence(f));
        Assert.Contains("dialog", f.UsageSummary, System.StringComparison.OrdinalIgnoreCase);
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterDialogFlow));
        // The recognition replaces the generic unknown-form questions.
        Assert.False(HasQuestion(f, UncertaintySignal.NoRecordSource));
        Assert.False(HasQuestion(f, UncertaintySignal.UnrecognizedVbaPattern));
        // A parameter dialog is not a launcher.
        Assert.False(Fired(f, UsageSignalType.LauncherSurface));
    }

    [Fact]
    public void Param_Dialog_Opened_By_Report_Gets_OpenedAsDialog_Evidence()
    {
        // When the dialog is a harvested navigation target (a report opens it via an
        // OnOpen macro), that corroborates the recognition with opened-as-dialog.
        var db = Db(
            forms: [Form("frmReportParam_Client", vbaCode: DialogVba, properties: DialogChrome())],
            macros: [OpenFormMacro("mcrOpenClientParam", "frmReportParam_Client")]);

        var f = Classify(db, "frmReportParam_Client");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains("opened-as-dialog", ParamEvidence(f));
    }

    [Fact]
    public void Filter_Named_Form_Without_Chrome_Is_Still_Recognized()
    {
        // The name pattern alone (unbound) is enough for F8; chrome only corroborates.
        var db = Db(forms: [Form("frmServiceFilter", vbaCode: "Private Sub Form_Load()\nEnd Sub")]);

        var f = Classify(db, "frmServiceFilter");
        Assert.True(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.Contains("param-dialog-name-pattern", ParamEvidence(f));
        Assert.DoesNotContain("dialog-chrome", ParamEvidence(f));
        Assert.True(HasQuestion(f, UncertaintySignal.ParameterDialogFlow));
    }

    // ── Negative guards ──────────────────────────────────────────────────────────

    [Fact]
    public void Bound_Param_Named_Form_Stays_Data_Entry_Not_A_Param_Dialog()
    {
        // A bound form whose name contains "Param" classifies from its RecordSource;
        // the RecordSource branch wins before F8, so it is NOT a parameter dialog.
        var db = Db(
            forms: [Form("frmClientParameters", recordSource: "tblClients")],
            tables: [Table("tblClients", 10)]);

        var f = Classify(db, "frmClientParameters");
        Assert.NotEqual(UsageCategory.Unknown, f.Classification);
        Assert.False(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.False(HasQuestion(f, UncertaintySignal.ParameterDialogFlow));
    }

    [Fact]
    public void Launcher_Named_Form_Wins_Over_Param_Dialog()
    {
        // A form matching both a launcher pattern (Menu) and a param pattern (Filter)
        // is a launcher first (F5b before F8) — it must not be read as a param dialog.
        var db = Db(forms:
        [
            Form("frmFilterMenu", vbaCode: "Private Sub Form_Load()\nEnd Sub"),
        ]);

        var f = Classify(db, "frmFilterMenu");
        Assert.Equal(UsageCategory.NavigationShell, f.Classification);
        Assert.True(Fired(f, UsageSignalType.LauncherSurface));
        Assert.False(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.False(HasQuestion(f, UncertaintySignal.ParameterDialogFlow));
    }

    [Fact]
    public void Generic_Unknown_Form_Still_Gets_Generic_Questions()
    {
        // Suppression is specific to recognized parameter dialogs: a generic unbound
        // VBA-only form (no param name) keeps its NoRecordSource / UnrecognizedVbaPattern
        // questions and fires no ParameterizedInput.
        var db = Db(forms:
        [
            Form("frmAbout", vbaCode: "Private Sub Form_Load()\nEnd Sub"),
        ]);

        var f = Classify(db, "frmAbout");
        Assert.Equal(UsageCategory.Unknown, f.Classification);
        Assert.False(Fired(f, UsageSignalType.ParameterizedInput));
        Assert.False(HasQuestion(f, UncertaintySignal.ParameterDialogFlow));
        Assert.True(HasQuestion(f, UncertaintySignal.NoRecordSource));
        Assert.True(HasQuestion(f, UncertaintySignal.UnrecognizedVbaPattern));
    }
}
