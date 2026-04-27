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

// End-to-end tests that drive ReferenceResolver with a populated
// AccessObjectModelRegistry. Confirms that the SubChildLookup hook
// added in Session 1 routes member access through the registry and
// produces ResolvedToObjectModel outcomes for synthetic targets.
public sealed class ReferenceResolverAccessIntegrationTests
{
    private static AccessControl Section(string name, params AccessControl[] children)
        => new() { Type = "Section", Name = name, Children = children };

    private static AccessControl Control(string type, string name,
        IReadOnlyDictionary<string, JsonElement>? properties = null)
        => new() { Type = type, Name = name, Properties = properties };

    private static IReadOnlyDictionary<string, JsonElement> StringProps(string key, string value)
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

    private static ClassDeclaration FormClass(string name)
        => new(
            name: name,
            provenance: new SourceDeclarationProvenance(
                new SourceRange(new SourceLocation("", 0, 0), new SourceLocation("", 0, 1))),
            kind: ClassDeclarationKind.FormModule,
            implementedInterfaces: Array.Empty<TypeReference>());

    // ── Forms collection top-level ─────────────────────────────────

    [Fact]
    public void Identifier_Forms_ResolvesToCollectionDecl()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("frmCustomers") },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Sub Demo()
    Dim f As Object
    Set f = Forms
End Sub";
        var (ast, scope) = BuildModule(src, "modDemo");
        var refs = Resolve(ast, scope, registry);

        var formsUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "Forms");
        Assert.NotNull(formsUse);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(formsUse!.Outcome);
        Assert.IsType<SyntheticFormsCollectionDeclaration>(outcome.Declaration);
    }

    [Fact]
    public void Forms_BangFormName_ResolvesToFormDecl()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("frmCustomers") },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Sub Demo()
    Forms!frmCustomers.Refresh
End Sub";
        var (ast, scope) = BuildModule(src, "modDemo");
        var refs = Resolve(ast, scope, registry);

        // The Forms!frmCustomers BangAccessExpression should resolve
        // to the form's synthetic declaration.
        var bang = refs.FirstOrDefault(r =>
            r.Expression is BangAccessExpression);
        Assert.NotNull(bang);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(bang!.Outcome);
        var formDecl = Assert.IsType<SyntheticFormDeclaration>(outcome.Declaration);
        Assert.Equal("frmCustomers", formDecl.FormName);
    }

    // ── Me.<control> in a form module ──────────────────────────────

    [Fact]
    public void MeDotControl_InFormModule_ResolvesToControlDecl()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "txtFirstName")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    Me.txtFirstName = """"
End Sub";
        var owningClass = FormClass("Form_Customers");
        var (ast, scope) = BuildModule(src, "Form_Customers", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        var memberAccess = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression);
        Assert.NotNull(memberAccess);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(memberAccess!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(outcome.Declaration);
        Assert.Equal("txtFirstName", ctl.ControlName);
        Assert.Equal("Access.TextBox", ctl.TypeLibraryTypeName);
    }

    [Fact]
    public void MeDotControl_DotValue_ResolvesViaTypeLibrary()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "txtName")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    Dim s As String
    s = Me.txtName.Value
End Sub";
        var owningClass = FormClass("Form_Customers");
        var (ast, scope) = BuildModule(src, "Form_Customers", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        // The chain emits 3 member-access entries: Me, Me.txtName,
        // Me.txtName.Value. The outermost (.Value) should be a
        // ResolvedToObjectModel pointing at the type-library member.
        var valueAccess = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "Value");
        Assert.NotNull(valueAccess);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(valueAccess!.Outcome);
        var member = Assert.IsType<SyntheticTypeLibraryMemberDeclaration>(outcome.Declaration);
        Assert.Equal("Value", member.Member.Name);
        Assert.True(member.Member.IsDefault);
    }

    [Fact]
    public void MeDotSubform_DotForm_ResolvesViaSubformNavigation()
    {
        var registry = new AccessObjectModelRegistry(
            forms: new[]
            {
                Form("Cases", "Form_Cases",
                    Control("Subform", "sub", StringProps("SourceObject", "frmChild"))),
                Form("frmChild", null, Control("TextBox", "txtChild")),
            },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    Dim x As Object
    Set x = Me.sub.Form
End Sub";
        var owningClass = FormClass("Form_Cases");
        var (ast, scope) = BuildModule(src, "Form_Cases", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        var formMember = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "Form");
        Assert.NotNull(formMember);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(formMember!.Outcome);
        var formDecl = Assert.IsType<SyntheticFormDeclaration>(outcome.Declaration);
        Assert.Equal("frmChild", formDecl.FormName);
    }

    [Fact]
    public void NullRegistry_UnresolvedTarget_StillEmitsMemberAccessOnUnresolved()
    {
        // Non-regression: when the target is unresolved, member access
        // emits MemberAccessOnUnresolvedTarget — this path never reaches
        // SubChildLookup, so the null-registry behaviour matches the
        // pre-Session-1 baseline.
        const string src = @"
Sub Demo()
    Dim x As Object
    Set x = SomeObject.Foo
End Sub";
        var (ast, scope) = BuildModule(src, "modDemo");
        var refs = new ReferenceResolver().ResolveModule(scope, ast);

        var fooAccess = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "Foo");
        Assert.NotNull(fooAccess);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(fooAccess!.Outcome);
        Assert.Equal(UnresolvedReasons.MemberAccessOnUnresolvedTarget, u.Reason);
    }

    [Fact]
    public void NullRegistry_ResolvedTargetMember_FallsToDefaultMemberNotYetResolved()
    {
        // The actual SubChildLookup path: target resolves to a
        // source-defined declaration (`Dim x`). With the null registry,
        // TryResolveMemberOf returns null, and the resolver emits the
        // default-member-not-yet-resolved reason — the same contract
        // it had before the interface gained TryResolveMemberOf.
        const string src = @"
Sub Demo()
    Dim x As Object
    x.Foo = 1
End Sub";
        var (ast, scope) = BuildModule(src, "modDemo");
        var refs = new ReferenceResolver().ResolveModule(scope, ast);

        var fooMember = refs.FirstOrDefault(r =>
            r.Expression is MemberAccessExpression ma && ma.MemberName == "Foo");
        Assert.NotNull(fooMember);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(fooMember!.Outcome);
        Assert.Equal(UnresolvedReasons.DefaultMemberNotYetResolved, u.Reason);
    }

    [Fact]
    public void BareControl_InFormModule_ResolvesViaRegistry()
    {
        // Verifier round-1 Major finding: VBA allows bare control
        // identifiers in a form module (`txtFirstName = ""` is the same
        // as `Me.txtFirstName = ""`). ResolveIdentifier consults the
        // form's control registry between scope-chain and top-level
        // lookup, so the bare reference resolves to the synthetic
        // control declaration.
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "txtFirstName")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    txtFirstName = """"
End Sub";
        var owningClass = FormClass("Form_Customers");
        var (ast, scope) = BuildModule(src, "Form_Customers", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        var bareUse = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "txtFirstName");
        Assert.NotNull(bareUse);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(bareUse!.Outcome);
        var ctl = Assert.IsType<SyntheticControlDeclaration>(outcome.Declaration);
        Assert.Equal("txtFirstName", ctl.ControlName);
    }

    [Fact]
    public void BareControl_LocalVariableShadowsControl()
    {
        // Locals shadow controls (Step 1 of ResolveIdentifier wins).
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "txtFirstName")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    Dim txtFirstName As String
    txtFirstName = ""local""
End Sub";
        var owningClass = FormClass("Form_Customers");
        var (ast, scope) = BuildModule(src, "Form_Customers", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        var assigned = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "txtFirstName"
            && r.EnclosingScope is ProcedureScope);
        Assert.NotNull(assigned);
        // The local Dim wins over the form's control declaration.
        var resolved = Assert.IsType<ResolutionOutcome.Resolved>(assigned!.Outcome);
        Assert.IsType<VariableDeclaration>(resolved.Declaration);
    }

    [Fact]
    public void BareControl_ShadowsTopLevelForms()
    {
        // Pins ResolveIdentifier's Step-2-over-Step-3 precedence:
        // a control named `Forms` on the current form's class must
        // resolve to the synthetic control declaration, not to the
        // top-level Forms collection.
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "Forms")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    Forms = """"
End Sub";
        var owningClass = FormClass("Form_Customers");
        var (ast, scope) = BuildModule(src, "Form_Customers", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        var use = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "Forms");
        Assert.NotNull(use);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(use!.Outcome);
        // Step 2 wins: synthetic control, not the Forms collection.
        Assert.IsType<SyntheticControlDeclaration>(outcome.Declaration);
    }

    [Fact]
    public void BareControl_ShadowsBuiltInFunction()
    {
        // Pins ResolveIdentifier's Step-2-over-Step-4 precedence:
        // a control named `Format` (a real VBA built-in) on the form
        // must resolve to the control, not to the built-in function.
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "Format")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Private Sub Form_Load()
    Dim s As String
    s = Format
End Sub";
        var owningClass = FormClass("Form_Customers");
        var (ast, scope) = BuildModule(src, "Form_Customers", ModuleKind.Form, owningClass);
        var refs = Resolve(ast, scope, registry);

        // The relevant `Format` use is the rhs of the assignment in
        // procedure scope (not the parameter or anything else).
        var use = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "Format"
            && r.EnclosingScope is ProcedureScope);
        Assert.NotNull(use);
        var outcome = Assert.IsType<ResolutionOutcome.ResolvedToObjectModel>(use!.Outcome);
        Assert.IsType<SyntheticControlDeclaration>(outcome.Declaration);
    }

    [Fact]
    public void BareControl_StandardModule_DoesNotResolveAsControl()
    {
        // Non-form/non-report modules have no class context, so the
        // control-context lookup must NOT fire — bare `txtFirstName` in
        // a standard module remains NotInScope even with a registry.
        var registry = new AccessObjectModelRegistry(
            forms: new[] { Form("Customers", "Form_Customers", Control("TextBox", "txtFirstName")) },
            reports: Array.Empty<ReportMetadata>(),
            typeLibrary: new TypeLibraryRegistry());
        const string src = @"
Sub Demo()
    txtFirstName = """"
End Sub";
        var (ast, scope) = BuildModule(src, "modDemo");
        var refs = Resolve(ast, scope, registry);

        var use = refs.FirstOrDefault(r =>
            r.Expression is IdentifierExpression id && id.Name == "txtFirstName");
        Assert.NotNull(use);
        var u = Assert.IsType<ResolutionOutcome.Unresolved>(use!.Outcome);
        Assert.Equal(UnresolvedReasons.NotInScope, u.Reason);
    }
}
