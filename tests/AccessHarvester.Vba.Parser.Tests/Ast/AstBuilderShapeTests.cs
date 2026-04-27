using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Vba.Parser.Tests.Ast;

// Validates that representative VBA inputs produce the AST shape the
// design doc commits to (docs/session-sets/parser-infrastructure/
// ast-shape.md §4). Each test asserts shape — type, count, and key
// fields — rather than full equality, so adding a node carries
// minimal disruption.
public class AstBuilderShapeTests
{
    private static Module BuildAst(string source) =>
        VbaParserPipeline.Parse(source).BuildAst();

    [Fact]
    public void Module_EmptySource_HasNoItems()
    {
        var ast = BuildAst("");
        Assert.NotNull(ast);
        Assert.Empty(ast.Items);
        Assert.IsType<SourceProvenance>(ast.Provenance);
    }

    [Fact]
    public void Module_OptionExplicit_BuildsOptionDirective()
    {
        var ast = BuildAst("Option Explicit\n");
        var item = Assert.Single(ast.Items);
        var option = Assert.IsType<OptionDirective>(item);
        Assert.Equal(OptionKind.Explicit, option.Kind);
    }

    [Fact]
    public void Module_OptionCompareDatabase_PreservesArgument()
    {
        var ast = BuildAst("Option Compare Database\n");
        var option = Assert.IsType<OptionDirective>(ast.Items.Single());
        Assert.Equal(OptionKind.Compare, option.Kind);
        Assert.Equal("Database", option.Argument);
    }

    [Fact]
    public void SubProcedure_NoParameters_BuildsEmptyParameterList()
    {
        var src = """
            Public Sub Greet()
                Debug.Print "hi"
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = Assert.IsType<SubProcedure>(ast.Items.Single());
        Assert.Equal("Greet", sub.Name);
        Assert.Equal(AccessModifier.Public, sub.Access);
        Assert.False(sub.IsStatic);
        Assert.Empty(sub.Parameters);
        Assert.Single(sub.Body);
    }

    [Fact]
    public void SubProcedure_WithByValAndOptionalParameters_PreservesModifiers()
    {
        var src = """
            Sub S(ByVal x As Long, Optional y As String = "z")
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = Assert.IsType<SubProcedure>(ast.Items.Single());
        Assert.Collection(sub.Parameters,
            p => {
                Assert.Equal("x", p.Name);
                Assert.Equal(ParameterPassMode.ByVal, p.PassMode);
                Assert.Equal(ParameterModifier.Required, p.Modifier);
                Assert.NotNull(p.Type);
                Assert.Equal(new[] { "Long" }, p.Type!.QualifiedName);
                Assert.Null(p.DefaultValue);
            },
            p => {
                Assert.Equal("y", p.Name);
                Assert.Equal(ParameterModifier.Optional, p.Modifier);
                Assert.NotNull(p.DefaultValue);
                var lit = Assert.IsType<LiteralExpression>(p.DefaultValue);
                Assert.Equal(LiteralKind.String, lit.Kind);
            });
    }

    [Fact]
    public void FunctionProcedure_WithReturnType_BuildsReturnTypeReference()
    {
        var src = """
            Private Function Add(a As Long, b As Long) As Long
                Add = a + b
            End Function
            """;
        var ast = BuildAst(src);
        var fn = Assert.IsType<FunctionProcedure>(ast.Items.Single());
        Assert.Equal("Add", fn.Name);
        Assert.Equal(AccessModifier.Private, fn.Access);
        Assert.NotNull(fn.ReturnType);
        Assert.Equal(new[] { "Long" }, fn.ReturnType!.QualifiedName);
    }

    [Fact]
    public void PropertyGet_BuildsPropertyKindGet()
    {
        var src = """
            Public Property Get Name() As String
                Name = "x"
            End Property
            """;
        var ast = BuildAst(src);
        var prop = Assert.IsType<PropertyProcedure>(ast.Items.Single());
        Assert.Equal(PropertyKind.Get, prop.Kind);
        Assert.NotNull(prop.ReturnType);
    }

    [Fact]
    public void IfBlock_WithElseIfAndElse_BuildsClauses()
    {
        var src = """
            Sub S()
                If x = 1 Then
                    a = 1
                ElseIf x = 2 Then
                    a = 2
                Else
                    a = 3
                End If
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ifStmt = Assert.IsType<IfStatement>(sub.Body.Single());
        Assert.False(ifStmt.IsSingleLine);
        Assert.Single(ifStmt.ThenBody);
        Assert.Single(ifStmt.ElseIfClauses);
        Assert.NotNull(ifStmt.ElseBody);
        Assert.Single(ifStmt.ElseBody!);
    }

    [Fact]
    public void IfSingleLine_BuildsIsSingleLineTrue()
    {
        var src = """
            Sub S()
                If x = 1 Then a = 1 Else a = 2
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ifStmt = Assert.IsType<IfStatement>(sub.Body.Single());
        Assert.True(ifStmt.IsSingleLine);
        Assert.Single(ifStmt.ThenBody);
        Assert.NotNull(ifStmt.ElseBody);
    }

    [Fact]
    public void ForNext_BuildsLoopVariableAndStep()
    {
        var src = """
            Sub S()
                For i = 1 To 10 Step 2
                    Debug.Print i
                Next i
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var forStmt = Assert.IsType<ForStatement>(sub.Body.Single());
        Assert.Equal("i", forStmt.LoopVariable);
        Assert.NotNull(forStmt.Step);
    }

    [Fact]
    public void ForEach_BuildsCollectionExpression()
    {
        var src = """
            Sub S()
                For Each item In items
                    Debug.Print item
                Next
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var fe = Assert.IsType<ForEachStatement>(sub.Body.Single());
        Assert.Equal("item", fe.LoopVariable);
        Assert.IsType<IdentifierExpression>(fe.Collection);
    }

    [Fact]
    public void DoLoop_PreTestWhile_TaggedCorrectly()
    {
        var src = """
            Sub S()
                Do While x > 0
                    x = x - 1
                Loop
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var dl = Assert.IsType<DoLoopStatement>(sub.Body.Single());
        Assert.Equal(DoLoopKind.PreTestWhile, dl.Kind);
        Assert.NotNull(dl.Condition);
    }

    [Fact]
    public void DoLoop_PostTestUntil_TaggedCorrectly()
    {
        var src = """
            Sub S()
                Do
                    x = x - 1
                Loop Until x = 0
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var dl = Assert.IsType<DoLoopStatement>(sub.Body.Single());
        Assert.Equal(DoLoopKind.PostTestUntil, dl.Kind);
    }

    [Fact]
    public void SelectCase_BuildsCases()
    {
        var src = """
            Sub S()
                Select Case x
                    Case 1
                        a = 1
                    Case 2 To 5
                        a = 2
                    Case Is > 10
                        a = 3
                    Case Else
                        a = 0
                End Select
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var sel = Assert.IsType<SelectCaseStatement>(sub.Body.Single());
        Assert.Equal(3, sel.Cases.Count);
        Assert.NotNull(sel.CaseElseBody);

        Assert.IsType<CaseValueExpression>(sel.Cases[0].Expressions.Single());
        Assert.IsType<CaseRangeExpression>(sel.Cases[1].Expressions.Single());
        var isCmp = Assert.IsType<CaseIsCompareExpression>(sel.Cases[2].Expressions.Single());
        Assert.Equal(BinaryOperator.Greater, isCmp.Operator);
    }

    [Fact]
    public void With_BuildsTargetAndBody()
    {
        var src = """
            Sub S()
                With Me.txtName
                    .Value = "x"
                    .SetFocus
                End With
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var w = Assert.IsType<WithStatement>(sub.Body.Single());
        Assert.IsType<MemberAccessExpression>(w.Target);
        Assert.Equal(2, w.Body.Count);
    }

    [Fact]
    public void OnError_GotoHandler_BuildsLabel()
    {
        var src = """
            Sub S()
                On Error GoTo Handler
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var oe = Assert.IsType<OnErrorStatement>(sub.Body.Single());
        Assert.Equal(OnErrorKind.Goto, oe.Kind);
        Assert.Equal("Handler", oe.Label);
    }

    [Fact]
    public void OnError_ResumeNext_HasNullLabel()
    {
        var src = """
            Sub S()
                On Error Resume Next
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var oe = Assert.IsType<OnErrorStatement>(sub.Body.Single());
        Assert.Equal(OnErrorKind.ResumeNext, oe.Kind);
        Assert.Null(oe.Label);
    }

    [Fact]
    public void LocalDim_WithMultipleDeclarators_PreservesEach()
    {
        var src = """
            Sub S()
                Dim a As Long, b As String, c
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ld = Assert.IsType<LocalDeclarationStatement>(sub.Body.Single());
        Assert.Equal(LocalDeclarationKind.Dim, ld.Kind);
        Assert.Equal(3, ld.Declarators.Count);
        Assert.NotNull(ld.Declarators[0].Type);
        Assert.NotNull(ld.Declarators[1].Type);
        Assert.Null(ld.Declarators[2].Type);            // implicit Variant
    }

    [Fact]
    public void LocalDim_WithAsNew_BuildsAutoInstantiated()
    {
        var src = """
            Sub S()
                Dim x As New Collection
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ld = Assert.IsType<LocalDeclarationStatement>(sub.Body.Single());
        Assert.True(ld.Declarators.Single().IsAutoInstantiated);
    }

    [Fact]
    public void SetStatement_BuildsTargetAndValue()
    {
        var src = """
            Sub S()
                Set x = New Collection
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ss = Assert.IsType<SetStatement>(sub.Body.Single());
        Assert.IsType<IdentifierExpression>(ss.Target);
        Assert.IsType<NewExpression>(ss.Value);
    }

    [Fact]
    public void LetStatement_ImplicitLet_FlagFalse()
    {
        var src = """
            Sub S()
                x = 1
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ls = Assert.IsType<LetStatement>(sub.Body.Single());
        Assert.False(ls.ExplicitLet);
    }

    [Fact]
    public void CallStatement_ExplicitCall_FlagTrue()
    {
        var src = """
            Sub S()
                Call DoIt(1, 2)
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var cs = Assert.IsType<CallStatement>(sub.Body.Single());
        Assert.True(cs.ExplicitCall);
        // The IndexOrCall's argument list lives on the Target l-expr.
        var target = Assert.IsType<IndexOrCallExpression>(cs.Target);
        Assert.Equal(2, target.Arguments.Count);
    }

    [Fact]
    public void Expression_BinaryAndUnary_PreservesOperators()
    {
        var src = """
            Sub S()
                x = -1 + 2 * 3
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ls = Assert.IsType<LetStatement>(sub.Body.Single());
        var add = Assert.IsType<BinaryExpression>(ls.Value);
        Assert.Equal(BinaryOperator.Add, add.Operator);
        var unary = Assert.IsType<UnaryExpression>(add.Left);
        Assert.Equal(UnaryOperator.Minus, unary.Operator);
        var mul = Assert.IsType<BinaryExpression>(add.Right);
        Assert.Equal(BinaryOperator.Multiply, mul.Operator);
    }

    [Fact]
    public void Expression_MemberAccessChain_PreservesShape()
    {
        var src = """
            Sub S()
                x = Me.Form.Caption
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var ls = (LetStatement)sub.Body.Single();
        var captionAccess = Assert.IsType<MemberAccessExpression>(ls.Value);
        Assert.Equal("Caption", captionAccess.MemberName);
        var formAccess = Assert.IsType<MemberAccessExpression>(captionAccess.Target);
        Assert.Equal("Form", formAccess.MemberName);
        Assert.IsType<MeExpression>(formAccess.Target);
    }

    [Fact]
    public void Expression_BangAccessAndImplicitWith_PreservedAsDistinctNodes()
    {
        var src = """
            Sub S()
                With Me
                    x = !Field1
                    y = .Caption
                End With
            End Sub
            """;
        var ast = BuildAst(src);
        var sub = (SubProcedure)ast.Items.Single();
        var with = Assert.IsType<WithStatement>(sub.Body.Single());
        var firstLet = Assert.IsType<LetStatement>(with.Body[0]);
        Assert.IsType<ImplicitWithBangExpression>(firstLet.Value);
        var secondLet = Assert.IsType<LetStatement>(with.Body[1]);
        Assert.IsType<ImplicitWithMemberExpression>(secondLet.Value);
    }

    [Fact]
    public void TypeDeclaration_BuildsMembers()
    {
        var src = """
            Public Type CustomerRow
                Id As Long
                Name As String
            End Type
            """;
        var ast = BuildAst(src);
        var t = Assert.IsType<TypeDeclaration>(ast.Items.Single());
        Assert.Equal("CustomerRow", t.Name);
        Assert.Equal(2, t.Members.Count);
    }

    [Fact]
    public void EnumDeclaration_BuildsMembersWithExplicitAndImplicitValues()
    {
        var src = """
            Public Enum Color
                Red = 1
                Green
                Blue
            End Enum
            """;
        var ast = BuildAst(src);
        var e = Assert.IsType<EnumDeclaration>(ast.Items.Single());
        Assert.Equal(3, e.Members.Count);
        Assert.NotNull(e.Members[0].Value);
        Assert.Null(e.Members[1].Value);
        Assert.Null(e.Members[2].Value);
    }

    [Fact]
    public void DeclareStatement_BuildsLibraryAndAlias()
    {
        var src = """
            Public Declare PtrSafe Function GetTickCount Lib "kernel32" Alias "GetTickCount" () As Long
            """;
        var ast = BuildAst(src);
        var d = Assert.IsType<DeclareStatement>(ast.Items.Single());
        Assert.Equal(DeclareKind.Function, d.Kind);
        Assert.True(d.IsPtrSafe);
        Assert.Equal("kernel32", d.LibraryName);
        Assert.Equal("GetTickCount", d.AliasName);
    }

    [Fact]
    public void Provenance_OnAllNodesIsSourceProvenance_WhenInputIsClean()
    {
        var src = """
            Public Sub Greet()
                Debug.Print "hi"
            End Sub
            """;
        var ast = BuildAst(src);
        AssertAllProvenance(ast, p => Assert.IsType<SourceProvenance>(p));
    }

    private static void AssertAllProvenance(VbaSyntaxNode node, System.Action<Provenance> check)
    {
        check(node.Provenance);
        foreach (var child in TestAstChildren(node))
            AssertAllProvenance(child, check);
    }

    // Tests cannot reach the internal AstChildren helper in the
    // production assembly, so duplicate the minimum traversal here.
    // This is intentional — keeping the production helper internal.
    private static System.Collections.Generic.IEnumerable<VbaSyntaxNode> TestAstChildren(VbaSyntaxNode node)
    {
        var props = node.GetType().GetProperties();
        foreach (var p in props)
        {
            var v = p.GetValue(node);
            if (v is VbaSyntaxNode n) yield return n;
            else if (v is System.Collections.IEnumerable enumerable && v is not string)
            {
                foreach (var item in enumerable)
                    if (item is VbaSyntaxNode child) yield return child;
            }
        }
    }
}
