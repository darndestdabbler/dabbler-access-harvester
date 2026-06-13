# Session-verification — 009-output-encoding-correctness, Session 1 (final session, closeout-grade)

Your response MUST begin with a detailed prose analysis for EACH numbered
concern below BEFORE the JSON block. Do not skip the analysis. Both the prose
analysis and the trailing JSON block are required.

You are the cross-provider verifier (different provider than the Claude/Opus
orchestrator). Adopt a devil's-advocate posture: your null hypothesis is that
the change is wrong or incomplete. Prefer a false-positive ISSUES_FOUND over a
false-negative VERIFIED.

## What this session implements

Single-session, Full-tier set. Source of truth: the 006 solution review,
findings **F-1-1 (Major)** and **F-1-8 (Minor, test posture)**. This session
implements the recorded recommendations; it does not re-litigate the diagnosis.

**F-1-1 — the bug.** `SaveAsTextTranslator.BuildPayload` read Access SaveAsText
`.txt` dumps with `File.ReadAllText(file)` / `File.ReadAllLines(file)`, which
default to UTF-8. SaveAsText output is Windows-1252 (ANSI). So a byte like
`0xE9` ("é" in cp1252) decoded to U+FFFD — a one-way corruption of every
non-ASCII caption/label/validation-string/VBA-literal flowing into kb/ and
therefore ir/ (the tiers the downstream AI consumer reads). The repo's manifest
path (`Pipeline.EmitManifestArtifacts`) and CLI `parse` path (`RunParse`)
already decode the same files as BOM-detect-then-cp1252, so the same caption
could differ between `manifest.json` and the ir/ file — a determinism/contract
violation across output surfaces. The corruption is invisible on the committed
all-ASCII HomeHealthCare fixtures.

**Re-grounding probe (run this session, 2026-06-13):** wrote cp1252 bytes
`43 E9 6C` to a temp file. Default `ReadAllText` → `C�l` (contains U+FFFD).
`ReadAllText(path, GetEncoding(1252))` → `Cél` (U+0043 U+00E9 U+006C, no
U+FFFD). Source read sites still UTF-8-default before the fix. Bug reproduces.

**The fix (diff below).** In `BuildPayload`, read with
`Encoding.GetEncoding(1252)` on both `ReadAllText` and `ReadAllLines`.
`File.ReadAllText(path, encoding)` opens its `StreamReader` with
`detectEncodingFromByteOrderMarks: true`, so a UTF-8/UTF-16 BOM is still
honoured and only the no-BOM case falls back to cp1252 — identical to the
strategy `RunParse` (Program.cs:581-600) and `Pipeline.cs:275` already use. A
static constructor registers `CodePagesEncodingProvider.Instance` so the cp1252
read works even from a host that never went through the CLI's `Main` (the
hermetic tests, an embedding caller). Registration is idempotent.

**F-1-8 — hermetic pins.** `SaveAsTextTranslator` previously appeared in zero
test files; the seam was exercised only behind Access-gated integration runs
with all-ASCII fixtures. New always-running hermetic tests
(`SaveAsTextTranslatorTests`) drive the real file-to-file `Translate` seam:
one per signature-routing branch (`<?xml`→Macro, `Attribute VB_Name`→Module,
`Option `→Module), the `keepRoot` flag (Macros keep-root vs Forms
first-child-promotion), and the F-1-1 lock — the canonical `43 E9 6C`→"Cél"
cp1252 round-trip plus an end-to-end `Caption ="Re/Création"` asserting no
U+FFFD reaches the kb JSON.

## Concerns to analyze (prose required for each, in order)

1. **F-1-1 correctness.** Does the fix actually decode SaveAsText as
   Windows-1252 while preserving BOM detection? Is `File.ReadAllText(path,
   Encoding.GetEncoding(1252))`'s BOM-honouring behavior accurately described
   (StreamReader `detectEncodingFromByteOrderMarks: true`)? Is the `cp1252`
   local correctly applied to BOTH read sites (the `ReadAllText` and the
   `ReadAllLines` block-format path)?
2. **Provider registration sufficiency.** Is registering
   `CodePagesEncodingProvider.Instance` in a static constructor sufficient and
   correct for all hosts? Could `GetEncoding(1252)` still throw from any path
   that reaches `BuildPayload` before the static ctor runs? (Note: the static
   ctor runs before any static member access — the factory properties
   `Forms`/`Reports`/`Macros`/`Modules` and `Translate` are all instance/static
   members on the type.)
3. **ASCII-stability / no golden regression.** The set's stop rule says no
   golden regeneration is expected because the committed HomeHealthCare
   fixtures are all-ASCII. Is the claim sound that ASCII bytes (0x00–0x7F)
   decode identically under cp1252 and UTF-8, and that BOM detection is
   preserved, so all-ASCII committed output is byte-identical under the fix?
   The COM golden test (`CanonicalGoldenRegressionTests`) was deliberately NOT
   run (it requires the Access COM tier; the change is pure file-to-file logic).
   Is that an acceptable gap given the by-construction argument?
4. **Test adequacy (F-1-8).** Do the hermetic tests pin every routing branch
   plus the cp1252 lock? Are the assertions robust (deserialize-and-compare the
   decoded .NET string, not raw escaped JSON)? Any branch or failure mode left
   unpinned that a closeout-grade reviewer would require?
5. **Scope discipline / no weakened assertions.** The set is P1b only
   (translator decode + its hermetic pins). The manifest path, the parser, and
   output-file BOM handling (F-2-3) are explicitly out of scope and belong to
   P2. Does the diff stay within that boundary? Were any assertions or tests
   weakened to force green?

## Out of scope (present in the repo, NOT changed this session)

- `Pipeline.cs:275` (`Encoding.RegisterProvider`) and `Pipeline.cs:297`
  (`Encoding.GetEncoding(1252)`) — the manifest path; already correct, shown
  only as the precedent the fix mirrors.
- `Program.cs:581-600` (`RunParse`) — the CLI parse path's explicit
  BOM-detect-then-cp1252 block; already correct, the reference strategy.
- F-2-1 (query decode), F-3-5 (ByVal/suffix emission), F-2-3 (BOM on output
  JSON), label casing — all P2 contract-fidelity batch, the next set.

No golden regeneration was needed (ASCII-stable by construction, argued above);
no COM tier was run (pure file-to-file logic on hermetic fixtures).

---

## Diff: AccessHarvester.Core/Translation/SaveAsTextTranslator.cs

```diff
@@ -1,3 +1,4 @@
+using System.Text;
 using System.Text.Json;
 using AccessHarvester.Core.Abstractions;
@@ -14,6 +15,17 @@ public sealed class SaveAsTextTranslator : IObjectTranslator
 {
     private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
+
+    static SaveAsTextTranslator()
+    {
+        // SaveAsText writes Windows-1252 (ANSI) text. Reading it requires the
+        // code-pages provider, which the CLI registers in Main — but the
+        // translator can also run from a host that never went through Main
+        // (the hermetic tests, an embedding caller). Register it here so the
+        // cp1252 read below works regardless of host. Idempotent: a no-op when
+        // the provider is already registered.
+        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
+    }
+
     private SaveAsTextTranslator(string category)
@@ private static object BuildPayload(string file, string name, string category)
-        string text = File.ReadAllText(file).TrimStart();
+        // SaveAsText output is Windows-1252 (ANSI). Reading it with the .NET
+        // default (UTF-8) corrupts every non-ASCII byte to U+FFFD one-way
+        // (006 review F-1-1). Passing GetEncoding(1252) still honours a BOM
+        // when present (File.ReadAllText opens the StreamReader with
+        // detectEncodingFromByteOrderMarks: true), matching the BOM-detect-
+        // then-cp1252 strategy the CLI parse path (RunParse) and the manifest
+        // path (Pipeline.EmitManifestArtifacts) already use.
+        var cp1252 = Encoding.GetEncoding(1252);
+        string text = File.ReadAllText(file, cp1252).TrimStart();

         if (text.StartsWith("<?xml")) ... (routing unchanged)
@@
         bool keepRoot = category == "Macros";
-        return AccessTextParser.ParseBlockFormat(File.ReadAllLines(file), keepRoot);
+        return AccessTextParser.ParseBlockFormat(File.ReadAllLines(file, cp1252), keepRoot);
```

## Full current source: SaveAsTextTranslator.cs

```csharp
using System.Text;
using System.Text.Json;
using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Translation;

public sealed class SaveAsTextTranslator : IObjectTranslator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static SaveAsTextTranslator()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private SaveAsTextTranslator(string category) { Category = category; }
    public string Category { get; }

    public static SaveAsTextTranslator Forms { get; } = new("Forms");
    public static SaveAsTextTranslator Reports { get; } = new("Reports");
    public static SaveAsTextTranslator Macros { get; } = new("Macros");
    public static SaveAsTextTranslator Modules { get; } = new("ModulesAndClasses");

    public IReadOnlyList<TranslateOutcome> Translate(string rawCategoryDir, string kbCategoryDir)
    {
        Directory.CreateDirectory(kbCategoryDir);
        if (!Directory.Exists(rawCategoryDir)) return Array.Empty<TranslateOutcome>();
        var outcomes = new List<TranslateOutcome>();
        foreach (var file in Directory.GetFiles(rawCategoryDir, "*.txt"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            try
            {
                object payload = BuildPayload(file, name, Category);
                string outPath = Path.Combine(kbCategoryDir, name + ".json");
                File.WriteAllText(outPath, JsonSerializer.Serialize(payload, JsonOptions));
                outcomes.Add(new TranslateOutcome(name, true));
            }
            catch (Exception ex) { outcomes.Add(new TranslateOutcome(name, false, ex.Message)); }
        }
        return outcomes;
    }

    private static object BuildPayload(string file, string name, string category)
    {
        var cp1252 = Encoding.GetEncoding(1252);
        string text = File.ReadAllText(file, cp1252).TrimStart();
        if (text.StartsWith("<?xml")) return new { Name = name, Type = "Macro", XmlContent = text };
        if (text.StartsWith("Attribute VB_Name") || text.StartsWith("Option "))
            return new { Name = name, Type = "Module", VbaCode = text };
        bool keepRoot = category == "Macros";
        return AccessTextParser.ParseBlockFormat(File.ReadAllLines(file, cp1252), keepRoot);
    }
}
```

## New test file: tests/AccessHarvester.Tests/SaveAsTextTranslatorTests.cs

(7 facts, all passing; build 0 errors. Adjacent AccessTextParserTests 5/5 still green.)

```csharp
using System.Text;
using System.Text.Json;
using AccessHarvester.Core.Translation;
using Xunit;

namespace AccessHarvester.Tests;

public class SaveAsTextTranslatorTests
{
    private static readonly Encoding Cp1252;
    static SaveAsTextTranslatorTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp1252 = Encoding.GetEncoding(1252);
    }

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

    [Fact] public void Cp1252Caption_DecodesAccentedBytes_NoReplacementChar()
    {
        var prefix = Cp1252.GetBytes("Begin Form\n    Caption =\"");
        var captionBytes = new byte[] { 0x43, 0xE9, 0x6C }; // C é l
        var suffix = Cp1252.GetBytes("\"\nEnd");
        var raw = prefix.Concat(captionBytes).Concat(suffix).ToArray();
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Forms,
            new Dictionary<string, byte[]> { ["frmEnc"] = raw });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmEnc");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Cél", caption);
        Assert.DoesNotContain('�', caption);
    }

    [Fact] public void Cp1252Caption_EndToEnd_NoReplacementCharReachesKbJson()
    {
        var raw = Cp1252.GetBytes("Begin Form\n    Caption =\"Re/Création\"\nEnd");
        var (kbDir, outcomes) = RunTranslate(SaveAsTextTranslator.Forms,
            new Dictionary<string, byte[]> { ["frmEnc2"] = raw });
        Assert.True(Assert.Single(outcomes).Success);
        using var doc = ReadKbJson(kbDir, "frmEnc2");
        string caption = doc.RootElement.GetProperty("Properties").GetProperty("Caption").GetString()!;
        Assert.Equal("Re/Création", caption);
        string kbText = File.ReadAllText(Path.Combine(kbDir, "frmEnc2.json"));
        Assert.DoesNotContain('�', kbText);
    }
}
```

(Note: the JSON serializer escapes non-ASCII to `\uXXXX`, so the raw-file U+FFFD
check looks for the decoded char; the deserialize-and-compare assertions are the
primary lock.)

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
