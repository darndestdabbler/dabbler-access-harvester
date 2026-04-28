using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Integration tests for EnrichmentPipelineRunner. Each test builds a minimal
// FormComponentSpec + FormMetadata + VBA procedures, runs the pipeline, and
// asserts on the composite EnrichmentResult. These tests exercise the wiring
// between the three Phase 6.3-6.5 enrichers; per-enricher edge cases live in
// EventHandlerClassifierTests, ValidationRuleExtractorTests, and
// RowSourceSpecBuilderTests respectively.
public class EnrichmentPipelineRunnerTests
{
    // ── Test helpers ──────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, JsonElement> Props(params (string Key, object Value)[] entries)
    {
        var d = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entries)
            d[k] = JsonSerializer.SerializeToElement(v);
        return d;
    }

    private static AccessControl Control(string name, string type = "TextBox",
        IReadOnlyDictionary<string, JsonElement>? props = null,
        IReadOnlyList<AccessControl>? children = null)
        => new() { Type = type, Name = name, Properties = props, Children = children };

    private static FormMetadata Form(string name, params AccessControl[] children)
        => new() { Name = name, Children = children };

    private static FormMetadata Form(string name, IReadOnlyDictionary<string, JsonElement>? formProps,
        params AccessControl[] children)
        => new() { Name = name, Properties = formProps, Children = children };

    private static IReadOnlyList<ProcedureDeclaration> ParseProcedures(string source)
    {
        var module = VbaParserPipeline.Parse(source).BuildAst();
        return module.Items.OfType<ProcedureDeclaration>().ToList();
    }

    private static EventHandlerReference HandlerRef(string propertyName, string eventName,
        string vbaProcedureName, EventHandlerHostKind hostKind = EventHandlerHostKind.Form,
        string? controlName = null)
        => new()
        {
            HostKind = hostKind,
            ControlName = controlName,
            PropertyName = propertyName,
            EventName = eventName,
            VbaProcedureName = vbaProcedureName,
        };

    private static ValidationRuleReference ValidationRef(ValidationRuleSource source,
        string? controlName = null, string? ruleExpression = null)
        => new()
        {
            HostKind = controlName is null ? ValidationRuleHostKind.Form : ValidationRuleHostKind.Control,
            ControlName = controlName,
            Source = source,
            RuleExpression = ruleExpression,
        };

    private static FormComponentSpec Spec(string formName,
        IReadOnlyList<EventHandlerReference>? eventHandlers = null,
        IReadOnlyList<ValidationRuleReference>? validationRules = null,
        IReadOnlyList<FormControlSpec>? controls = null)
        => new()
        {
            SourceFormName = formName,
            EventHandlers = eventHandlers,
            ValidationRules = validationRules,
            Controls = controls,
        };

    // ── No-procedures path ────────────────────────────────────────────

    [Fact]
    public void Run_With_No_Procedures_Returns_Spec_Unchanged()
    {
        var form = Form("frmTest");
        var spec = Spec("frmTest",
            eventHandlers: new[] { HandlerRef("OnLoad", "Load", "Form_Load") });

        var result = EnrichmentPipelineRunner.Run(spec, form);

        // Classification slots stay null when no procedures supplied
        Assert.Null(result.EnrichedSpec.EventHandlers![0].Classification);
        Assert.Empty(result.EventHandlerClassifications);
        Assert.Empty(result.RowSourceSpecs);
    }

    // ── Event-handler classification wiring ───────────────────────────

    [Fact]
    public void Single_Validation_Handler_Gets_Validation_Classification()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtName) Then Cancel = True
            End Sub
            """;
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            eventHandlers: new[] { HandlerRef("BeforeUpdate", "BeforeUpdate", "Form_BeforeUpdate") });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        Assert.Equal("Validation", result.EnrichedSpec.EventHandlers![0].Classification);
        Assert.Single(result.EventHandlerClassifications);
        Assert.Equal("Form_BeforeUpdate", result.EventHandlerClassifications[0].ProcedureName);
    }

    [Fact]
    public void Mixed_Handler_Gets_Mixed_Classification()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtName) Then Cancel = True
                Me.Filter = "Active = True"
                Me.FilterOn = True
            End Sub
            """;
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            eventHandlers: new[] { HandlerRef("BeforeUpdate", "BeforeUpdate", "Form_BeforeUpdate") });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        Assert.Equal("Mixed", result.EnrichedSpec.EventHandlers![0].Classification);
        Assert.True(result.EventHandlerClassifications[0].Mixed);
    }

    [Fact]
    public void Handler_Without_Matching_Procedure_Keeps_Null_Classification()
    {
        var src = """
            Private Sub Form_Load()
            End Sub
            """;
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            eventHandlers: new[]
            {
                HandlerRef("BeforeUpdate", "BeforeUpdate", "Form_BeforeUpdate"), // no match
                HandlerRef("OnLoad", "Load", "Form_Load"),                       // matches
            });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        Assert.Null(result.EnrichedSpec.EventHandlers![0].Classification);    // unmatched
        Assert.NotNull(result.EnrichedSpec.EventHandlers![1].Classification); // matched (empty body -> "Mixed" from fallback)
    }

    [Fact]
    public void Control_Level_Navigation_Handler_Gets_Navigation_Classification()
    {
        var src = """
            Private Sub btnSave_Click()
                DoCmd.OpenForm "frmDetail"
            End Sub
            """;
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            eventHandlers: new[]
            {
                HandlerRef("OnClick", "Click", "btnSave_Click",
                    EventHandlerHostKind.Control, "btnSave"),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        Assert.Equal("Navigation", result.EnrichedSpec.EventHandlers![0].Classification);
    }

    // ── Validation rule wiring ────────────────────────────────────────

    [Fact]
    public void RequiredField_Reference_Gets_Required_Classification_Without_VBA()
    {
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            validationRules: new[]
            {
                ValidationRef(ValidationRuleSource.RequiredField, "txtName"),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form);

        Assert.Equal("Required", result.EnrichedSpec.ValidationRules![0].Classification);
    }

    [Fact]
    public void LimitToList_Reference_Gets_Membership_Classification()
    {
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            validationRules: new[] { ValidationRef(ValidationRuleSource.LimitToList, "cboCaseworker") });

        var result = EnrichmentPipelineRunner.Run(spec, form);

        Assert.Equal("Membership", result.EnrichedSpec.ValidationRules![0].Classification);
    }

    [Fact]
    public void ControlValidationRule_Reference_Gets_Kind_From_Extracted_Rule()
    {
        var control = Control("txtAge", props: Props(
            ("ValidationRule", ">= 0 And <= 150"),
            ("ValidationText", "Age must be 0-150")));
        var form = Form("frmCases", control);
        var spec = Spec("frmCases",
            validationRules: new[]
            {
                ValidationRef(ValidationRuleSource.ControlValidationRule, "txtAge", ">= 0 And <= 150"),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form);

        Assert.Equal("Range", result.EnrichedSpec.ValidationRules![0].Classification);
    }

    [Fact]
    public void BeforeUpdate_Handler_Required_Pattern_Gets_Required_Classification()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtName) Then Cancel = True
            End Sub
            """;
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            validationRules: new[]
            {
                ValidationRef(ValidationRuleSource.BeforeUpdateHandler),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        Assert.Equal("Required", result.EnrichedSpec.ValidationRules![0].Classification);
    }

    [Fact]
    public void BeforeUpdate_Handler_With_No_Cancel_Pattern_Leaves_Classification_Null()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                Me.Filter = "Active = True"
            End Sub
            """;
        var form = Form("frmCases");
        var spec = Spec("frmCases",
            validationRules: new[]
            {
                ValidationRef(ValidationRuleSource.BeforeUpdateHandler),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        Assert.Null(result.EnrichedSpec.ValidationRules![0].Classification);
    }

    // ── RowSource + cascading driver wiring ───────────────────────────

    [Fact]
    public void RowSourceSpecs_Are_Built_For_Combo_Controls()
    {
        var combo = Control("cboStatus", "ComboBox", Props(
            ("RowSource", "Active;Inactive"),
            ("RowSourceType", "Value List")));
        var form = Form("frmCases", combo);
        var spec = Spec("frmCases");

        var result = EnrichmentPipelineRunner.Run(spec, form);

        var rowSpec = Assert.Single(result.RowSourceSpecs);
        Assert.Equal("cboStatus", rowSpec.ControlName);
        Assert.Equal(RowSourceKind.ValueList, rowSpec.Kind);
    }

    [Fact]
    public void CascadingDrivers_Are_Annotated_After_Classification()
    {
        var src = """
            Private Sub cboCaseworker_AfterUpdate()
                Me!cboClient.RowSource = "SELECT ClientID, Name FROM tblClients WHERE CaseworkerID = " & Me.cboCaseworker
            End Sub
            """;
        var combo = Control("cboClient", "ComboBox", Props(("RowSource", ""), ("RowSourceType", "Table/Query")));
        var form = Form("frmCases", combo);
        var spec = Spec("frmCases",
            eventHandlers: new[]
            {
                HandlerRef("AfterUpdate", "AfterUpdate", "cboCaseworker_AfterUpdate",
                    EventHandlerHostKind.Control, "cboCaseworker"),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        var rowSpec = Assert.Single(result.RowSourceSpecs);
        var driver = Assert.Single(rowSpec.CascadingDrivers!);
        Assert.Equal("cboCaseworker", driver.DriverControlName);
        // Event handler is also classified (CascadingUpdate from RowSource assignment)
        Assert.Equal("CascadingUpdate", result.EnrichedSpec.EventHandlers![0].Classification);
    }

    // ── Full integration: anchor fixture 02 round-trip ────────────────

    [Fact]
    public void Fixture_02_Full_Pipeline_Classifications_And_RowSourceSpecs_Are_Populated()
    {
        // Fixture 02: frmCaseManagement with a cboCaseworker (static RowSource)
        // driving cboClient (empty RowSource) via AfterUpdate, and a
        // Form_BeforeUpdate validation guard.
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.cboClient) Then Cancel = True
            End Sub

            Private Sub cboCaseworker_AfterUpdate()
                Me!cboClient.RowSource = "SELECT ClientID, ClientName FROM tblClients WHERE CaseworkerID = " & Me.cboCaseworker
                Me!cboClient.Requery
            End Sub
            """;

        var cboCaseworker = Control("cboCaseworker", "ComboBox", Props(
            ("RowSource", "SELECT CaseworkerID, CaseworkerName FROM tblCaseworkers ORDER BY CaseworkerName"),
            ("RowSourceType", "Table/Query"),
            ("BoundColumn", 1),
            ("ColumnCount", 2),
            ("ColumnWidths", "0;1440")));
        var cboClient = Control("cboClient", "ComboBox", Props(
            ("RowSource", ""),
            ("RowSourceType", "Table/Query"),
            ("BoundColumn", 1),
            ("ColumnCount", 2)));

        var form = Form("frmCaseManagement", cboCaseworker, cboClient);

        var spec = Spec("frmCaseManagement",
            eventHandlers: new[]
            {
                HandlerRef("BeforeUpdate", "BeforeUpdate", "Form_BeforeUpdate"),
                HandlerRef("AfterUpdate", "AfterUpdate", "cboCaseworker_AfterUpdate",
                    EventHandlerHostKind.Control, "cboCaseworker"),
            },
            validationRules: new[]
            {
                ValidationRef(ValidationRuleSource.BeforeUpdateHandler),
            });

        var result = EnrichmentPipelineRunner.Run(spec, form, ParseProcedures(src));

        // Event handler classifications
        var handlers = result.EnrichedSpec.EventHandlers!;
        Assert.Equal("Validation", handlers.Single(h => h.VbaProcedureName == "Form_BeforeUpdate").Classification);
        Assert.Equal("CascadingUpdate",
            handlers.Single(h => h.VbaProcedureName == "cboCaseworker_AfterUpdate").Classification);

        // Validation rules
        Assert.Equal("Required", result.EnrichedSpec.ValidationRules![0].Classification);
        var extractedRule = Assert.Single(result.ValidationRules.Where(r =>
            r.Origin == ValidationRuleOrigin.BeforeUpdateHandler));
        Assert.Equal(ValidationRuleKind.Required, extractedRule.Kind);

        // RowSource specs
        Assert.Equal(2, result.RowSourceSpecs.Count);
        var clientSpec = result.RowSourceSpecs.Single(s => s.ControlName == "cboClient");
        Assert.Equal(RowSourceKind.Empty, clientSpec.Kind);
        var driver = Assert.Single(clientSpec.CascadingDrivers!);
        Assert.Equal("cboCaseworker", driver.DriverControlName);

        var caseworkerSpec = result.RowSourceSpecs.Single(s => s.ControlName == "cboCaseworker");
        Assert.Equal(RowSourceKind.TableOrQueryInlineSql, caseworkerSpec.Kind);
        Assert.Null(caseworkerSpec.CascadingDrivers);
    }

    // ── Original spec reference integrity ────────────────────────────

    [Fact]
    public void Run_Returns_Same_List_Reference_When_No_Classifications_Apply()
    {
        // When nothing changes, the enriched spec reuses the original list objects
        var form = Form("frmTest");
        var handlers = new[] { HandlerRef("OnLoad", "Load", "Form_Load") };
        var spec = Spec("frmTest", eventHandlers: handlers);

        var result = EnrichmentPipelineRunner.Run(spec, form);

        // Same reference — no unnecessary allocations
        Assert.Same(handlers, result.EnrichedSpec.EventHandlers);
    }
}
