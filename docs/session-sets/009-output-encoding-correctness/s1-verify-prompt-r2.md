# Session-verification R2 — 009-output-encoding-correctness, Session 1 (closeout-grade)

Your response MUST begin with a prose analysis of each resolution below BEFORE
the JSON block. Both the prose and the trailing JSON block are required. Prefer
a false-positive ISSUES_FOUND over a false-negative VERIFIED.

## Context

In Round 1 you VERIFIED the production fix (cp1252 decode in
`SaveAsTextTranslator.BuildPayload`, both read sites, BOM detection preserved,
static-ctor provider registration), the ASCII-stability/no-golden argument, the
no-COM decision, and scope discipline. You returned **3 Minor test-posture
findings**. The production code is unchanged since R1; only the test file was
strengthened. Confirm each finding is resolved, and flag any NEW issue.

### R1 finding 1 — test masked the translator's self-registration
> "The test class globally registers CodePagesEncodingProvider before
> exercising the translator … does not verify the static-constructor
> registration is sufficient for non-CLI hosts."

**Resolution:** The test class no longer registers the code-pages provider and
no longer holds a `Cp1252` field built from `Encoding.GetEncoding(1252)`. All
non-ASCII inputs are now assembled from **literal cp1252 bytes** (e.g.
`new byte[] { 0x43, 0xE9, 0x6C }`) concatenated with `Encoding.ASCII.GetBytes`
prefixes/suffixes via a `Bytes(params byte[][])` helper — so building the input
has NO dependency on the provider being registered. The only thing that
registers cp1252 in these tests is `SaveAsTextTranslator`'s own static
constructor (triggered when a test accesses `SaveAsTextTranslator.Forms` /
`.Modules` / `.Macros` before `Translate`). The cp1252 tests therefore now
genuinely exercise the translator self-bootstrapping cp1252 support.
(Caveat acknowledged: provider registration is process-global and idempotent,
so a strict per-test isolation guarantee is not achievable without process
isolation; this removes the test's own masking pre-registration, which was the
specific ask.)

### R1 finding 2 — no non-ASCII test on the ReadAllText return path
> "The cp1252 regression tests both use a Forms block-format file … validates
> ReadAllLines … no accented module/VBA or XML test to pin VbaCode = text /
> XmlContent = text. A revert of only ReadAllText to UTF-8 would still pass."

**Resolution:** Added
`Cp1252Module_ReadAllTextPath_DecodesAccentedLiteral_NoReplacementChar`, which
routes through the **Module** branch (`Attribute VB_Name` →
`return new { … VbaCode = text }`), i.e. the `ReadAllText` return path. It feeds
cp1252 byte `0xE9` ("é") inside a `Const Caption = "Café"` literal and asserts
the decoded `VbaCode` contains `"Café"` with no U+FFFD. A regression reverting
only the `ReadAllText` site to UTF-8 now fails this test.

### R1 finding 3 — BOM precedence not hermetically pinned
> "no UTF-8-BOM or UTF-16-BOM fixture proving the translator still respects
> BOM-indicated encoding instead of blindly forcing cp1252."

**Resolution:** Added `Utf8Bom_HonouredOverCp1252Fallback`. It writes a UTF-8
BOM (`EF BB BF`) followed by `Begin Form … Caption ="Café" … End` encoded as
UTF-8 (so "é" is the two bytes `C3 A9`, which under cp1252 would decode to the
two-char "Ã©"). It asserts the decoded caption is `"Café"` and does NOT contain
`"Ã"` — proving the translator honours the BOM (UTF-8) over the cp1252 fallback,
i.e. BOM-detect-then-cp1252, not blind cp1252.

## Validation

`dotnet build` (0 errors). `SaveAsTextTranslatorTests` now 9/9 passing (was 7),
`AccessTextParserTests` 5/5 still green. No production-code change since R1; no
golden regen; no COM tier; no `.accdb` mutations.

## Full updated test file: tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs

```csharp
using System.Text;
using System.Text.Json;
using AccessHarvester.Core.Translation;
using Xunit;

namespace AccessHarvester.Tests;

public class SaveAsTextTranslatorTests
{
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

    [Fact] public void XmlSignature_RoutesToMacroType()
    {
        var raw = "<?xml version=\"1.0\"?>\n<UserInterfaceMacros></UserInterfaceMacros>";
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Macros,
            new Dictionary<string, byte[]> { ["mcrFoo"] = Ascii(raw) });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "mcrFoo");
        Assert.Equal("Macro", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal("mcrFoo", doc.RootElement.GetProperty("Name").GetString());
        Assert.Contains("UserInterfaceMacros", doc.RootElement.GetProperty("XmlContent").GetString());
    }

    [Fact] public void AttributeVbName_RoutesToModuleType()
    {
        var raw = "Attribute VB_Name = \"modUtil\"\nPublic Sub Foo()\nEnd Sub";
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Modules,
            new Dictionary<string, byte[]> { ["modUtil"] = Ascii(raw) });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "modUtil");
        Assert.Equal("Module", doc.RootElement.GetProperty("Type").GetString());
        Assert.Contains("Public Sub Foo()", doc.RootElement.GetProperty("VbaCode").GetString());
    }

    [Fact] public void OptionStatement_RoutesToModuleType()
    {
        var raw = "Option Compare Database\nOption Explicit\nPublic x As Long";
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Modules,
            new Dictionary<string, byte[]> { ["modOpt"] = Ascii(raw) });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "modOpt");
        Assert.Equal("Module", doc.RootElement.GetProperty("Type").GetString());
        Assert.Contains("Option Explicit", doc.RootElement.GetProperty("VbaCode").GetString());
    }

    [Fact] public void Macros_BlockFormat_KeepsAllRootChildren()
    {
        var raw = "Begin\n    Action =\"OpenForm\"\nEnd\nBegin\n    Action =\"Close\"\nEnd";
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Macros,
            new Dictionary<string, byte[]> { ["mcrStartup"] = Ascii(raw) });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "mcrStartup");
        Assert.Equal("DocumentRoot", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("Children").GetArrayLength());
    }

    [Fact] public void Forms_BlockFormat_FirstChildBecomesMainObject()
    {
        var raw = "Begin Form\n    Caption =\"Hello\"\nEnd";
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Forms,
            new Dictionary<string, byte[]> { ["frmMain"] = Ascii(raw) });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmMain");
        Assert.Equal("Form", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal("Hello", doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString());
    }

    [Fact] public void Cp1252Caption_BlockFormatPath_DecodesAccentedBytes_NoReplacementChar()
    {
        var raw = Bytes(Ascii("Begin Form\n    Caption =\""),
            new byte[] { 0x43, 0xE9, 0x6C }, Ascii("\"\nEnd"));
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Forms,
            new Dictionary<string, byte[]> { ["frmEnc"] = raw });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmEnc");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Cél", caption);
        Assert.DoesNotContain('�', caption);
    }

    [Fact] public void Cp1252Module_ReadAllTextPath_DecodesAccentedLiteral_NoReplacementChar()
    {
        var raw = Bytes(Ascii("Attribute VB_Name = \"modEnc\"\nPublic Const Caption = \"Caf"),
            new byte[] { 0xE9 }, Ascii("\""));
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Modules,
            new Dictionary<string, byte[]> { ["modEnc"] = raw });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "modEnc");
        string vba = doc.RootElement.GetProperty("VbaCode").GetString()!;
        Assert.Contains("Café", vba);
        Assert.DoesNotContain('�', vba);
    }

    [Fact] public void Cp1252Caption_EndToEnd_NoReplacementCharReachesKbJson()
    {
        var raw = Bytes(Ascii("Begin Form\n    Caption =\"Re/Cr"),
            new byte[] { 0xE9 }, Ascii("ation\"\nEnd"));
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Forms,
            new Dictionary<string, byte[]> { ["frmEnc2"] = raw });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmEnc2");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Re/Création", caption);
        string kbText = File.ReadAllText(Path.Combine(kbDir, "frmEnc2.json"));
        Assert.DoesNotContain('�', kbText);
    }

    [Fact] public void Utf8Bom_HonouredOverCp1252Fallback()
    {
        var raw = Bytes(new byte[] { 0xEF, 0xBB, 0xBF },
            Encoding.UTF8.GetBytes("Begin Form\n    Caption =\"Café\"\nEnd"));
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Forms,
            new Dictionary<string, byte[]> { ["frmBom"] = raw });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmBom");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Café", caption);
        Assert.DoesNotContain("Ã", caption);
    }
}
```

(Note: actual source uses the literal replacement glyph in DoesNotContain; shown
here as `�` for transport safety. Both are the same U+FFFD char.)

---

After your prose analysis, output a JSON block in this exact format:
```json
{
  "verdict": "VERIFIED" | "ISSUES_FOUND",
  "issues": [
    { "severity": "Critical" | "Major" | "Minor", "location": "<file or test name>", "summary": "<one-sentence description>", "fix": "<recommended fix>" }
  ]
}
```
If verdict is VERIFIED, `issues` must be an empty array `[]`. Do not omit this block.
