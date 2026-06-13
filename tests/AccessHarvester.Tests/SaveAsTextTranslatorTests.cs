using System.Text;
using System.Text.Json;
using AccessHarvester.Core.Translation;
using Xunit;

namespace AccessHarvester.Tests;

/// <summary>
/// Hermetic tests for <see cref="SaveAsTextTranslator"/> — the raw/ → kb/
/// file-to-file seam (006 review F-1-8: previously pinned only behind
/// Access-gated integration runs that use all-ASCII fixtures).
///
/// Two responsibilities are covered: signature-based routing (the
/// <c>&lt;?xml</c> → Macro / <c>Attribute VB_Name</c>/<c>Option </c> → Module /
/// block-format branches and the <c>keepRoot</c> macro flag), and — the F-1-1
/// lock — that SaveAsText's Windows-1252 bytes decode correctly instead of
/// corrupting to U+FFFD as the previous UTF-8-default read did.
///
/// Encoding-provider note: these tests deliberately do NOT pre-register the
/// code-pages provider. Building the non-ASCII inputs from literal cp1252
/// bytes (not <c>Encoding.GetEncoding(1252).GetBytes</c>) keeps the test from
/// masking the translator's own static-constructor registration — so the
/// cp1252 tests below exercise the translator self-bootstrapping cp1252
/// support, which is the host-independence the F-1-1 fix claims.
///
/// Pure file-to-file logic, no Access COM (Testing Hierarchy preference 1).
/// </summary>
public class SaveAsTextTranslatorTests
{
    /// <summary>
    /// Writes <paramref name="rawFiles"/> (name → bytes) into a fresh temp
    /// raw category dir, runs the translator, and returns the kb output dir
    /// plus the outcomes.
    /// </summary>
    private static (string KbDir, IReadOnlyList<Core.Abstractions.TranslateOutcome> Outcomes)
        RunTranslate(SaveAsTextTranslator translator, IReadOnlyDictionary<string, byte[]> rawFiles)
    {
        string root = Path.Combine(Path.GetTempPath(), "saat-" + Guid.NewGuid().ToString("N"));
        string rawDir = Path.Combine(root, "raw");
        string kbDir = Path.Combine(root, "kb");
        Directory.CreateDirectory(rawDir);

        foreach (var (name, bytes) in rawFiles)
            File.WriteAllBytes(Path.Combine(rawDir, name + ".txt"), bytes);

        var outcomes = translator.Translate(rawDir, kbDir);
        return (kbDir, outcomes);
    }

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    /// <summary>
    /// Concatenates ASCII text and raw cp1252 bytes without going through
    /// <c>Encoding.GetEncoding(1252)</c> — so the test input is built with no
    /// dependency on the code-pages provider being registered.
    /// </summary>
    private static byte[] Bytes(params byte[][] parts)
    {
        var result = new List<byte>();
        foreach (var p in parts) result.AddRange(p);
        return result.ToArray();
    }

    private static JsonDocument ReadKbJson(string kbDir, string name)
    {
        string path = Path.Combine(kbDir, name + ".json");
        Assert.True(File.Exists(path), $"expected kb output {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    // ── Signature routing ────────────────────────────────────────────

    [Fact]
    public void XmlSignature_RoutesToMacroType()
    {
        var raw = "<?xml version=\"1.0\"?>\n<UserInterfaceMacros></UserInterfaceMacros>";
        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Macros, new Dictionary<string, byte[]> { ["mcrFoo"] = Ascii(raw) });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "mcrFoo");
        Assert.Equal("Macro", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal("mcrFoo", doc.RootElement.GetProperty("Name").GetString());
        Assert.Contains("UserInterfaceMacros", doc.RootElement.GetProperty("XmlContent").GetString());
    }

    [Fact]
    public void AttributeVbName_RoutesToModuleType()
    {
        var raw = "Attribute VB_Name = \"modUtil\"\nPublic Sub Foo()\nEnd Sub";
        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Modules, new Dictionary<string, byte[]> { ["modUtil"] = Ascii(raw) });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "modUtil");
        Assert.Equal("Module", doc.RootElement.GetProperty("Type").GetString());
        Assert.Contains("Public Sub Foo()", doc.RootElement.GetProperty("VbaCode").GetString());
    }

    [Fact]
    public void OptionStatement_RoutesToModuleType()
    {
        // No Attribute header — the `Option ` prefix is the routing signal.
        var raw = "Option Compare Database\nOption Explicit\nPublic x As Long";
        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Modules, new Dictionary<string, byte[]> { ["modOpt"] = Ascii(raw) });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "modOpt");
        Assert.Equal("Module", doc.RootElement.GetProperty("Type").GetString());
        Assert.Contains("Option Explicit", doc.RootElement.GetProperty("VbaCode").GetString());
    }

    [Fact]
    public void Macros_BlockFormat_KeepsAllRootChildren()
    {
        // keepRoot == true for the Macros category: the flat sequence of
        // root-level Begin blocks is preserved under DocumentRoot.
        var raw = string.Join("\n", new[]
        {
            "Begin",
            "    Action =\"OpenForm\"",
            "End",
            "Begin",
            "    Action =\"Close\"",
            "End",
        });
        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Macros, new Dictionary<string, byte[]> { ["mcrStartup"] = Ascii(raw) });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "mcrStartup");
        Assert.Equal("DocumentRoot", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("Children").GetArrayLength());
    }

    [Fact]
    public void Forms_BlockFormat_FirstChildBecomesMainObject()
    {
        // keepRoot == false for Forms: the first Begin child is promoted to the
        // returned object (not wrapped in a DocumentRoot).
        var raw = string.Join("\n", new[]
        {
            "Begin Form",
            "    Caption =\"Hello\"",
            "End",
        });
        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Forms, new Dictionary<string, byte[]> { ["frmMain"] = Ascii(raw) });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmMain");
        Assert.Equal("Form", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal("Hello", doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString());
    }

    // ── F-1-1 lock: cp1252 decode ────────────────────────────────────

    [Fact]
    public void Cp1252Caption_BlockFormatPath_DecodesAccentedBytes_NoReplacementChar()
    {
        // The canonical 006 F-1-1 probe: cp1252 bytes 43 E9 6C ("Cél") through
        // the block-format ReadAllLines path. Under the old UTF-8-default read,
        // 0xE9 decoded to U+FFFD one-way; under the cp1252 read it round-trips
        // to U+00E9. Input is literal bytes — no provider pre-registration — so
        // the translator's own static ctor must register cp1252 for this to pass.
        var raw = Bytes(
            Ascii("Begin Form\n    Caption =\""),
            new byte[] { 0x43, 0xE9, 0x6C }, // C é l
            Ascii("\"\nEnd"));

        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Forms, new Dictionary<string, byte[]> { ["frmEnc"] = raw });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmEnc");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Cél", caption);
        Assert.DoesNotContain('�', caption);
    }

    [Fact]
    public void Cp1252Module_ReadAllTextPath_DecodesAccentedLiteral_NoReplacementChar()
    {
        // The Module branch returns the ReadAllText string directly (VbaCode).
        // This pins the OTHER repaired read site: a regression reverting only
        // ReadAllText (while ReadAllLines stayed fixed) would fail here even
        // though the block-format cp1252 test above would still pass.
        // 0xE9 = "é" in cp1252; the surrounding text is ASCII.
        var raw = Bytes(
            Ascii("Attribute VB_Name = \"modEnc\"\nPublic Const Caption = \"Caf"),
            new byte[] { 0xE9 }, // é
            Ascii("\""));

        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Modules, new Dictionary<string, byte[]> { ["modEnc"] = raw });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "modEnc");
        string vba = doc.RootElement.GetProperty("VbaCode").GetString()!;
        Assert.Contains("Café", vba);
        Assert.DoesNotContain('�', vba);
    }

    [Fact]
    public void Cp1252Caption_EndToEnd_NoReplacementCharReachesKbJson()
    {
        // The end-to-end 006 probe: a raw Forms .txt written as cp1252 with
        // Caption ="Re/Création". The pre-fix CLI produced "Re/Cr�ation"
        // in the kb JSON; the fix must let every accented byte survive.
        var raw = Bytes(
            Ascii("Begin Form\n    Caption =\"Re/Cr"),
            new byte[] { 0xE9 }, // é
            Ascii("ation\"\nEnd"));

        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Forms, new Dictionary<string, byte[]> { ["frmEnc2"] = raw });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmEnc2");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Re/Création", caption);

        // Also assert no replacement char anywhere in the serialized kb file.
        string kbText = File.ReadAllText(Path.Combine(kbDir, "frmEnc2.json"));
        Assert.DoesNotContain('�', kbText);
    }

    [Fact]
    public void Utf8Bom_HonouredOverCp1252Fallback()
    {
        // BOM precedence: the fix passes GetEncoding(1252) but File.ReadAllText
        // still detects a BOM and overrides it. Encode "Café" as UTF-8 — "é" is
        // the two bytes C3 A9, which under cp1252 would decode to "Ã©". With a
        // UTF-8 BOM present the translator must honour UTF-8 and yield "é",
        // proving BOM-detect-then-cp1252 (not blind cp1252).
        //
        // NB (DA-3 devil's-advocate finding): this is a NON-REGRESSION GUARD on
        // the reader configuration, NOT a discriminating lock for F-1-1 — the
        // pre-fix default ReadAllText(file) ALSO honoured a BOM, so this test
        // would pass even if the cp1252 fix were reverted. The actual F-1-1 lock
        // is the three no-BOM cp1252 tests above (block-format, Module-string,
        // end-to-end), each of which fails on a reversion.
        var raw = Bytes(
            new byte[] { 0xEF, 0xBB, 0xBF },                       // UTF-8 BOM
            Encoding.UTF8.GetBytes("Begin Form\n    Caption =\"Café\"\nEnd"));

        var (kbDir, outcomes) = RunTranslate(
            SaveAsTextTranslator.Forms, new Dictionary<string, byte[]> { ["frmBom"] = raw });

        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmBom");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Café", caption);            // BOM-UTF-8 honoured
        Assert.DoesNotContain("Ã", caption);      // NOT mis-decoded as cp1252
    }
}
