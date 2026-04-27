using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic unit tests for FormComponentSpecBuilder. Inputs are
// FormMetadata trees constructed directly via the public records, so
// the tests exercise the visitor logic without touching Access COM.
public class FormComponentSpecBuilderTests
{
    private static AccessControl Section(string typeOrName, params AccessControl[] children)
        => new() { Type = "Section", Name = typeOrName, Children = children };

    private static AccessControl FormHeader(params AccessControl[] children)
        => new() { Type = "FormHeader", Name = "FormHeader", Children = children };

    private static AccessControl FormFooter(params AccessControl[] children)
        => new() { Type = "FormFooter", Name = "FormFooter", Children = children };

    private static AccessControl Begin(params AccessControl[] children)
        => new() { Type = "Begin", Name = null, Children = children };

    private static AccessControl Control(
        string type,
        string name,
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        IReadOnlyList<AccessControl>? children = null,
        GeometryInches? geometry = null,
        LabelInfo? label = null,
        IReadOnlyList<string>? nonDefault = null)
        => new()
        {
            Type = type,
            Name = name,
            Properties = properties,
            Children = children,
            GeometryIn = geometry,
            Label = label,
            NonDefaultProperties = nonDefault,
        };

    private static IReadOnlyDictionary<string, JsonElement> Props(
        params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entries)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(v));
            dict[k] = doc.RootElement.Clone();
        }
        return dict;
    }

    // ── Component-name suggestion ──────────────────────────────────────

    [Theory]
    [InlineData("frmCaseManagement", "CaseManagementComponent")]
    [InlineData("frmMainSwitchboard", "MainSwitchboardComponent")]
    [InlineData("sfrmOpenCases", "OpenCasesComponent")]
    [InlineData("Customers By Region", "CustomersByRegionComponent")]
    [InlineData("simple", "SimpleComponent")]
    public void SuggestComponentName_StripsFormPrefixAndPascalCases(string source, string expected)
    {
        Assert.Equal(expected, FormComponentSpecBuilder.SuggestComponentName(source));
    }

    [Fact]
    public void SuggestComponentName_ReturnsNullForPunctuationOnlyName()
    {
        Assert.Null(FormComponentSpecBuilder.SuggestComponentName("---"));
    }

    [Fact]
    public void SuggestComponentName_DoesNotStripWhenFollowingCharIsLowercase()
    {
        // "frmaintain" should NOT have its 'frm' prefix stripped because
        // the character after the prefix is lowercase ('a' in 'maintain'),
        // signalling the prefix is part of a longer word, not the
        // Hungarian convention.
        Assert.Equal("FrmaintainComponent", FormComponentSpecBuilder.SuggestComponentName("frmaintain"));
    }

    // ── Source form name + module + data source ────────────────────────

    [Fact]
    public void Build_CapturesFormNameModuleAndDataSource()
    {
        var form = new FormMetadata
        {
            Name = "frmCaseManagement",
            ModuleName = "Form_frmCaseManagement",
            RecordSource = "tblCases",
            Filter = "CaseStatus = 'Open'",
            OrderBy = "OpenedDate DESC",
            DefaultView = "Single Form",
        };

        var spec = FormComponentSpecBuilder.Build(form);

        Assert.Equal("frmCaseManagement", spec.SourceFormName);
        Assert.Equal("Form_frmCaseManagement", spec.ModuleName);
        Assert.Equal("CaseManagementComponent", spec.SuggestedComponentName);
        Assert.Equal("Single Form", spec.DefaultView);
        Assert.NotNull(spec.DataSource);
        Assert.Equal(FormDataSourceKind.NamedReference, spec.DataSource!.Kind);
        Assert.Equal("tblCases", spec.DataSource.TargetName);
        Assert.Null(spec.DataSource.InlineSql);
        Assert.Equal("CaseStatus = 'Open'", spec.DataSource.Filter);
        Assert.Equal("OpenedDate DESC", spec.DataSource.OrderBy);
        Assert.Null(spec.DataSource.QueryAbstractionRef); // deferred to Session 3
    }

    [Fact]
    public void Build_ClassifiesInlineSqlRecordSource()
    {
        var form = new FormMetadata
        {
            Name = "frmReport",
            RecordSource = "SELECT CaseID, CaseNumber FROM tblCases WHERE Status = 'Open'",
        };

        var spec = FormComponentSpecBuilder.Build(form);

        Assert.NotNull(spec.DataSource);
        Assert.Equal(FormDataSourceKind.InlineSql, spec.DataSource!.Kind);
        Assert.Null(spec.DataSource.TargetName);
        Assert.Equal(form.RecordSource, spec.DataSource.InlineSql);
    }

    [Fact]
    public void Build_ReturnsNullDataSourceWhenAllNull()
    {
        var form = new FormMetadata { Name = "frmDialog" };
        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Null(spec.DataSource);
    }

    [Fact]
    public void Build_ReturnsNoneDataSourceKindWhenOnlyFilterSet()
    {
        // A form might carry only a Filter without a record source — rare,
        // but the typed field has a value so we surface it.
        var form = new FormMetadata { Name = "frmWeird", Filter = "x = 1" };
        var spec = FormComponentSpecBuilder.Build(form);
        Assert.NotNull(spec.DataSource);
        Assert.Equal(FormDataSourceKind.None, spec.DataSource!.Kind);
        Assert.Equal("x = 1", spec.DataSource.Filter);
    }

    [Fact]
    public void Build_AcceptsBracketedBarewordRecordSource()
    {
        var form = new FormMetadata
        {
            Name = "frmCustomers",
            RecordSource = "[My Customers]",
        };
        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Equal(FormDataSourceKind.NamedReference, spec.DataSource!.Kind);
        Assert.Equal("[My Customers]", spec.DataSource.TargetName);
    }

    // ── Section + control walk ─────────────────────────────────────────

    [Fact]
    public void Build_FlattensSectionsAndControlsWithSectionAttribution()
    {
        var form = new FormMetadata
        {
            Name = "frmCustomers",
            Children = new[]
            {
                FormHeader(Control("Label", "lblTitle")),
                Section("Detail",
                    Control("TextBox", "txtFirstName"),
                    Control("TextBox", "txtLastName"),
                    Control("CommandButton", "cmdSave")),
                FormFooter(Control("Label", "lblFooter")),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);

        Assert.NotNull(spec.Sections);
        Assert.Equal(3, spec.Sections!.Count);
        Assert.Equal(FormSectionKind.FormHeader, spec.Sections[0].Kind);
        Assert.Equal(FormSectionKind.Detail, spec.Sections[1].Kind);
        Assert.Equal(FormSectionKind.FormFooter, spec.Sections[2].Kind);

        Assert.Equal(new[] { "lblTitle" }, spec.Sections[0].ControlNames);
        Assert.Equal(new[] { "txtFirstName", "txtLastName", "cmdSave" }, spec.Sections[1].ControlNames);
        Assert.Equal(new[] { "lblFooter" }, spec.Sections[2].ControlNames);

        Assert.NotNull(spec.Controls);
        Assert.Equal(5, spec.Controls!.Count);
        Assert.Equal(new[] { "lblTitle", "txtFirstName", "txtLastName", "cmdSave", "lblFooter" },
            spec.Controls.Select(c => c.Name).ToArray());
        Assert.Equal("FormHeader", spec.Controls[0].SectionName);
        Assert.Equal("Detail", spec.Controls[1].SectionName);
        Assert.Equal("FormFooter", spec.Controls[4].SectionName);
    }

    [Fact]
    public void Build_SkipsAnonymousBeginWrappersAndAttributesChildrenToSection()
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

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Equal(2, spec.Controls!.Count);
        Assert.Equal(new[] { "txtA", "lblB" }, spec.Controls.Select(c => c.Name).ToArray());
        Assert.All(spec.Controls, c => Assert.Equal("Detail", c.SectionName));
        Assert.Equal(new[] { "txtA", "lblB" }, spec.Sections![0].ControlNames);
    }

    [Fact]
    public void Build_DropsUnnamedNodesFromControlList()
    {
        // An anonymous decorative shape with no Name should be dropped
        // rather than emitted with Name = null.
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtNamed"),
                    new AccessControl { Type = "Rectangle", Name = null }),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Single(spec.Controls!);
        Assert.Equal("txtNamed", spec.Controls![0].Name);
    }

    [Fact]
    public void Build_ClassifiesComponentKindForKnownTypes()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txt"),
                    Control("ComboBox", "cbo"),
                    Control("ListBox", "lst"),
                    Control("CheckBox", "chk"),
                    Control("OptionButton", "opt"),
                    Control("ToggleButton", "tgl"),
                    Control("CommandButton", "cmd"),
                    Control("Label", "lbl"),
                    Control("Image", "img"),
                    Control("Subform", "sfrm"),
                    Control("TabCtl", "tabs"),
                    Control("Rectangle", "rect"),
                    Control("Bogus", "??")),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var byName = spec.Controls!.ToDictionary(c => c.Name);
        Assert.Equal(FormControlComponentKind.TextInput, byName["txt"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Dropdown, byName["cbo"].ComponentKind);
        Assert.Equal(FormControlComponentKind.ListBox, byName["lst"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Toggle, byName["chk"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Toggle, byName["opt"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Toggle, byName["tgl"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Button, byName["cmd"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Label, byName["lbl"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Image, byName["img"].ComponentKind);
        Assert.Equal(FormControlComponentKind.SubformHost, byName["sfrm"].ComponentKind);
        Assert.Equal(FormControlComponentKind.TabControl, byName["tabs"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Decoration, byName["rect"].ComponentKind);
        Assert.Equal(FormControlComponentKind.Other, byName["??"].ComponentKind);
    }

    // ── Binding ────────────────────────────────────────────────────────

    [Fact]
    public void Build_LiftsControlSourceAsTableField()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtName",
                        Props(("ControlSource", "CustomerName")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var binding = spec.Controls![0].Binding;
        Assert.NotNull(binding);
        Assert.Equal(ControlBindingKind.TableField, binding!.Kind);
        Assert.Equal("CustomerName", binding.ControlSource);
        Assert.Null(binding.RowSource);
    }

    [Fact]
    public void Build_LiftsComputedExpressionFromLeadingEqualsControlSource()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtFullName",
                        Props(("ControlSource", "=[FirstName] & \" \" & [LastName]")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var binding = spec.Controls![0].Binding!;
        Assert.Equal(ControlBindingKind.ComputedExpression, binding.Kind);
        Assert.StartsWith("=", binding.ControlSource);
    }

    [Fact]
    public void Build_LiftsRowSourceListWithBoundColumnAndCounts()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("ComboBox", "cboCaseworker",
                        Props(
                            ("RowSource", "SELECT CaseworkerID, CaseworkerName FROM tblCaseworkers ORDER BY CaseworkerName"),
                            ("RowSourceType", "Table/Query"),
                            ("BoundColumn", 1),
                            ("ColumnCount", 4),
                            ("ColumnWidths", "0;1440;1440;1440")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var binding = spec.Controls![0].Binding!;
        Assert.Equal(ControlBindingKind.RowSourceList, binding.Kind);
        Assert.StartsWith("SELECT", binding.RowSource);
        Assert.Equal("Table/Query", binding.RowSourceType);
        Assert.Equal(1, binding.BoundColumn);
        Assert.Equal(4, binding.ColumnCount);
        Assert.Equal("0;1440;1440;1440", binding.ColumnWidths);
        Assert.Null(binding.QueryAbstractionRef); // deferred to Session 3
    }

    [Fact]
    public void Build_LeavesBindingNullForControlsWithoutBindingProperties()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("CommandButton", "cmdSave",
                        Props(("Caption", "Save")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Null(spec.Controls![0].Binding);
        Assert.NotNull(spec.Controls[0].RenderHints);
        Assert.Equal("Save", spec.Controls[0].RenderHints!.Caption);
    }

    // ── Render hints ───────────────────────────────────────────────────

    [Fact]
    public void Build_LiftsRenderHintsFromPropertyBag()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtPhone",
                        Props(
                            ("Caption", "Phone:"),
                            ("Format", "(@@@) @@@-@@@@"),
                            ("InputMask", "(999) 000-0000"),
                            ("DefaultValue", "=Null"),
                            ("Visible", true),
                            ("Enabled", true),
                            ("Locked", false),
                            ("TabIndex", 3)))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var hints = spec.Controls![0].RenderHints!;
        Assert.Equal("Phone:", hints.Caption);
        Assert.Equal("(@@@) @@@-@@@@", hints.Format);
        Assert.Equal("(999) 000-0000", hints.InputMask);
        Assert.Equal("=Null", hints.DefaultValue);
        Assert.True(hints.IsVisible);
        Assert.True(hints.IsEnabled);
        Assert.False(hints.IsLocked);
        Assert.Equal(3, hints.TabIndex);
    }

    [Fact]
    public void Build_ParsesAccessStyleStringBoolsForRenderHints()
    {
        // Access often serialises Visible/Enabled as the string "True" /
        // "False" rather than a JSON boolean. The reader should accept
        // both encodings.
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txt",
                        Props(("Visible", "True"), ("Enabled", "False"), ("Locked", "0")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var hints = spec.Controls![0].RenderHints!;
        Assert.True(hints.IsVisible);
        Assert.False(hints.IsEnabled);
        Assert.False(hints.IsLocked);
    }

    // ── Subform relationships ──────────────────────────────────────────

    [Fact]
    public void Build_EmitsSubformRelationshipForSubformControls()
    {
        var form = new FormMetadata
        {
            Name = "frmCaseManagement",
            Children = new[]
            {
                Section("Detail",
                    Control("Subform", "sfrmOpenCases",
                        Props(
                            ("SourceObject", "frmOpenCasesEmbed"),
                            ("LinkChildFields", "CaseworkerID;CaseStatus"),
                            ("LinkMasterFields", "cboCaseworker;txtStatus")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);

        Assert.NotNull(spec.Subforms);
        var sub = Assert.Single(spec.Subforms!);
        Assert.Equal("sfrmOpenCases", sub.SubformControlName);
        Assert.Equal("frmOpenCasesEmbed", sub.ContainedObjectName);
        Assert.Equal(new[] { "CaseworkerID", "CaseStatus" }, sub.LinkChildFields);
        Assert.Equal(new[] { "cboCaseworker", "txtStatus" }, sub.LinkMasterFields);

        // Subform control also appears in Controls flattened list.
        Assert.Single(spec.Controls!);
        Assert.Equal(FormControlComponentKind.SubformHost, spec.Controls![0].ComponentKind);
    }

    [Fact]
    public void Build_LeavesSubformsNullWhenNoSubformControls()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail", Control("TextBox", "txtA")),
            },
        };
        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Null(spec.Subforms);
    }

    // ── Event handlers ─────────────────────────────────────────────────

    [Fact]
    public void Build_LiftsControlEventHandlersFromEventProcedureSentinel()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("CommandButton", "btnSave",
                        Props(
                            ("OnClick", "[Event Procedure]"),
                            ("Caption", "Save"))),
                    Control("ComboBox", "cboCaseworker",
                        Props(
                            ("AfterUpdate", "[Event Procedure]"),
                            ("BeforeUpdate", "[Event Procedure]")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.NotNull(spec.EventHandlers);
        var byKey = spec.EventHandlers!.ToDictionary(e => $"{e.ControlName}.{e.PropertyName}");
        Assert.Equal(EventHandlerHostKind.Control, byKey["btnSave.OnClick"].HostKind);
        Assert.Equal("Click", byKey["btnSave.OnClick"].EventName);
        Assert.Equal("btnSave_Click", byKey["btnSave.OnClick"].VbaProcedureName);
        Assert.Null(byKey["btnSave.OnClick"].Classification); // deferred to set #15

        Assert.Equal("AfterUpdate", byKey["cboCaseworker.AfterUpdate"].EventName);
        Assert.Equal("cboCaseworker_AfterUpdate", byKey["cboCaseworker.AfterUpdate"].VbaProcedureName);

        Assert.Equal("BeforeUpdate", byKey["cboCaseworker.BeforeUpdate"].EventName);
        Assert.Equal("cboCaseworker_BeforeUpdate", byKey["cboCaseworker.BeforeUpdate"].VbaProcedureName);
    }

    [Fact]
    public void Build_LiftsFormLevelEventHandlersFromFormProperties()
    {
        var form = new FormMetadata
        {
            Name = "frmMain",
            Properties = Props(
                ("OnLoad", "[Event Procedure]"),
                ("OnCurrent", "[Event Procedure]")),
            Children = new[]
            {
                Section("Detail", Control("TextBox", "txtA")),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var formEvents = spec.EventHandlers!
            .Where(e => e.HostKind == EventHandlerHostKind.Form)
            .ToList();
        Assert.Equal(2, formEvents.Count);
        Assert.Contains(formEvents, e => e.PropertyName == "OnLoad" && e.EventName == "Load" &&
                                         e.VbaProcedureName == "Form_Load" && e.ControlName is null);
        Assert.Contains(formEvents, e => e.PropertyName == "OnCurrent" && e.EventName == "Current" &&
                                         e.VbaProcedureName == "Form_Current");
    }

    [Fact]
    public void Build_DoesNotLiftPropertiesThatAreNotEventProcedureSentinel()
    {
        // OnLoad="" or OnLoad="=SomeMacro()" should not be lifted as a
        // VBA event handler reference — only the literal sentinel counts.
        var form = new FormMetadata
        {
            Name = "frmMain",
            Properties = Props(
                ("OnLoad", "=Macro1"),
                ("OnCurrent", "")),
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Null(spec.EventHandlers);
    }

    [Fact]
    public void Build_DoesNotMisclassifyOnlyAsAnOnEventProperty()
    {
        // The "On..." event-property heuristic requires the character
        // after "On" to be uppercase to avoid lifting properties whose
        // names happen to start with "On" (e.g., the hypothetical
        // "Online" or "OnboardWizardStep").
        var form = new FormMetadata
        {
            Name = "frmMain",
            Properties = Props(
                ("OnboardingDone", "[Event Procedure]")),
        };
        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Null(spec.EventHandlers);
    }

    // ── Validation rules ───────────────────────────────────────────────

    [Fact]
    public void Build_LiftsControlValidationRuleAndText()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtAge",
                        Props(
                            ("ControlSource", "Age"),
                            ("ValidationRule", ">= 0 And <= 150"),
                            ("ValidationText", "Age must be between 0 and 150.")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var rule = Assert.Single(spec.ValidationRules!);
        Assert.Equal(ValidationRuleHostKind.Control, rule.HostKind);
        Assert.Equal("txtAge", rule.ControlName);
        Assert.Equal(ValidationRuleSource.ControlValidationRule, rule.Source);
        Assert.Equal(">= 0 And <= 150", rule.RuleExpression);
        Assert.Equal("Age must be between 0 and 150.", rule.ValidationText);
    }

    [Fact]
    public void Build_LiftsRequiredAndLimitToListAsValidationReferences()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtName",
                        Props(("ControlSource", "Name"), ("Required", true))),
                    Control("ComboBox", "cboCategory",
                        Props(("RowSource", "qryCategories"), ("LimitToList", true)))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.NotNull(spec.ValidationRules);
        Assert.Contains(spec.ValidationRules!, r =>
            r.ControlName == "txtName" && r.Source == ValidationRuleSource.RequiredField);
        Assert.Contains(spec.ValidationRules!, r =>
            r.ControlName == "cboCategory" && r.Source == ValidationRuleSource.LimitToList);
    }

    [Fact]
    public void Build_LiftsBeforeUpdateHandlerAsValidationCrossReference()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txtAge",
                        Props(("BeforeUpdate", "[Event Procedure]")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Contains(spec.ValidationRules!, r =>
            r.ControlName == "txtAge" && r.Source == ValidationRuleSource.BeforeUpdateHandler);
        // It also surfaces as an EventHandlerReference.
        Assert.Contains(spec.EventHandlers!, e =>
            e.ControlName == "txtAge" && e.PropertyName == "BeforeUpdate");
    }

    [Fact]
    public void Build_LiftsFormLevelRecordValidationRule()
    {
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Properties = Props(
                ("ValidationRule", "[StartDate] <= [EndDate]"),
                ("ValidationText", "Start must be before end.")),
        };

        var spec = FormComponentSpecBuilder.Build(form);
        var rule = Assert.Single(spec.ValidationRules!);
        Assert.Equal(ValidationRuleHostKind.Form, rule.HostKind);
        Assert.Equal(ValidationRuleSource.RecordValidationRule, rule.Source);
        Assert.Equal("[StartDate] <= [EndDate]", rule.RuleExpression);
        Assert.Equal("Start must be before end.", rule.ValidationText);
    }

    // ── Geometry pass-through ──────────────────────────────────────────

    [Fact]
    public void Build_PassesThroughFormAndControlGeometry()
    {
        var formGeom = new GeometryInches { W = 10.0 };
        var ctlGeom = new GeometryInches { X = 0.5, Y = 0.25, W = 2.0, H = 0.3 };
        var form = new FormMetadata
        {
            Name = "frmDemo",
            GeometryIn = formGeom,
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txt", geometry: ctlGeom)),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Equal(formGeom, spec.GeometryIn);
        Assert.Equal(ctlGeom, spec.Controls![0].GeometryIn);
    }

    [Fact]
    public void Build_PassesThroughLabelInfoAndNonDefaultProperties()
    {
        var label = new LabelInfo { Source = "attached", Text = "Customer Name" };
        var form = new FormMetadata
        {
            Name = "frmDemo",
            Children = new[]
            {
                Section("Detail",
                    Control("TextBox", "txt",
                        properties: Props(("ControlSource", "Name")),
                        label: label,
                        nonDefault: new[] { "Visible" })),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Equal(label, spec.Controls![0].Label);
        Assert.Equal(new[] { "Visible" }, spec.Controls[0].NonDefaultProperties);
    }

    // ── Empty form ─────────────────────────────────────────────────────

    [Fact]
    public void Build_LeavesAllListsNullForEmptyForm()
    {
        var form = new FormMetadata { Name = "frmEmpty" };
        var spec = FormComponentSpecBuilder.Build(form);

        Assert.Equal("frmEmpty", spec.SourceFormName);
        Assert.Null(spec.Sections);
        Assert.Null(spec.Controls);
        Assert.Null(spec.Subforms);
        Assert.Null(spec.EventHandlers);
        Assert.Null(spec.ValidationRules);
        Assert.Null(spec.DataSource);
        Assert.Null(spec.Purpose);
        Assert.Null(spec.Intent);
        Assert.Null(spec.Notes);
    }

    // ── Section-classification edge cases ──────────────────────────────

    [Fact]
    public void Build_ClassifiesGroupLevelSections()
    {
        var form = new FormMetadata
        {
            Name = "frmReport",
            Children = new[]
            {
                new AccessControl { Type = "GroupLevel0Header", Name = "GroupLevel0Header" },
                new AccessControl { Type = "GroupLevel0Footer", Name = "GroupLevel0Footer" },
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);
        Assert.Equal(FormSectionKind.GroupLevelHeader, spec.Sections![0].Kind);
        Assert.Equal(FormSectionKind.GroupLevelFooter, spec.Sections[1].Kind);
    }

    // ── Switchboard scenario (fixture-12-shaped) ──────────────────────

    [Fact]
    public void Build_HandlesSwitchboardLikeFormWithFormLoadAndButtonClicks()
    {
        // Mirrors the structure of test-case 12 (frmMainSwitchboard):
        // a Form_Load handler plus a series of CommandButton _Click
        // handlers, no record source.
        var form = new FormMetadata
        {
            Name = "frmMainSwitchboard",
            Properties = Props(("OnLoad", "[Event Procedure]")),
            Children = new[]
            {
                Section("Detail",
                    Control("CommandButton", "btnNewCase",
                        Props(("OnClick", "[Event Procedure]"), ("Caption", "New Case"))),
                    Control("CommandButton", "btnSearchCases",
                        Props(("OnClick", "[Event Procedure]"), ("Caption", "Search Cases"))),
                    Control("CommandButton", "btnReports",
                        Props(("OnClick", "[Event Procedure]"), ("Caption", "Reports"))),
                    Control("CommandButton", "btnExit",
                        Props(("OnClick", "[Event Procedure]"), ("Caption", "Exit")))),
            },
        };

        var spec = FormComponentSpecBuilder.Build(form);

        Assert.Null(spec.DataSource); // unbound switchboard
        Assert.Equal("MainSwitchboardComponent", spec.SuggestedComponentName);
        Assert.Equal(4, spec.Controls!.Count);
        Assert.All(spec.Controls, c => Assert.Equal(FormControlComponentKind.Button, c.ComponentKind));

        Assert.NotNull(spec.EventHandlers);
        Assert.Equal(5, spec.EventHandlers!.Count); // Form_Load + 4 button clicks
        Assert.Single(spec.EventHandlers, e => e.HostKind == EventHandlerHostKind.Form &&
                                                e.VbaProcedureName == "Form_Load");
        Assert.Equal(4, spec.EventHandlers.Count(e => e.HostKind == EventHandlerHostKind.Control &&
                                                       e.EventName == "Click"));
    }
}
