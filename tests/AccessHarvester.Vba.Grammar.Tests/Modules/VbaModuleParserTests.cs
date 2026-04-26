using System;
using System.IO;
using AccessHarvester.Vba.Grammar.Generated;
using Antlr4.Runtime;
using Xunit;

namespace AccessHarvester.Vba.Grammar.Tests.Modules;

// Parser tests for the Phase 1.4 module section of Vba.g4 (Session 2
// of vba-statement-and-module-grammar).
//
// Coverage targets (from spec, ms-vbal-coverage.csv §4.* rows, and
// hazard-list.md Sections E and F):
//   - Option directives: Explicit / Compare <kind> / Base 0|1 /
//     Private Module (§4.1).
//   - Module-level Implements (§4.3 / H-F03).
//   - Module-level variable declarations (§4.4 / H-E06):
//     Public/Private/Dim/Global with optional WithEvents (H-F01),
//     array bounds, qualified types, As New <type>.
//   - Module-level Const (Public/Private prefix, multi-decl).
//   - Type / Enum declarations (§4.4) — empty and with members,
//     fixed-length-string field via `As String * N`.
//   - Declare and Declare PtrSafe Sub / Function (§4.4).
//   - Public Event declaration (§4.4).
//   - Procedure declarations (§4.5):
//       Sub / Function / Property Get/Let/Set, with modifier head
//       (Public/Private/Friend, optional Static), parameter list,
//       AS-clause return type. Property triplet (H-E03).
//   - Parameter list (§4.6 / H-E01, H-E02, H-E07):
//       implicit ByRef, explicit ByVal, Optional with default value,
//       ParamArray, array parameter `arr()`, qualified parameter
//       type `As DAO.Database`.
//   - Class_Initialize / Class_Terminate as ordinary Private Sub
//     declarations (H-F04).
//   - Form/Report event handler signature shape (H-F05).
//   - Composite smoke test: parses fixture 10 (10-class-with-events.cls)
//     end-to-end through the `module` start rule.
public class VbaModuleParserTests
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

    private static VbaParser.ModuleContext Parse(string source)
    {
        // The grammar is newline-significant, so end every fragment with
        // a final newline so the trailing module item (e.g. `End Sub`)
        // is followed by a NEWLINE before EOF.
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

        return parser.module();
    }

    private static void Accept(string src) => Parse(src);

    // ---------- Option directives (§4.1) ----------

    [Fact]
    public void Option_Explicit() => Accept("Option Explicit\n");

    [Fact]
    public void Option_Compare_Database() => Accept("Option Compare Database\n");

    [Fact]
    public void Option_Compare_Binary() => Accept("Option Compare Binary\n");

    [Fact]
    public void Option_Compare_Text() => Accept("Option Compare Text\n");

    [Fact]
    public void Option_Base_Zero() => Accept("Option Base 0\n");

    [Fact]
    public void Option_Base_One() => Accept("Option Base 1\n");

    [Fact]
    public void Option_Private_Module() => Accept("Option Private Module\n");

    [Fact]
    public void Options_StackedAtModuleHead()
        => Accept(
            "Option Compare Database\n" +
            "Option Explicit\n" +
            "\n");

    // ---------- Implements (§4.3) ----------

    [Fact]
    public void Implements_Simple() => Accept("Implements IAuditLogger\n");

    [Fact]
    public void Implements_Qualified()
        => Accept("Implements MyLib.IAuditLogger\n");

    [Fact]
    public void Implements_MultipleStatements()
        => Accept(
            "Implements IAuditLogger\n" +
            "Implements ISerializable\n");

    // ---------- Module variable declarations (§4.4) ----------

    [Fact]
    public void ModuleVar_Public_SimpleType()
        => Accept("Public g_CurrentUserID As Long\n");

    [Fact]
    public void ModuleVar_Private_Currency()
        => Accept("Private m_GroupTotal As Currency\n");

    [Fact]
    public void ModuleVar_Dim_Boolean()
        => Accept("Dim m_AuditEnabled As Boolean\n");

    [Fact]
    public void ModuleVar_Global_String()
        => Accept("Global g_ConnectionString As String\n");

    [Fact]
    public void ModuleVar_QualifiedType()
        => Accept("Private rs As DAO.Recordset\n");

    [Fact]
    public void ModuleVar_AsNew()
        => Accept("Private oAuditor As New clsCaseAuditor\n");

    [Fact]
    public void ModuleVar_BareDim_Variant()
        => Accept("Dim m_LastSavedSnapshot\n");

    [Fact]
    public void ModuleVar_TypedIdentifier_Suffix()
        => Accept("Private m_Count&\n");

    [Fact]
    public void ModuleVar_DynamicArray()
        => Accept("Public g_Items() As String\n");

    [Fact]
    public void ModuleVar_FixedArray()
        => Accept("Private g_Buffer(100) As Byte\n");

    [Fact]
    public void ModuleVar_MultiDecl()
        => Accept("Private m_GroupTotal As Currency, m_GroupCount As Long\n");

    // ---------- WithEvents (H-F01) ----------

    [Fact]
    public void WithEvents_Private_Form()
        => Accept("Private WithEvents m_Form As Form\n");

    [Fact]
    public void WithEvents_Public_CustomClass()
        => Accept("Public WithEvents m_Source As clsEventSource\n");

    // ---------- Module-level Const ----------

    [Fact]
    public void ModuleConst_Public()
        => Accept("Public Const APP_NAME As String = \"Switchboard\"\n");

    [Fact]
    public void ModuleConst_Private()
        => Accept("Private Const MAX_ROWS As Long = 1000\n");

    [Fact]
    public void ModuleConst_NoModifier()
        => Accept("Const PI = 3.14159\n");

    [Fact]
    public void ModuleConst_MultiDecl()
        => Accept("Const A As Long = 1, B As Long = 2\n");

    // ---------- Type declaration (§4.4) ----------

    [Fact]
    public void Type_Empty()
        => Accept(
            "Public Type EmptyRecord\n" +
            "End Type\n");

    [Fact]
    public void Type_PrimitiveFields()
        => Accept(
            "Public Type CustomerRecord\n" +
            "    ID As Long\n" +
            "    Balance As Currency\n" +
            "    IsActive As Boolean\n" +
            "End Type\n");

    [Fact]
    public void Type_FixedLengthString()
        => Accept(
            "Private Type Header\n" +
            "    Magic As String * 4\n" +
            "    Name As String * 64\n" +
            "End Type\n");

    [Fact]
    public void Type_ArrayField()
        => Accept(
            "Public Type Coords\n" +
            "    Points(1 To 3) As Long\n" +
            "End Type\n");

    [Fact]
    public void Type_BlankLinesBetweenFields()
        => Accept(
            "Public Type R\n" +
            "    A As Long\n" +
            "\n" +
            "    B As Long\n" +
            "End Type\n");

    // ---------- Enum declaration (§4.4) ----------

    [Fact]
    public void Enum_Empty()
        => Accept(
            "Public Enum E\n" +
            "End Enum\n");

    [Fact]
    public void Enum_AutoIncrementMembers()
        => Accept(
            "Public Enum CustomerStatus\n" +
            "    csActive\n" +
            "    csInactive\n" +
            "    csSuspended\n" +
            "End Enum\n");

    [Fact]
    public void Enum_ExplicitValues()
        => Accept(
            "Private Enum Severity\n" +
            "    sevInfo = 1\n" +
            "    sevWarn = 2\n" +
            "    sevError = 4\n" +
            "End Enum\n");

    [Fact]
    public void Enum_PartialValues()
        => Accept(
            "Public Enum Mixed\n" +
            "    First = 10\n" +
            "    Second\n" +
            "    Third = 100\n" +
            "    Fourth\n" +
            "End Enum\n");

    // ---------- Declare (§4.4) ----------

    [Fact]
    public void Declare_Sub_NoParams()
        => Accept(
            "Declare Sub Sleep Lib \"kernel32\" (ByVal dwMs As Long)\n");

    [Fact]
    public void Declare_PtrSafe_Sub()
        => Accept(
            "Public Declare PtrSafe Sub Sleep Lib \"kernel32\" " +
            "(ByVal dwMs As Long)\n");

    [Fact]
    public void Declare_Function_WithAlias_AndReturnType()
        => Accept(
            "Private Declare PtrSafe Function GetTickCount " +
            "Lib \"kernel32\" Alias \"GetTickCount\" () As Long\n");

    [Fact]
    public void Declare_Sub_NoParenList()
        => Accept(
            "Declare Sub Beep Lib \"kernel32\"\n");

    // ---------- Public Event declaration (§4.4) ----------

    [Fact]
    public void Event_NoParams()
        => Accept("Public Event Logged()\n");

    [Fact]
    public void Event_WithParams()
        => Accept(
            "Public Event Logged(ByVal Severity As Long, " +
            "ByVal Message As String)\n");

    // Note: `Private Event` is intentionally rejected — see
    // Reject_PrivateEvent below. VBA event declarations are
    // `[Public] Event ...` only per §4.4.

    // ---------- Sub declaration (§4.5.1) ----------

    [Fact]
    public void Sub_NoModifier_NoParams_EmptyBody()
        => Accept(
            "Sub DoNothing()\n" +
            "End Sub\n");

    [Fact]
    public void Sub_PublicModifier()
        => Accept(
            "Public Sub AttachToForm(frmTarget As Form)\n" +
            "    Set m_Form = frmTarget\n" +
            "End Sub\n");

    [Fact]
    public void Sub_PrivateModifier_ClassInitialize()
        => Accept(
            "Private Sub Class_Initialize()\n" +
            "    m_AuditEnabled = True\n" +
            "End Sub\n");

    [Fact]
    public void Sub_PrivateModifier_ClassTerminate()
        => Accept(
            "Private Sub Class_Terminate()\n" +
            "    Set m_Form = Nothing\n" +
            "End Sub\n");

    [Fact]
    public void Sub_FriendModifier()
        => Accept(
            "Friend Sub Helper()\n" +
            "End Sub\n");

    [Fact]
    public void Sub_StaticModifier()
        => Accept(
            "Public Static Sub Counter()\n" +
            "    Dim c As Long\n" +
            "    c = c + 1\n" +
            "End Sub\n");

    [Fact]
    public void Sub_EventHandler_FormBeforeUpdate()
        => Accept(
            "Private Sub Form_BeforeUpdate(Cancel As Integer)\n" +
            "    If Not ValidateData() Then Cancel = True\n" +
            "End Sub\n");

    [Fact]
    public void Sub_EventHandler_TwoParamFormatEvent()
        => Accept(
            "Private Sub Detail_Format(Cancel As Integer, " +
            "FormatCount As Integer)\n" +
            "    m_GroupTotal = m_GroupTotal + 1\n" +
            "End Sub\n");

    [Fact]
    public void Sub_EventHandler_DetailRetreat_NoParams()
        => Accept(
            "Private Sub Detail_Retreat()\n" +
            "    m_GroupTotal = m_GroupTotal - 1\n" +
            "End Sub\n");

    // ---------- Function declaration (§4.5.2) ----------

    [Fact]
    public void Function_NoParams_ReturnLong()
        => Accept(
            "Public Function CurrentUser() As Long\n" +
            "    CurrentUser = 42\n" +
            "End Function\n");

    [Fact]
    public void Function_NoReturnType()
        => Accept(
            "Public Function Notify()\n" +
            "    Notify = True\n" +
            "End Function\n");

    [Fact]
    public void Function_OptionalByVal_WithDefault()
        => Accept(
            "Public Function RunFilteredCaseReport(" +
            "Optional ByVal sFilterArgs As String = \"\") As Long\n" +
            "    RunFilteredCaseReport = 0\n" +
            "End Function\n");

    [Fact]
    public void Function_QualifiedReturnType()
        => Accept(
            "Private Function GetDb() As DAO.Database\n" +
            "    Set GetDb = CurrentDb\n" +
            "End Function\n");

    [Fact]
    public void Function_ParamArray_VariantArray()
        => Accept(
            "Public Sub LogMessage(ParamArray Parts() As Variant)\n" +
            "End Sub\n");

    [Fact]
    public void Function_ArrayParameter()
        => Accept(
            "Private Sub Process(arr() As Variant)\n" +
            "End Sub\n");

    [Fact]
    public void Function_MultipleParameters_MixedModes()
        => Accept(
            "Public Function Combine(" +
            "ByVal a As Long, ByRef b As Long, c As String) As Long\n" +
            "    Combine = a + b\n" +
            "End Function\n");

    // ---------- Property declaration (§4.5.3) ----------

    [Fact]
    public void Property_Get()
        => Accept(
            "Public Property Get EntityName() As String\n" +
            "    EntityName = m_AuditEntityName\n" +
            "End Property\n");

    [Fact]
    public void Property_Let()
        => Accept(
            "Public Property Let EntityName(ByVal sValue As String)\n" +
            "    m_AuditEntityName = sValue\n" +
            "End Property\n");

    [Fact]
    public void Property_Set()
        => Accept(
            "Public Property Set Source(ByVal oSource As Object)\n" +
            "    Set m_Source = oSource\n" +
            "End Property\n");

    [Fact]
    public void Property_Triplet_GetLetSet_AllThree()
        => Accept(
            "Public Property Get Item() As Variant\n" +
            "    Item = m_Item\n" +
            "End Property\n" +
            "\n" +
            "Public Property Let Item(ByVal v As Variant)\n" +
            "    m_Item = v\n" +
            "End Property\n" +
            "\n" +
            "Public Property Set Item(ByVal v As Object)\n" +
            "    Set m_Item = v\n" +
            "End Property\n");

    [Fact]
    public void Property_PrivateGet_ImplementsConvention()
        => Accept(
            "Private Property Get IAuditLogger_Enabled() As Boolean\n" +
            "    IAuditLogger_Enabled = m_AuditEnabled\n" +
            "End Property\n");

    [Fact]
    public void Property_IndexedGet()
        => Accept(
            "Public Property Get Items(ByVal index As Long) As String\n" +
            "    Items = m_Items(index)\n" +
            "End Property\n");

    // ---------- Composite: full fixture-style class module ----------

    [Fact]
    public void Composite_StandardModuleHead()
        => Accept(
            "Option Compare Database\n" +
            "Option Explicit\n" +
            "\n" +
            "Public g_CurrentUserID As Long\n" +
            "Public g_UserRole As String\n" +
            "\n" +
            "Public Sub Initialize()\n" +
            "    g_CurrentUserID = 1\n" +
            "End Sub\n");

    [Fact]
    public void Composite_ClassModule_FixtureTen_Subset()
        => Accept(
            "Option Compare Database\n" +
            "Option Explicit\n" +
            "\n" +
            "Implements IAuditLogger\n" +
            "\n" +
            "Private WithEvents m_Form As Form\n" +
            "Private m_AuditEntityName As String\n" +
            "Private m_AuditEnabled As Boolean\n" +
            "Private m_LastSavedSnapshot As Object\n" +
            "\n" +
            "Private Sub Class_Initialize()\n" +
            "    m_AuditEnabled = True\n" +
            "    Set m_LastSavedSnapshot = CreateObject(\"Scripting.Dictionary\")\n" +
            "End Sub\n" +
            "\n" +
            "Private Sub Class_Terminate()\n" +
            "    Set m_Form = Nothing\n" +
            "    Set m_LastSavedSnapshot = Nothing\n" +
            "End Sub\n" +
            "\n" +
            "Public Sub AttachToForm(frmTarget As Form)\n" +
            "    Set m_Form = frmTarget\n" +
            "    m_AuditEntityName = frmTarget.Name\n" +
            "End Sub\n" +
            "\n" +
            "Public Property Get EntityName() As String\n" +
            "    EntityName = m_AuditEntityName\n" +
            "End Property\n" +
            "\n" +
            "Public Property Let EntityName(ByVal sValue As String)\n" +
            "    m_AuditEntityName = sValue\n" +
            "End Property\n" +
            "\n" +
            "Private Sub m_Form_BeforeUpdate(Cancel As Integer)\n" +
            "    If Not m_AuditEnabled Then Exit Sub\n" +
            "    m_Form!ModifiedDate = Now()\n" +
            "End Sub\n" +
            "\n" +
            "Private Sub IAuditLogger_LogEvent(EventType As String, " +
            "Description As String)\n" +
            "    Debug.Print EventType\n" +
            "End Sub\n" +
            "\n" +
            "Private Property Get IAuditLogger_Enabled() As Boolean\n" +
            "    IAuditLogger_Enabled = m_AuditEnabled\n" +
            "End Property\n");

    // ---------- Fixture 10 end-to-end ----------
    //
    // Reads the canonical 10-class-with-events.cls fixture from the
    // committed corpus and parses it through the `module` start rule.
    // This is the integration smoke test for the module section.

    [Fact]
    public void Composite_FixtureTen_FullParse()
    {
        string fixturePath = LocateFixture("10-class-with-events.cls");
        string source = File.ReadAllText(fixturePath);
        Accept(source);
    }

    private static string LocateFixture(string fileName)
    {
        // Tests run from the test assembly's bin directory. Walk up to
        // the repo root and resolve docs/test-cases/<file>.
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            string candidate = Path.Combine(
                dir, "docs", "test-cases", fileName);
            if (File.Exists(candidate)) return candidate;
            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException(
            $"Could not locate fixture '{fileName}' walking up from " +
            $"'{AppContext.BaseDirectory}'.");
    }

    // ---------- Negative tests ----------
    //
    // These confirm that the grammar rejects forms whose shape was
    // surfaced by Round-1 cross-provider verification as illegal VBA
    // syntax. Each `Reject_*` case documents one specific
    // over-acceptance that the corresponding grammar-tightening edit
    // closed.

    private static void Reject(string src)
    {
        Assert.Throws<InvalidOperationException>(() => Parse(src));
    }

    [Fact]
    public void Reject_PrivateEvent()
        => Reject("Private Event Internal()\n");

    [Fact]
    public void Reject_WithEvents_AsNew()
        => Reject("Private WithEvents m_Form As New Form\n");

    [Fact]
    public void Reject_WithEvents_NoAsClause()
        => Reject("Private WithEvents m_Form\n");

    [Fact]
    public void Reject_PropertyLet_NoParams()
        => Reject(
            "Public Property Let X()\n" +
            "End Property\n");

    [Fact]
    public void Reject_PropertyLet_WithReturnType()
        => Reject(
            "Public Property Let X(ByVal v As Long) As Long\n" +
            "End Property\n");

    [Fact]
    public void Reject_PropertySet_WithReturnType()
        => Reject(
            "Public Property Set X(ByVal v As Object) As Object\n" +
            "End Property\n");

    [Fact]
    public void Reject_OptionalParamArrayCombination()
        => Reject(
            "Sub S(Optional ParamArray args() As Variant)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_ByValParamArrayCombination()
        => Reject(
            "Sub S(ByVal ParamArray args() As Variant)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_ParamArray_WithoutArrayParens()
        => Reject(
            "Sub S(ParamArray args As Variant)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_DefaultValue_WithoutOptional()
        => Reject(
            "Sub S(x As Long = 1)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_ParamArray_NotLastParameter()
        => Reject(
            "Sub S(ParamArray args() As Variant, x As Long)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_PropertySet_NoParams()
        => Reject(
            "Public Property Set X()\n" +
            "End Property\n");

    [Fact]
    public void Reject_ByRefParamArrayCombination()
        => Reject(
            "Sub S(ByRef ParamArray args() As Variant)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_ParamArray_DefaultValue()
        => Reject(
            "Sub S(ParamArray args() As Variant = Empty)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_WithEvents_ArrayDeclarator()
        => Reject("Private WithEvents m_Forms() As Form\n");

    [Fact]
    public void Reject_WithEvents_FixedLengthString()
        => Reject("Private WithEvents m_Form As String * 50\n");

    [Fact]
    public void Reject_FriendEvent()
        => Reject("Friend Event Internal()\n");

    [Fact]
    public void Reject_RequiredAfterOptional()
        => Reject(
            "Sub F(Optional x As Variant = Empty, y As Long)\n" +
            "End Sub\n");

    [Fact]
    public void Reject_EventOptionalParameter()
        => Reject("Public Event E(Optional x As Variant)\n");

    [Fact]
    public void Reject_EventParamArrayParameter()
        => Reject("Public Event E(ParamArray a() As Variant)\n");
}
