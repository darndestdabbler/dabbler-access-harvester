using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccessHarvester.Access;
using AccessHarvester.Pipeline.Enrichment;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Pipeline.Tests;

// Hermetic unit tests for ValidationRuleExtractor. The three entry points
// (table, form, BeforeUpdate handler) are exercised independently; the
// pipeline that stitches them together is a Session 4 deliverable so these
// tests focus on the per-source extraction primitives only.
public class ValidationRuleExtractorTests
{
    // ── Helpers ────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, JsonElement> Props(params (string Key, object Value)[] entries)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entries)
            dict[k] = JsonSerializer.SerializeToElement(v);
        return dict;
    }

    private static AccessControl Control(string name, IReadOnlyDictionary<string, JsonElement>? props = null,
                                         IReadOnlyList<AccessControl>? children = null,
                                         string type = "TextBox")
        => new() { Type = type, Name = name, Properties = props, Children = children };

    private static FormMetadata Form(string name, IReadOnlyList<AccessControl>? children = null,
                                     IReadOnlyDictionary<string, JsonElement>? props = null)
        => new() { Name = name, Children = children, Properties = props };

    private static ProcedureDeclaration Procedure(string source, string name)
    {
        var module = VbaParserPipeline.Parse(source).BuildAst();
        return module.Items.OfType<ProcedureDeclaration>().First(p => p.Name == name);
    }

    // ── Table-side extraction ──────────────────────────────────────────

    [Fact]
    public void Table_Field_Required_True_Emits_Required_Rule()
    {
        var table = new TableMetadata
        {
            Name = "tblCases",
            Fields = new[]
            {
                new TableField { Name = "CaseId", Required = true },
            },
        };

        var rules = ValidationRuleExtractor.ExtractFromTable(table);

        Assert.Single(rules);
        Assert.Equal(ValidationRuleScope.TableField, rules[0].Scope);
        Assert.Equal("tblCases", rules[0].SourceObject);
        Assert.Equal("CaseId", rules[0].FieldOrControlName);
        Assert.Equal(ValidationRuleOrigin.TableFieldRequired, rules[0].Origin);
        Assert.Equal(ValidationRuleKind.Required, rules[0].Kind);
        Assert.Null(rules[0].Expression);
    }

    [Fact]
    public void Table_Field_Required_False_Emits_Nothing()
    {
        var table = new TableMetadata
        {
            Name = "tblCases",
            Fields = new[]
            {
                new TableField { Name = "Notes", Required = false },
            },
        };

        Assert.Empty(ValidationRuleExtractor.ExtractFromTable(table));
    }

    [Fact]
    public void Table_Field_ValidationRule_Between_Date_Literals_Parses_As_Range()
    {
        var table = new TableMetadata
        {
            Name = "tblPermits",
            Fields = new[]
            {
                new TableField
                {
                    Name = "IssueDate",
                    ValidationRule = "Between #1/1/2000# And Date()",
                    ValidationText = "Issue date must be between 2000-01-01 and today.",
                },
            },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));

        Assert.Equal(ValidationRuleKind.Range, rule.Kind);
        Assert.Equal(ValidationRuleOrigin.TableFieldValidationRule, rule.Origin);
        Assert.Equal("Between #1/1/2000# And Date()", rule.Expression);
        Assert.Equal("Issue date must be between 2000-01-01 and today.", rule.Message);
        Assert.NotNull(rule.Range);
        Assert.Equal("#1/1/2000#", rule.Range!.Minimum);
        Assert.True(rule.Range.MinimumInclusive);
        Assert.Equal("Date()", rule.Range.Maximum);
        Assert.True(rule.Range.MaximumInclusive);
    }

    [Theory]
    [InlineData(">=0 And <=100", "0", true, "100", true)]
    [InlineData(">0 And <100", "0", false, "100", false)]
    [InlineData(">=0 And <100", "0", true, "100", false)]
    public void Table_Field_ValidationRule_Compound_Comparisons_Parse_As_Range(
        string expr, string min, bool minInclusive, string max, bool maxInclusive)
    {
        var table = new TableMetadata
        {
            Name = "tblScores",
            Fields = new[] { new TableField { Name = "Score", ValidationRule = expr } },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));

        Assert.Equal(ValidationRuleKind.Range, rule.Kind);
        Assert.Equal(min, rule.Range!.Minimum);
        Assert.Equal(minInclusive, rule.Range.MinimumInclusive);
        Assert.Equal(max, rule.Range.Maximum);
        Assert.Equal(maxInclusive, rule.Range.MaximumInclusive);
    }

    [Theory]
    [InlineData(">=0", "0", true)]
    [InlineData(">0", "0", false)]
    public void Table_Field_ValidationRule_Half_Range_Lower_Parses_As_Range(
        string expr, string min, bool inclusive)
    {
        var table = new TableMetadata
        {
            Name = "tblOrders",
            Fields = new[] { new TableField { Name = "Qty", ValidationRule = expr } },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));
        Assert.Equal(ValidationRuleKind.Range, rule.Kind);
        Assert.Equal(min, rule.Range!.Minimum);
        Assert.Equal(inclusive, rule.Range.MinimumInclusive);
        Assert.Null(rule.Range.Maximum);
    }

    [Theory]
    [InlineData("<=100", "100", true)]
    [InlineData("<100", "100", false)]
    public void Table_Field_ValidationRule_Half_Range_Upper_Parses_As_Range(
        string expr, string max, bool inclusive)
    {
        var table = new TableMetadata
        {
            Name = "tblOrders",
            Fields = new[] { new TableField { Name = "Qty", ValidationRule = expr } },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));
        Assert.Equal(ValidationRuleKind.Range, rule.Kind);
        Assert.Equal(max, rule.Range!.Maximum);
        Assert.Equal(inclusive, rule.Range.MaximumInclusive);
        Assert.Null(rule.Range.Minimum);
    }

    [Fact]
    public void Table_Field_ValidationRule_Like_Pattern_Falls_Through_To_CustomPredicate()
    {
        var table = new TableMetadata
        {
            Name = "tblCustomers",
            Fields = new[]
            {
                new TableField { Name = "Postal", ValidationRule = "Like \"##### \"" },
            },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));
        Assert.Equal(ValidationRuleKind.CustomPredicate, rule.Kind);
        Assert.Equal("Like \"##### \"", rule.Expression);
    }

    [Fact]
    public void Table_Field_ValidationRule_Unstructured_Text_Emits_Unstructured()
    {
        var table = new TableMetadata
        {
            Name = "tblTickets",
            Fields = new[]
            {
                new TableField { Name = "Status", ValidationRule = "see business rules" },
            },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));
        Assert.Equal(ValidationRuleKind.Unstructured, rule.Kind);
        Assert.Equal("see business rules", rule.Expression);
    }

    [Fact]
    public void Table_Field_Required_And_ValidationRule_Emits_Both_In_Order()
    {
        var table = new TableMetadata
        {
            Name = "tblOrders",
            Fields = new[]
            {
                new TableField
                {
                    Name = "Qty",
                    Required = true,
                    ValidationRule = ">0",
                },
            },
        };

        var rules = ValidationRuleExtractor.ExtractFromTable(table);

        Assert.Equal(2, rules.Count);
        Assert.Equal(ValidationRuleOrigin.TableFieldRequired, rules[0].Origin);
        Assert.Equal(ValidationRuleOrigin.TableFieldValidationRule, rules[1].Origin);
        Assert.Equal(ValidationRuleKind.Range, rules[1].Kind);
    }

    [Fact]
    public void Table_Level_ValidationRule_Emits_Last()
    {
        var table = new TableMetadata
        {
            Name = "tblBookings",
            Fields = new[]
            {
                new TableField { Name = "EndDate", Required = true },
            },
            ValidationRule = "[EndDate] >= [StartDate]",
            ValidationText = "End must be on or after start.",
        };

        var rules = ValidationRuleExtractor.ExtractFromTable(table);

        Assert.Equal(2, rules.Count);
        var record = rules[1];
        Assert.Equal(ValidationRuleScope.Table, record.Scope);
        Assert.Null(record.FieldOrControlName);
        Assert.Equal(ValidationRuleOrigin.TableValidationRule, record.Origin);
        Assert.Equal(ValidationRuleKind.CustomPredicate, record.Kind);
        Assert.Equal("[EndDate] >= [StartDate]", record.Expression);
        Assert.Equal("End must be on or after start.", record.Message);
    }

    // ── Form / control extraction ──────────────────────────────────────

    [Fact]
    public void Control_Required_True_Emits_Required_Rule()
    {
        var form = Form("frmCustomers", new[]
        {
            Control("txtCustomerName", Props(("Required", true))),
        });

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromForm(form));
        Assert.Equal(ValidationRuleScope.Control, rule.Scope);
        Assert.Equal("frmCustomers", rule.SourceObject);
        Assert.Equal("txtCustomerName", rule.FieldOrControlName);
        Assert.Equal(ValidationRuleOrigin.ControlRequired, rule.Origin);
        Assert.Equal(ValidationRuleKind.Required, rule.Kind);
    }

    [Fact]
    public void Control_ValidationRule_With_ValidationText_Carries_Message()
    {
        var form = Form("frmOrders", new[]
        {
            Control("txtQty", Props(
                ("ValidationRule", ">0"),
                ("ValidationText", "Quantity must be positive."))),
        });

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromForm(form));
        Assert.Equal(ValidationRuleKind.Range, rule.Kind);
        Assert.Equal(ValidationRuleOrigin.ControlValidationRule, rule.Origin);
        Assert.Equal("Quantity must be positive.", rule.Message);
        Assert.Equal("0", rule.Range!.Minimum);
        Assert.False(rule.Range.MinimumInclusive);
    }

    [Fact]
    public void Control_LimitToList_True_Emits_Membership_Rule()
    {
        var form = Form("frmCases", new[]
        {
            Control("cboCaseworker", Props(("LimitToList", true))),
        });

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromForm(form));
        Assert.Equal(ValidationRuleKind.Membership, rule.Kind);
        Assert.Equal(ValidationRuleOrigin.ControlLimitToList, rule.Origin);
        Assert.NotNull(rule.Membership);
        Assert.Equal("cboCaseworker", rule.Membership!.SourceControlName);
    }

    [Fact]
    public void Control_InputMask_Emits_Pattern_Rule_With_Verbatim_Mask()
    {
        var form = Form("frmContacts", new[]
        {
            Control("txtPhone", Props(("InputMask", "(999) 000-0000"))),
        });

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromForm(form));
        Assert.Equal(ValidationRuleKind.Pattern, rule.Kind);
        Assert.Equal(ValidationRuleOrigin.ControlInputMask, rule.Origin);
        Assert.Equal("(999) 000-0000", rule.Pattern!.InputMask);
        Assert.Equal("(999) 000-0000", rule.Expression);
    }

    [Fact]
    public void Form_Level_ValidationRule_Emits_Form_Scope_Rule_Last()
    {
        var form = Form(
            name: "frmBookings",
            children: new[] { Control("txtStart", Props(("Required", true))) },
            props: Props(
                ("ValidationRule", "[EndDate] >= [StartDate]"),
                ("ValidationText", "End must be on or after start.")));

        var rules = ValidationRuleExtractor.ExtractFromForm(form);

        Assert.Equal(2, rules.Count);
        Assert.Equal(ValidationRuleScope.Control, rules[0].Scope);
        var record = rules[1];
        Assert.Equal(ValidationRuleScope.Form, record.Scope);
        Assert.Equal(ValidationRuleOrigin.FormValidationRule, record.Origin);
        Assert.Null(record.FieldOrControlName);
        Assert.Equal(ValidationRuleKind.CustomPredicate, record.Kind);
        Assert.Equal("[EndDate] >= [StartDate]", record.Expression);
        Assert.Equal("End must be on or after start.", record.Message);
    }

    [Fact]
    public void Form_With_Nested_Section_Walks_Child_Controls_In_Order()
    {
        var form = Form("frmCustomers", new[]
        {
            Control("Detail", type: "Section", children: new[]
            {
                Control("txtFirst", Props(("Required", true))),
                Control("txtLast", Props(("Required", true))),
            }),
        });

        var rules = ValidationRuleExtractor.ExtractFromForm(form);
        Assert.Equal(2, rules.Count);
        Assert.Equal("txtFirst", rules[0].FieldOrControlName);
        Assert.Equal("txtLast", rules[1].FieldOrControlName);
    }

    [Fact]
    public void Form_With_No_Children_And_No_Properties_Emits_Nothing()
    {
        var form = Form("frmEmpty");
        Assert.Empty(ValidationRuleExtractor.ExtractFromForm(form));
    }

    // ── BeforeUpdate handler extraction ────────────────────────────────

    [Fact]
    public void BeforeUpdate_IsNull_Of_Control_Emits_Required_Rule_For_That_Control()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtCustomerName) Then
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rules = ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers");

        var rule = Assert.Single(rules);
        Assert.Equal(ValidationRuleScope.Form, rule.Scope);
        Assert.Equal("frmCustomers", rule.SourceObject);
        Assert.Equal("txtCustomerName", rule.FieldOrControlName);
        Assert.Equal(ValidationRuleOrigin.BeforeUpdateHandler, rule.Origin);
        Assert.Equal(ValidationRuleKind.Required, rule.Kind);
        Assert.NotNull(rule.Location);
        Assert.Equal("Form_BeforeUpdate", rule.Location!.ProcedureName);
    }

    [Fact]
    public void BeforeUpdate_IsNull_Or_Empty_String_Still_Emits_Required_For_Same_Control()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtCustomerName) Or Me.txtCustomerName = "" Then
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rule = Assert.Single(
            ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers"));
        Assert.Equal(ValidationRuleKind.Required, rule.Kind);
        Assert.Equal("txtCustomerName", rule.FieldOrControlName);
    }

    [Fact]
    public void BeforeUpdate_Custom_Comparison_Predicate_Emits_CustomPredicate_With_Expression()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If Me.txtAge < 18 Then
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rule = Assert.Single(
            ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmRegistration"));

        Assert.Equal(ValidationRuleKind.CustomPredicate, rule.Kind);
        // Multi-field / arbitrary-condition guards do not attribute back
        // to a specific control — the field slot stays null.
        Assert.Null(rule.FieldOrControlName);
        Assert.Equal("Me.txtAge < 18", rule.Expression);
    }

    [Fact]
    public void BeforeUpdate_Multiple_If_Cancel_Patterns_Each_Emit_A_Rule()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtCustomerName) Then
                    Cancel = True
                End If
                If IsNull(Me.txtPhone) Then
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rules = ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers");

        Assert.Equal(2, rules.Count);
        Assert.All(rules, r => Assert.Equal(ValidationRuleKind.Required, r.Kind));
        Assert.Equal("txtCustomerName", rules[0].FieldOrControlName);
        Assert.Equal("txtPhone", rules[1].FieldOrControlName);
    }

    [Fact]
    public void BeforeUpdate_If_Then_ElseIf_Each_Emit_A_Rule()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtA) Then
                    Cancel = True
                ElseIf IsNull(Me.txtB) Then
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rules = ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers");

        Assert.Equal(2, rules.Count);
        Assert.Equal("txtA", rules[0].FieldOrControlName);
        Assert.Equal("txtB", rules[1].FieldOrControlName);
    }

    [Fact]
    public void BeforeUpdate_With_No_Cancel_True_Emits_Nothing()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                Me.txtUpdated = Now()
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        Assert.Empty(ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers"));
    }

    [Fact]
    public void BeforeUpdate_Per_Control_Handler_Emits_Control_Scope_Rule()
    {
        var src = """
            Private Sub txtAge_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtAge) Then
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "txtAge_BeforeUpdate");
        var rule = Assert.Single(
            ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(
                proc, "frmRegistration", hostControlName: "txtAge"));

        Assert.Equal(ValidationRuleScope.Control, rule.Scope);
        // Per-control handler attributes back to the host control even
        // when the IsNull-guard targets a different field reference.
        Assert.Equal("txtAge", rule.FieldOrControlName);
    }

    [Fact]
    public void BeforeUpdate_Lifts_MsgBox_Literal_As_Message()
    {
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                If IsNull(Me.txtCustomerName) Then
                    MsgBox "Customer Name is required.", vbExclamation
                    Cancel = True
                End If
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rule = Assert.Single(
            ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers"));
        Assert.Equal("Customer Name is required.", rule.Message);
    }

    [Fact]
    public void BeforeUpdate_Cancel_Inside_For_Loop_Is_Not_Recognised()
    {
        // Per Phase 6.4 the recognised pattern is the top-level guard.
        // Cancel writes from inside loops or nested constructs do not
        // emit structured rules — the classifier still flags the handler
        // as Validation but no rule is produced here.
        var src = """
            Private Sub Form_BeforeUpdate(Cancel As Integer)
                Dim i As Integer
                For i = 1 To 3
                    Cancel = True
                Next i
            End Sub
            """;
        var proc = Procedure(src, "Form_BeforeUpdate");
        Assert.Empty(ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers"));
    }

    // ── Anchor-fixture sanity ──────────────────────────────────────────

    [Fact]
    public void Anchor_Fixture_01_Simple_Validation_Yields_Two_Required_Rules()
    {
        // Mirrors docs/test-cases/01-simple-validation.bas — the
        // canonical anchor fixture for Phase 6.4 validation extraction.
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
        var proc = Procedure(src, "Form_BeforeUpdate");
        var rules = ValidationRuleExtractor.ExtractFromBeforeUpdateHandler(proc, "frmCustomers");

        Assert.Equal(2, rules.Count);
        Assert.All(rules, r =>
        {
            Assert.Equal(ValidationRuleKind.Required, r.Kind);
            Assert.Equal(ValidationRuleOrigin.BeforeUpdateHandler, r.Origin);
            Assert.NotNull(r.Message);
        });
        Assert.Equal("txtCustomerName", rules[0].FieldOrControlName);
        Assert.Equal("Customer Name is required.", rules[0].Message);
        Assert.Equal("txtPhone", rules[1].FieldOrControlName);
        Assert.Equal("Phone Number is required.", rules[1].Message);
    }

    [Fact]
    public void Anchor_Fixture_06_Issue_Date_Range_Yields_Range_Rule()
    {
        // Mirrors expression 6c from docs/test-cases/06-calculated-expressions.txt:
        //   ValidationRule on tblPermits.IssueDate.
        var table = new TableMetadata
        {
            Name = "tblPermits",
            Fields = new[]
            {
                new TableField
                {
                    Name = "IssueDate",
                    ValidationRule = "Between #1/1/2000# And Date()",
                },
            },
        };

        var rule = Assert.Single(ValidationRuleExtractor.ExtractFromTable(table));
        Assert.Equal(ValidationRuleKind.Range, rule.Kind);
        Assert.Equal("#1/1/2000#", rule.Range!.Minimum);
        Assert.Equal("Date()", rule.Range.Maximum);
    }
}
