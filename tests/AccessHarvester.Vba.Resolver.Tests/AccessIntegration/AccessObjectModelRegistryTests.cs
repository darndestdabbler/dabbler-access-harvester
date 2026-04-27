using System;
using System.Collections.Generic;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using AccessHarvester.Vba.Resolver.Declarations;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.AccessIntegration;

public sealed class AccessObjectModelRegistryTests
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

    private static ReportMetadata Report(string name, string? moduleName = null,
        params AccessControl[] children)
        => new()
        {
            Name = name,
            ModuleName = moduleName,
            Children = children.Length == 0 ? null : new[] { Section("Detail", children) },
        };

    private static AccessObjectModelRegistry BuildRegistry(
        FormMetadata[]? forms = null,
        ReportMetadata[]? reports = null,
        ITypeLibraryRegistry? typeLib = null)
        => new(
            forms ?? Array.Empty<FormMetadata>(),
            reports ?? Array.Empty<ReportMetadata>(),
            typeLib ?? new TypeLibraryRegistry());

    // ── TryResolveTopLevelMember ───────────────────────────────────

    [Fact]
    public void TopLevel_FormsCollection_Resolves()
    {
        var reg = BuildRegistry(forms: new[] { Form("frmCustomers") });
        var hit = reg.TryResolveTopLevelMember("Forms");
        var coll = Assert.IsType<SyntheticFormsCollectionDeclaration>(hit);
        Assert.Equal(SyntheticHostCollectionKind.Forms, coll.Kind);
    }

    [Fact]
    public void TopLevel_ReportsCollection_Resolves()
    {
        var reg = BuildRegistry();
        var hit = reg.TryResolveTopLevelMember("Reports");
        var coll = Assert.IsType<SyntheticFormsCollectionDeclaration>(hit);
        Assert.Equal(SyntheticHostCollectionKind.Reports, coll.Kind);
    }

    [Fact]
    public void TopLevel_FormsCollection_IsCachedSingleton()
    {
        var reg = BuildRegistry();
        var a = reg.TryResolveTopLevelMember("Forms");
        var b = reg.TryResolveTopLevelMember("forms");  // case-insensitive
        Assert.Same(a, b);
    }

    [Fact]
    public void TopLevel_BareFormName_Resolves()
    {
        var reg = BuildRegistry(forms: new[] { Form("frmCustomers") });
        var hit = reg.TryResolveTopLevelMember("frmCustomers");
        Assert.IsType<SyntheticFormDeclaration>(hit);
    }

    [Fact]
    public void TopLevel_BareReportName_Resolves()
    {
        var reg = BuildRegistry(reports: new[] { Report("rptInvoice") });
        var hit = reg.TryResolveTopLevelMember("rptInvoice");
        Assert.IsType<SyntheticReportDeclaration>(hit);
    }

    [Fact]
    public void TopLevel_UnknownName_ReturnsNull()
    {
        var reg = BuildRegistry();
        Assert.Null(reg.TryResolveTopLevelMember("DoCmd"));
        Assert.Null(reg.TryResolveTopLevelMember(""));
    }

    // ── TryResolveControlOnForm ────────────────────────────────────

    [Fact]
    public void ControlOnForm_ByModuleName_Resolves()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("Customers", moduleName: "Form_Customers",
                Control("TextBox", "txtFirstName"))
        });

        // The resolver passes the OwningClass.Name, which is the module name
        var hit = reg.TryResolveControlOnForm("Form_Customers", "txtFirstName");
        Assert.IsType<SyntheticControlDeclaration>(hit);
    }

    [Fact]
    public void ControlOnForm_ByBareFormName_Resolves()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frmCustomers", children: Control("TextBox", "txtName"))
        });

        var hit = reg.TryResolveControlOnForm("frmCustomers", "txtName");
        Assert.IsType<SyntheticControlDeclaration>(hit);
    }

    [Fact]
    public void ControlOnForm_CaseInsensitive()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frmCustomers", children: Control("TextBox", "txtCustomerName"))
        });

        Assert.NotNull(reg.TryResolveControlOnForm("FRMCUSTOMERS", "txtcustomername"));
    }

    [Fact]
    public void ControlOnForm_UnknownForm_ReturnsNull()
    {
        var reg = BuildRegistry();
        Assert.Null(reg.TryResolveControlOnForm("ghost", "txt"));
    }

    [Fact]
    public void ControlOnForm_UnknownControl_ReturnsNull()
    {
        var reg = BuildRegistry(forms: new[] { Form("frmCustomers") });
        Assert.Null(reg.TryResolveControlOnForm("frmCustomers", "ghost"));
    }

    // ── TryResolveControlOnReport ──────────────────────────────────

    [Fact]
    public void ControlOnReport_ByModuleName_Resolves()
    {
        var reg = BuildRegistry(reports: new[]
        {
            Report("Invoice", moduleName: "Report_Invoice",
                Control("TextBox", "txtAmount"))
        });

        Assert.NotNull(reg.TryResolveControlOnReport("Report_Invoice", "txtAmount"));
    }

    // ── TryResolveMemberOf — synthetic control + type-library ─────

    [Fact]
    public void MemberOf_ControlValue_ResolvesViaTypeLibrary()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frmCustomers", children: Control("TextBox", "txtName"))
        });
        var ctl = reg.TryResolveControlOnForm("frmCustomers", "txtName")!;

        var hit = reg.TryResolveMemberOf(ctl, "Value");

        var member = Assert.IsType<SyntheticTypeLibraryMemberDeclaration>(hit);
        Assert.Equal("Value", member.Member.Name);
        Assert.Equal(MemberKind.Property, member.Member.Kind);
        Assert.True(member.Member.IsDefault);
    }

    [Fact]
    public void MemberOf_ControlInheritedMember_ResolvesViaBaseChain()
    {
        // .Visible lives on Access.Control; lookup on Access.TextBox
        // must walk the BaseTypeName chain.
        var reg = BuildRegistry(forms: new[]
        {
            Form("frm", children: Control("TextBox", "txt"))
        });
        var ctl = reg.TryResolveControlOnForm("frm", "txt")!;

        var hit = reg.TryResolveMemberOf(ctl, "Visible");

        var member = Assert.IsType<SyntheticTypeLibraryMemberDeclaration>(hit);
        Assert.Equal("Visible", member.Member.Name);
    }

    [Fact]
    public void MemberOf_UnknownMember_ReturnsNull()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frm", children: Control("TextBox", "txt"))
        });
        var ctl = reg.TryResolveControlOnForm("frm", "txt")!;

        Assert.Null(reg.TryResolveMemberOf(ctl, "ThisMemberDoesNotExist"));
    }

    [Fact]
    public void MemberOf_TypeLibraryMember_IsCachedSingleton()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frm", children: Control("TextBox", "txt"))
        });
        var ctl = reg.TryResolveControlOnForm("frm", "txt")!;

        var a = reg.TryResolveMemberOf(ctl, "Value");
        var b = reg.TryResolveMemberOf(ctl, "value"); // case-insensitive
        Assert.Same(a, b);
    }

    // ── TryResolveMemberOf — subform navigation ────────────────────

    [Fact]
    public void MemberOf_SubformDotForm_ResolvesToContainedForm()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frmParent", children: Control("Subform", "sub",
                StringProps("SourceObject", "frmChild"))),
            Form("frmChild", children: Control("TextBox", "txtChild")),
        });
        var sub = reg.TryResolveControlOnForm("frmParent", "sub")!;

        var hit = reg.TryResolveMemberOf(sub, "Form");

        var formDecl = Assert.IsType<SyntheticFormDeclaration>(hit);
        Assert.Equal("frmChild", formDecl.FormName);
    }

    [Fact]
    public void MemberOf_SubformDotForm_CaseInsensitive()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frmParent", children: Control("Subform", "sub",
                StringProps("SourceObject", "frmChild"))),
            Form("frmChild"),
        });
        var sub = reg.TryResolveControlOnForm("frmParent", "sub")!;

        Assert.NotNull(reg.TryResolveMemberOf(sub, "form"));
        Assert.NotNull(reg.TryResolveMemberOf(sub, "FORM"));
    }

    [Fact]
    public void MemberOf_SubreportDotReport_ResolvesToContainedReport()
    {
        var reg = BuildRegistry(
            reports: new[]
            {
                Report("rptParent", children: Control("Subreport", "sub",
                    StringProps("SourceObject", "rptChild"))),
                Report("rptChild"),
            });
        var sub = reg.TryResolveControlOnReport("rptParent", "sub")!;

        var hit = reg.TryResolveMemberOf(sub, "Report");
        var reportDecl = Assert.IsType<SyntheticReportDeclaration>(hit);
        Assert.Equal("rptChild", reportDecl.ReportName);
    }

    [Fact]
    public void MemberOf_SubformOtherMember_FallsThroughToControlSurface()
    {
        // .Name lives on Access.Control; subform map -> Access.SubForm
        // is unregistered in #11a, so the lookup against the type-library
        // returns null. Currently this returns null — that is the
        // expected Session 1 behaviour and a future #11a enhancement
        // would register Access.SubForm so it inherits Access.Control.
        var reg = BuildRegistry(forms: new[]
        {
            Form("frm", children: Control("Subform", "sub")),
        });
        var sub = reg.TryResolveControlOnForm("frm", "sub")!;

        Assert.Null(reg.TryResolveMemberOf(sub, "Name"));
    }

    // ── TryResolveMemberOf — form / report / collection paths ─────

    [Fact]
    public void MemberOf_FormDecl_ResolvesAccessFormProperty()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frm", children: Control("TextBox", "txt"))
        });
        var formDecl = reg.TryResolveTopLevelMember("frm")!;

        var hit = reg.TryResolveMemberOf(formDecl, "RecordSource");
        var member = Assert.IsType<SyntheticTypeLibraryMemberDeclaration>(hit);
        Assert.Equal("RecordSource", member.Member.Name);
    }

    [Fact]
    public void MemberOf_FormDecl_FallsBackToNamedControl()
    {
        var reg = BuildRegistry(forms: new[]
        {
            Form("frm", children: Control("TextBox", "txtCustomer"))
        });
        var formDecl = reg.TryResolveTopLevelMember("frm")!;

        // txtCustomer is not on Access.Form; falls back to Controls dict
        var hit = reg.TryResolveMemberOf(formDecl, "txtCustomer");
        Assert.IsType<SyntheticControlDeclaration>(hit);
    }

    [Fact]
    public void MemberOf_FormsCollection_ResolvesNamedForm()
    {
        var reg = BuildRegistry(forms: new[] { Form("frmCustomers") });
        var coll = reg.TryResolveTopLevelMember("Forms")!;

        var hit = reg.TryResolveMemberOf(coll, "frmCustomers");
        Assert.IsType<SyntheticFormDeclaration>(hit);
    }

    [Fact]
    public void MemberOf_ReportsCollection_ResolvesNamedReport()
    {
        var reg = BuildRegistry(reports: new[] { Report("rptInvoice") });
        var coll = reg.TryResolveTopLevelMember("Reports")!;

        var hit = reg.TryResolveMemberOf(coll, "rptInvoice");
        Assert.IsType<SyntheticReportDeclaration>(hit);
    }

    [Fact]
    public void MemberOf_FormsCollection_UnknownName_ReturnsNull()
    {
        var reg = BuildRegistry(forms: new[] { Form("frmCustomers") });
        var coll = reg.TryResolveTopLevelMember("Forms")!;
        Assert.Null(reg.TryResolveMemberOf(coll, "ghost"));
    }

    // ── TryResolveMemberOf — chained type-library walk ────────────

    [Fact]
    public void MemberOf_TypeLibraryMember_WithReturnType_ChainsToNextType()
    {
        // Me.RecordsetClone returns DAO.Recordset; walking .MoveNext
        // off that should hit DAO.Recordset's MoveNext.
        var reg = BuildRegistry(forms: new[]
        {
            Form("Customers", moduleName: "Form_Customers")
        });
        var formDecl = reg.TryResolveTopLevelMember("Customers")!;
        var rsClone = reg.TryResolveMemberOf(formDecl, "RecordsetClone")!;
        Assert.IsType<SyntheticTypeLibraryMemberDeclaration>(rsClone);

        var moveNext = reg.TryResolveMemberOf(rsClone, "MoveNext");
        var member = Assert.IsType<SyntheticTypeLibraryMemberDeclaration>(moveNext);
        Assert.Equal("MoveNext", member.Member.Name);
        Assert.Equal(MemberKind.Method, member.Member.Kind);
    }

    // ── TryResolveMemberOf — fall-through ─────────────────────────

    [Fact]
    public void MemberOf_SourceDefinedDecl_ReturnsNull()
    {
        var reg = BuildRegistry();

        // A source-defined VariableDeclaration (no synthetic surface):
        var range = new AccessHarvester.Vba.Parser.Ast.SourceRange(
            new AccessHarvester.Vba.Parser.Ast.SourceLocation("", 1, 0),
            new AccessHarvester.Vba.Parser.Ast.SourceLocation("", 1, 1));
        var src = new VariableDeclaration(
            name: "x",
            provenance: new SourceDeclarationProvenance(range),
            declarationScope: VariableScope.Local,
            access: AccessLevel.Private,
            isDefaultedAccess: true,
            isStatic: false,
            isWithEvents: false,
            isAutoInstantiated: false,
            type: null,
            typeSuffix: null,
            isArray: false,
            arrayBounds: Array.Empty<AccessHarvester.Vba.Parser.Ast.ArrayBound>());

        Assert.Null(reg.TryResolveMemberOf(src, "anything"));
    }

    [Fact]
    public void NullRegistry_TryResolveMemberOf_ReturnsNull()
    {
        var reg = AccessHarvester.Vba.Resolver.References.NullAccessObjectModelRegistry.Instance;
        var ctl = new SyntheticControlDeclaration("txt", "TextBox", "Access.TextBox", "frm");
        Assert.Null(reg.TryResolveMemberOf(ctl, "Value"));
    }
}
