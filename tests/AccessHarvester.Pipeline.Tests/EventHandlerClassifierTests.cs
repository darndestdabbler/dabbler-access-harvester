using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic unit tests for EventHandlerClassifier. Inputs are VBA
// procedure snippets parsed via VbaParserPipeline -> BuildAst, so the
// tests exercise both the classifier rules and the contract between
// the parser AST and the classifier visitor.
public class EventHandlerClassifierTests
{
    private static EventHandlerClassification ClassifyFirst(string source)
    {
        var module = VbaParserPipeline.Parse(source).BuildAst();
        var procedure = module.Items.OfType<ProcedureDeclaration>().First();
        return EventHandlerClassifier.Classify(procedure);
    }

    private static EventHandlerClassification Classify(string source, string procedureName)
    {
        var module = VbaParserPipeline.Parse(source).BuildAst();
        var procedure = module.Items
            .OfType<ProcedureDeclaration>()
            .First(p => p.Name == procedureName);
        return EventHandlerClassifier.Classify(procedure);
    }

    // ── Validation ────────────────────────────────────────────────────

    [Fact]
    public void Cancel_True_Top_Level_Classifies_As_Validation()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                Cancel = True
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Equal("Form_BeforeUpdate", result.ProcedureName);
        Assert.Single(result.Categories, EventHandlerClassificationCategory.Validation);
        Assert.False(result.Mixed);
        Assert.Single(result.Signals);
        Assert.Equal(EventHandlerSignalKind.CancelAssignment, result.Signals[0].Kind);
        Assert.Equal("Cancel", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Cancel_Inside_If_Still_Classifies_As_Validation()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtCustomerName) Then
                    Cancel = True
                End If
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.Validation, result.Categories);
        Assert.Single(result.Signals);
    }

    [Fact]
    public void Cancel_Is_Case_Insensitive()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                cancel = True
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.Validation, result.Categories);
        Assert.Equal(EventHandlerSignalKind.CancelAssignment, result.Signals[0].Kind);
    }

    // ── Calculation ───────────────────────────────────────────────────

    [Fact]
    public void Bare_Variable_Assignment_Classifies_As_Calculation()
    {
        var src = """
            Private Sub Detail_Format(Cancel As Integer, FormatCount As Integer)
                m_GroupTotal = m_GroupTotal + 1
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.Calculation);
        Assert.Equal(EventHandlerSignalKind.BareVariableAssignment, result.Signals[0].Kind);
        Assert.Equal("m_GroupTotal", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Set_To_Local_Variable_Classifies_As_Calculation()
    {
        var src = """
            Private Sub btnExit_Click()
                Dim db As DAO.Database
                Set db = CurrentDb
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.Calculation, result.Categories);
        Assert.Contains(result.Signals, s => s.Kind == EventHandlerSignalKind.BareVariableAssignment && s.TargetExpression == "db");
    }

    // ── CascadingUpdate ───────────────────────────────────────────────

    [Fact]
    public void Me_Dot_Control_Assignment_Classifies_As_CascadingUpdate()
    {
        var src = """
            Private Sub btnGo_Click()
                Me.txtFoo = "bar"
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.CascadingUpdate);
        Assert.Equal(EventHandlerSignalKind.ControlAssignment, result.Signals[0].Kind);
        Assert.Equal("Me.txtFoo", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Me_Bang_Control_RowSource_Classifies_As_CascadingUpdate()
    {
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                Me!cboClient.RowSource = "SELECT 1 FROM tblClients"
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.CascadingUpdate, result.Categories);
        Assert.Equal("Me!cboClient.RowSource", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Forms_Bang_Form_Bang_Control_Classifies_As_CascadingUpdate()
    {
        var src = """
            Private Sub btnGo_Click()
                Forms!frmDistrictReport!cboDistrict = 5
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.CascadingUpdate, result.Categories);
        Assert.Equal("Forms!frmDistrictReport!cboDistrict", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Control_Level_Requery_Classifies_As_CascadingUpdate()
    {
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                Me!cboClient.Requery
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.CascadingUpdate, result.Categories);
        Assert.Contains(result.Signals, s => s.Kind == EventHandlerSignalKind.ControlRequeryCall);
    }

    [Fact]
    public void Control_Set_To_Null_Classifies_As_CascadingUpdate()
    {
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                Me!cboClient = Null
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.CascadingUpdate, result.Categories);
        Assert.Equal("Me!cboClient", result.Signals[0].TargetExpression);
    }

    // ── FilterSearch ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Me.Filter")]
    [InlineData("Me.FilterOn")]
    [InlineData("Me.RecordSource")]
    [InlineData("Me.OrderBy")]
    [InlineData("Me.OrderByOn")]
    [InlineData("Me.ServerFilter")]
    [InlineData("Me.ServerFilterOn")]
    public void Filter_Family_Assignment_Classifies_As_FilterSearch(string lhs)
    {
        var src = $$"""
            Private Sub btnApply_Click()
                {{lhs}} = "1"
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.FilterSearch, result.Categories);
        Assert.Equal(EventHandlerSignalKind.FilterAssignment, result.Signals[0].Kind);
    }

    [Fact]
    public void Subform_Form_Filter_Classifies_As_FilterSearch()
    {
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                Me.sfrmOpenCases.Form.Filter = "CaseworkerID = 1"
                Me.sfrmOpenCases.Form.FilterOn = True
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.FilterSearch);
        Assert.Equal(2, result.Signals.Count);
        Assert.All(result.Signals, s => Assert.Equal(EventHandlerSignalKind.FilterAssignment, s.Kind));
    }

    [Fact]
    public void Bare_Me_Requery_Classifies_As_FormRequery()
    {
        var src = """
            Private Sub btnRefresh_Click()
                Me.Requery
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.FilterSearch);
        Assert.Equal(EventHandlerSignalKind.FormRequeryCall, result.Signals[0].Kind);
        Assert.Equal("Me.Requery", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Subform_Form_Requery_Classifies_As_SubformRequery()
    {
        var src = """
            Private Sub btnRefresh_Click()
                Me.sfrmOpenCases.Form.Requery
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.FilterSearch);
        Assert.Equal(EventHandlerSignalKind.SubformRequeryCall, result.Signals[0].Kind);
    }

    // ── Navigation ────────────────────────────────────────────────────

    [Theory]
    [InlineData("OpenForm \"frmFoo\"")]
    [InlineData("OpenReport \"rptBar\"")]
    [InlineData("OpenQuery \"qryBaz\"")]
    [InlineData("OpenTable \"tblQux\"")]
    [InlineData("Close acForm, \"frmFoo\"")]
    [InlineData("RunMacro \"mcrFoo\"")]
    [InlineData("RunCommand acCmdSave")]
    [InlineData("GoToRecord acDataForm, \"frmFoo\", acNext")]
    [InlineData("GoToControl \"txtFoo\"")]
    [InlineData("ApplyFilter \"qryFoo\", \"x = 1\"")]
    public void DoCmd_Navigation_Members_Classify_As_Navigation(string call)
    {
        var src = $$"""
            Private Sub btnGo_Click()
                DoCmd.{{call}}
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.Navigation);
        Assert.Equal(EventHandlerSignalKind.DoCmdNavigationCall, result.Signals[0].Kind);
        Assert.StartsWith("DoCmd.", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Application_Quit_Classifies_As_Navigation()
    {
        var src = """
            Private Sub btnExit_Click()
                Application.Quit acQuitSaveNone
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.Navigation);
        Assert.Equal(EventHandlerSignalKind.ApplicationQuitCall, result.Signals[0].Kind);
        Assert.Equal("Application.Quit", result.Signals[0].TargetExpression);
    }

    [Fact]
    public void Bare_FollowHyperlink_Classifies_As_Navigation()
    {
        var src = """
            Private Sub btnHelp_Click()
                FollowHyperlink "http://example.com/"
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.Navigation);
        Assert.Equal(EventHandlerSignalKind.FollowHyperlinkCall, result.Signals[0].Kind);
    }

    [Fact]
    public void Bare_RunCommand_Classifies_As_Navigation()
    {
        var src = """
            Private Sub btnSave_Click()
                RunCommand acCmdSave
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.Navigation);
        Assert.Equal(EventHandlerSignalKind.RunCommandCall, result.Signals[0].Kind);
    }

    // ── SideEffect ────────────────────────────────────────────────────

    [Fact]
    public void Db_Execute_Classifies_As_SideEffect()
    {
        var src = """
            Private Sub btnSave_Click()
                Dim db As DAO.Database
                Set db = CurrentDb
                db.Execute "UPDATE tblFoo SET x = 1", dbFailOnError
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.SideEffect, result.Categories);
        Assert.Contains(result.Signals, s => s.Kind == EventHandlerSignalKind.DatabaseExecuteCall);
    }

    [Fact]
    public void CurrentDb_Execute_Classifies_As_SideEffect()
    {
        var src = """
            Private Sub btnSave_Click()
                CurrentDb.Execute "DELETE FROM tblFoo"
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.SideEffect, result.Categories);
    }

    // ── FormattingDisplay ─────────────────────────────────────────────

    [Theory]
    [InlineData("Me.btnAdminTools.Visible", "= True")]
    [InlineData("Me.btnAdminTools.Enabled", "= False")]
    [InlineData("Me.lblHeader.Caption", "= \"x\"")]
    [InlineData("Me.txtFoo.ForeColor", "= 0")]
    [InlineData("Me.txtFoo.BackColor", "= 0")]
    [InlineData("Me.txtFoo.FontBold", "= True")]
    [InlineData("Me.txtFoo.FontItalic", "= True")]
    [InlineData("Me.txtFoo.FontSize", "= 10")]
    [InlineData("Me.txtFoo.Locked", "= True")]
    public void Appearance_Property_Assignments_Classify_As_FormattingDisplay(string lhs, string rhs)
    {
        var src = $$"""
            Private Sub Form_Load()
                {{lhs}} {{rhs}}
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.FormattingDisplay);
        Assert.Equal(EventHandlerSignalKind.AppearanceAssignment, result.Signals[0].Kind);
    }

    // ── Mixed handlers ────────────────────────────────────────────────

    [Fact]
    public void Cascading_Combo_Handler_Mixes_CascadingUpdate_And_FilterSearch()
    {
        // Drawn from fixture 02 cboCaseworker_AfterUpdate.
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                Me!cboClient.RowSource = "SELECT 1 FROM tblClients"
                Me!cboClient.Requery
                Me!cboClient = Null
                Me.txtCaseworkerEmail = Me!cboCaseworker.Column(2)
                Me.sfrmOpenCases.Form.Filter = "CaseworkerID = 1"
                Me.sfrmOpenCases.Form.FilterOn = True
                Call RecalculateCaseloadTotals(Me!cboCaseworker)
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Contains(EventHandlerClassificationCategory.CascadingUpdate, result.Categories);
        Assert.Contains(EventHandlerClassificationCategory.FilterSearch, result.Categories);
        Assert.True(result.Mixed);
        // First-appearance ordering: CascadingUpdate before FilterSearch.
        Assert.Equal(EventHandlerClassificationCategory.CascadingUpdate, result.Categories[0]);
        Assert.Equal(EventHandlerClassificationCategory.FilterSearch, result.Categories[1]);
    }

    [Fact]
    public void Report_Open_Mixes_Validation_FormattingDisplay_Calculation()
    {
        // Drawn from fixture 09 Report_Open.
        var src = """
            Private Sub Report_Open(Cancel As Integer)
                If Not CurrentProject.AllForms("frmDistrictReport").IsLoaded Then
                    Cancel = True
                    Exit Sub
                End If
                Me.Caption = "Caseload"
                m_GrandTotal = 0
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.True(result.Mixed);
        Assert.Contains(EventHandlerClassificationCategory.Validation, result.Categories);
        Assert.Contains(EventHandlerClassificationCategory.FormattingDisplay, result.Categories);
        Assert.Contains(EventHandlerClassificationCategory.Calculation, result.Categories);
    }

    [Fact]
    public void Switchboard_Exit_Mixes_Calculation_SideEffect_Navigation()
    {
        // Drawn from fixture 12 btnExit_Click.
        var src = """
            Private Sub btnExit_Click()
                Dim db As DAO.Database
                Set db = CurrentDb
                db.Execute "UPDATE tblSessionLog SET EndTime = Now()", dbFailOnError
                Set db = Nothing
                DoCmd.Close acForm, Me.Name
                Application.Quit acQuitSaveNone
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.True(result.Mixed);
        Assert.Contains(EventHandlerClassificationCategory.Calculation, result.Categories);
        Assert.Contains(EventHandlerClassificationCategory.SideEffect, result.Categories);
        Assert.Contains(EventHandlerClassificationCategory.Navigation, result.Categories);
    }

    [Fact]
    public void Pure_Validation_Handler_Is_Not_Mixed()
    {
        // Drawn from fixture 01 Form_BeforeUpdate, with MsgBox + SetFocus
        // included to confirm those produce no signals.
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtCustomerName) Or Me.txtCustomerName = "" Then
                    MsgBox "Customer Name is required.", vbExclamation
                    Cancel = True
                    Me.txtCustomerName.SetFocus
                    Exit Sub
                End If
                If IsNull(Me.txtPhone) Then
                    MsgBox "Phone Number is required.", vbExclamation
                    Cancel = True
                    Me.txtPhone.SetFocus
                End If
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.Validation);
        Assert.False(result.Mixed);
        Assert.Equal(2, result.Signals.Count);
        Assert.All(result.Signals, s => Assert.Equal(EventHandlerSignalKind.CancelAssignment, s.Kind));
    }

    // ── Silence semantics ─────────────────────────────────────────────

    [Fact]
    public void Empty_Body_Has_No_Signals_And_Is_Not_Mixed()
    {
        var src = """
            Private Sub Form_Load()
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Empty(result.Categories);
        Assert.Empty(result.Signals);
        Assert.False(result.Mixed);
    }

    [Fact]
    public void MsgBox_And_SetFocus_Produce_No_Signals()
    {
        var src = """
            Private Sub btnGo_Click()
                MsgBox "Hello"
                Me.txtFoo.SetFocus
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Empty(result.Categories);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void Helper_Procedure_Calls_Produce_No_Signals()
    {
        var src = """
            Private Sub btnGo_Click()
                Call RecalculateTotals(Me!cboFoo)
                ProcessRecord 1, 2
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Empty(result.Categories);
        Assert.Empty(result.Signals);
    }

    // ── Source-position threading ─────────────────────────────────────

    [Fact]
    public void Signal_Line_Matches_Statement_Provenance()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                Cancel = True
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Signals);
        // Statement is on the second line of the source (1-based).
        Assert.Equal(2, result.Signals[0].Line);
    }

    // ── With-block implicit-with handling ─────────────────────────────

    [Fact]
    public void With_Block_Filter_Assignment_Classifies_As_FilterSearch()
    {
        var src = """
            Private Sub btnApply_Click()
                With Me
                    .Filter = "x = 1"
                    .FilterOn = True
                End With
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.FilterSearch);
        Assert.Equal(2, result.Signals.Count);
        Assert.All(result.Signals, s => Assert.Equal(EventHandlerSignalKind.FilterAssignment, s.Kind));
    }

    [Fact]
    public void With_Block_Visible_Assignment_Classifies_As_FormattingDisplay()
    {
        var src = """
            Private Sub Form_Load()
                With Me.btnAdminTools
                    .Visible = True
                    .Caption = "Admin"
                End With
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.FormattingDisplay);
        Assert.Equal(2, result.Signals.Count);
        Assert.All(result.Signals, s => Assert.Equal(EventHandlerSignalKind.AppearanceAssignment, s.Kind));
    }

    [Fact]
    public void With_Block_RowSource_Assignment_Classifies_As_CascadingUpdate()
    {
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                With Me!cboClient
                    .RowSource = "SELECT 1 FROM tblClients"
                End With
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.CascadingUpdate);
        Assert.Equal(EventHandlerSignalKind.ControlAssignment, result.Signals[0].Kind);
    }

    [Fact]
    public void With_Block_Execute_Call_Classifies_As_SideEffect()
    {
        // .Execute is receiver-independent — anything.Execute is a
        // database side effect. Classification works regardless of
        // whether the With target is db, CurrentDb, or a recordset.
        var src = """
            Private Sub btnSave_Click()
                With CurrentDb
                    .Execute "DELETE FROM tblFoo", dbFailOnError
                End With
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.SideEffect);
        Assert.Equal(EventHandlerSignalKind.DatabaseExecuteCall, result.Signals[0].Kind);
    }

    [Fact]
    public void With_Block_Bare_Requery_Emits_No_Signal_When_Receiver_Is_Implicit()
    {
        // Per project-guidance.md -> VBA Parser Strategy
        // ("Unresolvable constructs are explicit, not silent"), the
        // classifier prefers to emit nothing when a With-block
        // `.Requery` carries an implicit receiver it cannot resolve
        // into form / subform / control without the resolver. A future
        // session that threads With-target through the visitor can
        // promote this to a definite category.
        var src = """
            Private Sub btnRefresh_Click()
                With Me
                    .Requery
                End With
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Empty(result.Signals);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public void With_Block_Bare_Bang_Member_Assignment_Classifies_As_CascadingUpdate()
    {
        // `.!Foo` ImplicitWithBang appears when the With target is an
        // intermediate object whose default property is being indexed
        // by name. Less common than `.Foo` but the classifier handles
        // it the same way.
        var src = """
            Private Sub Form_Load()
                With Me
                    !cboClient = Null
                End With
            End Sub
            """;
        var result = ClassifyFirst(src);

        Assert.Single(result.Categories, EventHandlerClassificationCategory.CascadingUpdate);
        Assert.Equal(EventHandlerSignalKind.ControlAssignment, result.Signals[0].Kind);
    }

    // ── Categories ordering ───────────────────────────────────────────

    [Fact]
    public void Categories_Are_Ordered_By_First_Appearance_And_Deduplicated()
    {
        var src = """
            Private Sub btnGo_Click()
                Cancel = True
                Me.txtFoo = 1
                Cancel = False
                Me.btnA.Visible = True
                Me.txtFoo = 2
            End Sub
            """;
        var result = ClassifyFirst(src);

        // Three distinct categories, in first-appearance order.
        Assert.Equal(new[]
        {
            EventHandlerClassificationCategory.Validation,
            EventHandlerClassificationCategory.CascadingUpdate,
            EventHandlerClassificationCategory.FormattingDisplay,
        }, result.Categories);
        Assert.True(result.Mixed);
        Assert.Equal(5, result.Signals.Count);
    }
}
