using System.Text.Json.Nodes;
using AccessHarvester.Core.Enrichment;
using AccessHarvester.Core.Enrichment.Vba;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic unit tests for VBA signature extraction. Covers the
/// <see cref="VbaSignatureParser"/> (one logical line in, one
/// <see cref="VbaProcedureInfo"/> out), the <see cref="VbaModuleSplitter"/>
/// (multi-procedure module → blocks), and end-to-end enrichment via
/// <see cref="VbaSignatureEnricher"/>.
/// </summary>
public class VbaSignatureParserTests
{
    [Fact]
    public void Sub_NoArgs_NoModifier_DefaultsToPublic()
    {
        var info = VbaSignatureParser.ParseSignature("Sub Foo()");
        Assert.NotNull(info);
        Assert.Equal("Foo", info!.Name);
        Assert.Equal("Sub", info.ProcedureType);
        Assert.Equal("Public", info.AccessModifier);
        Assert.False(info.IsStatic);
        Assert.Null(info.ReturnType);
        Assert.Empty(info.Parameters);
    }

    [Fact]
    public void Function_PrivateTypedReturn_CapturesReturnType()
    {
        var info = VbaSignatureParser.ParseSignature("Private Function Bar() As Long");
        Assert.NotNull(info);
        Assert.Equal("Bar", info!.Name);
        Assert.Equal("Function", info.ProcedureType);
        Assert.Equal("Private", info.AccessModifier);
        Assert.Equal("Long", info.ReturnType);
    }

    [Fact]
    public void Parameters_AllModifiers_ParsedCorrectly()
    {
        var info = VbaSignatureParser.ParseSignature(
            "Sub X(ByVal a As Long, ByRef b() As String, Optional c As Variant = \"z\")");
        Assert.NotNull(info);
        Assert.Equal(3, info!.Parameters.Count);

        var a = info.Parameters[0];
        Assert.Equal("a", a.Name);
        Assert.Equal("ByVal", a.PassingMechanism);
        Assert.Equal("Long", a.DataType);
        Assert.False(a.IsArray);
        Assert.False(a.IsOptional);

        var b = info.Parameters[1];
        Assert.Equal("b", b.Name);
        Assert.Equal("ByRef", b.PassingMechanism);
        Assert.Equal("String", b.DataType);
        Assert.True(b.IsArray);

        var c = info.Parameters[2];
        Assert.Equal("c", c.Name);
        Assert.True(c.IsOptional);
        Assert.Equal("Variant", c.DataType);
        Assert.Equal("\"z\"", c.DefaultValue);
    }

    [Fact]
    public void PropertyGet_PublicTypedReturn()
    {
        var info = VbaSignatureParser.ParseSignature("Public Property Get Name() As String");
        Assert.NotNull(info);
        Assert.Equal("Name", info!.Name);
        Assert.Equal("PropertyGet", info.ProcedureType);
        Assert.Equal("Public", info.AccessModifier);
        Assert.Equal("String", info.ReturnType);
    }

    [Fact]
    public void PropertyLet_HasSingleByValParam()
    {
        var info = VbaSignatureParser.ParseSignature("Public Property Let Name(ByVal value As String)");
        Assert.NotNull(info);
        Assert.Equal("PropertyLet", info!.ProcedureType);
        Assert.Single(info.Parameters);
        Assert.Equal("value", info.Parameters[0].Name);
        Assert.Equal("ByVal", info.Parameters[0].PassingMechanism);
        Assert.Equal("String", info.Parameters[0].DataType);
    }

    [Fact]
    public void PropertySet_RecognizedAsPropertySet()
    {
        var info = VbaSignatureParser.ParseSignature("Public Property Set Owner(ByVal newOwner As Object)");
        Assert.NotNull(info);
        Assert.Equal("PropertySet", info!.ProcedureType);
    }

    [Fact]
    public void Static_FlagsIsStatic()
    {
        var info = VbaSignatureParser.ParseSignature("Static Sub Counter()");
        Assert.NotNull(info);
        Assert.True(info!.IsStatic);
        Assert.Equal("Sub", info.ProcedureType);
        Assert.Equal("Public", info.AccessModifier);
    }

    [Fact]
    public void LineContinuation_FoldedBySplitter_ProducesOneSignature()
    {
        var src =
            "Public Function Sum( _\r\n" +
            "    ByVal a As Long, _\r\n" +
            "    ByVal b As Long) As Long\r\n" +
            "    Sum = a + b\r\n" +
            "End Function\r\n";
        var blocks = VbaModuleSplitter.Split(src);
        Assert.Single(blocks);
        var info = VbaSignatureParser.ParseSignature(blocks[0].SignatureLine);
        Assert.NotNull(info);
        Assert.Equal("Sum", info!.Name);
        Assert.Equal(2, info.Parameters.Count);
        Assert.Equal("Long", info.ReturnType);
        Assert.Equal(1, blocks[0].StartLine);
    }

    [Fact]
    public void Declare_PtrSafe_CapturesLibAliasAndReturn()
    {
        var info = VbaSignatureParser.ParseSignature(
            "Public Declare PtrSafe Function MessageBox Lib \"user32\" Alias \"MessageBoxW\" " +
            "(ByVal hWnd As Long, ByVal lpText As String, ByVal lpCaption As String, ByVal uType As Long) As Long");
        Assert.NotNull(info);
        Assert.Equal("MessageBox", info!.Name);
        Assert.Equal("DeclareFunction", info.ProcedureType);
        Assert.Equal("user32", info.Lib);
        Assert.Equal("MessageBoxW", info.Alias);
        Assert.Equal("Long", info.ReturnType);
        Assert.Equal(4, info.Parameters.Count);
    }

    [Fact]
    public void Declare_WithoutAlias_AliasIsNull()
    {
        var info = VbaSignatureParser.ParseSignature(
            "Private Declare Sub Sleep Lib \"kernel32\" (ByVal dwMs As Long)");
        Assert.NotNull(info);
        Assert.Equal("DeclareSub", info!.ProcedureType);
        Assert.Equal("kernel32", info.Lib);
        Assert.Null(info.Alias);
    }

    [Fact]
    public void CommentOnly_SignatureLineIgnored()
    {
        var src =
            "' This is a header comment\r\n" +
            "' Author: Someone\r\n" +
            "Public Sub Doit()\r\n" +
            "End Sub\r\n";
        var blocks = VbaModuleSplitter.Split(src);
        Assert.Single(blocks);
        Assert.Equal(3, blocks[0].StartLine);
        var info = VbaSignatureParser.ParseSignature(blocks[0].SignatureLine);
        Assert.Equal("Doit", info!.Name);
    }

    [Fact]
    public void CommentedOutSignature_NotPickedUp()
    {
        var src =
            "' Sub Unused()\r\n" +
            "Sub Used()\r\n" +
            "End Sub\r\n";
        var blocks = VbaModuleSplitter.Split(src);
        Assert.Single(blocks);
        Assert.Equal(2, blocks[0].StartLine);
    }

    [Fact]
    public void MultiProcedureModule_FourEntries_InOrder_WithCorrectStartLines()
    {
        var src =
            "Option Explicit\r\n" +
            "\r\n" +
            "Public Sub First()\r\n" +
            "    Debug.Print \"first\"\r\n" +
            "End Sub\r\n" +
            "\r\n" +
            "Private Sub Second(ByVal n As Long)\r\n" +
            "End Sub\r\n" +
            "\r\n" +
            "Public Function Third() As String\r\n" +
            "    Third = \"x\"\r\n" +
            "End Function\r\n" +
            "\r\n" +
            "Public Property Get Fourth() As Long\r\n" +
            "End Property\r\n";

        var blocks = VbaModuleSplitter.Split(src);
        Assert.Equal(4, blocks.Count);

        Assert.Equal(3, blocks[0].StartLine);
        Assert.Equal(7, blocks[1].StartLine);
        Assert.Equal(10, blocks[2].StartLine);
        Assert.Equal(14, blocks[3].StartLine);

        var infos = blocks.Select(b => VbaSignatureParser.ParseSignature(b.SignatureLine)).ToList();
        Assert.Equal("First", infos[0]!.Name);
        Assert.Equal("Sub", infos[0]!.ProcedureType);
        Assert.Equal("Second", infos[1]!.Name);
        Assert.Equal("Third", infos[2]!.Name);
        Assert.Equal("Function", infos[2]!.ProcedureType);
        Assert.Equal("Fourth", infos[3]!.Name);
        Assert.Equal("PropertyGet", infos[3]!.ProcedureType);
    }

    [Fact]
    public void UnknownParamType_DefaultsToVariant_ReturnTypeStaysNull()
    {
        var info = VbaSignatureParser.ParseSignature("Sub X(a, b)");
        Assert.NotNull(info);
        Assert.Null(info!.ReturnType);
        Assert.Equal(2, info.Parameters.Count);
        Assert.Equal("Variant", info.Parameters[0].DataType);
        Assert.Equal("Variant", info.Parameters[1].DataType);
    }

    [Fact]
    public void ParamArray_FlagsIsParamArrayAndIsArray()
    {
        var info = VbaSignatureParser.ParseSignature("Sub X(ParamArray things() As Variant)");
        Assert.NotNull(info);
        Assert.Single(info!.Parameters);
        var p = info.Parameters[0];
        Assert.True(p.IsParamArray);
        Assert.True(p.IsArray);
        Assert.Equal("Variant", p.DataType);
    }

    [Fact]
    public void Splitter_DeclareInModule_NoBodyCaptured_NextProcedureScannedSeparately()
    {
        var src =
            "Private Declare Sub Sleep Lib \"kernel32\" (ByVal dwMs As Long)\r\n" +
            "\r\n" +
            "Public Sub WaitASec()\r\n" +
            "    Sleep 1000\r\n" +
            "End Sub\r\n";
        var blocks = VbaModuleSplitter.Split(src);
        Assert.Equal(2, blocks.Count);
        Assert.Equal("", blocks[0].Body);
        Assert.Equal(1, blocks[0].StartLine);
        Assert.Equal(3, blocks[1].StartLine);
    }

    // ============================================================
    // Enricher integration (file in / file out) — exercises ToJsonNode shape
    // ============================================================

    [Fact]
    public void Enricher_ModulesCategory_ReadsRootVbaCode_AndWritesProceduresArray()
    {
        var input = new JsonObject
        {
            ["Name"] = "modThing",
            ["Type"] = "Module",
            ["VbaCode"] =
                "Option Explicit\r\n" +
                "Public Function Add(ByVal a As Long, ByVal b As Long) As Long\r\n" +
                "    Add = a + b\r\n" +
                "End Function\r\n",
        };

        var result = RunEnricher(VbaSignatureEnricher.ModulesAndClasses, input);

        var procedures = result["Procedures"] as JsonArray;
        Assert.NotNull(procedures);
        Assert.Single(procedures!);

        var p = procedures![0]!.AsObject();
        Assert.Equal("Add", p["Name"]!.GetValue<string>());
        Assert.Equal("Function", p["ProcedureType"]!.GetValue<string>());
        Assert.Equal("Public", p["AccessModifier"]!.GetValue<string>());
        Assert.False(p["IsStatic"]!.GetValue<bool>());
        Assert.Equal("Long", p["ReturnType"]!.GetValue<string>());
        Assert.Equal(2, p["StartLine"]!.GetValue<int>());

        // Always-present null slots.
        Assert.Null(p["Lib"]);
        Assert.Null(p["Alias"]);
        Assert.Null(p["InferredFunctionality"]);
        Assert.Null(p["Notes"]);

        var paramArr = p["Parameters"]!.AsArray();
        Assert.Equal(2, paramArr.Count);
        Assert.Equal("a", paramArr[0]!["Name"]!.GetValue<string>());
        Assert.Equal("ByVal", paramArr[0]!["PassingMechanism"]!.GetValue<string>());
        Assert.Equal("Long", paramArr[0]!["DataType"]!.GetValue<string>());
        Assert.False(paramArr[0]!["IsOptional"]!.GetValue<bool>());
        Assert.False(paramArr[0]!["IsParamArray"]!.GetValue<bool>());
        Assert.False(paramArr[0]!["IsArray"]!.GetValue<bool>());
        Assert.Null(paramArr[0]!["DefaultValue"]);
    }

    [Fact]
    public void Enricher_FormCategory_NoVbaCode_EmitsEmptyProceduresArray()
    {
        // Mirrors the "Label: { source: \"unresolved\" }" pattern: always-present
        // shape so consumers don't special-case absence.
        var input = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject(),
            ["Children"] = new JsonArray(),
        };
        var result = RunEnricher(VbaSignatureEnricher.Forms, input);

        var procedures = result["Procedures"] as JsonArray;
        Assert.NotNull(procedures);
        Assert.Empty(procedures!);
    }

    [Fact]
    public void Enricher_FormCategory_PreservesExistingFieldsWhileAddingProcedures()
    {
        var input = new JsonObject
        {
            ["Type"] = "Form",
            ["Properties"] = new JsonObject { ["Width"] = "10080" },
            ["Children"] = new JsonArray(),
            ["VbaCode"] =
                "CodeBehindForm\r\n" +
                "Option Explicit\r\n" +
                "Private Sub Form_Load()\r\n" +
                "    Me.Caption = \"Loaded\"\r\n" +
                "End Sub\r\n",
        };
        var result = RunEnricher(VbaSignatureEnricher.Forms, input);

        Assert.Equal("Form", result["Type"]!.GetValue<string>());
        Assert.Equal("10080", result["Properties"]!["Width"]!.GetValue<string>());

        var procedures = result["Procedures"]!.AsArray();
        Assert.Single(procedures);
        Assert.Equal("Form_Load", procedures[0]!["Name"]!.GetValue<string>());
        Assert.Equal("Sub", procedures[0]!["ProcedureType"]!.GetValue<string>());
        Assert.Equal("Private", procedures[0]!["AccessModifier"]!.GetValue<string>());
        Assert.Equal(3, procedures[0]!["StartLine"]!.GetValue<int>());
    }

    private static JsonObject RunEnricher(VbaSignatureEnricher enricher, JsonObject input)
    {
        var inFile = Path.GetTempFileName();
        var outFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(inFile, input.ToJsonString());
            enricher.EnrichFile(inFile, outFile);
            return JsonNode.Parse(File.ReadAllText(outFile))!.AsObject();
        }
        finally
        {
            File.Delete(inFile);
            if (File.Exists(outFile)) File.Delete(outFile);
        }
    }
}
