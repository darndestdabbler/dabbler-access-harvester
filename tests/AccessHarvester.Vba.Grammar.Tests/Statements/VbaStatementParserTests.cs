using System;
using System.IO;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Xunit;

namespace AccessHarvester.Vba.Grammar.Tests.Statements;

// Parser tests for the Phase 1.4 statement section of Vba.g4
// (Session 1 of vba-statement-and-module-grammar).
//
// Coverage targets (from spec and hazard list):
//   - Local declarations: Dim / Static / Public / Private / Global,
//     including type-decl suffix identifiers (H-A02 / coverage row
//     "Type-declaration chars on identifiers"), bare Dim (Variant
//     default), explicit `As <typeSpec>`, multi-decl with mixed
//     type-suffix and `As`-clause forms.
//   - ReDim and ReDim Preserve.
//   - Const declarations.
//   - Set vs implicit Let assignment (H-B04, §5.3.1, §5.3.2). `Set`
//     parses as its own statement node; assignment without `Set`
//     parses as `letStatement`.
//   - Block If / single-line If / single-line If with colon-chain
//     Then-body (H-D06).
//   - Select Case with value, range, and Is-comparison clauses
//     (§5.4.2); single-line `Case <value>: <stmt>` form (H-D07);
//     `Case Else`.
//   - For/Next, For Each/Next, Do/Loop (pre- and post-test),
//     While/Wend.
//   - With block (§5.4.7).
//   - On Error variants (H-D04) and Resume forms (H-D05), including
//     the `0` deactivation sentinel.
//   - GoTo / GoSub / Return (H-D01, H-D02).
//   - Numeric line-number labels (H-A03) and alpha labels.
//   - Exit Sub / Exit Function / Exit Property / Exit For / Exit Do
//     (H-D10).
//   - Stop / End standalone statements.
//   - RaiseEvent statement form.
//   - Call statement: explicit `Call`, bare call without args, bare
//     call with args (H-D09), bare call with empty positional
//     arguments (H-D08, e.g. `DoCmd.OpenForm "f", , , , acFormAdd`).
//   - Debug.Print as a member-access call (DEBUG keyword as primary).
public class VbaStatementParserTests
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

    private static VbaParser.StatementListEofContext Parse(string source)
    {
        // Tests use raw multi-line strings (no leading "\n" required).
        // The grammar is newline-significant, so end every fragment
        // with a final newline so the trailing block closer (e.g.
        // `End If`) is followed by a NEWLINE before EOF.
        if (!source.EndsWith("\n", StringComparison.Ordinal))
            source += "\n";

        var stream = new AntlrInputStream(source);
        var lexer = new VbaLexer(stream);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(ThrowingErrorListener.Instance);

        var tokens = new CommonTokenStream(lexer);
        var parser = new VbaParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(ThrowingErrorListener.Instance);

        return parser.statementListEof();
    }

    private static void Accept(string src) => Parse(src);

    // ---------- Local declarations (§4.4) ----------

    [Fact]
    public void Dim_BareIdentifier_Variant() => Accept("Dim vData\n");

    [Fact]
    public void Dim_AsObject() => Accept("Dim oExcel As Object\n");

    [Fact]
    public void Dim_TypedIdentifier_LongSuffix() => Accept("Dim i&\n");

    [Fact]
    public void Dim_MultipleTypedIdentifiers()
        => Accept("Dim i&, j&, lRow&, lCol&\n");

    [Fact]
    public void Dim_MixedTypedAndAsClause()
        => Accept("Dim sFilePath$, oWB As Object, vData\n");

    [Fact]
    public void Dim_QualifiedType()
        => Accept("Dim rs As DAO.Recordset\n");

    [Fact]
    public void Static_LocalDecl()
        => Accept("Static counter As Long\n");

    [Fact]
    public void Public_ModuleDecl()
        => Accept("Public g_CurrentUserID As Long\n");

    [Fact]
    public void Private_ModuleDecl()
        => Accept("Private m_GroupTotal As Currency\n");

    [Fact]
    public void Global_ModuleDecl()
        => Accept("Global g_ConnectionString As String\n");

    [Fact]
    public void Dim_WithArrayBounds()
        => Accept("Dim arr(10) As String\n");

    [Fact]
    public void Dim_WithMultiDimArrayBounds()
        => Accept("Dim matrix(1 To 5, 1 To 10) As Double\n");

    [Fact]
    public void Const_Single()
        => Accept("Const MaxRows As Long = 100\n");

    [Fact]
    public void Const_Public()
        => Accept("Public Const APP_NAME As String = \"Switchboard\"\n");

    [Fact]
    public void Const_InferredType()
        => Accept("Const PI = 3.14159\n");

    [Fact]
    public void Dim_DynamicArray()
        => Accept("Dim customerNames() As String\n");

    [Fact]
    public void ReDim_FixedBound()
        => Accept("ReDim arr(10) As String\n");

    [Fact]
    public void ReDim_ToBound()
        => Accept("ReDim arr(1 To 10) As Variant\n");

    [Fact]
    public void ReDim_Preserve()
        => Accept("ReDim Preserve arr(1 To n)\n");

    // ---------- Set / Let / Call (§5.3, §5.5) ----------

    [Fact]
    public void Set_ObjectAssignment()
        => Accept("Set oExcel = CreateObject(\"Excel.Application\")\n");

    [Fact]
    public void Set_ToNothing()
        => Accept("Set rs = Nothing\n");

    [Fact]
    public void ImplicitLet_SimpleScalar() => Accept("lRow = UBound(vData, 1)\n");

    [Fact]
    public void ImplicitLet_BangAssignTarget()
        => Accept("rs!ClientName = Nz(vData(i, 1), \"\")\n");

    [Fact]
    public void ExplicitLet_Keyword()
        => Accept("Let x = 5\n");

    [Fact]
    public void Call_Explicit_WithParenArgs()
        => Accept("Call RecalculateCaseloadTotals(Me!cboCaseworker)\n");

    [Fact]
    public void Call_BareNoArgs()
        => Accept("DoEvents\n");

    [Fact]
    public void Call_BareWithArgs_NoParens()
        => Accept("MsgBox \"Error: \" & Err.Description, vbCritical\n");

    [Fact]
    public void Call_DebugPrint()
        => Accept("Debug.Print \"Row \" & i & \": \" & sErr\n");

    [Fact]
    public void Call_DocmdOpenForm_EmptyPositionalArgs()
        => Accept("DoCmd.OpenForm \"frmCaseDetail\", , , , acFormAdd, , \"NewCase\"\n");

    [Fact]
    public void Call_BareWithNamedArgs()
        => Accept("MsgBox Title:=\"Complete\", Prompt:=\"Operation finished\"\n");

    // ---------- Block If / single-line If (§5.4.1) ----------

    [Fact]
    public void If_SingleLine_GotoBody()
        => Accept("If oExcel Is Nothing Then GoTo NoExcel\n");

    [Fact]
    public void If_SingleLine_ColonChain_Body()
        => Accept("If FormatCount = 1 Then m_GroupTotal = 0 : m_GroupCount = 0\n");

    [Fact]
    public void If_SingleLine_WithElse()
        => Accept("If x > 0 Then y = 1 Else y = -1\n");

    [Fact]
    public void If_Block_Empty()
        => Accept("If x Then\nEnd If\n");

    [Fact]
    public void If_Block_WithBody()
        => Accept(
            "If sErr <> \"\" Then\n" +
            "    Debug.Print sErr\n" +
            "    Exit Sub\n" +
            "End If\n");

    [Fact]
    public void If_Block_WithElseIfAndElse()
        => Accept(
            "If x = 1 Then\n" +
            "    a = 10\n" +
            "ElseIf x = 2 Then\n" +
            "    a = 20\n" +
            "Else\n" +
            "    a = 0\n" +
            "End If\n");

    // ---------- Select Case (§5.4.2) ----------

    [Fact]
    public void SelectCase_ValueAndElse()
        => Accept(
            "Select Case g_UserRole\n" +
            "    Case \"Administrator\"\n" +
            "        DoCmd.OpenForm \"frmAdminSwitchboard\"\n" +
            "    Case Else\n" +
            "        DoCmd.OpenForm \"frmMainSwitchboard\"\n" +
            "End Select\n");

    [Fact]
    public void SelectCase_SingleLineColonBody()
        => Accept(
            "Select Case g_UserRole\n" +
            "    Case \"Administrator\": DoCmd.OpenForm \"frmAdmin\"\n" +
            "    Case \"Caseworker\": DoCmd.OpenForm \"frmCaseworkerHome\"\n" +
            "    Case Else: DoCmd.OpenForm \"frmMain\"\n" +
            "End Select\n");

    [Fact]
    public void SelectCase_RangeAndList()
        => Accept(
            "Select Case n\n" +
            "    Case 1 To 10\n" +
            "        a = 1\n" +
            "    Case 11, 13, 17\n" +
            "        a = 2\n" +
            "End Select\n");

    [Fact]
    public void SelectCase_IsComparison()
        => Accept(
            "Select Case n\n" +
            "    Case Is < 0\n" +
            "        a = -1\n" +
            "    Case Is >= 100\n" +
            "        a = 100\n" +
            "End Select\n");

    // ---------- For / For Each / Do / While ----------

    [Fact]
    public void For_Counter_StepOmitted()
        => Accept(
            "For i = 2 To lRow\n" +
            "    Debug.Print i\n" +
            "Next i\n");

    [Fact]
    public void For_Counter_Step()
        => Accept(
            "For i = 1 To 10 Step 2\n" +
            "    a = i\n" +
            "Next\n");

    [Fact]
    public void ForEach_OverControlsCollection()
        => Accept(
            "For Each ctl In m_Form.Controls\n" +
            "    Debug.Print ctl.Name\n" +
            "Next ctl\n");

    [Fact]
    public void Do_While_PreTest()
        => Accept(
            "Do While Not rs.EOF\n" +
            "    rs.MoveNext\n" +
            "Loop\n");

    [Fact]
    public void Do_Until_PostTest()
        => Accept(
            "Do\n" +
            "    rs.MoveNext\n" +
            "Loop Until rs.EOF\n");

    [Fact]
    public void Do_Bare_Infinite_WithExit()
        => Accept(
            "Do\n" +
            "    Exit Do\n" +
            "Loop\n");

    [Fact]
    public void While_Wend()
        => Accept(
            "While n > 0\n" +
            "    n = n - 1\n" +
            "Wend\n");

    // ---------- With (§5.4.7) ----------

    [Fact]
    public void With_Block_AccessesLeadingDot()
        => Accept(
            "With Forms!frmMain\n" +
            "    .Caption = \"Hello\"\n" +
            "    Set rs = .RecordsetClone\n" +
            "End With\n");

    // ---------- On Error / Resume (§5.4.11, §5.4.12) ----------

    [Fact]
    public void OnError_Goto_Label()
        => Accept("On Error GoTo ErrHandler\n");

    [Fact]
    public void OnError_Goto_ZeroDeactivate()
        => Accept("On Error GoTo 0\n");

    [Fact]
    public void OnError_ResumeNext()
        => Accept("On Error Resume Next\n");

    [Fact]
    public void Resume_Bare()
        => Accept("Resume\n");

    [Fact]
    public void Resume_Next()
        => Accept("Resume Next\n");

    [Fact]
    public void Resume_Label()
        => Accept("Resume RetryPoint\n");

    [Fact]
    public void Resume_ZeroSentinel()
        => Accept("Resume 0\n");

    // ---------- GoTo / GoSub / Return / labels ----------

    [Fact]
    public void Goto_AlphaLabel()
        => Accept("GoTo NoExcel\n");

    [Fact]
    public void Gosub_Return_Pair()
        => Accept(
            "GoSub ValidateRow\n" +
            "ValidateRow:\n" +
            "Return\n");

    [Fact]
    public void NumericLineLabel_NoColon()
        => Accept("100 Set oExcel = Nothing\n");

    [Fact]
    public void NumericLineLabel_WithColon()
        => Accept("100: Set oExcel = Nothing\n");

    [Fact]
    public void AlphaLabel_BareLine()
        => Accept(
            "ErrHandler:\n" +
            "    MsgBox \"oops\"\n" +
            "    Exit Sub\n");

    // ---------- Exit / Stop / End / RaiseEvent ----------

    [Fact]
    public void Exit_AllKinds()
        => Accept(
            "Exit Sub\n" +
            "Exit Function\n" +
            "Exit Property\n" +
            "Exit For\n" +
            "Exit Do\n");

    [Fact]
    public void Stop_Standalone() => Accept("Stop\n");

    [Fact]
    public void End_Standalone() => Accept("End\n");

    [Fact]
    public void RaiseEvent_NoArgs()
        => Accept("RaiseEvent Logged\n");

    [Fact]
    public void RaiseEvent_WithArgs()
        => Accept("RaiseEvent Logged(\"info\", 42)\n");

    [Fact]
    public void RaiseEvent_WithEmptyParens()
        => Accept("RaiseEvent DataRefreshed()\n");

    // ---------- Composite fixture-style snippet ----------
    //
    // Smoke test: a short script that mixes many of the constructs
    // above so left-recursive prediction surprises do not slip past
    // the per-form unit tests.

    [Fact]
    public void Composite_FixtureStylePatterns()
        => Accept(
            "On Error Resume Next\n" +
            "Dim i&, lRow&\n" +
            "Set oExcel = CreateObject(\"Excel.Application\")\n" +
            "If oExcel Is Nothing Then GoTo NoExcel\n" +
            "For i = 2 To lRow\n" +
            "    If IsEmpty(vData(i, 1)) Then GoTo NextRow\n" +
            "    GoSub ValidateRow\n" +
            "    rs.AddNew\n" +
            "    rs!ClientName = vData(i, 1)\n" +
            "    rs.Update\n" +
            "NextRow:\n" +
            "Next i\n" +
            "Exit Function\n" +
            "ValidateRow:\n" +
            "    sErr = \"\"\n" +
            "    Return\n" +
            "NoExcel:\n" +
            "    MsgBox \"Excel not available\"\n");
}
