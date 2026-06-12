using System.Text;
using System.Text.RegularExpressions;

namespace AccessHarvester.Vba.Parser;

// Line-based VBE-header strip for VBA source lifted out of harvester
// artifacts (the kb tier's VbaCode field, SaveAsText code-behind
// sections). The VBE-format header lines (`VERSION x.y CLASS`, `BEGIN`,
// `END`, `Attribute VB_*`, and the leading `CodeBehindForm` / `Class`
// marker the harvester emits) are not accepted by the standalone VBA
// parser. They sit before any executable code, so we drop every line up
// to the first one that looks like real source — `Option ...`, an access
// modifier (`Public`/`Private`/`Friend`), `Sub`/`Function`/`Property`/
// `Enum`/`Type`/`Declare`/`Const`/`Dim`/`Implements`/`Event`, or a
// comment.
//
// This is intentionally looser than VbaModuleBodyLoader's canonical-
// header recognition: the loader's regex requires the strict SaveAsText
// module shape (VERSION/BEGIN..END plus at least one `Attribute VB_*`
// line at offset 0) and leaves anything else untouched, so a
// `CodeBehindForm`-led form/report header never matches it. Shared by
// the SymbolDb kb ingestion (KbVbaSourceLoader) and the Core manifest
// pipeline (Pipeline.EmitManifestArtifacts).
public static class VbeHeaderStripper
{
    public static string Strip(string source)
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
