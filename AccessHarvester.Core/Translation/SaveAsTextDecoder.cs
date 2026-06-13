using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AccessHarvester.Core.Translation;

/// <summary>
/// One decode for Access <c>SaveAsText</c> output, shared by the <c>run</c>
/// pipeline's translate and manifest read sites so they agree on encoding.
/// <para>
/// SaveAsText output is not a single fixed encoding: modern Access emits UTF-8
/// (with or without a BOM) while legacy/ANSI content is Windows-1252. A fixed
/// decode is wrong for one of the two — reading cp1252 bytes as UTF-8 corrupts
/// every non-ASCII byte to U+FFFD (006 review F-1-1), and reading UTF-8-without-BOM
/// bytes as cp1252 produces mojibake (e.g. a section sign "§" → "Â§"; the regression
/// the set 010 golden regen surfaced when a UTF-8-encoded "§" decoded under the set
/// 009 fixed-cp1252 path).
/// </para>
/// <para>
/// Strategy: honour an explicit BOM when present (UTF-8 / UTF-16 LE+BE / UTF-32 via
/// the framework's StreamReader detection), otherwise try a STRICT UTF-8 decode and
/// fall back to Windows-1252 only when the bytes are not valid UTF-8. This fixes both
/// observed failure modes. It is a best-effort heuristic, not a proof: a BOM-less file
/// is inherently ambiguous, so a genuinely-cp1252 file whose bytes happen to form valid
/// UTF-8 (e.g. cp1252 "Ã©" = bytes C3 A9, also valid UTF-8 for "é") will be read as
/// UTF-8. In practice most cp1252 SaveAsText content uses lone high bytes (e.g. the
/// em-dash 0x97) that are NOT valid UTF-8 and so fall through to cp1252 correctly; a
/// fully unambiguous decode would need encoding metadata from the export step (see DW-6).
/// </para>
/// </summary>
public static class SaveAsTextDecoder
{
    // Field initializers run in textual order: register the code-pages provider
    // (needed for GetEncoding(1252) on .NET Core+) BEFORE the cp1252 lookup below.
    private static readonly bool _providerRegistered = RegisterCodePagesProvider();
    private static readonly Encoding Cp1252 = Encoding.GetEncoding(1252);

    private static bool RegisterCodePagesProvider()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // idempotent
        return true;
    }

    // Strict: throws DecoderFallbackException on bytes that are not valid UTF-8,
    // which is exactly the "this file is actually ANSI" signal we fall back on.
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads a SaveAsText file as text, honouring a BOM, else UTF-8, else cp1252.</summary>
    public static string ReadText(string path) => Decode(File.ReadAllBytes(path));

    /// <summary>
    /// Reads a SaveAsText file as lines (terminator-stripped), with the same decode as
    /// <see cref="ReadText"/>. Matches <c>File.ReadAllLines</c> semantics (handles
    /// CRLF/CR/LF; no trailing empty line for a file ending in a newline).
    /// </summary>
    public static string[] ReadLines(string path)
    {
        var lines = new List<string>();
        using var reader = new StringReader(ReadText(path));
        string? line;
        while ((line = reader.ReadLine()) is not null)
            lines.Add(line);
        return lines.ToArray();
    }

    /// <summary>Decodes already-read SaveAsText bytes. Exposed for hermetic tests.</summary>
    public static string Decode(byte[] bytes)
    {
        // An explicit BOM is authoritative; let the framework's StreamReader pick the
        // matching Unicode encoding (UTF-8, UTF-16 LE/BE, UTF-32 LE/BE) and strip it.
        if (HasBom(bytes))
        {
            using var reader = new StreamReader(
                new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        // No BOM: prefer strict UTF-8 (modern SaveAsText), fall back to cp1252 (ANSI).
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Cp1252.GetString(bytes);
        }
    }

    private static bool HasBom(byte[] b) =>
        (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) ||                 // UTF-8
        (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0x00 && b[3] == 0x00) || // UTF-32 LE
        (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF) || // UTF-32 BE
        (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) ||                                 // UTF-16 LE
        (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF);                                   // UTF-16 BE
}
