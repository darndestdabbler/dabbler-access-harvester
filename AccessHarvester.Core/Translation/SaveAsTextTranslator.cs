using System.Text;
using System.Text.Json;
using AccessHarvester.Core.Abstractions;

namespace AccessHarvester.Core.Translation;

/// <summary>
/// Translates .txt artifacts produced by Access's SaveAsText into JSON.
/// One instance per category (Forms, Reports, Macros, ModulesAndClasses, Queries).
/// Uses signature-based routing identical to the preliminary AccessJsonParser so that
/// legacy-format macros (Begin/End blocks) and code-style modules (Attribute VB_Name /
/// Option) are handled the same way regardless of which folder they live in.
/// </summary>
public sealed class SaveAsTextTranslator : IObjectTranslator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static SaveAsTextTranslator()
    {
        // SaveAsText writes Windows-1252 (ANSI) text. Reading it requires the
        // code-pages provider, which the CLI registers in Main — but the
        // translator can also run from a host that never went through Main
        // (the hermetic tests, an embedding caller). Register it here so the
        // cp1252 read below works regardless of host. Idempotent: a no-op when
        // the provider is already registered.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private SaveAsTextTranslator(string category)
    {
        Category = category;
    }

    public string Category { get; }

    public static SaveAsTextTranslator Forms { get; } = new("Forms");
    public static SaveAsTextTranslator Reports { get; } = new("Reports");
    public static SaveAsTextTranslator Macros { get; } = new("Macros");
    public static SaveAsTextTranslator Modules { get; } = new("ModulesAndClasses");

    public IReadOnlyList<TranslateOutcome> Translate(string rawCategoryDir, string kbCategoryDir)
    {
        Directory.CreateDirectory(kbCategoryDir);

        if (!Directory.Exists(rawCategoryDir))
            return Array.Empty<TranslateOutcome>();

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
            catch (Exception ex)
            {
                outcomes.Add(new TranslateOutcome(name, false, ex.Message));
            }
        }

        return outcomes;
    }

    private static object BuildPayload(string file, string name, string category)
    {
        // SaveAsText output is Windows-1252 (ANSI). Reading it with the .NET
        // default (UTF-8) corrupts every non-ASCII byte to U+FFFD one-way
        // (006 review F-1-1). Passing GetEncoding(1252) still honours a BOM
        // when present (File.ReadAllText opens the StreamReader with
        // detectEncodingFromByteOrderMarks: true), matching the BOM-detect-
        // then-cp1252 strategy the CLI parse path (RunParse) and the manifest
        // path (Pipeline.EmitManifestArtifacts) already use.
        var cp1252 = Encoding.GetEncoding(1252);
        string text = File.ReadAllText(file, cp1252).TrimStart();

        if (text.StartsWith("<?xml"))
            return new { Name = name, Type = "Macro", XmlContent = text };

        if (text.StartsWith("Attribute VB_Name") || text.StartsWith("Option "))
            return new { Name = name, Type = "Module", VbaCode = text };

        // Standalone macros are a flat sequence of root-level Begin blocks (no
        // enclosing Begin Macro block). Return the document root with all Begin
        // children intact so downstream enrichers can walk every block.
        bool keepRoot = category == "Macros";
        return AccessTextParser.ParseBlockFormat(File.ReadAllLines(file, cp1252), keepRoot);
    }
}
