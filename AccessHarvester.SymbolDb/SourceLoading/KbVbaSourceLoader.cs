using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AccessHarvester.SymbolDb.Extraction;

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

    // The VBE-format module header lines (`VERSION x.y CLASS`, `BEGIN`,
    // `END`, `Attribute VB_*`, and the leading `CodeBehindForm` / `Class`
    // marker the harvester emits) are not accepted by the standalone VBA
    // parser. They sit before any executable code, so we drop every line
    // up to the first one that looks like real source — `Option ...`, an
    // access modifier (`Public`/`Private`/`Friend`), `Sub`/`Function`/
    // `Property`/`Enum`/`Type`/`Declare`/`Const`/`Dim`/`Implements`/
    // `Event`, or a comment.
    private static string StripVbeHeader(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        var normalised = source.Replace("\r\n", "\n").Split('\n');
        int dropUntil = -1;
        for (int i = 0; i < normalised.Length; i++)
        {
            string line = normalised[i].TrimStart();
            if (IsHeaderLine(line))
            {
                dropUntil = i;
                continue;
            }
            break;
        }
        if (dropUntil < 0) return source;

        var sb = new StringBuilder();
        for (int i = dropUntil + 1; i < normalised.Length; i++)
        {
            sb.Append(normalised[i]);
            if (i + 1 < normalised.Length) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static readonly Regex HeaderPatterns = new(
        @"^(VERSION\s|BEGIN\b|END\b|MultiUse\b|Attribute\s+VB_|CodeBehindForm\b|Class\b|Object\s*=)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsHeaderLine(string trimmedLine)
    {
        if (trimmedLine.Length == 0) return true;             // blank lines before the header end
        return HeaderPatterns.IsMatch(trimmedLine);
    }
}
