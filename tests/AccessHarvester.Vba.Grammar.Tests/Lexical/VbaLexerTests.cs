using System.Collections.Generic;
using System.Linq;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Xunit;

namespace AccessHarvester.Vba.Grammar.Tests.Lexical;

// Lexer-only tests for the Phase 1.4 lexical section of Vba.g4.
// Exercise: token kind sequences, hidden-channel handling, and the
// hazard-list Section A entries (line continuation, type-decl
// suffixes, line numbers, date literals, bracketed identifiers,
// apostrophe-vs-string-quoted apostrophes).
public class VbaLexerTests
{
    private static IList<IToken> Tokenize(string source)
    {
        var stream = new AntlrInputStream(source);
        var lexer = new VbaLexer(stream);
        lexer.RemoveErrorListeners();
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();
        return tokens.GetTokens();
    }

    private static IList<IToken> DefaultChannel(string source)
        => Tokenize(source).Where(t => t.Channel == Lexer.DefaultTokenChannel && t.Type != Antlr4.Runtime.TokenConstants.EOF).ToList();

    private static int[] Kinds(IEnumerable<IToken> tokens)
        => tokens.Select(t => t.Type).ToArray();

    // ---------- Identifiers and keywords ----------

    [Fact]
    public void Keyword_Dim_IsCaseInsensitive()
    {
        Assert.Equal(VbaLexer.DIM, DefaultChannel("Dim").Single().Type);
        Assert.Equal(VbaLexer.DIM, DefaultChannel("DIM").Single().Type);
        Assert.Equal(VbaLexer.DIM, DefaultChannel("dim").Single().Type);
        Assert.Equal(VbaLexer.DIM, DefaultChannel("dIm").Single().Type);
    }

    [Fact]
    public void Identifier_StartingWithKeywordPrefix_IsIdentifier()
    {
        // 'Remediate' starts with 'Rem' but is a single identifier.
        var tok = DefaultChannel("Remediate").Single();
        Assert.Equal(VbaLexer.IDENTIFIER, tok.Type);
        Assert.Equal("Remediate", tok.Text);
    }

    [Fact]
    public void Identifier_BracketEscaped_PreservesContent()
    {
        // Hazard H-A08: bracketed identifier — reserved word as field name.
        var tok = DefaultChannel("[Order Date]").Single();
        Assert.Equal(VbaLexer.IDENTIFIER, tok.Type);
        Assert.Equal("[Order Date]", tok.Text);
    }

    // ---------- Comments and whitespace ----------

    [Fact]
    public void ApostropheComment_IsHidden()
    {
        var tokens = Tokenize("Dim x ' a comment\n");
        var hiddenComment = tokens.Single(t => t.Type == VbaLexer.LINE_COMMENT);
        Assert.Equal(Lexer.Hidden, hiddenComment.Channel);
        Assert.Equal("' a comment", hiddenComment.Text);
    }

    [Fact]
    public void RemKeyword_IsHiddenComment_WhenFollowedByWhitespace()
    {
        var tokens = Tokenize("REM heading\n");
        var rem = tokens.Single(t => t.Type == VbaLexer.REM_COMMENT);
        Assert.Equal(Lexer.Hidden, rem.Channel);
    }

    [Fact]
    public void RemKeyword_AtBareEol_IsHiddenComment()
    {
        // Regression for Round-1 verifier finding #4 (Major): bare
        // REM at end-of-line must tokenize as a comment.
        var tokens = Tokenize("REM\n");
        Assert.Contains(tokens, t => t.Type == VbaLexer.REM_COMMENT && t.Channel == Lexer.Hidden);
    }

    [Fact]
    public void RemKeyword_AtEof_IsHiddenComment()
    {
        // Same regression: REM at end-of-input.
        var tokens = Tokenize("REM");
        Assert.Contains(tokens, t => t.Type == VbaLexer.REM_COMMENT && t.Channel == Lexer.Hidden);
    }

    [Theory]
    [InlineData("REM:")]
    [InlineData("REM.")]
    [InlineData("REM(")]
    public void RemKeyword_FollowedByNonBoundary_IsNotComment(string source)
    {
        // Regression for Round-2 verifier finding: bare REM followed
        // by `:`, `.`, `(`, etc. must NOT tokenize as a comment. The
        // semantic predicate `{IsRemBoundary()}?` requires the post-
        // body lookahead to be CR/LF/EOF.
        var tokens = Tokenize(source);
        Assert.DoesNotContain(tokens, t => t.Type == VbaLexer.REM_COMMENT);
    }

    [Fact]
    public void Identifier_RemediatePrefixed_IsIdentifierNotComment()
    {
        // Sanity: longest-match must keep `Remediate` as an
        // identifier even though `Rem` alone could match REM_COMMENT.
        var tok = DefaultChannel("Remediate").Single();
        Assert.Equal(VbaLexer.IDENTIFIER, tok.Type);
        Assert.Equal("Remediate", tok.Text);
    }

    [Fact]
    public void RemComment_LeadingContinuation_ExtendsAcrossPhysicalLines()
    {
        // Regression for Round-3 verifier finding (Major): when the
        // first body content of a REM comment is a line-continuation
        // marker (`REM _\nstillCommented`), the comment must span both
        // physical lines per §3.3.3 + §3.3.1. Pre-fix, the leading
        // whitespace was consumed before LINE_CONT_FRAGMENT could fire.
        var source = "REM _\r\nstillCommented\r\n";
        var tokens = Tokenize(source);
        var rem = tokens.Single(t => t.Type == VbaLexer.REM_COMMENT);
        Assert.Contains("stillCommented", rem.Text);
        Assert.Equal(Lexer.Hidden, rem.Channel);
    }

    [Fact]
    public void Comment_TrailingUnderscoreContinuation_ExtendsAcrossPhysicalLines()
    {
        // Regression for Round-1 verifier finding #3 (Major):
        // `' comment _\n nextLine` collapses both physical lines into
        // one logical comment per §3.3.3 + §3.3.1.
        var source = "' comment _\r\nstillCommented\r\n";
        var tokens = Tokenize(source);
        var comment = tokens.Single(t => t.Type == VbaLexer.LINE_COMMENT);
        // Both physical lines absorbed into one hidden comment token.
        Assert.Contains("stillCommented", comment.Text);
        Assert.Equal(Lexer.Hidden, comment.Channel);
    }

    [Fact]
    public void ApostropheInsideString_IsNotComment()
    {
        // Hazard H-A07: apostrophe inside string literal is data.
        var tokens = DefaultChannel("\"It's fine\"");
        var literal = Assert.Single(tokens);
        Assert.Equal(VbaLexer.STRING_LITERAL, literal.Type);
        Assert.Equal("\"It's fine\"", literal.Text);
    }

    // ---------- Line continuation and newlines ----------

    [Fact]
    public void LineContinuation_JoinsLogicalLines()
    {
        // Hazard H-A01: `<WS>+ '_' <NL>` collapses two physical lines.
        var source = "x = 1 + _\r\n  2\r\n";
        var defaults = DefaultChannel(source);
        var kinds = Kinds(defaults);
        Assert.Equal(
            new[] { VbaLexer.IDENTIFIER, VbaLexer.EQ, VbaLexer.INTEGER_LITERAL,
                    VbaLexer.PLUS, VbaLexer.INTEGER_LITERAL, VbaLexer.NEWLINE },
            kinds);
    }

    [Fact]
    public void TrailingUnderscore_OnIdentifier_IsNotContinuation()
    {
        // Identifier `my_var_` (no leading whitespace before `_`) must
        // not be misread as a continuation.
        var defaults = DefaultChannel("my_var_\n");
        var kinds = Kinds(defaults);
        Assert.Equal(new[] { VbaLexer.IDENTIFIER, VbaLexer.NEWLINE }, kinds);
        Assert.Equal("my_var_", defaults[0].Text);
    }

    [Fact]
    public void Newline_CrLf_Lf_AndCr_AllProduceNewlineTokens()
    {
        Assert.Equal(VbaLexer.NEWLINE, DefaultChannel("\r\n").Single().Type);
        Assert.Equal(VbaLexer.NEWLINE, DefaultChannel("\n").Single().Type);
        Assert.Equal(VbaLexer.NEWLINE, DefaultChannel("\r").Single().Type);
    }

    // ---------- Numeric literals ----------

    [Fact]
    public void IntegerLiteral_Decimal()
    {
        Assert.Equal("42", DefaultChannel("42").Single().Text);
        Assert.Equal(VbaLexer.INTEGER_LITERAL, DefaultChannel("42").Single().Type);
    }

    [Fact]
    public void IntegerLiteral_PercentSuffix_IsCapturedAsOneToken()
    {
        // `%` is the only INT_SUFFIX retained — see grammar comment
        // on operator-vs-suffix collision (Round-1 verifier finding #1).
        var tok = DefaultChannel("42%").Single();
        Assert.Equal(VbaLexer.INTEGER_LITERAL, tok.Type);
        Assert.Equal("42%", tok.Text);
    }

    [Fact]
    public void HexLiteral_AmpH_IsSingleToken()
    {
        var tok = DefaultChannel("&HFF").Single();
        Assert.Equal(VbaLexer.HEX_LITERAL, tok.Type);
        Assert.Equal("&HFF", tok.Text);

        var longTok = DefaultChannel("&HFF&").Single();
        Assert.Equal(VbaLexer.HEX_LITERAL, longTok.Type);
        Assert.Equal("&HFF&", longTok.Text);
    }

    [Fact]
    public void OctalLiteral_AmpO_IsSingleToken()
    {
        var tok = DefaultChannel("&O77").Single();
        Assert.Equal(VbaLexer.OCTAL_LITERAL, tok.Type);
        Assert.Equal("&O77", tok.Text);
    }

    [Fact]
    public void FloatLiteral_FractionalAndExponent()
    {
        Assert.Equal(VbaLexer.FLOAT_LITERAL, DefaultChannel("3.14").Single().Type);
        Assert.Equal(VbaLexer.FLOAT_LITERAL, DefaultChannel(".5").Single().Type);
        Assert.Equal(VbaLexer.FLOAT_LITERAL, DefaultChannel("2E10").Single().Type);
        Assert.Equal(VbaLexer.FLOAT_LITERAL, DefaultChannel("1.5D-3").Single().Type);
        // Single-suffix on float
        Assert.Equal(VbaLexer.FLOAT_LITERAL, DefaultChannel("3.14!").Single().Type);
    }

    // ---------- Date literal ----------

    [Fact]
    public void DateLiteral_HashContentHash()
    {
        // Hazard H-A05: `#1/1/2024#` is a single token, not three operators.
        var tok = DefaultChannel("#1/1/2024#").Single();
        Assert.Equal(VbaLexer.DATE_LITERAL, tok.Type);
        Assert.Equal("#1/1/2024#", tok.Text);
    }

    // ---------- Preprocessor directives ----------

    [Fact]
    public void PreprocessorDirective_HashIf_IsKeywordToken()
    {
        // `#If VBA7 Then` — tokenize the directive head; full
        // evaluation is parser-infrastructure (#6).
        var defaults = DefaultChannel("#If VBA7 Then\n");
        var kinds = Kinds(defaults);
        Assert.Equal(
            new[] { VbaLexer.HASH_IF, VbaLexer.IDENTIFIER, VbaLexer.THEN, VbaLexer.NEWLINE },
            kinds);
    }

    [Fact]
    public void PreprocessorDirective_HashEndIf_IsKeywordToken()
    {
        var tok = DefaultChannel("#End If").Single();
        Assert.Equal(VbaLexer.HASH_END_IF, tok.Type);
    }

    [Fact]
    public void PreprocessorDirective_HashConstWithStringContainingHash_IsHashConst()
    {
        // Regression for Round-1 verifier finding #2 (Major):
        // `#Const X = "#"` must lex as a directive head, not as a
        // single DATE_LITERAL spanning the line.
        var defaults = DefaultChannel("#Const X = \"#\"");
        Assert.Equal(VbaLexer.HASH_CONST, defaults[0].Type);
        Assert.Equal(VbaLexer.IDENTIFIER, defaults[1].Type);
        Assert.Equal(VbaLexer.EQ, defaults[2].Type);
        Assert.Equal(VbaLexer.STRING_LITERAL, defaults[3].Type);
    }

    // ---------- Type-declaration suffixes ----------

    [Fact]
    public void TypeDeclSuffix_OnIdentifier_IsTypedIdentifierToken()
    {
        // Hazard H-A02: `sFilePath$` -> TYPED_IDENTIFIER (single token
        // capturing both the base name and the String suffix).
        var tok = DefaultChannel("sFilePath$").Single();
        Assert.Equal(VbaLexer.TYPED_IDENTIFIER, tok.Type);
        Assert.Equal("sFilePath$", tok.Text);
    }

    [Theory]
    [InlineData("i&")]   // Long
    [InlineData("n%")]   // Integer
    [InlineData("rate#")] // Double
    [InlineData("amount@")] // Currency
    [InlineData("name$")] // String
    public void TypeDeclSuffix_AllUnambiguousSuffixes_AreTypedIdentifiers(string source)
    {
        var tok = DefaultChannel(source).Single();
        Assert.Equal(VbaLexer.TYPED_IDENTIFIER, tok.Type);
        Assert.Equal(source, tok.Text);
    }

    [Fact]
    public void Bang_AsBangOperator_NotSuffix()
    {
        // `Forms!frmMain` — bang as dictionary access. `!` is excluded
        // from TYPED_IDENTIFIER (parser composes Single suffix from
        // IDENTIFIER + BANG at declaration positions).
        var defaults = DefaultChannel("Forms!frmMain");
        Assert.Equal(
            new[] { VbaLexer.IDENTIFIER, VbaLexer.BANG, VbaLexer.IDENTIFIER },
            Kinds(defaults));
        Assert.Equal("Forms", defaults[0].Text);
        Assert.Equal("frmMain", defaults[2].Text);
    }

    [Fact]
    public void SingleSuffix_RateBang_TokenizesAsIdentifierThenBang()
    {
        // `rate!` in declaration -> IDENTIFIER BANG (parser composes
        // a Single-typed name; same shape as bang access).
        var defaults = DefaultChannel("rate!");
        Assert.Equal(new[] { VbaLexer.IDENTIFIER, VbaLexer.BANG }, Kinds(defaults));
    }

    // ---------- Issue-driven regressions: operator vs. suffix collision ----------

    [Fact]
    public void IntegerOperator_OnePlusTwo_TokenizesAsThreeOperatorTokens()
    {
        // Regression for Round-1 verifier finding #1 (Critical):
        // INT_SUFFIX must not absorb operator characters that would
        // otherwise lex as binary operators on adjacent literals.
        var defaults = DefaultChannel("1+2");
        Assert.Equal(
            new[] { VbaLexer.INTEGER_LITERAL, VbaLexer.PLUS, VbaLexer.INTEGER_LITERAL },
            Kinds(defaults));
    }

    [Fact]
    public void IntegerOperator_TwoCaretThree_TokenizesAsExponentExpression()
    {
        // Same regression: `2^3` must not be `INTEGER("2^"), INTEGER("3")`.
        var defaults = DefaultChannel("2^3");
        Assert.Equal(
            new[] { VbaLexer.INTEGER_LITERAL, VbaLexer.CARET, VbaLexer.INTEGER_LITERAL },
            Kinds(defaults));
    }

    [Fact]
    public void IntegerConcat_OneAmpString_TokenizesAsConcatExpression()
    {
        // Same regression: `1&"x"` must lex as concatenation, not as
        // a Long-typed `1&` followed by an orphan string literal.
        var defaults = DefaultChannel("1&\"x\"");
        Assert.Equal(
            new[] { VbaLexer.INTEGER_LITERAL, VbaLexer.AMP, VbaLexer.STRING_LITERAL },
            Kinds(defaults));
    }

    // ---------- Operators with VBA-specific keyword forms ----------

    [Fact]
    public void KeywordOperators_AndOrNotModLikeIs_AreKeywordTokens()
    {
        Assert.Equal(VbaLexer.AND, DefaultChannel("And").Single().Type);
        Assert.Equal(VbaLexer.OR, DefaultChannel("Or").Single().Type);
        Assert.Equal(VbaLexer.NOT, DefaultChannel("Not").Single().Type);
        Assert.Equal(VbaLexer.MOD, DefaultChannel("Mod").Single().Type);
        Assert.Equal(VbaLexer.LIKE, DefaultChannel("Like").Single().Type);
        Assert.Equal(VbaLexer.IS, DefaultChannel("Is").Single().Type);
    }

    [Fact]
    public void NamedArgSeparator_ColonEqual_IsSingleToken()
    {
        Assert.Equal(VbaLexer.COLON_EQ, DefaultChannel(":=").Single().Type);
    }

    // ---------- Line numbers ----------

    [Fact]
    public void LineNumber_AtStatementStart_TokenizesAsInteger()
    {
        // Hazard H-A03: a leading integer at statement start is a
        // line label. The lexer emits INTEGER_LITERAL; the parser
        // distinguishes label position from numeric expression
        // position. This test pins the token kind only.
        var defaults = DefaultChannel("100 Set oExcel = CreateObject(\"Excel.Application\")");
        Assert.Equal(VbaLexer.INTEGER_LITERAL, defaults[0].Type);
        Assert.Equal("100", defaults[0].Text);
        Assert.Equal(VbaLexer.SET, defaults[1].Type);
    }

    // ---------- End-to-end fixture smoke ----------

    [Fact]
    public void Fixture01_SimpleValidation_TokenizesWithoutErrorTokens()
    {
        // Smoke test against fixture 01 — every token must be a known
        // kind (no ERROR_TOKEN catch-all matches). Pinning fixture
        // contents inline keeps the test hermetic.
        const string source =
            "Private Sub Form_BeforeUpdate(Cancel As Integer)\r\n" +
            "    If IsNull(Me.txtCustomerName) Or Me.txtCustomerName = \"\" Then\r\n" +
            "        Cancel = True\r\n" +
            "        Exit Sub\r\n" +
            "    End If\r\n" +
            "End Sub\r\n";

        var defaults = DefaultChannel(source);
        Assert.DoesNotContain(defaults, t => t.Type == VbaLexer.ERROR_TOKEN);
    }

    [Fact]
    public void Fixture07_LegacyLineNumbersAndSuffixes_TokenizesWithoutErrorTokens()
    {
        // Hazards H-A02 (suffixes), H-A03 (line numbers), and basic
        // operators all in one fragment from fixture 07.
        const string source =
            "Public Function ImportClientBatch(sFilePath$) As Variant\r\n" +
            "10  Dim oExcel As Object\r\n" +
            "20  Dim i&, j&, lRow&, lCol&\r\n" +
            "30  Dim sErr$\r\n" +
            "40  On Error Resume Next\r\n";

        var defaults = DefaultChannel(source);
        Assert.DoesNotContain(defaults, t => t.Type == VbaLexer.ERROR_TOKEN);
        // Suffix-typed declarations now produce TYPED_IDENTIFIER tokens
        // (single-token capture per Hazard H-A02 spec semantics).
        var typed = defaults.Where(t => t.Type == VbaLexer.TYPED_IDENTIFIER).ToList();
        Assert.Contains(typed, t => t.Text == "sFilePath$");
        Assert.Contains(typed, t => t.Text == "i&");
        Assert.Contains(typed, t => t.Text == "sErr$");
    }
}
