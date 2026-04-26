using System;
using System.Collections.Generic;
using System.IO;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Xunit;

namespace AccessHarvester.Vba.Grammar.Tests.Expressions;

// Parser tests for the Phase 1.4 expression section of Vba.g4.
//
// Coverage targets (from spec and hazard list):
//   - Primary expressions: literals, identifiers, Me/True/False/Null/
//     Nothing/Empty, parenthesized expressions
//   - Member access chains (.) and bang access (!) including the
//     H-B01 (bang/dot precedence) and H-B02 (bang preserved as
//     distinct AST node) hazards
//   - Implicit-With access (H-C01 leading-`.member`, H-C02
//     leading-`!key`)
//   - Index/call expressions with positional, named (H-D12), and
//     empty (H-D08) arguments
//   - Operator precedence per MS-VBAL §5.6.9: ^ * / \ Mod + - & comp
//     Not And Or Xor Eqv Imp; right-associativity of ^; H-B07 (Not vs
//     Is binding)
//   - TypeOf...Is (H-B06)
//   - New <typeName>
//   - AddressOf <l-expression>
//   - Bracketed identifiers (H-A08) in expression chains
//   - Date literals (H-A05) in expression position
public class VbaExpressionParserTests
{
    // ---------- Test plumbing ----------

    private sealed class ThrowingErrorListener
        : BaseErrorListener, IAntlrErrorListener<int>
    {
        public static readonly ThrowingErrorListener Instance = new();

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new InvalidOperationException(
                $"Parser syntax error at line {line}:{charPositionInLine}: {msg}");
        }

        public void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            int offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            throw new InvalidOperationException(
                $"Lexer syntax error at line {line}:{charPositionInLine}: {msg}");
        }
    }

    private static VbaParser.ExpressionEofContext Parse(string source)
    {
        var stream = new AntlrInputStream(source);
        var lexer = new VbaLexer(stream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(ThrowingErrorListener.Instance);

        var tokens = new CommonTokenStream(lexer);
        var parser = new VbaParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(ThrowingErrorListener.Instance);

        return parser.expressionEof();
    }

    private static VbaParser.ExpressionContext Expr(string source)
        => Parse(source).expression();

    // Walk an lExpression chain up to the leftmost primary (for shape
    // assertions on member-access chains).
    private static VbaParser.LExpressionContext UnwrapToLExpr(
        VbaParser.ExpressionContext ctx)
    {
        var lExprPrimary = Assert.IsType<VbaParser.LExprPrimaryContext>(ctx);
        return lExprPrimary.lExpression();
    }

    // ---------- Primaries ----------

    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("3.14")]
    [InlineData(".5")]
    [InlineData("1E10")]
    [InlineData("&H1F")]
    [InlineData("&O17")]
    [InlineData("\"hello\"")]
    [InlineData("\"a \"\"quoted\"\" word\"")]
    [InlineData("#1/1/2024#")]
    public void Literal_Parses(string source)
    {
        var ctx = Parse(source);
        Assert.NotNull(ctx);
    }

    [Theory]
    [InlineData("Me")]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("Null")]
    [InlineData("Nothing")]
    [InlineData("Empty")]
    public void SpecialPrimary_Parses(string source)
    {
        Assert.NotNull(Parse(source));
    }

    [Fact]
    public void Identifier_BareName_ParsesAsPrimary()
    {
        var lExpr = UnwrapToLExpr(Expr("foo"));
        var primary = Assert.IsType<VbaParser.PrimaryExprContext>(lExpr);
        Assert.IsType<VbaParser.IdentExprContext>(primary.primary());
    }

    [Fact]
    public void TypedIdentifier_WithStringSuffix_ParsesAsPrimary()
    {
        // Hazard H-A02: `sFilePath$` is a single TYPED_IDENTIFIER token
        // captured by the lexer; the parser sees it as a typed primary.
        var lExpr = UnwrapToLExpr(Expr("sFilePath$"));
        var primary = Assert.IsType<VbaParser.PrimaryExprContext>(lExpr);
        Assert.IsType<VbaParser.TypedIdentExprContext>(primary.primary());
    }

    [Fact]
    public void BracketedIdentifier_InPrimaryPosition_Parses()
    {
        // Hazard H-A08: `[Order Date]` is a single IDENTIFIER token
        // (lexer absorbs the brackets), so the parser sees it as a
        // bare identifier primary.
        Assert.NotNull(Parse("[Order Date]"));
    }

    [Fact]
    public void ParenthesizedExpression_Parses()
    {
        var lExpr = UnwrapToLExpr(Expr("(1 + 2)"));
        var primary = Assert.IsType<VbaParser.PrimaryExprContext>(lExpr);
        Assert.IsType<VbaParser.ParenExprContext>(primary.primary());
    }

    // ---------- Member access chains ----------

    [Fact]
    public void DotAccess_TwoLevels_ParsesLeftToRight()
    {
        // a.b.c -> ((a.b).c)
        var lExpr = UnwrapToLExpr(Expr("a.b.c"));
        var outer = Assert.IsType<VbaParser.MemberAccessContext>(lExpr);
        Assert.Equal("c", outer.memberName().GetText());

        var inner = Assert.IsType<VbaParser.MemberAccessContext>(outer.lExpression());
        Assert.Equal("b", inner.memberName().GetText());

        var leaf = Assert.IsType<VbaParser.PrimaryExprContext>(inner.lExpression());
        Assert.Equal("a", leaf.GetText());
    }

    [Fact]
    public void BangAccess_PreservedAsDistinctNode()
    {
        // Hazard H-B02: `Forms!frm` must retain `!` as a separate
        // AST kind (BangAccess), not collapse to MemberAccess.
        var lExpr = UnwrapToLExpr(Expr("Forms!frm"));
        var bang = Assert.IsType<VbaParser.BangAccessContext>(lExpr);
        Assert.Equal("frm", bang.memberName().GetText());
    }

    [Fact]
    public void BangAndDotMixed_LeftToRight_FormsBangChainBeforeDot()
    {
        // Hazard H-B01: `Forms!frm!ctl.Property` parses as
        // `((Forms!frm)!ctl).Property`. With `.` and `!` both postfix
        // and left-associative, the chain is built left to right
        // regardless of which alternative is listed first.
        var lExpr = UnwrapToLExpr(Expr("Forms!frm!ctl.Property"));
        var dotProp = Assert.IsType<VbaParser.MemberAccessContext>(lExpr);
        Assert.Equal("Property", dotProp.memberName().GetText());

        var bangCtl = Assert.IsType<VbaParser.BangAccessContext>(dotProp.lExpression());
        Assert.Equal("ctl", bangCtl.memberName().GetText());

        var bangFrm = Assert.IsType<VbaParser.BangAccessContext>(bangCtl.lExpression());
        Assert.Equal("frm", bangFrm.memberName().GetText());

        var formsLeaf = Assert.IsType<VbaParser.PrimaryExprContext>(bangFrm.lExpression());
        Assert.Equal("Forms", formsLeaf.GetText());
    }

    [Fact]
    public void BangAccess_WithBracketedKey_Parses()
    {
        // Hazard H-A08 + H-B02: `Me![Field With Spaces]` — the
        // bracketed key tokenizes as a single IDENTIFIER, so the
        // chain shape is BangAccess(Me, "[Field With Spaces]").
        var lExpr = UnwrapToLExpr(Expr("Me![Field With Spaces]"));
        var bang = Assert.IsType<VbaParser.BangAccessContext>(lExpr);
        Assert.Equal("[Field With Spaces]", bang.memberName().GetText());
    }

    [Fact]
    public void DotAccess_OnTypedMemberName_Parses()
    {
        // The right side of a `.` may itself carry a type-decl suffix
        // (rare but legal: `obj.field$`).
        var lExpr = UnwrapToLExpr(Expr("obj.field$"));
        var dot = Assert.IsType<VbaParser.MemberAccessContext>(lExpr);
        Assert.Equal("field$", dot.memberName().GetText());
    }

    // ---------- Implicit-With member access ----------

    [Fact]
    public void ImplicitWithMember_LeadingDot_Parses()
    {
        // Hazard H-C01: `.BOF` at primary-expression position is an
        // implicit-With target member.
        var lExpr = UnwrapToLExpr(Expr(".BOF"));
        var implicitDot = Assert.IsType<VbaParser.ImplicitWithMemberContext>(lExpr);
        Assert.Equal("BOF", implicitDot.memberName().GetText());
    }

    [Fact]
    public void ImplicitWithBang_LeadingBang_Parses()
    {
        // Hazard H-C02: `!Status` at primary-expression position is
        // an implicit-With target default-collection access.
        var lExpr = UnwrapToLExpr(Expr("!Status"));
        var implicitBang = Assert.IsType<VbaParser.ImplicitWithBangContext>(lExpr);
        Assert.Equal("Status", implicitBang.memberName().GetText());
    }

    [Fact]
    public void ImplicitWithMember_ChainedWithDotAndCall_Parses()
    {
        // `.RecordsetClone.RecordCount` from fixture 03.
        Assert.NotNull(Parse(".RecordsetClone.RecordCount"));
    }

    // ---------- Index / call expressions ----------

    [Fact]
    public void Call_NoArgs_Parses()
    {
        var lExpr = UnwrapToLExpr(Expr("CurrentDb()"));
        var call = Assert.IsType<VbaParser.IndexOrCallContext>(lExpr);
        Assert.NotNull(call.argumentList());
    }

    [Fact]
    public void Call_PositionalArgs_Parses()
    {
        var lExpr = UnwrapToLExpr(Expr("DLookup(\"Field\", \"Table\", \"Filter\")"));
        var call = Assert.IsType<VbaParser.IndexOrCallContext>(lExpr);
        var args = call.argumentList().argument();
        Assert.Equal(3, args.Length);
    }

    [Fact]
    public void Call_NamedArgs_Parses()
    {
        // Hazard H-D12: `:=` is the named-arg separator (lexed by the
        // COLON_EQ token in the lexical section).
        var lExpr = UnwrapToLExpr(Expr("DoCmd.OpenForm(FormName := \"frmX\", DataMode := acFormAdd)"));
        var call = Assert.IsType<VbaParser.IndexOrCallContext>(lExpr);
        var args = call.argumentList().argument();
        Assert.Equal(2, args.Length);
        Assert.NotNull(args[0].namedArgument());
        Assert.NotNull(args[1].namedArgument());
        Assert.Equal("FormName", args[0].namedArgument().IDENTIFIER().GetText());
    }

    [Fact]
    public void Call_EmptyPositionalArgs_PreserveSlots()
    {
        // Hazard H-D08: `DoCmd.OpenForm "frmX", , , , acFormAdd, , "ctx"`
        // has positions 2, 3, 4, 6 omitted. The argumentList grammar
        // emits a missing-argument slot for each empty position.
        // (Comma count = 6, so 7 argument slots total.)
        var ctx = Parse("OpenForm(\"frmX\", , , , acFormAdd, , \"ctx\")");
        var lExpr = UnwrapToLExpr(ctx.expression());
        var call = Assert.IsType<VbaParser.IndexOrCallContext>(lExpr);
        var argList = call.argumentList();

        // 6 commas in the list -> 7 argument-slot positions.
        var commaCount = 0;
        for (int i = 0; i < argList.ChildCount; i++)
        {
            if (argList.GetChild(i) is ITerminalNode tn
                && tn.Symbol.Type == VbaParser.COMMA)
            {
                commaCount++;
            }
        }
        Assert.Equal(6, commaCount);

        // 3 positions are filled (positions 1, 5, 7).
        Assert.Equal(3, argList.argument().Length);
    }

    [Fact]
    public void Call_OnMemberAccess_DotColumnIndex_Parses()
    {
        // From fixture 02: `Me!cboCaseworker.Column(2)`
        var lExpr = UnwrapToLExpr(Expr("Me!cboCaseworker.Column(2)"));
        var call = Assert.IsType<VbaParser.IndexOrCallContext>(lExpr);
        var dotColumn = Assert.IsType<VbaParser.MemberAccessContext>(call.lExpression());
        Assert.Equal("Column", dotColumn.memberName().GetText());
    }

    // ---------- Operator precedence ----------

    [Fact]
    public void Multiplication_BindsTighterThanAddition()
    {
        // 1 + 2 * 3 -> AddSubExpr(1, MulDivExpr(2, 3))
        var add = Assert.IsType<VbaParser.AddSubExprContext>(Expr("1 + 2 * 3"));
        Assert.IsType<VbaParser.MulDivExprContext>(add.expression(1));
    }

    [Fact]
    public void Power_BindsTighterThanUnaryMinus_AndIsRightAssociative()
    {
        // -2^2 — VBA evaluates as -(2^2) per MS-VBAL §5.6.9.3.2 (unary
        // minus is below `^` in precedence).
        var unary = Assert.IsType<VbaParser.UnarySignExprContext>(Expr("-2^2"));
        Assert.IsType<VbaParser.PowerExprContext>(unary.expression());

        // 2^3^4 must parse as 2^(3^4) (right-associative).
        var outerPow = Assert.IsType<VbaParser.PowerExprContext>(Expr("2^3^4"));
        Assert.IsType<VbaParser.PowerExprContext>(outerPow.expression(1));
    }

    [Fact]
    public void IntegerDivision_AndModulus_Parse()
    {
        Assert.IsType<VbaParser.IntDivExprContext>(Expr("10 \\ 3"));
        Assert.IsType<VbaParser.ModExprContext>(Expr("10 Mod 3"));
    }

    [Fact]
    public void StringConcat_BindsLooserThanAddition()
    {
        // "a" & 1 + 2 -> Concat("a", Add(1, 2))
        var concat = Assert.IsType<VbaParser.ConcatExprContext>(Expr("\"a\" & 1 + 2"));
        Assert.IsType<VbaParser.AddSubExprContext>(concat.expression(1));
    }

    [Fact]
    public void Comparison_BindsLooserThanConcat()
    {
        // "a" & b = "ab" -> Compare(Concat("a", b), "ab")
        var cmp = Assert.IsType<VbaParser.CompareExprContext>(Expr("\"a\" & b = \"ab\""));
        Assert.IsType<VbaParser.ConcatExprContext>(cmp.expression(0));
    }

    [Fact]
    public void Comparison_LikeAndIs_Parse()
    {
        Assert.IsType<VbaParser.CompareExprContext>(Expr("name Like \"*foo*\""));
        Assert.IsType<VbaParser.CompareExprContext>(Expr("rs Is Nothing"));
    }

    [Fact]
    public void NotIsNothing_ParsesAsNotOfIsComparison()
    {
        // Hazard H-B07: `Not rs Is Nothing` -> `Not (rs Is Nothing)`
        // because `Is` (comparison tier) binds tighter than `Not`
        // (logical-unary tier, one tier lower).
        var notExpr = Assert.IsType<VbaParser.NotExprContext>(Expr("Not rs Is Nothing"));
        Assert.IsType<VbaParser.CompareExprContext>(notExpr.expression());
    }

    [Fact]
    public void LogicalChain_AndOrXorEqvImp_Parse()
    {
        // `a And b Or c Xor d Eqv e Imp f` exercises every logical
        // tier; the AND clause must group tightest, then OR, etc.
        var imp = Assert.IsType<VbaParser.ImpExprContext>(Expr("a And b Or c Xor d Eqv e Imp f"));
        var eqv = Assert.IsType<VbaParser.EqvExprContext>(imp.expression(0));
        var xor = Assert.IsType<VbaParser.XorExprContext>(eqv.expression(0));
        var or = Assert.IsType<VbaParser.OrExprContext>(xor.expression(0));
        Assert.IsType<VbaParser.AndExprContext>(or.expression(0));
    }

    [Fact]
    public void NotPrecedence_AboveAndButBelowComparison()
    {
        // `Not a = 1 And b` -> ((Not (a = 1)) And b)
        var and = Assert.IsType<VbaParser.AndExprContext>(Expr("Not a = 1 And b"));
        var not = Assert.IsType<VbaParser.NotExprContext>(and.expression(0));
        Assert.IsType<VbaParser.CompareExprContext>(not.expression());
    }

    [Fact]
    public void ParenthesesOverridePrecedence()
    {
        // (1 + 2) * 3 -> top is MulDivExpr, with a paren-wrapped
        // AddSubExpr on the left.
        var mul = Assert.IsType<VbaParser.MulDivExprContext>(Expr("(1 + 2) * 3"));
        var leftLExpr = UnwrapToLExpr(mul.expression(0));
        var paren = Assert.IsType<VbaParser.PrimaryExprContext>(leftLExpr);
        Assert.IsType<VbaParser.ParenExprContext>(paren.primary());
    }

    // ---------- Special expression forms ----------

    [Fact]
    public void TypeOfIs_Parses()
    {
        // Hazard H-B06.
        var ctx = Parse("TypeOf ctl Is TextBox");
        var typeOf = Assert.IsType<VbaParser.TypeOfPrimaryContext>(ctx.expression());
        Assert.Equal("TextBox", typeOf.typeOfExpr().typeName().GetText());
    }

    [Fact]
    public void TypeOfIs_InOrChain_Parses()
    {
        // From fixture 10: `TypeOf ctl Is TextBox Or TypeOf ctl Is ComboBox`.
        var or = Assert.IsType<VbaParser.OrExprContext>(
            Expr("TypeOf ctl Is TextBox Or TypeOf ctl Is ComboBox"));
        Assert.IsType<VbaParser.TypeOfPrimaryContext>(or.expression(0));
        Assert.IsType<VbaParser.TypeOfPrimaryContext>(or.expression(1));
    }

    [Fact]
    public void New_BareClassName_Parses()
    {
        var newExpr = Assert.IsType<VbaParser.NewPrimaryContext>(Expr("New Collection"));
        Assert.Equal("Collection", newExpr.newExpr().typeName().GetText());
    }

    [Fact]
    public void New_QualifiedClassName_Parses()
    {
        // `New Excel.Application` — the qualified type name is dotted.
        var newExpr = Assert.IsType<VbaParser.NewPrimaryContext>(Expr("New Excel.Application"));
        Assert.Equal("Excel.Application", newExpr.newExpr().typeName().GetText());
    }

    [Fact]
    public void AddressOf_ProcedureName_Parses()
    {
        var addr = Assert.IsType<VbaParser.AddressOfPrimaryContext>(Expr("AddressOf MyHandler"));
        Assert.Equal("MyHandler", addr.addressOfExpr().procedureReference().GetText());
    }

    [Fact]
    public void AddressOf_QualifiedProcedureName_Parses()
    {
        // `AddressOf modUtilities.Callback`.
        var addr = Assert.IsType<VbaParser.AddressOfPrimaryContext>(
            Expr("AddressOf modUtilities.Callback"));
        Assert.Equal("modUtilities.Callback", addr.addressOfExpr().procedureReference().GetText());
    }

    [Fact]
    public void AddressOf_MeQualifiedProcedureName_Parses()
    {
        // `AddressOf Me.MyMethod` — `Me` may start a procedure-pointer
        // reference chain.
        var addr = Assert.IsType<VbaParser.AddressOfPrimaryContext>(
            Expr("AddressOf Me.MyMethod"));
        Assert.Equal("Me.MyMethod", addr.addressOfExpr().procedureReference().GetText());
    }

    // ---------- Negative tests: invalid TypeOf / AddressOf operands ----------
    //
    // Round-1 verifier finding (Major): the previous grammar version
    // delegated to `lExpression` for both TypeOf and AddressOf operands,
    // which let invalid forms parse (`TypeOf 1 Is T`, `AddressOf F()`,
    // `AddressOf Forms!frm`, `AddressOf .Handler`). The fix introduces
    // `procedureReference` (AddressOf) and `referenceExpression` (TypeOf)
    // to enforce the §5.2.6 / §5.6.4 syntactic constraints. The tests
    // below pin those rejections so a future relaxation requires an
    // explicit decision rather than a silent regression.

    [Theory]
    [InlineData("TypeOf 1 Is Integer")]
    [InlineData("TypeOf \"x\" Is String")]
    [InlineData("TypeOf #1/1/2024# Is Date")]
    public void TypeOf_LiteralOperand_FailsToParse(string source)
    {
        Assert.Throws<InvalidOperationException>(() => Parse(source));
    }

    [Theory]
    [InlineData("AddressOf MyHandler()")]      // call as operand — invalid per §5.6.4
    [InlineData("AddressOf Forms!frm")]         // bang access — invalid (procedure pointers do not bang)
    [InlineData("AddressOf .Handler")]          // implicit-With — invalid (no With target for a pointer)
    [InlineData("AddressOf 1")]                 // literal — invalid
    [InlineData("AddressOf Me")]                // bare Me — Me is the instance, not a pointer (R2 finding)
    public void AddressOf_NonProcedureReference_FailsToParse(string source)
    {
        Assert.Throws<InvalidOperationException>(() => Parse(source));
    }

    // ---------- Date literal in expression position ----------

    [Fact]
    public void DateLiteral_InComparison_Parses()
    {
        // Hazard H-A05: `dt > #1/1/2024#`.
        var cmp = Assert.IsType<VbaParser.CompareExprContext>(Expr("dt > #1/1/2024#"));
        Assert.NotNull(cmp);
    }

    // ---------- Corpus-witness expressions ----------

    [Theory]
    [InlineData("Me.txtCaseworkerEmail")]
    [InlineData("Me!cboCaseworker.Column(2)")]
    [InlineData("Forms!frmDistrictReport!cboDistrict.Column(1)")]
    [InlineData("Me!cboClient.RowSource")]
    [InlineData("CurrentDb()")]
    [InlineData("Nz([SubTotal], 0) + Nz([TaxAmount], 0)")]
    [InlineData("IIf(IsNull([DateOfBirth]), \"Unknown\", \"Known\")")]
    [InlineData("DCount(\"*\", \"tblCases\", \"AssignedCaseworkerID = \" & [cboCaseworker])")]
    [InlineData("DLookup(\"OutstandingBalance\", \"qryApplicantBalances\", \"ApplicantID = \" & !ApplicantID)")]
    [InlineData("Not (.BOF And .EOF)")]
    [InlineData(".RecordsetClone.RecordCount = 0")]
    [InlineData("CurrentUser()")]
    [InlineData("Format(Date() - 30, \"yyyy-mm-dd\")")]
    [InlineData("Mid(sFilter, iStart + 6, iEnd - iStart - 6)")]
    [InlineData("oWB.Sheets(1)")]
    [InlineData("\"#\" & Format(Date(), \"yyyy-mm-dd\") & \"#\"")]
    public void CorpusExpression_Parses(string source)
    {
        Assert.NotNull(Parse(source));
    }
}
