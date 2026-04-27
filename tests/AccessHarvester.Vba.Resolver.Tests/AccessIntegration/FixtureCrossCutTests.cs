using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.Declarations;
using AccessHarvester.Vba.Resolver.References;
using AccessHarvester.Vba.Resolver.Scopes;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.AccessIntegration;

// Cross-cut fixture tests that drive ReferenceResolver against VBA code
// drawn from docs/test-cases/ (01, 02, 03) with an AccessObjectModelRegistry
// populated from the matching form metadata. These tests are the Session 2
// "run resolver against fixtures with synthetic declarations injected" step:
// they confirm that control references that were Unresolved.NotInScope before
// Session 1 now produce ResolvedToObjectModel outcomes.
//
// All tests are hermetic (no Access COM required). Form metadata is built
// in-process to match each test case's hostContext declaration.
public sealed class FixtureCrossCutTests
{
    // ── Metadata builders (shared with ReferenceResolverAccessIntegrationTests) ──

    private static AccessControl Section(string name, params AccessControl[] children)
        => new() { Type = "Section", Name = name, Children = children };

    private static AccessControl Control(string type, string name,
        IReadOnlyDictionary<string, JsonElement>? properties = null)
        => new() { Type = type, Name = name, Properties = properties };

    private static IReadOnlyDictionary<string, JsonElement> StringProp(string key, string value)
    {
        var dict = new Dictionary<string, JsonElement>();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
        dict[key] = doc.RootElement.Clone();
        return dict;
    }

    private static FormMetadata Form(string name, string? moduleName = null,
        params AccessControl[] children)
        => new()
        {
            Name = name,
            ModuleName = moduleName,
            Children = children.Length == 0 ? null : new[] { Section("Detail", children) },
        };

    private static ClassDeclaration FormClass(string name)
        => new(
            name: name,
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            kind: ClassDeclarationKind.FormModule,
            implementedInterfaces: Array.Empty<TypeReference>());

    private static (Module ast, ModuleScope scope) BuildModule(
        string source, string moduleName,
        ModuleKind kind = ModuleKind.Standard,
        ClassDeclaration? owningClass = null)
    {
        var parsed = VbaParserPipeline.Parse(source);
        Module ast = parsed.BuildAst(moduleName);
        var project = new ProjectScope();
        var visitor = new ScopeBuildingVisitor();
        var scope = visitor.BuildModuleScope(project, ast, moduleName, kind, owningClass);
        return (ast, scope);
    }

    private static IReadOnlyList<ResolvedReference> Resolve(
        Module ast, ModuleScope scope, IAccessObjectModelRegistry registry)
        => new ReferenceResolver(registry).ResolveModule(scope, ast);

    // ── Fixture 01: Simple validation handler ──────────────────────────────
    // Form: frmCustomers  Module: Form_frmCustomers
    // Controls: txtCustomerName (TextBox), txtPhone (TextBox)
    // VBA: Me.txtCustomerName and Me.txtPhone in a BeforeUpdate handler.
    //
    // Before Session 1 these resolved to Unresolved.NotInScope because the
    // resolver had no knowledge of the form's controls. After Session 1 the
    // AccessObjectModelRegistry is populated and Me.<control> resolves via
    // MeRegistryLookup → TryResolveControlOnForm.

    private const string Fixture01Vba = @"
Private Sub Form_BeforeUpdate(Cancel As Integer)
    If IsNull(Me.txtCustomerName) Or Me.txtCustomerName = """" Then
        MsgBox ""Customer Name is required."", vbExclamation
        Cancel = True
        Me.txtCustomerName.SetFocus
        Exit Sub
    End If
    If IsNull(Me.txtPhone) Then
        MsgBox ""Phone Number is required."", vbExclamation
        Cancel = True
        Me.txtPhone.SetFocus
    End If
End Sub";

    [Fact]
    public void Fixture01_MeDotTxtCustomerName_ResolvesToSyntheticTextBoxControl()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("frmCustomers", "Form_frmCustomers",
                Control("TextBox", "txtCustomerName"),
                Control("TextBox", "txtPhone")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());

        var owning = FormClass("Form_frmCustomers");
        var (ast, scope) = BuildModule(Fixture01Vba, "Form_frmCustomers", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        // The first Me.txtCustomerName member-access should resolve to the
        // synthetic TextBox control declaration.
        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "txtCustomerName");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("txtCustomerName", ctl.ControlName);
        Assert.Equal("Access.TextBox", ctl.TypeLibraryTypeName);
    }

    [Fact]
    public void Fixture01_MeDotTxtPhone_ResolvesToSyntheticTextBoxControl()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("frmCustomers", "Form_frmCustomers",
                Control("TextBox", "txtCustomerName"),
                Control("TextBox", "txtPhone")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());

        var owning = FormClass("Form_frmCustomers");
        var (ast, scope) = BuildModule(Fixture01Vba, "Form_frmCustomers", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "txtPhone");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("txtPhone", ctl.ControlName);
        Assert.Equal("Access.TextBox", ctl.TypeLibraryTypeName);
    }

    [Fact]
    public void Fixture01_WithoutRegistry_MeDotControl_WasUnresolved()
    {
        // Regression guard: confirms that the same VBA with a null registry
        // produces a DefaultMemberNotYetResolved outcome, establishing what
        // "previously unresolved" means for this fixture.
        var owning = FormClass("Form_frmCustomers");
        var (ast, scope) = BuildModule(Fixture01Vba, "Form_frmCustomers", ModuleKind.Form, owning);
        var refs = new ReferenceResolver().ResolveModule(scope, ast);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "txtCustomerName");
        Assert.NotNull(hit);
        // Without the registry there is no MeRegistryLookup hit, so the
        // outcome falls to the default-member-not-yet-resolved path.
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(hit!.Outcome);
        Assert.Equal(UnresolvedReasons.DefaultMemberNotYetResolved, u.Reason);
    }

    // ── Fixture 02: Cascading combo with subform filtering ─────────────────
    // Form: frmCaseManagement  Module: Form_frmCaseManagement
    // Controls: cboCaseworker (ComboBox), cboClient (ComboBox),
    //           txtCaseworkerEmail (TextBox), txtCaseworkerPhone (TextBox),
    //           sfrmOpenCases (Subform → sfrmOpenCases form)
    // VBA: Me!cboClient bang-access, Me.sfrmOpenCases.Form subform navigation.

    private const string Fixture02Vba = @"
Private Sub cboCaseworker_AfterUpdate()
    Me!cboClient.RowSource = ""SELECT ClientID, ClientName FROM tblClients "" & _
                             ""WHERE AssignedCaseworkerID = "" & Me!cboCaseworker & _
                             "" ORDER BY ClientName""
    Me!cboClient.Requery
    Me!cboClient = Null
    Me.txtCaseworkerEmail = Me!cboCaseworker.Column(2)
    Me.txtCaseworkerPhone = Me!cboCaseworker.Column(3)
    Me.sfrmOpenCases.Form.Filter = ""CaseworkerID = "" & Me!cboCaseworker & _
                                   "" AND CaseStatus IN ('Open', 'Pending Review')""
    Me.sfrmOpenCases.Form.FilterOn = True
    Call RecalculateCaseloadTotals(Me!cboCaseworker)
End Sub";

    private static AccessObjectModelRegistry BuildFixture02Registry()
    {
        var subformControl = Control("Subform", "sfrmOpenCases",
            StringProp("SourceObject", "sfrmOpenCases"));
        var mainForm = Form("frmCaseManagement", "Form_frmCaseManagement",
            Control("ComboBox", "cboCaseworker"),
            Control("ComboBox", "cboClient"),
            Control("TextBox", "txtCaseworkerEmail"),
            Control("TextBox", "txtCaseworkerPhone"),
            subformControl);
        var containedForm = Form("sfrmOpenCases", null);

        return new AccessObjectModelRegistry(
            forms: new[] { mainForm, containedForm },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
    }

    [Fact]
    public void Fixture02_MeBangCboClient_ResolvesToSyntheticComboBoxControl()
    {
        var registry = BuildFixture02Registry();
        var owning = FormClass("Form_frmCaseManagement");
        var (ast, scope) = BuildModule(Fixture02Vba, "Form_frmCaseManagement", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        // Me!cboClient is a BangAccessExpression; the first occurrence is
        // the RowSource assignment target.
        var hit = refs.FirstOrDefault(r =>
            r.Expression is BangAccessExpression ba && ba.MemberName == "cboClient");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("cboClient", ctl.ControlName);
        Assert.Equal("Access.ComboBox", ctl.TypeLibraryTypeName);
    }

    [Fact]
    public void Fixture02_MeBangCboCaseworker_ResolvesToSyntheticComboBoxControl()
    {
        var registry = BuildFixture02Registry();
        var owning = FormClass("Form_frmCaseManagement");
        var (ast, scope) = BuildModule(Fixture02Vba, "Form_frmCaseManagement", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is BangAccessExpression ba && ba.MemberName == "cboCaseworker");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("cboCaseworker", ctl.ControlName);
        Assert.Equal("Access.ComboBox", ctl.TypeLibraryTypeName);
    }

    [Fact]
    public void Fixture02_MeDotSfrmOpenCasesFormFilter_SubformNavigationResolvesToFormDecl()
    {
        var registry = BuildFixture02Registry();
        var owning = FormClass("Form_frmCaseManagement");
        var (ast, scope) = BuildModule(Fixture02Vba, "Form_frmCaseManagement", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        // Me.sfrmOpenCases → SyntheticSubformControlDeclaration
        var subformHit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "sfrmOpenCases");
        Assert.NotNull(subformHit);
        var subResolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(subformHit!.Outcome);
        Assert.IsType<SyntheticSubformControlDeclaration>(subResolved.Declaration);

        // .Form on the subform control → SyntheticFormDeclaration("sfrmOpenCases")
        var formHit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "Form");
        Assert.NotNull(formHit);
        var formResolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(formHit!.Outcome);
        var formDecl = Assert.IsType<SyntheticFormDeclaration>(formResolved.Declaration);
        Assert.Equal("sfrmOpenCases", formDecl.FormName);
    }

    [Fact]
    public void Fixture02_MeDotTxtCaseworkerEmail_ResolvesToSyntheticTextBoxControl()
    {
        var registry = BuildFixture02Registry();
        var owning = FormClass("Form_frmCaseManagement");
        var (ast, scope) = BuildModule(Fixture02Vba, "Form_frmCaseManagement", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "txtCaseworkerEmail");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("txtCaseworkerEmail", ctl.ControlName);
    }

    [Fact]
    public void Fixture02_MeDotTxtCaseworkerPhone_ResolvesToSyntheticTextBoxControl()
    {
        var registry = BuildFixture02Registry();
        var owning = FormClass("Form_frmCaseManagement");
        var (ast, scope) = BuildModule(Fixture02Vba, "Form_frmCaseManagement", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "txtCaseworkerPhone");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("txtCaseworkerPhone", ctl.ControlName);
        Assert.Equal("Access.TextBox", ctl.TypeLibraryTypeName);
    }

    // ── Fixture 03: With block flattening + shadowing ──────────────────────
    // Form: frmPermitApplication  Module: Form_frmPermitApplication
    // Controls: txtPermitID (TextBox), sfrmInspections (Subform → frmInspections),
    //           lblStatus (Label)
    // VBA: Me!txtPermitID bang-access, Me.sfrmInspections.Form subform navigation
    //      inside a nested With block.

    private const string Fixture03Vba = @"
Private Sub UpdatePermitStatus()
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Dim sStatus As String
    Set db = CurrentDb()
    Set rs = db.OpenRecordset(""SELECT * FROM tblPermits WHERE PermitID = "" & _
                              Me!txtPermitID, dbOpenDynaset)
    With rs
        If Not (.BOF And .EOF) Then
            .Edit
            !LastReviewedBy = CurrentUser()
            !LastReviewedDate = Now()
            If DLookup(""OutstandingBalance"", ""qryApplicantBalances"", _
                       ""ApplicantID = "" & !ApplicantID) > 0 Then
                !Status = ""Hold - Balance Due""
                sStatus = ""Hold""
            Else
                With Me.sfrmInspections.Form
                    If .RecordsetClone.RecordCount = 0 Then
                        !Status = ""Pending Inspection""
                        sStatus = ""Pending""
                    ElseIf DCount(""*"", ""tblInspections"", _
                                  ""PermitID = "" & Me!txtPermitID & _
                                  "" AND Result = 'Failed'"") > 0 Then
                        !Status = ""Failed Inspection""
                        sStatus = ""Failed""
                    Else
                        !Status = ""Approved""
                        sStatus = ""Approved""
                    End If
                End With
            End If
            .Update
        End If
        .Close
    End With
    Set rs = Nothing
    Set db = Nothing
    Me.lblStatus.Caption = ""Status: "" & sStatus
End Sub";

    private static AccessObjectModelRegistry BuildFixture03Registry()
    {
        var subformControl = Control("Subform", "sfrmInspections",
            StringProp("SourceObject", "frmInspections"));
        var mainForm = Form("frmPermitApplication", "Form_frmPermitApplication",
            Control("TextBox", "txtPermitID"),
            subformControl,
            Control("Label", "lblStatus"));
        var containedForm = Form("frmInspections", null);

        return new AccessObjectModelRegistry(
            forms: new[] { mainForm, containedForm },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
    }

    [Fact]
    public void Fixture03_MeBangTxtPermitID_ResolvesToSyntheticTextBoxControl()
    {
        var registry = BuildFixture03Registry();
        var owning = FormClass("Form_frmPermitApplication");
        var (ast, scope) = BuildModule(Fixture03Vba, "Form_frmPermitApplication", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        var hit = refs.FirstOrDefault(r =>
            r.Expression is BangAccessExpression ba && ba.MemberName == "txtPermitID");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("txtPermitID", ctl.ControlName);
        Assert.Equal("Access.TextBox", ctl.TypeLibraryTypeName);
    }

    [Fact]
    public void Fixture03_MeDotSfrmInspections_ResolvesToSubformControlDeclaration()
    {
        var registry = BuildFixture03Registry();
        var owning = FormClass("Form_frmPermitApplication");
        var (ast, scope) = BuildModule(Fixture03Vba, "Form_frmPermitApplication", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        // Me.sfrmInspections → SyntheticSubformControlDeclaration
        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "sfrmInspections");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var sub = Assert.IsType<SyntheticSubformControlDeclaration>(resolved.Declaration);
        Assert.Equal("sfrmInspections", sub.ControlName);
        Assert.Equal("frmInspections", sub.ContainedFormName);
    }

    [Fact]
    public void Fixture03_MeDotSfrmInspectionsDotForm_ResolvesToContainedFormDeclaration()
    {
        var registry = BuildFixture03Registry();
        var owning = FormClass("Form_frmPermitApplication");
        var (ast, scope) = BuildModule(Fixture03Vba, "Form_frmPermitApplication", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        // Me.sfrmInspections.Form → SyntheticFormDeclaration("frmInspections")
        // This is the With target in the nested With block.
        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "Form");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var formDecl = Assert.IsType<SyntheticFormDeclaration>(resolved.Declaration);
        Assert.Equal("frmInspections", formDecl.FormName);
    }

    [Fact]
    public void Fixture03_MeDotLblStatusCaption_LblStatusResolvesToSyntheticLabelControl()
    {
        var registry = BuildFixture03Registry();
        var owning = FormClass("Form_frmPermitApplication");
        var (ast, scope) = BuildModule(Fixture03Vba, "Form_frmPermitApplication", ModuleKind.Form, owning);
        var refs = Resolve(ast, scope, registry);

        // Me.lblStatus → SyntheticControlDeclaration("lblStatus", "Label", "Access.Label")
        var hit = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "lblStatus");
        Assert.NotNull(hit);
        var resolved = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(hit!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(resolved.Declaration);
        Assert.Equal("lblStatus", ctl.ControlName);
        Assert.Equal("Access.Label", ctl.TypeLibraryTypeName);
    }
}
