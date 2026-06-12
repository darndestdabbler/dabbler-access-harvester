using System;
using System.IO;
using System.Text.Json;
using AccessHarvester.SymbolDb.Extraction;
using AccessHarvester.Vba.Parser;

namespace AccessHarvester.SymbolDb.SourceLoading;

// Loads the VbaCode field out of the harvester's kb tier JSON files. Form
// and report kb files store VbaCode as a top-level string; standalone
// modules (kb/ModulesAndClasses/<Name>.json) likewise expose it at the top
// level. The kb tier already strips the file's UTF-16 BOM and CRLF
// artifacts so consumers do not need to reparse the raw VBE text format.
public static class KbVbaSourceLoader
{
    public static RawModule? FromKbJson(string kbJsonPath, string moduleFqn, string kind)
    {
        if (!File.Exists(kbJsonPath))
        {
            throw new FileNotFoundException(
                $"kb JSON not found at {kbJsonPath}. Did the harvester pipeline run for this fixture?",
                kbJsonPath);
        }

        using var stream = File.OpenRead(kbJsonPath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (!root.TryGetProperty("VbaCode", out var vbaCode)
            || vbaCode.ValueKind != JsonValueKind.String)
        {
            return null; // Form/report without code-behind, or schema drift.
        }

        string source = StripVbeHeader(vbaCode.GetString()!);

        // Per the spec's encoding column: forms and reports are stored on
        // disk as UTF-16 LE; standard modules / class modules are ASCII.
        // The kb tier already decoded both into UTF-8 strings, so the column
        // here records the *original* on-disk encoding, not the in-memory
        // representation.
        string encoding = kind switch
        {
            "Form" => "utf-16-le",
            "Report" => "utf-16-le",
            _ => "ascii",
        };

        return new RawModule(
            Fqn: moduleFqn,
            Kind: kind,
            SourcePath: kbJsonPath,
            Encoding: encoding,
            Source: source);
    }

    // The VBE-header strip lives in the parser project
    // (VbeHeaderStripper) so the Core manifest pipeline can share it;
    // this wrapper keeps the loader's historical entry point.
    private static string StripVbeHeader(string source)
        => VbeHeaderStripper.Strip(source);
}
