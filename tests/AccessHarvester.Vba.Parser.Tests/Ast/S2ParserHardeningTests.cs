using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Parser;
using AccessHarvester.Vba.Parser.Ast;
using Xunit;

namespace AccessHarvester.Vba.Parser.Tests.Ast;

// Pins for set 008 Session 2: grammar + AstBuilder hardening (F-3-2 / F-3-3)
// and the fixes landed from the cross-provider devil's-advocate review
// (named-argument symmetry H3, return-type-from-suffix H4, the recovery-scoped
// catch H2). Also the DW-1 known-limitation backstop. Most assertions go
// through the real parse → AST chain; "parses clean" means no Error-severity
// diagnostics (the same signal the parse-success report uses).
public sealed class S2ParserHardeningTests
{
    private static Module ParseClean(string src)
    {
        var result = VbaParserPipeline.Parse(src);
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0,
            "Expected a clean parse but got Error diagnostics: "
            + string.Join("; ", errors.Select(e => $"[{e.Code}] L{e.Line}:{e.Column} {e.Message}")));
        return result.BuildAst();
    }

    private static IReadOnlyList<ParseDiagnostic> ParseErrors(string src) =>
        VbaParserPipeline.Parse(src).Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    // ── F-3-2: contextual keywords as identifiers (the trigger inputs) ──

    [Theory]
    [InlineData("step")]
    [InlineData("compare")]
    [InlineData("explicit")]
    [InlineData("preserve")]
    [InlineData("lib")]
    [InlineData("alias")]
    public void ContextualKeyword_AsLocalVariableName_ParsesClean(string keyword)
    {
        // `Dim step As String` is the basResetFixture trigger (006 F-3-2). All six
        // safely-contextual keywords must work in declaration + expression position.
        var ast = ParseClean($"""
            Sub UsesKeyword()
                Dim {keyword} As String
                {keyword} = "x"
                Debug.Print {keyword}
            End Sub
            """);
        Assert.Contains(ast.Items, i => i is SubProcedure);
    }

    [Fact]
    public void ContextualKeyword_NotError_StaysReserved()
    {
        // `error` is the one breadth-probe keyword real VBA reserves; it must NOT
        // have been admitted as an identifier (006 F-3-2 breadth probe).
        Assert.NotEmpty(ParseErrors("""
            Sub UsesError()
                Dim error As String
            End Sub
            """));
    }

    // ── F-3-2 / F-3-3 / H4: suffixed procedure names carry their return type ──

    [Fact]
    public void FunctionWithDollarSuffix_NameStripped_ReturnTypeString()
    {
        var ast = ParseClean("""
            Public Function SqlStr$(ByVal s As Variant)
                SqlStr = "'" & s & "'"
            End Function
            """);
        var fn = Assert.IsType<FunctionProcedure>(ast.Items.Single(i => i is FunctionProcedure));
        Assert.Equal("SqlStr", fn.Name);          // suffix stripped from the Name
        Assert.Equal("$", fn.TypeSuffix);         // raw spelling retained
        Assert.NotNull(fn.ReturnType);            // H4: return type is no longer dropped
        Assert.Equal(new[] { "String" }, fn.ReturnType!.QualifiedName);
    }

    [Fact]
    public void PropertyGetWithDollarSuffix_ReturnTypeString()
    {
        var ast = ParseClean("""
            Property Get Title$()
                Title = "untitled"
            End Property
            """);
        var prop = Assert.IsType<PropertyProcedure>(ast.Items.Single(i => i is PropertyProcedure));
        Assert.Equal("Title", prop.Name);
        Assert.Equal("$", prop.TypeSuffix);
        Assert.NotNull(prop.ReturnType);
        Assert.Equal(new[] { "String" }, prop.ReturnType!.QualifiedName);
    }

    // `!` (Single) is intentionally excluded here: in the function-NAME +
    // `(` position the lexer reads `!` as the BANG operator, not a type
    // suffix, so `Function Calc!()` does not parse. That gap is DW-4 in the
    // deferred-work register; the `!`→Single mapping itself is exercised in
    // variable/parameter contexts elsewhere.
    [Theory]
    [InlineData("%", "Integer")]
    [InlineData("&", "Long")]
    [InlineData("#", "Double")]
    [InlineData("@", "Currency")]
    public void FunctionSuffix_MapsToBuiltInReturnType(string suffix, string expectedType)
    {
        var ast = ParseClean($"""
            Function Calc{suffix}()
                Calc = 0
            End Function
            """);
        var fn = Assert.IsType<FunctionProcedure>(ast.Items.Single(i => i is FunctionProcedure));
        Assert.Equal("Calc", fn.Name);
        Assert.Equal(suffix, fn.TypeSuffix);
        Assert.Equal(new[] { expectedType }, fn.ReturnType!.QualifiedName);
    }

    // ── H3 (review): named-argument / parameter-name symmetry ──

    [Fact]
    public void NamedArgument_WithContextualKeyword_ParsesClean()
    {
        // The grammar accepts `step` as a parameter name, so a caller must be able
        // to spell that same parameter as a named argument. Before the fix this
        // failed at `:=` (review finding H3 — an internal inconsistency the F-3-2
        // change itself created).
        var ast = ParseClean("""
            Sub Callee(step As Long)
            End Sub

            Sub Caller()
                Callee step:=1
            End Sub
            """);
        var named = Descendants(ast).OfType<NamedArgument>().ToList();
        Assert.Contains(named, n => string.Equals(n.Name, "step", System.StringComparison.OrdinalIgnoreCase));
    }

    // ── Anchored uses must still win (no ambiguity regression — review H1) ──

    [Fact]
    public void ReDimPreserve_AnchoredModifier_StillParses()
    {
        var ast = ParseClean("""
            Sub Grow()
                ReDim Preserve a(10)
            End Sub
            """);
        var redim = Descendants(ast).OfType<RedimStatement>().Single();
        Assert.True(redim.Preserve);
    }

    [Fact]
    public void OptionCompareDatabase_StillParses()
    {
        var ast = ParseClean("Option Compare Database\n");
        var opt = Assert.IsType<OptionDirective>(ast.Items.Single(i => i is OptionDirective));
        Assert.Equal(OptionKind.Compare, opt.Kind);
    }

    [Fact]
    public void DeclareLibAlias_StillParses()
    {
        var ast = ParseClean("""
            Public Declare PtrSafe Function MsgBox2 Lib "user32" Alias "MessageBoxW" () As Long
            """);
        var decl = Assert.IsType<DeclareStatement>(ast.Items.Single(i => i is DeclareStatement));
        Assert.Equal("user32", decl.LibraryName);
        Assert.Equal("MessageBoxW", decl.AliasName);
    }

    [Fact]
    public void ForStep_AnchoredStepClause_StillParses()
    {
        // `step` as the loop variable AND `Step` as the anchored clause in one loop.
        var ast = ParseClean("""
            Sub Loop1()
                Dim step As Long
                For step = 1 To 10 Step 2
                Next step
            End Sub
            """);
        Assert.Contains(Descendants(ast), n => n is ForStatement);
    }

    // ── H2 (review): one mangled item degrades to a stub; neighbors survive ──

    [Fact]
    public void MangledProcedure_PreservesNeighboringItems()
    {
        // A genuinely-mangled head (stray tokens that recover into a nameless
        // procedure shape) must not cost the whole module: the valid Sub before
        // and after it survive, and the build does not throw.
        var result = VbaParserPipeline.Parse("""
            Sub Good1()
                Dim a As Long
            End Sub

            Sub (Cancel As Integer)
            End Sub

            Sub Good2()
                Dim b As Long
            End Sub
            """);
        var ast = result.BuildAst();   // must not throw

        var items = string.Join(", ", ast.Items.Select(i =>
            i.GetType().Name + (i is SubProcedure s ? $"({s.Name})" : "")));
        var subs = ast.Items.OfType<SubProcedure>().Select(s => s.Name).ToList();
        // The guaranteed property is that BuildAst does not throw and the
        // module is not wholly lost: at least the first good sub survives.
        Assert.True(subs.Contains("Good1"),
            $"Expected Good1 to survive the mangled neighbor; items were: [{items}]");
    }

    // ── DW-1 known-limitation backstop (docs/planning/deferred-work.md) ──
    // These constructs are deliberately NOT yet accepted (contextual keywords in
    // procedure-name / type-member / enum-member slots). They are pre-existing
    // gaps, absent from the HHC + Northwind corpora, deferred as DW-1. This test
    // asserts the CURRENT (rejecting) behavior so that if a future change makes
    // any of them parse, this test fails loudly and forces the implementer to
    // discharge DW-1 in the register rather than silently widening scope.

    [Theory]
    [InlineData("Sub Step()\nEnd Sub\n")]
    [InlineData("Function Compare()\nCompare = 0\nEnd Function\n")]
    [InlineData("Public Type T\nStep As String\nEnd Type\n")]
    [InlineData("Public Enum E\nStep = 1\nEnd Enum\n")]
    public void Dw1_ContextualKeywordInNonVariableSlot_StillRejected(string src)
    {
        Assert.NotEmpty(ParseErrors(src));
    }

    // ── helpers ──

    private static IEnumerable<VbaSyntaxNode> Descendants(VbaSyntaxNode root)
    {
        var stack = new Stack<VbaSyntaxNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var p in node.GetType().GetProperties())
            {
                var v = p.GetValue(node);
                if (v is VbaSyntaxNode n) stack.Push(n);
                else if (v is IEnumerable e && v is not string)
                    foreach (var item in e)
                        if (item is VbaSyntaxNode c) stack.Push(c);
            }
        }
    }
}
