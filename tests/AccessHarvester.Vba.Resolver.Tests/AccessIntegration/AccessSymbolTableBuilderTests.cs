using System.Collections.Generic;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Vba.Resolver.AccessIntegration;
using Xunit;

namespace AccessHarvester.Vba.Resolver.Tests.AccessIntegration;

public sealed class AccessSymbolTableBuilderTests
{
    private static AccessControl Section(string name, params AccessControl[] children)
        => new() { Type = "Section", Name = name, Children = children };

    private static AccessControl Begin(params AccessControl[] children)
        => new() { Type = "Begin", Name = null, Children = children };

    private static AccessControl Control(string type, string name,
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        IReadOnlyList<AccessControl>? children = null)
        => new() { Type = type, Name = name, Properties = properties, Children = children };

    private static IReadOnlyDictionary<string, JsonElement> Props(
        params (string Key, string Value)[] entries)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entries)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(v));
            dict[k] = doc.RootElement.Clone();
        }
        return dict;
    }

    [Fact]
    public void BuildForm_EmitsOneDeclarationPerNamedLeafControl()
    {
        var form = new FormMetadata
        {
            Name = "frmCustomers",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtFirstName"),
                    Control("TextBox", "txtLastName"),
                    Control("CommandButton", "cmdSave")),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        Assert.Equal("frmCustomers", decl.FormName);
        Assert.Null(decl.ModuleName);
        Assert.Equal("Access.Form", decl.TypeLibraryTypeName);
        Assert.Equal(3, decl.Controls.Count);
        Assert.Contains("txtFirstName", decl.Controls.Keys);
        Assert.Contains("txtLastName", decl.Controls.Keys);
        Assert.Contains("cmdSave", decl.Controls.Keys);
        Assert.IsType<SyntheticControlDeclaration>(decl.Controls["txtFirstName"]);
        var ctl = (SyntheticControlDeclaration)decl.Controls["txtFirstName"];
        Assert.Equal("TextBox", ctl.AccessClassName);
        Assert.Equal("Access.TextBox", ctl.TypeLibraryTypeName);
        Assert.Equal("frmCustomers", ctl.OwnerArtifact);
    }

    [Fact]
    public void BuildForm_SkipsAnonymousBeginWrappers()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Begin(Control("TextBox", "txtA")),
                    Control("Label", "lblB")),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        Assert.Equal(2, decl.Controls.Count);
        Assert.Contains("txtA", decl.Controls.Keys);
        Assert.Contains("lblB", decl.Controls.Keys);
    }

    [Fact]
    public void BuildForm_LookupsAreCaseInsensitive()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail", Control("TextBox", "txtCustomerName")),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        Assert.True(decl.Controls.ContainsKey("txtCustomerName"));
        Assert.True(decl.Controls.ContainsKey("TXTCUSTOMERNAME"));
        Assert.True(decl.Controls.ContainsKey("txtcustomername"));
    }

    [Fact]
    public void BuildForm_DuplicateNamesFirstWins()
    {
        var form = new FormMetadata
        {
            Name = "frmDuplicates",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txt", Props(("Tag", "first"))),
                    Control("ComboBox", "txt", Props(("Tag", "second")))),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        Assert.Single(decl.Controls);
        var ctl = (SyntheticControlDeclaration)decl.Controls["txt"];
        Assert.Equal("TextBox", ctl.AccessClassName);
    }

    [Fact]
    public void BuildForm_SubformWithBareSourceObject_BindsToContainedForm()
    {
        var form = new FormMetadata
        {
            Name = "frmCases",
            Children = new[]
            {
                Section("Detail",
                    Control("Subform", "sfrmOpen",
                        properties: Props(("SourceObject", "sfrmOpenCases")))),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        var sub = Assert.IsType<SyntheticSubformControlDeclaration>(decl.Controls["sfrmOpen"]);
        Assert.Equal("sfrmOpenCases", sub.ContainedFormName);
        Assert.Null(sub.ContainedReportName);
        Assert.Equal("Access.SubForm", sub.TypeLibraryTypeName);
    }

    [Fact]
    public void BuildForm_SubformWithReportPrefix_BindsToContainedReport()
    {
        var form = new FormMetadata
        {
            Name = "frmHybrid",
            Children = new[]
            {
                Section("Detail",
                    Control("Subform", "sfrmReport",
                        properties: Props(("SourceObject", "Report.rptDetail")))),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        var sub = Assert.IsType<SyntheticSubformControlDeclaration>(decl.Controls["sfrmReport"]);
        Assert.Null(sub.ContainedFormName);
        Assert.Equal("rptDetail", sub.ContainedReportName);
    }

    [Fact]
    public void BuildForm_SubreportWithoutPrefix_BindsToContainedReport()
    {
        var report = new ReportMetadata
        {
            Name = "rptInvoiceWithDetails",
            Children = new[]
            {
                Section("Detail",
                    Control("Subreport", "sub",
                        properties: Props(("SourceObject", "rptInvoiceLines")))),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildReport(report);

        var sub = Assert.IsType<SyntheticSubformControlDeclaration>(decl.Controls["sub"]);
        Assert.Null(sub.ContainedFormName);
        Assert.Equal("rptInvoiceLines", sub.ContainedReportName);
    }

    [Fact]
    public void BuildForm_SubformWithoutSourceObject_BindingIsNull()
    {
        var form = new FormMetadata
        {
            Name = "frmDetached",
            Children = new[]
            {
                Section("Detail", Control("Subform", "sfrm")),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildForm(form);

        var sub = Assert.IsType<SyntheticSubformControlDeclaration>(decl.Controls["sfrm"]);
        Assert.Null(sub.ContainedFormName);
        Assert.Null(sub.ContainedReportName);
    }

    [Fact]
    public void BuildForm_EmptyForm_HasNoControls()
    {
        var form = new FormMetadata { Name = "frmEmpty" };
        var decl = AccessSymbolTableBuilder.BuildForm(form);
        Assert.Empty(decl.Controls);
        Assert.Null(decl.ModuleName);
    }

    [Fact]
    public void BuildForm_GroupLevelHeaderFooterTreatedAsWrappers()
    {
        var report = new ReportMetadata
        {
            Name = "rptOrders",
            Children = new[]
            {
                new AccessControl
                {
                    Type = "GroupLevel0Header",
                    Name = "GroupLevel0Header",
                    Children = new[] { Control("TextBox", "txtCategory") },
                },
                Section("Detail", Control("TextBox", "txtAmount")),
            },
        };

        var decl = AccessSymbolTableBuilder.BuildReport(report);

        Assert.Equal(2, decl.Controls.Count);
        Assert.Contains("txtCategory", decl.Controls.Keys);
        Assert.Contains("txtAmount", decl.Controls.Keys);
    }

    [Fact]
    public void BuildForm_PreservesModuleName()
    {
        var form = new FormMetadata
        {
            Name = "Customers",
            ModuleName = "Form_Customers",
        };
        var decl = AccessSymbolTableBuilder.BuildForm(form);
        Assert.Equal("Form_Customers", decl.ModuleName);
    }
}
